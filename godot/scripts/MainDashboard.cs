using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Economy;
using GreenDistrict.Simulation.Localization;
using GreenDistrict.Simulation.Map;
using Godot;

namespace GreenDistrict.Godot.Scripts;

public partial class MainDashboard : Control
{
    private const int SpeedOneTicksPerSecond = 5;
    private const int SpeedTwoTicksPerSecond = 60;
    private const int SpeedThreeTicksPerSecond = 360;
    private const float CompactWidth = 1200f;
    private static readonly Vector2I[] AvailableResolutions =
    {
        new(1152, 648),
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080)
    };

    private readonly SimulationBridge _bridge = new();
    private readonly LocalizationSystem _localization = new();
    private bool _isRunning;
    private int _ticksPerSecond = SpeedOneTicksPerSecond;
    private double _autoTickAccumulator;
    private int? _selectedDistrictId;
    private int? _selectedEventId;
    private string? _selectedMapObjectId;
    private string? _projectMessage;
    private int? _projectMessageProjectId;
    private string? _eventMessage;
    private string? _systemMessage;
    private MainUiState _uiState = MainUiState.MainMenu;
    private bool _hasActiveGame;
    private bool _wasRunningBeforeMenu;
    private ContextPanelMode _contextPanelMode = ContextPanelMode.Overview;
    private bool _isContextPanelOpen;
    private GameSettings _settings = GameSettings.Default();
    private AnalyticsSnapshot? _previousAnalytics;

    private Control? _uiRoot;
    private TabContainer _tabs = null!;
    private Label _timeLabel = null!;
    private Label _budgetLabel = null!;
    private Label _populationLabel = null!;
    private Label _supportLabel = null!;
    private Label _satisfactionLabel = null!;
    private Label _unemploymentLabel = null!;
    private Button _playPauseButton = null!;
    private Button _speed1Button = null!;
    private Button _speed5Button = null!;
    private Button _speed20Button = null!;
    private Label _projectActionLabel = null!;
    private DistrictMapView _districtMap = null!;
    private VBoxContainer _contextList = null!;
    private VBoxContainer _projectList = null!;
    private VBoxContainer _diagnosticsList = null!;
    private string? _projectPanelKey;
    private bool _compactLayout;
    private Control? _loadingOverlay;
    private Label? _loadingSpinnerLabel;
    private double _loadingSpinnerAccumulator;
    private int _loadingSpinnerFrame;

    public override void _Ready()
    {
        AddChild(_bridge);
        LoadLocalizationDictionaries();
        LoadSettings();
        _localization.SetLanguage(_settings.Language);
        ApplySettings();
        BuildInterface();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationResized || _uiRoot == null) return;

        var nextCompact = GetViewportRect().Size.X <= CompactWidth;
        if (nextCompact == _compactLayout) return;

        BuildInterface();
        if (_uiState is MainUiState.InGame or MainUiState.PausedMenu)
        {
            Refresh();
        }
    }

    public override void _Process(double delta)
    {
        UpdateLoadingIndicator(delta);
        if (!_isRunning) return;

        _autoTickAccumulator += delta * _ticksPerSecond;
        var ticksToRun = (int)Math.Floor(_autoTickAccumulator);
        if (ticksToRun <= 0) return;

        _autoTickAccumulator -= ticksToRun;
        _bridge.StepTicks(ticksToRun);
        Refresh();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;

        if (_uiState == MainUiState.InGame && key.CtrlPressed && key.Keycode == Key.S)
        {
            SaveWorld();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_uiState == MainUiState.InGame && key.CtrlPressed && key.Keycode == Key.L)
        {
            LoadWorld();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            if (_uiState == MainUiState.InGame)
            {
                if (_isContextPanelOpen)
                {
                    _isContextPanelOpen = false;
                    BuildInterface();
                    Refresh();
                }
                else
                {
                    OpenPausedMenu();
                }
            }
            else if (_uiState is MainUiState.PausedMenu or MainUiState.Settings && _hasActiveGame)
            {
                ResumeGame();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (_uiState != MainUiState.InGame) return;

        switch (key.Keycode)
        {
            case Key.Space:
                ToggleAutoRun();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Key1:
                SetSpeed(SpeedOneTicksPerSecond);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Key2:
                SetSpeed(SpeedTwoTicksPerSecond);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Key3:
                SetSpeed(SpeedThreeTicksPerSecond);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Tab:
                CycleContextTab();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void BuildInterface()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        _compactLayout = GetViewportRect().Size.X <= CompactWidth;
        if (_uiRoot != null)
        {
            RemoveChild(_uiRoot);
            _uiRoot.QueueFree();
            _uiRoot = null;
        }

        var background = new PanelContainer();
        _uiRoot = background;
        background.MouseFilter = MouseFilterEnum.Ignore;
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.AddThemeStyleboxOverride("panel", UiTheme.PanelStyle(UiTheme.UiBackground, UiTheme.UiBackground, 0, 0));
        AddChild(background);

        var root = new MarginContainer();
        root.MouseFilter = MouseFilterEnum.Ignore;
        root.AddThemeConstantOverride("margin_left", _compactLayout ? 8 : 12);
        root.AddThemeConstantOverride("margin_top", _compactLayout ? 8 : 10);
        root.AddThemeConstantOverride("margin_right", _compactLayout ? 8 : 12);
        root.AddThemeConstantOverride("margin_bottom", _compactLayout ? 8 : 10);
        background.AddChild(root);

        var layout = new VBoxContainer();
        layout.MouseFilter = MouseFilterEnum.Ignore;
        layout.AddThemeConstantOverride("separation", _compactLayout ? 7 : 10);
        root.AddChild(layout);

        switch (_uiState)
        {
            case MainUiState.MainMenu:
                BuildMainMenu(layout);
                break;
            case MainUiState.PausedMenu:
                BuildGameInterface(layout);
                BuildPauseOverlay(background);
                break;
            case MainUiState.Settings:
                BuildSettingsMenu(layout);
                break;
            default:
                BuildGameInterface(layout);
                break;
        }
    }

    private void BuildGameInterface(VBoxContainer layout)
    {
        BuildHud(layout);
        BuildBody(layout);
    }

    private void BuildMainMenu(VBoxContainer layout)
    {
        layout.Alignment = BoxContainer.AlignmentMode.Center;

        var menuPanel = CreatePanel(UiTheme.Panel);
        menuPanel.CustomMinimumSize = new Vector2(_compactLayout ? 360 : 420, 0);
        menuPanel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        layout.AddChild(menuPanel);

        var menuRows = new VBoxContainer();
        menuRows.MouseFilter = MouseFilterEnum.Ignore;
        menuRows.AddThemeConstantOverride("separation", 10);
        menuPanel.AddChild(menuRows);

        var title = new Label
        {
            Text = T("ui.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UiTheme.ApplyLabel(title, 26);
        menuRows.AddChild(title);

        if (!string.IsNullOrWhiteSpace(_systemMessage))
        {
            menuRows.AddChild(CreateWrappedLabel(_systemMessage, UiTheme.Info));
        }

        menuRows.AddChild(CreateMenuButton(T("ui.new_game"), StartNewGame));

        var loadButton = CreateMenuButton(T("ui.load_game"), LoadGameFromMenu);
        loadButton.Disabled = !HasSaveFile();
        menuRows.AddChild(loadButton);

        var saveButton = CreateMenuButton(T("ui.save_game"), SaveGameFromMenu);
        saveButton.Disabled = !_hasActiveGame;
        menuRows.AddChild(saveButton);

        menuRows.AddChild(CreateMenuButton(T("ui.settings"), OpenSettings));
        menuRows.AddChild(CreateMenuButton(T("ui.exit"), ExitGame));
        menuRows.AddChild(CreateMenuBottomPadding());

        if (_uiState == MainUiState.PausedMenu)
        {
            menuRows.AddChild(CreateMenuButton(T("ui.resume"), ResumeGame));
        }
    }

    private void BuildPauseOverlay(Control background)
    {
        var overlay = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddThemeStyleboxOverride(
            "panel",
            UiTheme.PanelStyle(new Color(UiTheme.Text, 0.20f), new Color(UiTheme.Text, 0f), 0, 0));
        background.AddChild(overlay);

        var center = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);

        var menuPanel = CreatePanel(UiTheme.Panel);
        menuPanel.CustomMinimumSize = new Vector2(_compactLayout ? 340 : 400, 0);
        center.AddChild(menuPanel);

        var rows = new VBoxContainer();
        rows.MouseFilter = MouseFilterEnum.Ignore;
        rows.AddThemeConstantOverride("separation", 10);
        menuPanel.AddChild(rows);

        var title = new Label
        {
            Text = T("ui.paused"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UiTheme.ApplyLabel(title, 24);
        rows.AddChild(title);

        if (!string.IsNullOrWhiteSpace(_systemMessage))
        {
            rows.AddChild(CreateWrappedLabel(_systemMessage, UiTheme.Info));
        }

        rows.AddChild(CreateMenuButton(T("ui.resume"), ResumeGame));
        rows.AddChild(CreateMenuButton(T("ui.save_game"), SaveGameFromMenu));

        var loadButton = CreateMenuButton(T("ui.load_game"), LoadGameFromMenu);
        loadButton.Disabled = !HasSaveFile();
        rows.AddChild(loadButton);

        rows.AddChild(CreateMenuButton(T("ui.settings"), OpenSettings));
        rows.AddChild(CreateMenuButton(T("ui.new_game"), StartNewGame));
        rows.AddChild(CreateMenuButton(T("ui.exit"), ExitGame));
        rows.AddChild(CreateMenuBottomPadding());
    }

    private void BuildSettingsMenu(VBoxContainer layout)
    {
        layout.Alignment = BoxContainer.AlignmentMode.Center;

        var panel = CreatePanel(UiTheme.Panel);
        panel.CustomMinimumSize = new Vector2(_compactLayout ? 380 : 460, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        layout.AddChild(panel);

        var rows = new VBoxContainer();
        rows.MouseFilter = MouseFilterEnum.Ignore;
        rows.AddThemeConstantOverride("separation", 10);
        panel.AddChild(rows);

        var title = new Label
        {
            Text = T("ui.settings"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UiTheme.ApplyLabel(title, 24);
        rows.AddChild(title);

        rows.AddChild(CreateSectionTitle(T("ui.language"), "L"));
        var language = CreateLanguageSelector();
        language.CustomMinimumSize = new Vector2(220, 34);
        language.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        rows.AddChild(language);

        rows.AddChild(CreateSectionTitle(T("ui.audio"), "A"));
        rows.AddChild(CreateSettingsSlider(T("ui.master_volume"), _settings.MasterVolume, value =>
        {
            _settings.MasterVolume = value;
            PersistAndApplySettings();
        }));
        rows.AddChild(CreateSettingsSlider(T("ui.music_volume"), _settings.MusicVolume, value =>
        {
            _settings.MusicVolume = value;
            PersistAndApplySettings();
        }));
        rows.AddChild(CreateSettingsSlider(T("ui.effects_volume"), _settings.EffectsVolume, value =>
        {
            _settings.EffectsVolume = value;
            PersistAndApplySettings();
        }));

        rows.AddChild(CreateSectionTitle(T("ui.display"), "D"));
        rows.AddChild(CreateResolutionSelector());
        rows.AddChild(CreateWindowModeSelector());
        rows.AddChild(CreateMenuButton(T("ui.back"), _hasActiveGame ? ResumeGame : OpenMainMenu));
    }

    private void BuildHud(VBoxContainer layout)
    {
        var panel = CreatePanel(UiTheme.Hud);
        panel.CustomMinimumSize = new Vector2(0, _compactLayout ? 54 : 58);
        layout.AddChild(panel);

        var header = new HBoxContainer();
        header.MouseFilter = MouseFilterEnum.Ignore;
        header.AddThemeConstantOverride("separation", _compactLayout ? 5 : 7);
        panel.AddChild(header);

        _budgetLabel = CreateMetricPill(header, "$", T("ui.budget"), _compactLayout ? 104 : 116, true, UiTheme.Success);
        _populationLabel = CreateMetricPill(header, "P", T("ui.population"), _compactLayout ? 74 : 82, true, UiTheme.Info);
        _supportLabel = CreateMetricPill(header, "%", T("ui.support"), _compactLayout ? 74 : 82, true, UiTheme.Trend);
        _timeLabel = CreateMetricPill(header, "D", T("ui.time"), _compactLayout ? 82 : 92);
        _satisfactionLabel = CreateMetricPill(header, "+", T("ui.satisfaction"), _compactLayout ? 58 : 66);
        _unemploymentLabel = CreateMetricPill(header, "!", T("ui.unemployment"), _compactLayout ? 58 : 66);

        header.AddChild(CreateSpacer());

        var controls = new HBoxContainer();
        controls.MouseFilter = MouseFilterEnum.Ignore;
        controls.AddThemeConstantOverride("separation", _compactLayout ? 4 : 5);
        header.AddChild(controls);
        _playPauseButton = CreateButton(">", ToggleAutoRun, _compactLayout ? 32 : 34, T("ui.play_pause"));
        controls.AddChild(_playPauseButton);
        _speed1Button = CreateButton(T("ui.speed_1x"), () => SetSpeed(SpeedOneTicksPerSecond), _compactLayout ? 34 : 38);
        _speed5Button = CreateButton(T("ui.speed_5x"), () => SetSpeed(SpeedTwoTicksPerSecond), _compactLayout ? 34 : 38);
        _speed20Button = CreateButton(T("ui.speed_20x"), () => SetSpeed(SpeedThreeTicksPerSecond), _compactLayout ? 40 : 44);
        controls.AddChild(_speed1Button);
        controls.AddChild(_speed5Button);
        controls.AddChild(_speed20Button);
        controls.AddChild(CreateButton(T("ui.one_day"), () => StepAndRefresh(1440), _compactLayout ? 50 : 54));
        header.AddChild(CreateButton("M", OpenPausedMenu, _compactLayout ? 32 : 34, T("ui.menu")));
    }

    private void BuildBody(VBoxContainer layout)
    {
        var body = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            CustomMinimumSize = new Vector2(0, 240)
        };
        layout.AddChild(body);

        _districtMap = new DistrictMapView
        {
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _districtMap.SetAnchorsPreset(LayoutPreset.FullRect);
        _districtMap.DistrictSelected += districtId =>
        {
            if (IsSingleCityMode(_bridge.World))
            {
                _selectedDistrictId = null;
                _selectedEventId = null;
                _selectedMapObjectId = null;
                _eventMessage = null;
                _contextPanelMode = ContextPanelMode.Overview;
                _isContextPanelOpen = true;
                BuildInterface();
                Refresh();
                return;
            }

            _selectedDistrictId = districtId;
            _selectedEventId = null;
            _selectedMapObjectId = null;
            _eventMessage = null;
            _contextPanelMode = ContextPanelMode.Overview;
            _isContextPanelOpen = true;
            BuildInterface();
            Refresh();
        };
        _districtMap.MapObjectSelected += objectId =>
        {
            _selectedMapObjectId = string.IsNullOrWhiteSpace(objectId) ? null : objectId;
            _selectedDistrictId = null;
            _selectedEventId = null;
            _eventMessage = null;
            _contextPanelMode = ContextPanelMode.Overview;
            _isContextPanelOpen = true;
            BuildInterface();
            Refresh();
        };
        body.AddChild(_districtMap);

        BuildContextOverlay(body);
    }

    private void BuildContextOverlay(Control body)
    {
        var panelWidth = _compactLayout ? 300f : 330f;
        var buttonWidth = 38f;
        var buttonGap = 6f;

        var sideButtons = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        sideButtons.AnchorLeft = 1f;
        sideButtons.AnchorRight = 1f;
        sideButtons.AnchorTop = 0.5f;
        sideButtons.AnchorBottom = 0.5f;
        sideButtons.OffsetLeft = _isContextPanelOpen ? -panelWidth - buttonWidth - buttonGap : -buttonWidth - buttonGap;
        sideButtons.OffsetRight = _isContextPanelOpen ? -panelWidth - buttonGap : -buttonGap;
        sideButtons.OffsetTop = -68f;
        sideButtons.OffsetBottom = 68f;
        sideButtons.AddThemeConstantOverride("separation", 6);
        body.AddChild(sideButtons);
        sideButtons.AddChild(CreateContextModeButton("O", T("ui.overview"), ContextPanelMode.Overview));
        sideButtons.AddChild(CreateContextModeButton("P", T("ui.projects"), ContextPanelMode.Projects));
        sideButtons.AddChild(CreateContextModeButton("D", T("ui.diagnostics_short"), ContextPanelMode.Diagnostics));

        var rightPanel = CreatePanel(UiTheme.Panel);
        rightPanel.Visible = _isContextPanelOpen;
        rightPanel.MouseFilter = MouseFilterEnum.Stop;
        rightPanel.AnchorLeft = 1f;
        rightPanel.AnchorRight = 1f;
        rightPanel.AnchorTop = 0f;
        rightPanel.AnchorBottom = 1f;
        rightPanel.OffsetLeft = -panelWidth;
        rightPanel.OffsetRight = 0f;
        rightPanel.OffsetTop = 0f;
        rightPanel.OffsetBottom = 0f;
        body.AddChild(rightPanel);

        var rightRows = new VBoxContainer();
        rightRows.MouseFilter = MouseFilterEnum.Ignore;
        rightRows.AddThemeConstantOverride("separation", 8);
        rightPanel.AddChild(rightRows);

        var panelHeader = new HBoxContainer();
        panelHeader.MouseFilter = MouseFilterEnum.Ignore;
        panelHeader.AddThemeConstantOverride("separation", 6);
        rightRows.AddChild(panelHeader);
        var title = CreateSectionTitle(T("ui.context"), "I");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panelHeader.AddChild(title);
        panelHeader.AddChild(CreateButton("X", CloseContextPanel, 28, T("ui.close")));

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _tabs.TabsVisible = false;
        UiTheme.ApplyTabs(_tabs);
        rightRows.AddChild(_tabs);

        _contextList = CreateTabList(_tabs, T("ui.overview"));
        _projectList = CreateTabList(_tabs, T("ui.projects"));
        _diagnosticsList = CreateTabList(_tabs, T("ui.diagnostics_short"));
        _projectPanelKey = null;
        _tabs.CurrentTab = (int)_contextPanelMode;
    }

    private OptionButton CreateLanguageSelector()
    {
        var language = new OptionButton();
        language.AddItem("EN", (int)GameLanguage.English);
        language.AddItem("RU", (int)GameLanguage.Russian);
        language.Select(_settings.Language == GameLanguage.Russian ? 1 : 0);
        language.ItemSelected += index =>
        {
            var selected = (GameLanguage)language.GetItemId((int)index);
            _settings.Language = selected;
            _localization.SetLanguage(selected);
            SaveSettings();
            BuildInterface();
            if (_uiState == MainUiState.InGame)
            {
                Refresh();
            }
        };
        UiTheme.ApplyButton(language);
        return language;
    }

    private Control CreateSettingsSlider(string title, float value, Action<float> changed)
    {
        var rows = new VBoxContainer();
        rows.MouseFilter = MouseFilterEnum.Ignore;
        rows.AddThemeConstantOverride("separation", 3);

        var label = new Label { Text = $"{title}: {(int)Math.Round(value * 100f)}%" };
        UiTheme.ApplyLabel(label, 12, UiTheme.TextMuted);
        rows.AddChild(label);

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            Value = Math.Round(value * 100f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        slider.ValueChanged += raw =>
        {
            var next = (float)(raw / 100.0);
            label.Text = $"{title}: {(int)Math.Round(raw)}%";
            changed(next);
        };
        rows.AddChild(slider);
        return rows;
    }

    private Control CreateResolutionSelector()
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(CreateSettingsLabel(T("ui.resolution")));

        var selector = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        for (var i = 0; i < AvailableResolutions.Length; i++)
        {
            var resolution = AvailableResolutions[i];
            selector.AddItem($"{resolution.X}x{resolution.Y}", i);
        }

        selector.Select(Math.Clamp(_settings.ResolutionIndex, 0, AvailableResolutions.Length - 1));
        selector.ItemSelected += index =>
        {
            _settings.ResolutionIndex = (int)selector.GetItemId((int)index);
            PersistAndApplySettings();
        };
        UiTheme.ApplyButton(selector);
        row.AddChild(selector);
        return row;
    }

    private Control CreateWindowModeSelector()
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(CreateSettingsLabel(T("ui.window_mode")));

        var selector = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        selector.AddItem(T("ui.windowed"), (int)WindowModeSetting.Windowed);
        selector.AddItem(T("ui.fullscreen"), (int)WindowModeSetting.Fullscreen);
        selector.Select(_settings.WindowMode == WindowModeSetting.Fullscreen ? 1 : 0);
        selector.ItemSelected += index =>
        {
            _settings.WindowMode = (WindowModeSetting)selector.GetItemId((int)index);
            PersistAndApplySettings();
        };
        UiTheme.ApplyButton(selector);
        row.AddChild(selector);
        return row;
    }

    private static Label CreateSettingsLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(120, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(label, 12, UiTheme.TextMuted);
        return label;
    }

    private void ToggleAutoRun()
    {
        _isRunning = !_isRunning;
        _autoTickAccumulator = 0;
        RefreshRunState();
    }

    private void SetSpeed(int ticksPerSecond)
    {
        _ticksPerSecond = Math.Max(1, ticksPerSecond);
        RefreshRunState();
    }

    private void StepAndRefresh(int ticks)
    {
        _bridge.StepTicks(ticks);
        Refresh();
    }

    private void ResetWorld()
    {
        _isRunning = false;
        _autoTickAccumulator = 0;
        _selectedDistrictId = null;
        _selectedEventId = null;
        _selectedMapObjectId = null;
        _projectMessage = null;
        _projectMessageProjectId = null;
        _eventMessage = null;
        _systemMessage = null;
        _previousAnalytics = null;
        _bridge.ResetWorld();
        Refresh();
    }

    private void StartNewGame()
    {
        _isRunning = false;
        _autoTickAccumulator = 0;
        _selectedDistrictId = null;
        _selectedEventId = null;
        _selectedMapObjectId = null;
        _projectMessage = null;
        _projectMessageProjectId = null;
        _eventMessage = null;
        _systemMessage = null;
        _previousAnalytics = null;
        _bridge.ResetWorld(Random.Shared.Next(1, int.MaxValue));
        _hasActiveGame = true;
        _uiState = MainUiState.InGame;
        BuildInterface();
        Refresh();
    }

    private void OpenMainMenu()
    {
        _isRunning = false;
        _uiState = MainUiState.MainMenu;
        BuildInterface();
    }

    private void OpenPausedMenu()
    {
        _wasRunningBeforeMenu = _isRunning;
        _isRunning = false;
        ShowLoadingOverlay(T("ui.loading_menu"));
        CallDeferred(nameof(CompleteOpenPausedMenu));
    }

    private void CompleteOpenPausedMenu()
    {
        _uiState = MainUiState.PausedMenu;
        BuildInterface();
        Refresh();
        HideLoadingOverlay();
    }

    private void ResumeGame()
    {
        if (!_hasActiveGame)
        {
            OpenMainMenu();
            return;
        }

        _uiState = MainUiState.InGame;
        _isRunning = _wasRunningBeforeMenu;
        BuildInterface();
        Refresh();
    }

    private void OpenSettings()
    {
        if (_uiState == MainUiState.InGame)
        {
            _wasRunningBeforeMenu = _isRunning;
            _isRunning = false;
        }

        _uiState = MainUiState.Settings;
        BuildInterface();
    }

    private void ShowLoadingOverlay(string text)
    {
        HideLoadingOverlay();

        var overlay = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddThemeStyleboxOverride(
            "panel",
            UiTheme.PanelStyle(new Color(UiTheme.Text, 0.24f), new Color(UiTheme.Text, 0f), 0, 0));
        AddChild(overlay);
        overlay.MoveToFront();

        var center = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);

        var panel = CreatePanel(UiTheme.Panel);
        panel.CustomMinimumSize = new Vector2(220, 96);
        center.AddChild(panel);

        var rows = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        rows.AddThemeConstantOverride("separation", 8);
        panel.AddChild(rows);

        _loadingSpinnerLabel = new Label
        {
            Text = "|",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UiTheme.ApplyLabel(_loadingSpinnerLabel, 24, UiTheme.Info);
        rows.AddChild(_loadingSpinnerLabel);

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UiTheme.ApplyLabel(label, 14, UiTheme.Text);
        rows.AddChild(label);

        _loadingSpinnerAccumulator = 0;
        _loadingSpinnerFrame = 0;
        _loadingOverlay = overlay;
    }

    private void HideLoadingOverlay()
    {
        _loadingSpinnerLabel = null;
        if (_loadingOverlay == null) return;

        RemoveChild(_loadingOverlay);
        _loadingOverlay.QueueFree();
        _loadingOverlay = null;
    }

    private void UpdateLoadingIndicator(double delta)
    {
        if (_loadingSpinnerLabel == null) return;

        _loadingSpinnerAccumulator += delta;
        if (_loadingSpinnerAccumulator < 0.10) return;

        _loadingSpinnerAccumulator = 0;
        var frames = new[] { "|", "/", "-", "\\" };
        _loadingSpinnerFrame = (_loadingSpinnerFrame + 1) % frames.Length;
        _loadingSpinnerLabel.Text = frames[_loadingSpinnerFrame];
    }

    private void SaveGameFromMenu()
    {
        SaveWorld();
    }

    private void LoadGameFromMenu()
    {
        LoadWorld();
    }

    private void ExitGame()
    {
        GetTree().Quit();
    }

    private void Refresh()
    {
        var world = _bridge.World;
        NormalizeSelection(world);
        _timeLabel.Text = $"D{world.Clock.Day} {world.Clock.GetTimeString()}";
        _timeLabel.TooltipText = Tf("ui.day_time", world.Clock.Day, world.Clock.GetTimeString());
        _budgetLabel.Text = FormatMoney(world.Budget);
        _populationLabel.Text = world.GetTotalPopulation().ToString(CultureInfo.InvariantCulture);
        _supportLabel.Text = FormatPercent(world.SupportRating);
        _satisfactionLabel.Text = FormatPercent(world.GetAverageSatisfaction());
        _unemploymentLabel.Text = FormatPercent(world.LastUnemploymentRate);
        _budgetLabel.AddThemeColorOverride("font_color", world.Budget < 0f ? UiTheme.Danger : UiTheme.Text);
        _supportLabel.AddThemeColorOverride("font_color", StatusColor(world.SupportRating));
        _populationLabel.AddThemeColorOverride("font_color", UiTheme.Text);
        _satisfactionLabel.AddThemeColorOverride("font_color", StatusColor(world.GetAverageSatisfaction()));
        _unemploymentLabel.AddThemeColorOverride("font_color", world.LastUnemploymentRate > 18f ? UiTheme.Warning : UiTheme.TextMuted);

        RefreshRunState();
        _districtMap.SetWorld(world);
        if (!string.IsNullOrWhiteSpace(_selectedMapObjectId) && _districtMap.GetMapObject(_selectedMapObjectId) == null)
        {
            _selectedMapObjectId = null;
        }

        _districtMap.SetSelectedDistrict(_selectedDistrictId);
        _districtMap.SetSelectedEvent(_selectedEventId);
        _districtMap.SetSelectedMapObject(_selectedMapObjectId);
        SyncProjectMessage(world);
        RebuildContext(world);
        RebuildProjectsIfNeeded(world);
        RebuildDiagnostics(world);
    }

    private void NormalizeSelection(WorldState world)
    {
        if (_selectedDistrictId.HasValue && world.Districts.All(district => district.Id != _selectedDistrictId.Value))
        {
            _selectedDistrictId = null;
        }

        if (IsSingleCityMode(world))
        {
            _selectedDistrictId = null;
        }

        if (_selectedEventId.HasValue && world.Events.All(gameEvent => gameEvent.Id != _selectedEventId.Value))
        {
            _selectedEventId = null;
        }
    }

    private void RefreshRunState()
    {
        if (_playPauseButton != null)
        {
            _playPauseButton.Text = _isRunning ? "||" : ">";
            _playPauseButton.TooltipText = _isRunning
                ? $"{T("ui.running")} {FormatSpeedLabel(_ticksPerSecond)}"
                : $"{T("ui.paused")} {FormatSpeedLabel(_ticksPerSecond)}";
        }

        ApplySpeedButtonState(_speed1Button, _ticksPerSecond == SpeedOneTicksPerSecond);
        ApplySpeedButtonState(_speed5Button, _ticksPerSecond == SpeedTwoTicksPerSecond);
        ApplySpeedButtonState(_speed20Button, _ticksPerSecond == SpeedThreeTicksPerSecond);
    }

    private static string FormatSpeedLabel(int ticksPerSecond)
    {
        return ticksPerSecond switch
        {
            SpeedOneTicksPerSecond => "5m/s",
            SpeedTwoTicksPerSecond => "1h/s",
            SpeedThreeTicksPerSecond => "6h/s",
            _ => $"{ticksPerSecond}m/s"
        };
    }

    private static void ApplySpeedButtonState(Button? button, bool active)
    {
        if (button == null) return;

        button.AddThemeStyleboxOverride(
            "normal",
            active
                ? UiTheme.PanelStyle(UiTheme.ButtonPressed, UiTheme.Info, 5, 2)
                : UiTheme.ButtonStyle(UiTheme.Button));
        button.AddThemeStyleboxOverride(
            "hover",
            active
                ? UiTheme.PanelStyle(UiTheme.ButtonPressed, UiTheme.Info, 5, 2)
                : UiTheme.ButtonStyle(UiTheme.ButtonHover));
        button.AddThemeColorOverride("font_color", active ? UiTheme.Text : UiTheme.TextMuted);
    }

    private void RebuildContext(WorldState world)
    {
        ClearChildren(_contextList);
        if (!string.IsNullOrWhiteSpace(_systemMessage))
        {
            AddPanelRows(_contextList, T("ui.system"), "I")
                .AddChild(CreateWrappedLabel(_systemMessage, UiTheme.Info));
        }

        var selectedDistrict = _selectedDistrictId.HasValue
            ? world.Districts.FirstOrDefault(d => d.Id == _selectedDistrictId.Value)
            : null;
        var selectedEvent = _selectedEventId.HasValue
            ? world.Events.FirstOrDefault(e => e.Id == _selectedEventId.Value)
            : null;
        var selectedMapObject = _districtMap.GetMapObject(_selectedMapObjectId);

        if (selectedEvent != null)
        {
            RebuildEventDetails(world, selectedEvent);
            return;
        }

        if (selectedMapObject != null)
        {
            RebuildMapObjectDetails(world, selectedMapObject);
            return;
        }

        if (selectedDistrict == null)
        {
            var cityDistrict = SingleCityDistrict(world);
            AddPanelRows(_contextList, T("ui.city_status"), "I")
                .AddChild(CreateWrappedLabel(
                    cityDistrict == null ? T("ui.city_context_hint") : cityDistrict.Name,
                    UiTheme.TextMuted));
            var rows = AddPanelRows(_contextList, T("ui.overview"), "O");
            rows.AddChild(CreateStatRow(T("ui.population"), world.GetTotalPopulation().ToString(CultureInfo.InvariantCulture)));
            rows.AddChild(CreateStatRow(T("ui.budget"), FormatMoney(world.Budget)));
            rows.AddChild(CreateStatRow(T("ui.support"), FormatPercent(world.SupportRating)));
            rows.AddChild(CreateStatRow(T("ui.satisfaction"), FormatPercent(world.GetAverageSatisfaction())));
            rows.AddChild(CreateStatRow(T("ui.unemployment"), FormatPercent(world.LastUnemploymentRate)));
            if (cityDistrict != null)
            {
                rows.AddChild(CreateStatRow(T("ui.safety"), FormatPercent(cityDistrict.AverageSafetySatisfaction)));
                rows.AddChild(CreateStatRow(T("ui.housing"), $"{cityDistrict.OccupiedHousing}/{cityDistrict.HousingCapacity}"));
                rows.AddChild(CreateStatRow(T("ui.jobs"), $"{cityDistrict.TotalJobs - cityDistrict.OpenJobs}/{cityDistrict.TotalJobs}"));
                rows.AddChild(CreateStatRow(T("ui.services"), FormatPercent(cityDistrict.ServiceLevel)));
                rows.AddChild(CreateStatRow(T("ui.crisis"), FormatPercent(cityDistrict.CrisisRisk), cityDistrict.CrisisRisk > 60f ? UiTheme.Danger : UiTheme.Text));
            }
            rows.AddChild(CreateStatRow(T("ui.active_projects"), world.Projects.Count(p => !p.Completed).ToString(CultureInfo.InvariantCulture)));
            rows.AddChild(CreateStatRow(T("ui.complete_projects"), world.Projects.Count(p => p.Completed).ToString(CultureInfo.InvariantCulture)));
            AddCityIssueCards(_contextList, world);
            AddRecentEventRows(_contextList, world, world.Events.OrderByDescending(e => e.CreatedAtTick).Take(3), T("ui.recent_events"));
            AddProjectProgressList(_contextList, world.Projects.Where(p => !p.Completed).OrderBy(p => p.RemainingTicks), T("ui.active_projects"));
            return;
        }

        var districtRows = AddPanelRows(_contextList, selectedDistrict.Name, "D");
        districtRows.AddChild(CreateStatRow(T("ui.population"), selectedDistrict.Population.ToString(CultureInfo.InvariantCulture)));
        districtRows.AddChild(CreateStatRow(T("ui.support"), FormatPercent(selectedDistrict.SupportRating), StatusColor(selectedDistrict.SupportRating)));
        districtRows.AddChild(CreateStatRow(T("ui.satisfaction"), FormatPercent(selectedDistrict.AverageSatisfaction)));
        districtRows.AddChild(CreateStatRow(T("ui.safety"), FormatPercent(selectedDistrict.AverageSafetySatisfaction)));
        districtRows.AddChild(CreateStatRow(T("ui.housing"), $"{selectedDistrict.OccupiedHousing}/{selectedDistrict.HousingCapacity}"));
        districtRows.AddChild(CreateStatRow(T("ui.jobs"), $"{selectedDistrict.TotalJobs - selectedDistrict.OpenJobs}/{selectedDistrict.TotalJobs}"));
        districtRows.AddChild(CreateStatRow(T("ui.services"), FormatPercent(selectedDistrict.ServiceLevel)));
        districtRows.AddChild(CreateStatRow(T("ui.crisis"), FormatPercent(selectedDistrict.CrisisRisk), selectedDistrict.CrisisRisk > 60f ? UiTheme.Danger : UiTheme.Text));
        AddDistrictIssueCards(_contextList, world, selectedDistrict);
        AddRecentEventRows(
            _contextList,
            world,
            world.Events.Where(e => IsEventRelevantToDistrict(e, selectedDistrict)).OrderByDescending(e => e.CreatedAtTick).Take(3),
            T("ui.recent_events"));

        var projectRows = AddPanelRows(_contextList, T("ui.active_projects"), "P");
        var districtProjects = world.Projects
            .Where(p => p.DistrictId == selectedDistrict.Id && !p.Completed)
            .OrderBy(p => p.RemainingTicks)
            .ToList();
        if (districtProjects.Count == 0)
        {
            projectRows.AddChild(CreateWrappedLabel(T("ui.no_projects"), UiTheme.TextMuted));
        }
        else
        {
            AddProjectProgressRows(projectRows, districtProjects);
        }
    }

    private void RebuildMapObjectDetails(WorldState world, PlacedMapObject mapObject)
    {
        var rows = AddPanelRows(_contextList, MapObjectTitle(world, mapObject), MapObjectIcon(world, mapObject));
        rows.AddChild(CreateButton(T("ui.back"), ClearSelection, 112, T("ui.back")));
        rows.AddChild(CreateStatRow(T("ui.object_type"), MapObjectTypeLabel(mapObject)));
        rows.AddChild(CreateStatRow(T("ui.object_size"), $"{mapObject.FootprintWidth} x {mapObject.FootprintLength}"));
        rows.AddChild(CreateStatRow(T("ui.object_position"), $"{mapObject.Position.X}, {mapObject.Position.Y}"));
        rows.AddChild(CreateStatRow(T("ui.object_status"), MapObjectStatusText(world, mapObject), MapObjectStatusColor(world, mapObject)));

        switch (mapObject.EntityKind)
        {
            case MapObjectEntityKind.Business:
                AddBusinessObjectDetails(rows, world.Businesses.FirstOrDefault(business => business.Id == mapObject.EntityId));
                break;
            case MapObjectEntityKind.HousingUnit:
                AddHousingObjectDetails(rows, world.HousingUnits.FirstOrDefault(housing => housing.Id == mapObject.EntityId));
                break;
            case MapObjectEntityKind.GovernmentProject:
                AddProjectObjectDetails(rows, world.Projects.FirstOrDefault(project => project.Id == mapObject.EntityId));
                break;
            case MapObjectEntityKind.GameEvent:
                AddEventObjectDetails(rows, world.Events.FirstOrDefault(gameEvent => gameEvent.Id == mapObject.EntityId));
                break;
        }

        var eventRows = AddPanelRows(_contextList, T("ui.object_events"), "!");
        var localEvents = MapObjectLocalEvents(world, mapObject).ToList();
        if (localEvents.Count == 0)
        {
            eventRows.AddChild(CreateWrappedLabel(T("ui.no_object_events"), UiTheme.TextMuted));
        }
        else
        {
            foreach (var gameEvent in localEvents)
            {
                eventRows.AddChild(CreateEventCard(world, gameEvent, compact: true));
            }
        }
    }

    private void AddBusinessObjectDetails(Container rows, Business? business)
    {
        if (business == null) return;

        rows.AddChild(CreateStatRow(T("ui.staff"), $"{business.EmployeeIds.Count}/{business.MaxEmployees}"));
        rows.AddChild(CreateStatRow(T("ui.business_cash"), FormatMoney(business.Cash)));
        rows.AddChild(CreateStatRow(T("ui.business_output"), business.BaseOutput.ToString("F0", CultureInfo.InvariantCulture)));
        rows.AddChild(CreateStatRow(T("ui.business_product"), string.IsNullOrWhiteSpace(business.ProductionType) ? "-" : business.ProductionType));
    }

    private void AddHousingObjectDetails(Container rows, HousingUnit? housing)
    {
        if (housing == null) return;

        rows.AddChild(CreateStatRow(T("ui.capacity"), housing.Capacity.ToString(CultureInfo.InvariantCulture)));
        rows.AddChild(CreateStatRow(T("ui.occupied"), housing.IsOccupied ? T("ui.yes") : T("ui.no")));
        rows.AddChild(CreateStatRow(T("ui.rent"), FormatMoney(housing.RentPerTick)));
    }

    private void AddProjectObjectDetails(Container rows, GovernmentProject? project)
    {
        if (project == null) return;

        rows.AddChild(CreateStatRow(T("ui.project"), ProjectTypeLabel(project.Type)));
        rows.AddChild(CreateStatRow(T("ui.time"), project.Completed ? T("ui.complete") : FormatDurationTicks(project.RemainingTicks)));
        if (!project.Completed)
        {
            var progress = project.DurationTicks <= 0
                ? 100f
                : Math.Clamp((project.DurationTicks - project.RemainingTicks) / (float)project.DurationTicks * 100f, 0f, 100f);
            rows.AddChild(CreateStatRow(T("ui.progress"), FormatPercent(progress)));
        }
    }

    private void AddEventObjectDetails(Container rows, GameEvent? gameEvent)
    {
        if (gameEvent == null) return;

        rows.AddChild(CreateStatRow(T("ui.event_type"), gameEvent.Type.ToString(), EventColor(gameEvent)));
        rows.AddChild(CreateWrappedLabel(gameEvent.Description, UiTheme.TextMuted));
        rows.AddChild(CreateButton(T("ui.event_details"), () =>
        {
            _selectedEventId = gameEvent.Id;
            _selectedMapObjectId = null;
            _eventMessage = null;
            Refresh();
        }, 132, T("ui.event_details")));
    }

    private static IEnumerable<GameEvent> MapObjectLocalEvents(WorldState world, PlacedMapObject mapObject)
    {
        return world.Events
            .Where(gameEvent =>
                !gameEvent.IsResolved &&
                gameEvent.TargetEntityKind == mapObject.EntityKind &&
                gameEvent.TargetEntityId == mapObject.EntityId)
            .OrderByDescending(gameEvent => gameEvent.CreatedAtTick);
    }

    private void RebuildProjects(WorldState world)
    {
        ClearChildren(_projectList);

        var intro = AddPanelRows(_projectList, T("ui.quick_actions"), "A");
        var projectDistrictId = ProjectTargetDistrictId(world);
        intro.AddChild(CreateWrappedLabel(IsSingleCityMode(world) || !projectDistrictId.HasValue ? T("ui.city_wide") : DistrictName(world, projectDistrictId.Value), UiTheme.TextMuted));
        _projectActionLabel = CreateWrappedLabel(_projectMessage ?? T("ui.choose_project"));
        intro.AddChild(_projectActionLabel);

        foreach (var type in Enum.GetValues<ProjectType>().Where(t => t != ProjectType.Custom))
        {
            var project = GovernmentProject.CreateTyped(type, projectDistrictId);
            var rows = AddPanelRows(_projectList, ProjectTypeLabel(type), ProjectIcon(type));
            rows.AddChild(CreateStatRow(T("ui.project_upfront_cost"), FormatMoney(project.Cost)));
            rows.AddChild(CreateStatRow(T("ui.project_ongoing_cost"), FormatMoney(_bridge.World.ProjectOperatingExpensePerTick)));
            rows.AddChild(CreateStatRow(T("ui.time"), FormatDurationTicks(project.DurationTicks)));
            rows.AddChild(CreateWrappedLabel(Tf(
                "ui.effects_summary",
                FormatSigned(project.SupportEffect),
                FormatSigned(project.HousingSatisfactionEffect),
                FormatSigned(project.SafetySatisfactionEffect)), UiTheme.TextMuted));

            var button = CreateButton(T("ui.start"), () => StartProject(type), 96);
            button.Disabled = world.Budget < project.Cost;
            rows.AddChild(button);
        }
    }

    private void RebuildProjectsIfNeeded(WorldState world)
    {
        var nextKey = BuildProjectPanelKey(world);
        if (_projectPanelKey == nextKey) return;

        _projectPanelKey = nextKey;
        RebuildProjects(world);
    }

    private string BuildProjectPanelKey(WorldState world)
    {
        var affordability = string.Join(
            ",",
            Enum.GetValues<ProjectType>()
                .Where(type => type != ProjectType.Custom)
                .Select(type =>
                {
                    var project = GovernmentProject.CreateTyped(type, ProjectTargetDistrictId(world));
                    return world.Budget >= project.Cost ? "1" : "0";
                }));

        return string.Join(
            "|",
            _settings.Language,
            ProjectTargetDistrictId(world)?.ToString(CultureInfo.InvariantCulture) ?? "city",
            _projectMessage ?? string.Empty,
            world.ProjectOperatingExpensePerTick.ToString("F2", CultureInfo.InvariantCulture),
            affordability);
    }

    private void RebuildEventDetails(WorldState world, GameEvent gameEvent)
    {
        var rows = AddPanelRows(_contextList, T("ui.event_details"), EventIcon(gameEvent));
        rows.AddChild(CreateButton(T("ui.back"), ReturnFromEventDetails, 112, T("ui.back")));
        rows.AddChild(CreateWrappedLabel(gameEvent.Title));
        rows.AddChild(CreateWrappedLabel(gameEvent.Description, UiTheme.TextMuted));
        rows.AddChild(CreateStatRow(T("ui.time"), gameEvent.CreatedAtTick.ToString(CultureInfo.InvariantCulture)));
        rows.AddChild(CreateStatRow(T("ui.event_type"), gameEvent.Type.ToString(), EventColor(gameEvent)));
        rows.AddChild(CreateStatRow(
            T("ui.event_resolved"),
            gameEvent.IsResolved ? T("ui.event_resolved") : T("ui.event_unresolved"),
            gameEvent.IsResolved ? UiTheme.Success : UiTheme.Warning));

        if (!string.IsNullOrWhiteSpace(_eventMessage))
        {
            rows.AddChild(CreateWrappedLabel(_eventMessage, UiTheme.Info));
        }

        if (!gameEvent.HasChoices)
        {
            rows.AddChild(CreateWrappedLabel(T("ui.no_event_choices"), UiTheme.TextMuted));
            return;
        }

        if (gameEvent.IsResolved)
        {
            AddResolvedEventResult(_contextList, gameEvent);
            return;
        }

        var choiceRows = AddPanelRows(_contextList, T("ui.event_choices"), "!");
        foreach (var choice in gameEvent.Choices)
        {
            var choicePanel = CreatePanel(UiTheme.BuildCard);
            choicePanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            choiceRows.AddChild(choicePanel);

            var choiceContent = new VBoxContainer();
            choiceContent.MouseFilter = MouseFilterEnum.Ignore;
            choiceContent.AddThemeConstantOverride("separation", 5);
            choicePanel.AddChild(choiceContent);

            var label = new Label
            {
                Text = choice.Label,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            UiTheme.ApplyLabel(label, 13);
            choiceContent.AddChild(label);

            if (!string.IsNullOrWhiteSpace(choice.Description))
            {
                choiceContent.AddChild(CreateWrappedLabel(choice.Description, UiTheme.TextMuted));
            }

            choiceContent.AddChild(CreateWrappedLabel(ChoiceEffectsText(choice), UiTheme.TextMuted));
            var button = CreateButton(T("ui.apply_choice"), () => ResolveSelectedEventChoice(gameEvent.Id, choice.Id), 104);
            button.Disabled = world.Budget + choice.BudgetEffect < 0f;
            choiceContent.AddChild(button);
        }
    }

    private void AddResolvedEventResult(Container parent, GameEvent gameEvent)
    {
        var rows = AddPanelRows(parent, T("ui.event_result"), "O");
        var selectedChoice = gameEvent.Choices.FirstOrDefault(choice => choice.Id == gameEvent.SelectedChoiceId);
        if (selectedChoice == null)
        {
            rows.AddChild(CreateWrappedLabel(T("ui.event_resolved_no_choice"), UiTheme.TextMuted));
            return;
        }

        rows.AddChild(CreateStatRow(T("ui.selected_response"), selectedChoice.Label, UiTheme.Success));
        if (!string.IsNullOrWhiteSpace(selectedChoice.Description))
        {
            rows.AddChild(CreateWrappedLabel(selectedChoice.Description, UiTheme.TextMuted));
        }

        rows.AddChild(CreateWrappedLabel(ChoiceEffectsText(selectedChoice), UiTheme.TextMuted));
        rows.AddChild(CreateWrappedLabel(T("ui.event_no_more_choices"), UiTheme.Success));
    }

    private void RebuildDiagnostics(WorldState world)
    {
        ClearChildren(_diagnosticsList);
        var previous = _previousAnalytics;
        var analytics = AnalyticsSnapshot.FromWorld(world);

        var peopleRows = AddPanelRows(_diagnosticsList, T("ui.analytics_people"), "P");
        peopleRows.AddChild(CreateStatRow(T("ui.population"), analytics.Population.ToString(CultureInfo.InvariantCulture)));
        peopleRows.AddChild(CreateStatRow(T("ui.analytics_households"), analytics.Households.ToString(CultureInfo.InvariantCulture)));
        peopleRows.AddChild(CreateStatRow(T("ui.analytics_employed"), $"{analytics.Employed}/{analytics.Workforce}"));
        peopleRows.AddChild(CreateStatRow(T("ui.analytics_unemployed"), analytics.Unemployed.ToString(CultureInfo.InvariantCulture), analytics.Unemployed > 0 ? UiTheme.Warning : UiTheme.Text));
        peopleRows.AddChild(CreateStatRow(T("ui.analytics_students_retired"), $"{analytics.Students}/{analytics.Retired}"));

        var businessRows = AddPanelRows(_diagnosticsList, T("ui.analytics_business"), "B");
        businessRows.AddChild(CreateStatRow(T("ui.active"), $"{analytics.ActiveBusinesses}/{analytics.Businesses}"));
        businessRows.AddChild(CreateStatRow(T("ui.jobs"), $"{analytics.FilledJobs}/{analytics.JobCapacity}"));
        businessRows.AddChild(CreateStatRow(T("ui.analytics_revenue"), FormatMoney(analytics.BusinessRevenue)));
        businessRows.AddChild(CreateStatRow(T("ui.analytics_expenses"), FormatMoney(analytics.BusinessExpenses)));
        businessRows.AddChild(CreateStatRow(T("ui.analytics_profit"), FormatSignedMoney(analytics.BusinessProfit), analytics.BusinessProfit < 0f ? UiTheme.Danger : UiTheme.Success));
        businessRows.AddChild(CreateStatRow(T("ui.analytics_avg_level"), analytics.AverageBusinessLevel.ToString("F1", CultureInfo.InvariantCulture)));
        businessRows.AddChild(CreateStatRow(T("ui.analytics_avg_quality"), analytics.AverageProductQuality.ToString("F2", CultureInfo.InvariantCulture)));
        businessRows.AddChild(CreateStatRow(T("ui.analytics_investment"), FormatMoney(analytics.LastBusinessInvestment)));

        var businessHealthRows = AddPanelRows(_diagnosticsList, T("ui.analytics_business_health"), "B");
        if (analytics.BusinessHealth.Count == 0)
        {
            businessHealthRows.AddChild(CreateWrappedLabel(T("ui.analytics_no_business_health"), UiTheme.TextMuted));
        }
        else
        {
            foreach (var business in analytics.BusinessHealth)
            {
                var color = business.ClosureRisk >= 70f
                    ? UiTheme.Danger
                    : business.ClosureRisk >= 40f
                        ? UiTheme.Warning
                        : UiTheme.Success;
                var row = CreateStatRow(business.Name, FormatPercent(business.ClosureRisk), color);
                row.TooltipText = Tf(
                    "ui.analytics_business_health_tooltip",
                    business.Status,
                    business.BusinessLevel.ToString(CultureInfo.InvariantCulture),
                    business.ProductQuality.ToString("F2", CultureInfo.InvariantCulture),
                    FormatMoney(business.Cash),
                    $"{business.LastProducedUnits:F1}/{business.ProductionCapacity:F1}",
                    FormatMoney(business.LastLocalSalesRevenue),
                    FormatMoney(business.LastExternalSalesRevenue),
                    business.ConsecutiveLossTicks.ToString(CultureInfo.InvariantCulture));
                businessHealthRows.AddChild(row);
            }
        }

        var governmentRows = AddPanelRows(_diagnosticsList, T("ui.analytics_government"), "$");
        governmentRows.AddChild(CreateStatRow(T("ui.budget"), FormatMoney(analytics.Budget), analytics.Budget < 0f ? UiTheme.Danger : UiTheme.Text));
        governmentRows.AddChild(CreateStatRow(T("ui.analytics_net_budget"), FormatSignedMoney(analytics.NetBudgetChange), analytics.NetBudgetChange < 0f ? UiTheme.Warning : UiTheme.Success));
        governmentRows.AddChild(CreateStatRow(T("ui.analytics_income_tax"), FormatMoney(analytics.IncomeTax)));
        governmentRows.AddChild(CreateStatRow(T("ui.analytics_business_tax"), FormatMoney(analytics.BusinessTax)));
        governmentRows.AddChild(CreateStatRow(T("ui.analytics_operating_expenses"), FormatMoney(analytics.OperatingExpenses)));
        governmentRows.AddChild(CreateStatRow(T("ui.analytics_government_local_spending"), FormatMoney(analytics.LocalGovernmentSpending)));
        governmentRows.AddChild(CreateStatRow(T("ui.analytics_government_external_spending"), FormatMoney(analytics.ExternalGovernmentSpending), analytics.ExternalGovernmentSpending > 0f ? UiTheme.Warning : UiTheme.Text));
        governmentRows.AddChild(CreateStatRow(T("ui.analytics_projects"), $"{analytics.ActiveProjects}/{analytics.CompletedProjects}"));

        var moneyRows = AddPanelRows(_diagnosticsList, T("ui.analytics_money_supply"), "$");
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_external_inflow"), FormatMoney(analytics.ExternalInflow), analytics.ExternalInflow > 0f ? UiTheme.Success : UiTheme.Text));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_external_outflow"), FormatMoney(analytics.ExternalOutflow), analytics.ExternalOutflow > 0f ? UiTheme.Warning : UiTheme.Text));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_internal_transfers"), FormatMoney(analytics.InternalTransfers)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_consumer_spending"), FormatMoney(analytics.ConsumerSpending)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_gross_wages"), FormatMoney(analytics.GrossWagesPaid)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_net_wages"), FormatMoney(analytics.NetWagesPaid)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_public_budget"), FormatMoney(analytics.Budget)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_citizen_cash"), FormatMoney(analytics.CitizenCash)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_citizen_income"), FormatMoney(analytics.CitizenIncome)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_business_cash"), FormatMoney(analytics.BusinessCash)));
        moneyRows.AddChild(CreateStatRow(T("ui.analytics_total_tracked"), FormatMoney(analytics.TrackedMoney)));

        var diagnosis = world.Economy.Diagnose(world);
        var diagnosisRows = AddPanelRows(_diagnosticsList, T("ui.economy_diagnosis"), "E");
        diagnosisRows.AddChild(CreateStatRow(T("ui.economy_trend"), EconomyTrendText(diagnosis.Trend), EconomyTrendColor(diagnosis.Trend)));
        diagnosisRows.AddChild(CreateStatRow(T("ui.economy_primary_reason"), EconomyReasonText(diagnosis.PrimaryReason), EconomyReasonColor(diagnosis.PrimaryReason)));
        diagnosisRows.AddChild(CreateStatRow(T("ui.economy_net_external"), FormatSignedMoney(diagnosis.NetExternalFlow), diagnosis.NetExternalFlow < 0f ? UiTheme.Warning : UiTheme.Success));
        diagnosisRows.AddChild(CreateStatRow(T("ui.economy_unmet_demand"), FormatMoney(diagnosis.UnmetDemand), diagnosis.UnmetDemand > 10f ? UiTheme.Warning : UiTheme.Success));
        diagnosisRows.AddChild(CreateStatRow(T("ui.economy_at_risk_businesses"), diagnosis.AtRiskBusinesses.ToString(CultureInfo.InvariantCulture), diagnosis.AtRiskBusinesses > 0 ? UiTheme.Warning : UiTheme.Success));

        var needsRows = AddPanelRows(_diagnosticsList, T("ui.analytics_need_trends"), "+");
        AddNeedTrend(needsRows, T("ui.analytics_food"), analytics.Food, previous?.Food);
        AddNeedTrend(needsRows, T("ui.housing"), analytics.Housing, previous?.Housing);
        AddNeedTrend(needsRows, T("ui.safety"), analytics.Safety, previous?.Safety);
        AddNeedTrend(needsRows, T("ui.analytics_healthcare"), analytics.Healthcare, previous?.Healthcare);
        AddNeedTrend(needsRows, T("ui.analytics_entertainment"), analytics.Entertainment, previous?.Entertainment);

        var demandRows = AddPanelRows(_diagnosticsList, T("ui.analytics_unmet_demand"), "D");
        AddDemandRow(demandRows, T("ui.analytics_food"), analytics.FoodDemand);
        AddDemandRow(demandRows, T("ui.analytics_goods"), analytics.GoodsDemand);
        AddDemandRow(demandRows, T("ui.analytics_healthcare"), analytics.HealthcareDemand);

        _previousAnalytics = analytics;
    }

    private void AddNeedTrend(Container parent, string title, float value, float? previousValue)
    {
        var delta = previousValue.HasValue ? value - previousValue.Value : 0f;
        var trend = Math.Abs(delta) < 0.05f ? "0" : FormatSigned(delta);
        var color = value < 50f ? UiTheme.Danger : value < 65f ? UiTheme.Warning : UiTheme.Success;
        parent.AddChild(CreateStatRow(title, $"{FormatPercent(value)} ({trend})", color));
        parent.AddChild(CreateAnalyticsBar(value, color));
    }

    private void AddDemandRow(Container parent, string title, DemandSnapshot demand)
    {
        var color = demand.UnmetDemand > 10f ? UiTheme.Warning : UiTheme.Success;
        var text = $"{FormatMoney(demand.UnmetDemand)} / {FormatMoney(demand.DesiredSpending)}";
        var row = CreateStatRow(title, text, color);
        row.TooltipText = Tf(
            "ui.analytics_demand_tooltip",
            FormatMoney(demand.AvailableSupplyValue),
            FormatMoney(demand.AvailableCash),
            demand.AverageQuality.ToString("F2", CultureInfo.InvariantCulture));
        parent.AddChild(row);
    }

    private static ProgressBar CreateAnalyticsBar(float value, Color color)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = Math.Clamp(value, 0f, 100f),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 7),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        bar.AddThemeStyleboxOverride("background", UiTheme.PanelStyle(UiTheme.Button, UiTheme.Button, 4, 0));
        bar.AddThemeStyleboxOverride("fill", UiTheme.PanelStyle(color, color, 4, 0));
        return bar;
    }

    private void ResolveSelectedEventChoice(int eventId, string choiceId)
    {
        var resolved = _bridge.ResolveEventChoice(eventId, choiceId);
        _eventMessage = resolved ? T("ui.choice_applied") : T("ui.choice_failed");
        Refresh();
    }

    private void StartProject(ProjectType type)
    {
        var districtId = ProjectTargetDistrictId(_bridge.World);
        var project = GovernmentProject.CreateTyped(type, districtId);
        var startedProject = _bridge.StartProject(type, districtId);
        _projectMessageProjectId = startedProject?.Id;
        _projectMessage = startedProject != null
            ? Tf("ui.started_project", project.Name, FormatMoney(project.Cost))
            : Tf("ui.not_enough_budget", project.Name, FormatMoney(project.Cost));
        Refresh();
    }

    private void SyncProjectMessage(WorldState world)
    {
        if (!_projectMessageProjectId.HasValue) return;

        var project = world.Projects.FirstOrDefault(p => p.Id == _projectMessageProjectId.Value);
        if (project == null)
        {
            _projectMessageProjectId = null;
            _projectMessage = null;
            return;
        }

        if (!project.Completed) return;

        _projectMessageProjectId = null;
        _projectMessage = Tf("ui.project_completed_message", project.Name);
    }

    private void SaveWorld()
    {
        try
        {
            var path = SavePath();
            _bridge.SaveWorld(path);
            _hasActiveGame = true;
            _systemMessage = Tf("ui.save_success", path);
        }
        catch (Exception ex)
        {
            _systemMessage = Tf("ui.save_failed", ex.Message);
        }

        RefreshCurrentScreen();
    }

    private void LoadWorld()
    {
        try
        {
            var path = SavePath();
            if (_bridge.LoadWorld(path))
            {
                _isRunning = false;
                _autoTickAccumulator = 0;
                _selectedDistrictId = null;
                _selectedEventId = null;
                _selectedMapObjectId = null;
                _projectMessage = null;
                _projectMessageProjectId = null;
                _eventMessage = null;
                _systemMessage = Tf("ui.load_success", path);
                _previousAnalytics = null;
                _hasActiveGame = true;
                _uiState = MainUiState.InGame;
            }
            else
            {
                _systemMessage = Tf("ui.load_missing", path);
            }
        }
        catch (Exception ex)
        {
            _systemMessage = Tf("ui.load_failed", ex.Message);
        }

        RefreshCurrentScreen();
    }

    private void RefreshCurrentScreen()
    {
        if (_uiState == MainUiState.InGame)
        {
            Refresh();
            return;
        }

        if (_uiState == MainUiState.PausedMenu)
        {
            BuildInterface();
            Refresh();
            return;
        }

        BuildInterface();
    }

    private void ClearSelection()
    {
        _selectedDistrictId = null;
        _selectedEventId = null;
        _selectedMapObjectId = null;
        _eventMessage = null;
        Refresh();
    }

    private void ReturnFromEventDetails()
    {
        _selectedEventId = null;
        _eventMessage = null;
        Refresh();
    }

    private void CycleContextTab()
    {
        var next = ((int)_contextPanelMode + 1) % 3;
        _contextPanelMode = (ContextPanelMode)next;
        _isContextPanelOpen = true;
        BuildInterface();
        Refresh();
    }

    private void ToggleContextPanel(ContextPanelMode mode)
    {
        if (_isContextPanelOpen && _contextPanelMode == mode)
        {
            _isContextPanelOpen = false;
        }
        else
        {
            _contextPanelMode = mode;
            _isContextPanelOpen = true;
        }

        BuildInterface();
        Refresh();
    }

    private void CloseContextPanel()
    {
        _isContextPanelOpen = false;
        BuildInterface();
        Refresh();
    }

    private static string SavePath()
    {
        return ProjectSettings.GlobalizePath("user://green_district_save.json");
    }

    private static string SettingsPath()
    {
        return ProjectSettings.GlobalizePath("user://settings.json");
    }

    private static bool HasSaveFile()
    {
        return File.Exists(SavePath());
    }

    private static bool IsSingleCityMode(WorldState world)
    {
        return world.Districts.Count == 1;
    }

    private static District? SingleCityDistrict(WorldState world)
    {
        return IsSingleCityMode(world) ? world.Districts[0] : null;
    }

    private int? ProjectTargetDistrictId(WorldState world)
    {
        return SingleCityDistrict(world)?.Id ?? _selectedDistrictId;
    }

    private void AddCityIssueCards(Container parent, WorldState world)
    {
        var issues = AddPanelRows(parent, T("ui.priorities"), "!");
        var added = false;
        var cityDistrict = SingleCityDistrict(world);

        if (world.Budget < 2000f)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.low_budget"), T("ui.issue.low_budget_hint"), UiTheme.Warning, "$"));
            added = true;
        }

        if (world.SupportRating < 55f)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.low_support"), T("ui.issue.low_support_hint"), UiTheme.Warning, "%"));
            added = true;
        }

        if (world.GetAverageSatisfaction() < 60f)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.low_satisfaction"), T("ui.issue.low_satisfaction_hint"), UiTheme.Warning, "+"));
            added = true;
        }

        if (world.LastUnemploymentRate > 15f)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.unemployment"), T("ui.issue.unemployment_hint"), UiTheme.Danger, "J"));
            added = true;
        }

        if (cityDistrict is { CrisisRisk: >= 35f } || cityDistrict is { HasActiveCrisis: true })
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.crisis_risk"), T("ui.issue.crisis_risk_hint"), UiTheme.Danger, "!"));
            added = true;
        }

        if (cityDistrict is { AvailableHousing: <= 0 })
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.housing_shortage"), T("ui.issue.housing_shortage_hint"), UiTheme.Warning, "H"));
            added = true;
        }

        if (cityDistrict is { AverageSafetySatisfaction: < 60f })
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.low_safety"), T("ui.issue.low_safety_hint"), UiTheme.Danger, "S"));
            added = true;
        }

        if (cityDistrict is { ServiceLevel: < 60f })
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.low_services"), T("ui.issue.low_services_hint"), UiTheme.Warning, "V"));
            added = true;
        }

        var openEvents = world.Events.Count(e => e.HasChoices && !e.IsResolved);
        if (openEvents > 0)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.pending_events"), Tf("ui.issue.pending_events_hint", openEvents), UiTheme.Info, "?"));
            added = true;
        }

        if (!added)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.stable_city"), T("ui.issue.stable_city_hint"), UiTheme.Success, "O"));
        }
    }

    private void AddDistrictIssueCards(Container parent, WorldState world, District district)
    {
        var issues = AddPanelRows(parent, T("ui.priorities"), "!");
        var added = false;
        var diagnosis = world.Economy.Diagnose(world, district.Id);

        if (district.CrisisRisk >= 35f || district.HasActiveCrisis)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.crisis_risk"), T("ui.issue.crisis_risk_hint"), UiTheme.Danger, "!"));
            added = true;
        }

        if (district.AvailableHousing <= 0)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.housing_shortage"), T("ui.issue.housing_shortage_hint"), UiTheme.Warning, "H"));
            added = true;
        }

        if (district.OpenJobs <= 0 && district.TotalJobs > 0)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.jobs_full"), T("ui.issue.jobs_full_hint"), UiTheme.Warning, "J"));
            added = true;
        }

        if (district.AverageSafetySatisfaction < 60f)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.low_safety"), T("ui.issue.low_safety_hint"), UiTheme.Danger, "S"));
            added = true;
        }

        if (district.ServiceLevel < 60f)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.low_services"), T("ui.issue.low_services_hint"), UiTheme.Warning, "V"));
            added = true;
        }

        if (diagnosis.PrimaryReason != EconomyDiagnosisReason.Balanced &&
            diagnosis.PrimaryReason != EconomyDiagnosisReason.LocalSpending &&
            diagnosis.PrimaryReason != EconomyDiagnosisReason.ExternalInflow)
        {
            issues.AddChild(CreateIssueCard(
                T("ui.issue.economy_reason"),
                Tf("ui.issue.economy_reason_hint", EconomyReasonText(diagnosis.PrimaryReason)),
                EconomyReasonColor(diagnosis.PrimaryReason),
                "$"));
            added = true;
        }

        if (!added)
        {
            issues.AddChild(CreateIssueCard(T("ui.issue.stable_district"), T("ui.issue.stable_district_hint"), UiTheme.Success, "O"));
        }
    }

    private Control CreateIssueCard(string title, string body, Color severity, string icon)
    {
        var panel = CreatePanel(UiTheme.BuildCard);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddThemeStyleboxOverride("panel", UiTheme.PanelStyle(UiTheme.BuildCard, severity, UiTheme.Radius, 2));

        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.AddThemeConstantOverride("separation", 7);
        panel.AddChild(row);
        row.AddChild(UiTheme.Icon(icon, severity));

        var text = new VBoxContainer();
        text.MouseFilter = MouseFilterEnum.Ignore;
        text.AddThemeConstantOverride("separation", 1);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(text);

        var label = new Label
        {
            Text = title,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(label, 12, severity);
        text.AddChild(label);
        text.AddChild(CreateWrappedLabel(body, UiTheme.TextMuted));
        return panel;
    }

    private void AddRecentEventRows(Container parent, WorldState world, IEnumerable<GameEvent> events, string title)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0) return;

        var rows = AddPanelRows(parent, title, "!");
        foreach (var gameEvent in eventList)
        {
            rows.AddChild(CreateEventCard(world, gameEvent, compact: false));
        }
    }

    private PanelContainer CreateEventCard(WorldState world, GameEvent gameEvent, bool compact)
    {
        var severity = EventColor(gameEvent);
        var background = gameEvent.IsResolved
            ? UiTheme.PanelAlt
            : EventBackground(gameEvent);
        var card = CreatePanel(background);
        card.CustomMinimumSize = new Vector2(0, compact ? 52 : 62);
        card.MouseFilter = MouseFilterEnum.Stop;
        card.TooltipText = gameEvent.HasChoices && !gameEvent.IsResolved ? T("ui.event_unresolved") : gameEvent.Type.ToString();
        card.AddThemeStyleboxOverride(
            "panel",
            UiTheme.PanelStyle(
                _selectedEventId == gameEvent.Id ? UiTheme.EventHover : background,
                _selectedEventId == gameEvent.Id ? UiTheme.Info : severity,
                UiTheme.Radius,
                gameEvent.HasChoices && !gameEvent.IsResolved ? 2 : 1));
        card.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _selectedEventId = gameEvent.Id;
                _selectedMapObjectId = null;
                _selectedDistrictId = IsSingleCityMode(world) ? null : EventDistrictId(gameEvent) ?? _selectedDistrictId;
                _eventMessage = null;
                Refresh();
            }
        };

        var root = new HBoxContainer();
        root.MouseFilter = MouseFilterEnum.Ignore;
        root.AddThemeConstantOverride("separation", 7);
        card.AddChild(root);
        root.AddChild(UiTheme.Icon(EventIcon(gameEvent), severity));

        var rows = new VBoxContainer();
        rows.MouseFilter = MouseFilterEnum.Ignore;
        rows.AddThemeConstantOverride("separation", 2);
        rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(rows);

        var header = new HBoxContainer();
        header.MouseFilter = MouseFilterEnum.Ignore;
        header.AddThemeConstantOverride("separation", 6);
        rows.AddChild(header);

        var title = new Label
        {
            Text = gameEvent.Title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(title, 12, severity);
        header.AddChild(title);

        var time = new Label
        {
            Text = EventTimeText(gameEvent),
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(compact ? 42 : 52, 0)
        };
        UiTheme.ApplyLabel(time, 10, UiTheme.TextWeak);
        header.AddChild(time);

        var body = new Label
        {
            Text = compact ? EventStatusText(gameEvent) : gameEvent.Description,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(body, 11, UiTheme.TextMuted);
        rows.AddChild(body);
        return card;
    }

    private Label CreateMetricPill(Container parent, string icon, string title, float width, bool primary = false, Color? accent = null)
    {
        var panel = CreatePanel(primary ? UiTheme.BuildCard : UiTheme.PanelAlt);
        panel.CustomMinimumSize = new Vector2(width, primary ? 38 : 32);
        panel.TooltipText = title;
        if (primary)
        {
            panel.AddThemeStyleboxOverride("panel", UiTheme.PanelStyle(UiTheme.BuildCard, accent ?? UiTheme.Border, UiTheme.Radius, 2));
        }
        parent.AddChild(panel);

        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.AddThemeConstantOverride("separation", 4);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(row);
        row.AddChild(UiTheme.Icon(icon, accent ?? UiTheme.TextMuted));

        var value = new Label
        {
            Text = "-",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(value, primary ? 15 : 12, primary ? UiTheme.Text : UiTheme.TextMuted);
        row.AddChild(value);
        return value;
    }

    private HBoxContainer CreateSectionTitle(string title, string icon)
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(UiTheme.Icon(icon, UiTheme.Info));

        var label = new Label { Text = title };
        UiTheme.ApplyLabel(label, 15);
        row.AddChild(label);
        return row;
    }

    private static PanelContainer CreatePanel(Color? color = null)
    {
        var panel = new PanelContainer();
        panel.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddThemeStyleboxOverride("panel", UiTheme.PanelStyle(color));
        panel.AddThemeConstantOverride("margin_left", 8);
        panel.AddThemeConstantOverride("margin_top", 6);
        panel.AddThemeConstantOverride("margin_right", 8);
        panel.AddThemeConstantOverride("margin_bottom", 6);
        return panel;
    }

    private static Button CreateButton(string text, Action action, float width = 88f, string? tooltip = null)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 32),
            TooltipText = tooltip ?? string.Empty
        };
        UiTheme.ApplyButton(button);
        button.Pressed += action;
        return button;
    }

    private static Button CreateMenuButton(string text, Action action)
    {
        var button = CreateButton(text, action, 240);
        button.CustomMinimumSize = new Vector2(240, 40);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        return button;
    }

    private static Control CreateMenuBottomPadding()
    {
        return new Control
        {
            CustomMinimumSize = new Vector2(0, 10),
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

    private Button CreateContextModeButton(string text, string tooltip, ContextPanelMode mode)
    {
        var button = CreateButton(text, () => ToggleContextPanel(mode), 38, tooltip);
        button.CustomMinimumSize = new Vector2(38, 38);
        button.AddThemeStyleboxOverride(
            "normal",
            _isContextPanelOpen && _contextPanelMode == mode
                ? UiTheme.PanelStyle(UiTheme.ButtonPressed, UiTheme.Info, 5, 2)
                : UiTheme.ButtonStyle(UiTheme.Button));
        return button;
    }

    private static Control CreateSpacer()
    {
        return new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    }

    private static VBoxContainer CreateTabList(TabContainer parent, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        parent.AddChild(scroll);

        var list = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(list);
        return list;
    }

    private VBoxContainer AddPanelRows(Container parent, string title, string icon)
    {
        var panel = CreatePanel(UiTheme.PanelAlt);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(panel);

        var rows = new VBoxContainer();
        rows.MouseFilter = MouseFilterEnum.Ignore;
        rows.AddThemeConstantOverride("separation", 5);
        panel.AddChild(rows);
        rows.AddChild(CreateSectionTitle(title, icon));
        return rows;
    }

    private static HBoxContainer CreateStatRow(string title, string value, Color? valueColor = null)
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.AddThemeConstantOverride("separation", 8);

        var label = new Label
        {
            Text = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(label, 12, UiTheme.TextMuted);
        row.AddChild(label);

        var valueLabel = new Label
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(82, 0),
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(valueLabel, 12, valueColor ?? UiTheme.Text);
        row.AddChild(valueLabel);
        return row;
    }

    private void AddProjectProgressList(Container parent, IEnumerable<GovernmentProject> projects, string title)
    {
        var projectRows = AddPanelRows(parent, title, "P");
        var projectList = projects.ToList();
        if (projectList.Count == 0)
        {
            projectRows.AddChild(CreateWrappedLabel(T("ui.no_projects"), UiTheme.TextMuted));
            return;
        }

        AddProjectProgressRows(projectRows, projectList);
    }

    private void AddProjectProgressRows(Container parent, IEnumerable<GovernmentProject> projects)
    {
        foreach (var project in projects)
        {
            parent.AddChild(CreateProjectProgressCard(project));
        }
    }

    private Control CreateProjectProgressCard(GovernmentProject project)
    {
        var rows = new VBoxContainer();
        rows.MouseFilter = MouseFilterEnum.Ignore;
        rows.AddThemeConstantOverride("separation", 3);

        var title = new HBoxContainer();
        title.MouseFilter = MouseFilterEnum.Ignore;
        title.AddThemeConstantOverride("separation", 6);
        rows.AddChild(title);
        title.AddChild(UiTheme.Icon(ProjectIcon(project.Type), UiTheme.Info));

        var name = new Label
        {
            Text = ProjectTypeLabel(project.Type),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        UiTheme.ApplyLabel(name, 12);
        title.AddChild(name);

        var done = Math.Max(0, project.DurationTicks - project.RemainingTicks);
        var total = Math.Max(1, project.DurationTicks);
        var progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = total,
            Value = done,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 8),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        progress.AddThemeStyleboxOverride("background", UiTheme.PanelStyle(UiTheme.Button, UiTheme.Button, 4, 0));
        progress.AddThemeStyleboxOverride("fill", UiTheme.PanelStyle(UiTheme.Info, UiTheme.Info, 4, 0));
        rows.AddChild(progress);

        var percent = done / (float)total * 100f;
        rows.AddChild(CreateWrappedLabel($"{FormatDurationTicks(project.RemainingTicks)} · {percent:F0}%", UiTheme.TextMuted));
        return rows;
    }

    private static Label CreateWrappedLabel(string text, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        UiTheme.ApplyLabel(label, 12, color ?? UiTheme.Text);
        return label;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static string FormatMoney(float value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedMoney(float value)
    {
        if (Math.Abs(value) < 0.001f) return "0";

        var sign = value > 0f ? "+" : "-";
        return sign + Math.Abs(value).ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(float value)
    {
        return value.ToString("F1", CultureInfo.InvariantCulture) + "%";
    }

    private string FormatDurationTicks(int ticks)
    {
        var remaining = Math.Max(0, ticks);
        var days = remaining / 1440;
        var hours = remaining % 1440 / 60;
        var minutes = remaining % 60;
        var isRu = _settings.Language == GameLanguage.Russian;

        if (days > 0 && hours > 0)
        {
            return isRu ? $"{days} д {hours} ч" : $"{days}d {hours}h";
        }

        if (days > 0)
        {
            return isRu ? $"{days} д" : $"{days}d";
        }

        if (hours > 0 && minutes > 0)
        {
            return isRu ? $"{hours} ч {minutes} м" : $"{hours}h {minutes}m";
        }

        if (hours > 0)
        {
            return isRu ? $"{hours} ч" : $"{hours}h";
        }

        return isRu ? $"{minutes} м" : $"{minutes}m";
    }

    private void LoadLocalizationDictionaries()
    {
        var root = FindRepositoryRoot();
        if (root == null) return;

        var localizationRoot = Path.Combine(root, "data", "localization");
        var en = Path.Combine(localizationRoot, "en.json");
        var ru = Path.Combine(localizationRoot, "ru.json");
        if (File.Exists(en)) _localization.LoadJsonFile(en);
        if (File.Exists(ru)) _localization.LoadJsonFile(ru);
    }

    private void LoadSettings()
    {
        try
        {
            var path = SettingsPath();
            if (File.Exists(path))
            {
                _settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path)) ?? GameSettings.Default();
            }
        }
        catch
        {
            _settings = GameSettings.Default();
        }

        _settings.Normalize();
    }

    private void SaveSettings()
    {
        try
        {
            var path = SettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _settings.Normalize();
            File.WriteAllText(path, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Settings are convenience state; gameplay should continue if persistence fails.
        }
    }

    private void PersistAndApplySettings()
    {
        _settings.Normalize();
        SaveSettings();
        ApplySettings();
    }

    private void ApplySettings()
    {
        ApplyAudioVolume("Master", _settings.MasterVolume);
        ApplyAudioVolume("Music", _settings.MusicVolume);
        ApplyAudioVolume("Effects", _settings.EffectsVolume);

        if (_settings.WindowMode == WindowModeSetting.Fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            return;
        }

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        var resolution = AvailableResolutions[Math.Clamp(_settings.ResolutionIndex, 0, AvailableResolutions.Length - 1)];
        DisplayServer.WindowSetSize(resolution);
    }

    private static void ApplyAudioVolume(string busName, float volume)
    {
        var bus = AudioServer.GetBusIndex(busName);
        if (bus < 0) return;

        var clamped = Math.Clamp(volume, 0f, 1f);
        AudioServer.SetBusMute(bus, clamped <= 0.001f);
        AudioServer.SetBusVolumeDb(bus, clamped <= 0.001f ? -80f : Mathf.LinearToDb(clamped));
    }

    private static string? FindRepositoryRoot()
    {
        var candidates = new[]
        {
            ProjectSettings.GlobalizePath("res://.."),
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "data", "localization", "en.json")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private string T(string key)
    {
        return _localization.Translate(key);
    }

    private string Tf(string key, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, T(key), args);
    }

    private string ProjectTypeLabel(ProjectType type)
    {
        return type switch
        {
            ProjectType.Road => T("project.type.road"),
            ProjectType.Clinic => T("project.type.clinic"),
            ProjectType.School => T("project.type.school"),
            ProjectType.Police => T("project.type.police"),
            ProjectType.Housing => T("project.type.housing"),
            ProjectType.Park => T("project.type.park"),
            _ => type.ToString()
        };
    }

    private static string ProjectIcon(ProjectType type)
    {
        return type switch
        {
            ProjectType.Road => "R",
            ProjectType.Clinic => "H",
            ProjectType.School => "S",
            ProjectType.Police => "P",
            ProjectType.Housing => "B",
            ProjectType.Park => "G",
            _ => "A"
        };
    }

    private static Color StatusColor(float percent)
    {
        if (percent < 40f) return UiTheme.Danger;
        if (percent < 60f) return UiTheme.Warning;
        return UiTheme.Success;
    }

    private static Color EventColor(GameEvent gameEvent)
    {
        return gameEvent.Type switch
        {
            EventType.Crisis => UiTheme.Danger,
            EventType.Decision => UiTheme.Warning,
            EventType.Economic => UiTheme.Info,
            EventType.Political => UiTheme.Info,
            _ => UiTheme.Text
        };
    }

    private static string EventIcon(GameEvent gameEvent)
    {
        return gameEvent.Type switch
        {
            EventType.Crisis => "!",
            EventType.Decision => "?",
            EventType.Economic => "$",
            EventType.Election => "V",
            EventType.Political => "P",
            EventType.Social => "S",
            _ => "I"
        };
    }

    private static Color EventBackground(GameEvent gameEvent)
    {
        return gameEvent.Type switch
        {
            EventType.Crisis => UiTheme.EventHover,
            EventType.Decision => UiTheme.BuildCard,
            EventType.Economic => UiTheme.PanelAlt,
            EventType.Election => UiTheme.PanelAlt,
            EventType.Political => UiTheme.PanelAlt,
            EventType.Social => UiTheme.PanelAlt,
            _ => UiTheme.Event
        };
    }

    private static string EventTimeText(GameEvent gameEvent)
    {
        var tick = Math.Max(0, gameEvent.CreatedAtTick);
        var day = tick / 1440;
        var minuteOfDay = tick % 1440;
        return $"D{day} {minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
    }

    private string EventStatusText(GameEvent gameEvent)
    {
        if (gameEvent.HasChoices)
        {
            return gameEvent.IsResolved ? T("ui.event_resolved") : T("ui.event_unresolved");
        }

        return gameEvent.Type.ToString();
    }

    private static bool IsEventRelevantToDistrict(GameEvent gameEvent, District district)
    {
        if (gameEvent.Choices.Any(choice => choice.DistrictId == district.Id)) return true;

        return gameEvent.Title.Contains(district.Name, StringComparison.OrdinalIgnoreCase)
            || gameEvent.Description.Contains(district.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int? EventDistrictId(GameEvent gameEvent)
    {
        return gameEvent.Choices
            .Select(choice => choice.DistrictId)
            .FirstOrDefault(districtId => districtId.HasValue);
    }

    private string MapObjectTitle(WorldState world, PlacedMapObject mapObject)
    {
        return mapObject.EntityKind switch
        {
            MapObjectEntityKind.Business => world.Businesses.FirstOrDefault(business => business.Id == mapObject.EntityId)?.Name ?? T("ui.object_business"),
            MapObjectEntityKind.HousingUnit => $"{T("ui.object_housing")} #{mapObject.EntityId?.ToString(CultureInfo.InvariantCulture) ?? mapObject.Id}",
            MapObjectEntityKind.GovernmentProject => world.Projects.FirstOrDefault(project => project.Id == mapObject.EntityId)?.Name ?? T("ui.object_project"),
            MapObjectEntityKind.GameEvent => world.Events.FirstOrDefault(gameEvent => gameEvent.Id == mapObject.EntityId)?.Title ?? T("ui.object_event"),
            _ => mapObject.AssetKey
        };
    }

    private string MapObjectIcon(WorldState world, PlacedMapObject mapObject)
    {
        return mapObject.EntityKind switch
        {
            MapObjectEntityKind.Business => "$",
            MapObjectEntityKind.HousingUnit => "H",
            MapObjectEntityKind.GovernmentProject => world.Projects.FirstOrDefault(project => project.Id == mapObject.EntityId) is { } project
                ? ProjectIcon(project.Type)
                : "P",
            MapObjectEntityKind.GameEvent => world.Events.FirstOrDefault(gameEvent => gameEvent.Id == mapObject.EntityId) is { } gameEvent
                ? EventIcon(gameEvent)
                : "!",
            _ => mapObject.Type == PlacedMapObjectType.Park ? "G" : "B"
        };
    }

    private string MapObjectTypeLabel(PlacedMapObject mapObject)
    {
        return mapObject.Type switch
        {
            PlacedMapObjectType.Business => T("ui.object_business"),
            PlacedMapObjectType.Housing => T("ui.object_housing"),
            PlacedMapObjectType.GovernmentProject => T("ui.object_project"),
            PlacedMapObjectType.Service => T("ui.object_service"),
            PlacedMapObjectType.Park => T("ui.object_park"),
            PlacedMapObjectType.Marker => T("ui.object_event"),
            _ => T("ui.object_building")
        };
    }

    private string MapObjectStatusText(WorldState world, PlacedMapObject mapObject)
    {
        return mapObject.EntityKind switch
        {
            MapObjectEntityKind.Business => world.Businesses.FirstOrDefault(business => business.Id == mapObject.EntityId) is { } business
                ? BusinessStatusLabel(business.Status)
                : "-",
            MapObjectEntityKind.HousingUnit => world.HousingUnits.FirstOrDefault(housing => housing.Id == mapObject.EntityId) is { } housing
                ? housing.IsOccupied ? T("ui.occupied") : T("ui.available")
                : "-",
            MapObjectEntityKind.GovernmentProject => world.Projects.FirstOrDefault(project => project.Id == mapObject.EntityId) is { } project
                ? project.Completed ? T("ui.completed") : T("ui.in_progress")
                : "-",
            MapObjectEntityKind.GameEvent => world.Events.FirstOrDefault(gameEvent => gameEvent.Id == mapObject.EntityId) is { } gameEvent
                ? gameEvent.IsResolved ? T("ui.event_resolved") : T("ui.event_unresolved")
                : "-",
            _ => T("ui.available")
        };
    }

    private static Color MapObjectStatusColor(WorldState world, PlacedMapObject mapObject)
    {
        return mapObject.EntityKind switch
        {
            MapObjectEntityKind.Business => world.Businesses.FirstOrDefault(business => business.Id == mapObject.EntityId) is { Status: BusinessStatus.Active }
                ? UiTheme.Success
                : UiTheme.Warning,
            MapObjectEntityKind.GameEvent => world.Events.FirstOrDefault(gameEvent => gameEvent.Id == mapObject.EntityId) is { IsResolved: false }
                ? UiTheme.Warning
                : UiTheme.Success,
            MapObjectEntityKind.GovernmentProject => world.Projects.FirstOrDefault(project => project.Id == mapObject.EntityId) is { Completed: false }
                ? UiTheme.Info
                : UiTheme.Success,
            _ => UiTheme.Text
        };
    }

    private string ChoiceEffectsText(EventChoice choice)
    {
        var parts = new List<string>();
        if (Math.Abs(choice.BudgetEffect) > 0.001f)
        {
            parts.Add($"{T("ui.budget_effect")} {FormatSigned(choice.BudgetEffect)}");
        }

        if (Math.Abs(choice.SupportEffect) > 0.001f)
        {
            parts.Add($"{T("ui.support_effect")} {FormatSigned(choice.SupportEffect)}");
        }

        if (Math.Abs(choice.SafetySatisfactionEffect) > 0.001f)
        {
            parts.Add($"{T("ui.safety")} {FormatSigned(choice.SafetySatisfactionEffect)}");
        }

        if (Math.Abs(choice.HealthcareSatisfactionEffect) > 0.001f)
        {
            parts.Add("Health " + FormatSigned(choice.HealthcareSatisfactionEffect));
        }

        return parts.Count == 0 ? "-" : string.Join(" | ", parts);
    }

    private static string BusinessStatusLabel(BusinessStatus status)
    {
        return status switch
        {
            BusinessStatus.Active => "Active",
            _ => status.ToString()
        };
    }

    private string EconomyTrendText(EconomyTrend trend)
    {
        return trend switch
        {
            EconomyTrend.Growing => T("ui.economy_trend_growing"),
            EconomyTrend.Shrinking => T("ui.economy_trend_shrinking"),
            _ => T("ui.economy_trend_stagnant")
        };
    }

    private string EconomyReasonText(EconomyDiagnosisReason reason)
    {
        return reason switch
        {
            EconomyDiagnosisReason.ExternalInflow => T("ui.economy_reason_external_inflow"),
            EconomyDiagnosisReason.LocalSpending => T("ui.economy_reason_local_spending"),
            EconomyDiagnosisReason.UnmetDemand => T("ui.economy_reason_unmet_demand"),
            EconomyDiagnosisReason.ImportLeakage => T("ui.economy_reason_import_leakage"),
            EconomyDiagnosisReason.BusinessRisk => T("ui.economy_reason_business_risk"),
            EconomyDiagnosisReason.Unemployment => T("ui.economy_reason_unemployment"),
            EconomyDiagnosisReason.PublicDeficit => T("ui.economy_reason_public_deficit"),
            EconomyDiagnosisReason.LowCash => T("ui.economy_reason_low_cash"),
            _ => T("ui.economy_reason_balanced")
        };
    }

    private static Color EconomyTrendColor(EconomyTrend trend)
    {
        return trend switch
        {
            EconomyTrend.Growing => UiTheme.Success,
            EconomyTrend.Shrinking => UiTheme.Danger,
            _ => UiTheme.Warning
        };
    }

    private static Color EconomyReasonColor(EconomyDiagnosisReason reason)
    {
        return reason switch
        {
            EconomyDiagnosisReason.ExternalInflow or EconomyDiagnosisReason.LocalSpending or EconomyDiagnosisReason.Balanced => UiTheme.Success,
            EconomyDiagnosisReason.BusinessRisk or EconomyDiagnosisReason.ImportLeakage => UiTheme.Danger,
            _ => UiTheme.Warning
        };
    }

    private string DistrictName(WorldState world, int districtId)
    {
        return world.Districts.FirstOrDefault(d => d.Id == districtId)?.Name ?? T("ui.unknown_district");
    }

    private static string FormatSigned(float value)
    {
        return value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }

    private enum MainUiState
    {
        MainMenu,
        InGame,
        PausedMenu,
        Settings
    }

    private enum ContextPanelMode
    {
        Overview,
        Projects,
        Diagnostics
    }

    private enum WindowModeSetting
    {
        Windowed,
        Fullscreen
    }

    private sealed class AnalyticsSnapshot
    {
        public int Population { get; private init; }
        public int Households { get; private init; }
        public int Workforce { get; private init; }
        public int Employed { get; private init; }
        public int Unemployed { get; private init; }
        public int Students { get; private init; }
        public int Retired { get; private init; }
        public int Businesses { get; private init; }
        public int ActiveBusinesses { get; private init; }
        public int FilledJobs { get; private init; }
        public int JobCapacity { get; private init; }
        public float BusinessRevenue { get; private init; }
        public float BusinessExpenses { get; private init; }
        public float BusinessProfit { get; private init; }
        public float BusinessCash { get; private init; }
        public float AverageBusinessLevel { get; private init; }
        public float AverageProductQuality { get; private init; }
        public float LastBusinessInvestment { get; private init; }
        public IReadOnlyList<BusinessHealthSnapshot> BusinessHealth { get; private init; } = Array.Empty<BusinessHealthSnapshot>();
        public float Budget { get; private init; }
        public float NetBudgetChange { get; private init; }
        public float IncomeTax { get; private init; }
        public float BusinessTax { get; private init; }
        public float OperatingExpenses { get; private init; }
        public float LocalGovernmentSpending { get; private init; }
        public float ExternalGovernmentSpending { get; private init; }
        public int ActiveProjects { get; private init; }
        public int CompletedProjects { get; private init; }
        public float CitizenIncome { get; private init; }
        public float CitizenCash { get; private init; }
        public float TrackedMoney { get; private init; }
        public float ExternalInflow { get; private init; }
        public float ExternalOutflow { get; private init; }
        public float InternalTransfers { get; private init; }
        public float ConsumerSpending { get; private init; }
        public float GrossWagesPaid { get; private init; }
        public float NetWagesPaid { get; private init; }
        public float Food { get; private init; }
        public float Housing { get; private init; }
        public float Safety { get; private init; }
        public float Healthcare { get; private init; }
        public float Entertainment { get; private init; }
        public DemandSnapshot FoodDemand { get; private init; } = DemandSnapshot.Empty("food");
        public DemandSnapshot GoodsDemand { get; private init; } = DemandSnapshot.Empty("goods");
        public DemandSnapshot HealthcareDemand { get; private init; } = DemandSnapshot.Empty("healthcare");

        public static AnalyticsSnapshot FromWorld(WorldState world)
        {
            var activeBusinesses = world.Businesses.Where(business => business.Status == BusinessStatus.Active).ToList();
            var demand = world.Economy.EstimateConsumerDemand(world);
            var citizenIncome = world.Citizens.Sum(citizen => citizen.Income);
            var citizenCash = world.Citizens.Sum(citizen => citizen.Cash);
            var businessRevenue = world.Businesses.Sum(business => business.Revenue);
            var businessExpenses = world.Businesses.Sum(business => business.Expenses);
            var businessCash = world.Businesses.Sum(business => business.Cash);
            var averageBusinessLevel = activeBusinesses.Count == 0 ? 0f : (float)activeBusinesses.Average(business => business.BusinessLevel);
            var averageProductQuality = activeBusinesses.Count == 0 ? 0f : activeBusinesses.Average(business => business.ProductQuality);
            var lastBusinessInvestment = world.Businesses.Sum(business => business.LastInvestment);

            return new AnalyticsSnapshot
            {
                Population = world.Citizens.Count,
                Households = world.Households.Count,
                Workforce = world.Citizens.Count(citizen => citizen.EmploymentStatus is EmploymentStatus.Employed or EmploymentStatus.Unemployed),
                Employed = world.Citizens.Count(citizen => citizen.EmploymentStatus == EmploymentStatus.Employed),
                Unemployed = world.Citizens.Count(citizen => citizen.EmploymentStatus == EmploymentStatus.Unemployed),
                Students = world.Citizens.Count(citizen => citizen.EmploymentStatus == EmploymentStatus.Student),
                Retired = world.Citizens.Count(citizen => citizen.EmploymentStatus == EmploymentStatus.Retired),
                Businesses = world.Businesses.Count,
                ActiveBusinesses = activeBusinesses.Count,
                FilledJobs = activeBusinesses.Sum(business => business.EmployeeIds.Count),
                JobCapacity = activeBusinesses.Sum(business => Math.Max(0, business.MaxEmployees)),
                BusinessRevenue = businessRevenue,
                BusinessExpenses = businessExpenses,
                BusinessProfit = businessRevenue - businessExpenses,
                BusinessCash = businessCash,
                AverageBusinessLevel = averageBusinessLevel,
                AverageProductQuality = averageProductQuality,
                LastBusinessInvestment = lastBusinessInvestment,
                BusinessHealth = world.Businesses
                    .Select(BusinessHealthSnapshot.From)
                    .OrderByDescending(business => business.ClosureRisk)
                    .ThenBy(business => business.Cash)
                    .ThenBy(business => business.Name)
                    .Take(6)
                    .ToList(),
                Budget = world.Budget,
                NetBudgetChange = world.LastNetBudgetChange,
                IncomeTax = world.LastIncomeTaxCollected,
                BusinessTax = world.LastBusinessTaxCollected,
                OperatingExpenses = world.LastOperatingExpenses,
                LocalGovernmentSpending = world.LastLocalGovernmentSpending,
                ExternalGovernmentSpending = world.LastExternalGovernmentSpending,
                ActiveProjects = world.Projects.Count(project => !project.Completed),
                CompletedProjects = world.Projects.Count(project => project.Completed),
                CitizenIncome = citizenIncome,
                CitizenCash = citizenCash,
                TrackedMoney = world.Budget + citizenCash + businessCash,
                ExternalInflow = world.LastExternalInflow,
                ExternalOutflow = world.LastExternalOutflow,
                InternalTransfers = world.LastInternalTransfers,
                ConsumerSpending = world.LastConsumerSpending,
                GrossWagesPaid = world.LastGrossWagesPaid,
                NetWagesPaid = world.LastNetWagesPaid,
                Food = Average(world.Citizens, citizen => citizen.FoodSatisfaction),
                Housing = Average(world.Citizens, citizen => citizen.HousingSatisfaction),
                Safety = Average(world.Citizens, citizen => citizen.SafetySatisfaction),
                Healthcare = Average(world.Citizens, citizen => citizen.HealthcareSatisfaction),
                Entertainment = Average(world.Citizens, citizen => citizen.EntertainmentSatisfaction),
                FoodDemand = DemandSnapshot.From(demand.FirstOrDefault(item => item.Category == "food"), "food"),
                GoodsDemand = DemandSnapshot.From(demand.FirstOrDefault(item => item.Category == "goods"), "goods"),
                HealthcareDemand = DemandSnapshot.From(demand.FirstOrDefault(item => item.Category == "healthcare"), "healthcare")
            };
        }

        private static float Average(IEnumerable<Citizen> citizens, Func<Citizen, float> selector)
        {
            var list = citizens.ToList();
            return list.Count == 0 ? 0f : list.Average(selector);
        }
    }

    private sealed record BusinessHealthSnapshot(
        string Name,
        string Status,
        int BusinessLevel,
        float ProductQuality,
        float Cash,
        float LastProducedUnits,
        float ProductionCapacity,
        float LastLocalSalesRevenue,
        float LastExternalSalesRevenue,
        int ConsecutiveLossTicks,
        float ClosureRisk)
    {
        public static BusinessHealthSnapshot From(Business business)
        {
            var staffingCapacity = business.BaseOutput *
                                   business.GetProductionMultiplier() *
                                   business.GetStaffingRatio();

            return new BusinessHealthSnapshot(
                business.Name,
                business.Status.ToString(),
                business.BusinessLevel,
                business.ProductQuality,
                business.Cash,
                business.LastProducedUnits,
                staffingCapacity,
                business.LastLocalSalesRevenue,
                business.LastExternalSalesRevenue,
                business.ConsecutiveLossTicks,
                EstimateClosureRisk(business));
        }

        private static float EstimateClosureRisk(Business business)
        {
            if (business.Status == BusinessStatus.Bankrupt) return 100f;
            if (business.Status == BusinessStatus.Closed) return 90f;

            var payrollReserve = Math.Max(0f, business.WagePerEmployee) * Math.Max(1, business.EmployeeIds.Count);
            var risk = 0f;

            if (business.Cash < 0f)
            {
                risk += 45f;
            }
            else if (business.Cash < payrollReserve)
            {
                risk += 25f;
            }

            risk += Math.Min(45f, business.ConsecutiveLossTicks * 18f);

            if (business.LastProducedUnits > 0.001f &&
                business.LastSoldUnits <= 0.001f &&
                business.RevenueThisTick <= 0.001f)
            {
                risk += 20f;
            }

            if (business.GetStaffingRatio() < 0.25f)
            {
                risk += 15f;
            }

            if (business.ProfitThisTick < 0f)
            {
                risk += 15f;
            }

            return Math.Clamp(risk, 0f, 100f);
        }
    }

    private sealed record DemandSnapshot(
        string Category,
        float DesiredSpending,
        float AvailableCash,
        float AvailableSupplyValue,
        float UnmetDemand,
        float AverageQuality)
    {
        public static DemandSnapshot Empty(string category) => new(category, 0f, 0f, 0f, 0f, 0f);

        public static DemandSnapshot From(ConsumerDemandSnapshot? demand, string category)
        {
            return demand == null
                ? Empty(category)
                : new DemandSnapshot(
                    demand.Category,
                    demand.DesiredSpending,
                    demand.AvailableCash,
                    demand.AvailableSupplyValue,
                    demand.UnmetDemand,
                    demand.AverageQuality);
        }
    }

    private sealed class GameSettings
    {
        public float MasterVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 1f;
        public float EffectsVolume { get; set; } = 1f;
        public int ResolutionIndex { get; set; }
        public WindowModeSetting WindowMode { get; set; } = WindowModeSetting.Windowed;
        public GameLanguage Language { get; set; } = GameLanguage.English;

        public static GameSettings Default()
        {
            return new GameSettings();
        }

        public void Normalize()
        {
            MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
            MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
            EffectsVolume = Math.Clamp(EffectsVolume, 0f, 1f);
            ResolutionIndex = Math.Clamp(ResolutionIndex, 0, AvailableResolutions.Length - 1);
            if (!Enum.IsDefined(WindowMode))
            {
                WindowMode = WindowModeSetting.Windowed;
            }

            if (!Enum.IsDefined(Language))
            {
                Language = GameLanguage.English;
            }
        }
    }
}
