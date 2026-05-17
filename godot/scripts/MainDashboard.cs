using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Localization;
using Godot;

namespace GreenDistrict.Godot.Scripts;

public partial class MainDashboard : Control
{
    private const double AutoTicksPerSecondAt1X = 1.0;

    private readonly SimulationBridge _bridge = new();
    private readonly LocalizationSystem _localization = new();
    private bool _isRunning;
    private int _speedMultiplier = 1;
    private double _autoTickAccumulator;
    private int? _selectedDistrictId;
    private int? _selectedEventId;
    private string? _projectMessage;
    private string? _eventMessage;
    private string? _systemMessage;

    private Control? _uiRoot;
    private TabContainer _tabs = null!;
    private Label _timeLabel = null!;
    private Label _budgetLabel = null!;
    private Label _populationLabel = null!;
    private Label _supportLabel = null!;
    private Label _satisfactionLabel = null!;
    private Label _unemploymentLabel = null!;
    private Label _runStateLabel = null!;
    private Button _playPauseButton = null!;
    private Label _projectActionLabel = null!;
    private DistrictMapView _districtMap = null!;
    private VBoxContainer _contextList = null!;
    private VBoxContainer _projectList = null!;
    private VBoxContainer _diagnosticsList = null!;
    private HBoxContainer _eventFeed = null!;

    public override void _Ready()
    {
        AddChild(_bridge);
        _bridge.ResetWorld();
        LoadLocalizationDictionaries();
        BuildInterface();
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!_isRunning) return;

        _autoTickAccumulator += delta * AutoTicksPerSecondAt1X * _speedMultiplier;
        var ticksToRun = (int)Math.Floor(_autoTickAccumulator);
        if (ticksToRun <= 0) return;

        _autoTickAccumulator -= ticksToRun;
        _bridge.StepTicks(ticksToRun);
        Refresh();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;

        if (key.CtrlPressed && key.Keycode == Key.S)
        {
            SaveWorld();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.CtrlPressed && key.Keycode == Key.L)
        {
            LoadWorld();
            GetViewport().SetInputAsHandled();
            return;
        }

        switch (key.Keycode)
        {
            case Key.Space:
                ToggleAutoRun();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Key1:
                SetSpeed(1);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Key2:
                SetSpeed(5);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Key3:
                SetSpeed(20);
                GetViewport().SetInputAsHandled();
                break;
            case Key.Escape:
                ClearSelection();
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
        root.AddThemeConstantOverride("margin_left", 12);
        root.AddThemeConstantOverride("margin_top", 10);
        root.AddThemeConstantOverride("margin_right", 12);
        root.AddThemeConstantOverride("margin_bottom", 10);
        background.AddChild(root);

        var layout = new VBoxContainer();
        layout.MouseFilter = MouseFilterEnum.Ignore;
        layout.AddThemeConstantOverride("separation", 10);
        root.AddChild(layout);

        BuildHud(layout);
        BuildBody(layout);
        BuildEventFeed(layout);
    }

    private void BuildHud(VBoxContainer layout)
    {
        var panel = CreatePanel(UiTheme.Hud);
        panel.CustomMinimumSize = new Vector2(0, 58);
        layout.AddChild(panel);

        var header = new HBoxContainer();
        header.MouseFilter = MouseFilterEnum.Ignore;
        header.AddThemeConstantOverride("separation", 7);
        panel.AddChild(header);

        var titleBox = new VBoxContainer { CustomMinimumSize = new Vector2(108, 0) };
        titleBox.MouseFilter = MouseFilterEnum.Ignore;
        titleBox.AddThemeConstantOverride("separation", 0);
        header.AddChild(titleBox);

        var title = new Label { Text = T("ui.title"), ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        UiTheme.ApplyLabel(title, 20);
        titleBox.AddChild(title);

        _runStateLabel = new Label();
        UiTheme.ApplyLabel(_runStateLabel, 12, UiTheme.TextMuted);
        titleBox.AddChild(_runStateLabel);

        _budgetLabel = CreateMetricPill(header, "$", T("ui.budget"), 116, true, UiTheme.Success);
        _populationLabel = CreateMetricPill(header, "P", T("ui.population"), 82, true, UiTheme.Info);
        _supportLabel = CreateMetricPill(header, "%", T("ui.support"), 82, true, UiTheme.Trend);
        _timeLabel = CreateMetricPill(header, "D", T("ui.time"), 92);
        _satisfactionLabel = CreateMetricPill(header, "+", T("ui.satisfaction"), 66);
        _unemploymentLabel = CreateMetricPill(header, "!", T("ui.unemployment"), 66);

        header.AddChild(CreateSpacer());

        var controls = new HBoxContainer();
        controls.MouseFilter = MouseFilterEnum.Ignore;
        controls.AddThemeConstantOverride("separation", 5);
        header.AddChild(controls);
        _playPauseButton = CreateButton(">", ToggleAutoRun, 34, T("ui.play_pause"));
        controls.AddChild(_playPauseButton);
        controls.AddChild(CreateButton(T("ui.speed_1x"), () => SetSpeed(1), 42));
        controls.AddChild(CreateButton(T("ui.speed_5x"), () => SetSpeed(5), 42));
        controls.AddChild(CreateButton(T("ui.speed_20x"), () => SetSpeed(20), 48));
        controls.AddChild(CreateButton(T("ui.one_day"), () => StepAndRefresh(1440), 54));

        var language = CreateLanguageSelector();
        language.CustomMinimumSize = new Vector2(72, 32);
        header.AddChild(language);
        header.AddChild(CreateButton("S", SaveWorld, 30, T("ui.save")));
        header.AddChild(CreateButton("L", LoadWorld, 30, T("ui.load")));
        header.AddChild(CreateButton(T("ui.reset"), ResetWorld, 54));
    }

    private void BuildBody(VBoxContainer layout)
    {
        var body = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 10);
        layout.AddChild(body);

        var mapPanel = CreatePanel(UiTheme.Panel);
        mapPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        mapPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        mapPanel.SizeFlagsStretchRatio = 0.68f;
        body.AddChild(mapPanel);

        var mapRows = new VBoxContainer();
        mapRows.MouseFilter = MouseFilterEnum.Ignore;
        mapRows.AddThemeConstantOverride("separation", 8);
        mapPanel.AddChild(mapRows);
        mapRows.AddChild(CreateSectionTitle(T("ui.city_map"), "M"));

        _districtMap = new DistrictMapView
        {
            CustomMinimumSize = new Vector2(520, 260),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _districtMap.DistrictSelected += districtId =>
        {
            _selectedDistrictId = districtId;
            _selectedEventId = null;
            _eventMessage = null;
            Refresh();
        };
        mapRows.AddChild(_districtMap);

        var rightPanel = CreatePanel(UiTheme.Panel);
        rightPanel.CustomMinimumSize = new Vector2(330, 0);
        rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(rightPanel);

        var rightRows = new VBoxContainer();
        rightRows.MouseFilter = MouseFilterEnum.Ignore;
        rightRows.AddThemeConstantOverride("separation", 8);
        rightPanel.AddChild(rightRows);
        rightRows.AddChild(CreateSectionTitle(T("ui.context"), "I"));

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        UiTheme.ApplyTabs(_tabs);
        rightRows.AddChild(_tabs);

        _contextList = CreateTabList(_tabs, T("ui.overview"));
        _projectList = CreateTabList(_tabs, T("ui.projects"));
        _diagnosticsList = CreateTabList(_tabs, T("ui.diagnostics_short"));
    }

    private void BuildEventFeed(VBoxContainer layout)
    {
        var panel = CreatePanel(UiTheme.Event);
        panel.CustomMinimumSize = new Vector2(0, 86);
        layout.AddChild(panel);

        var rows = new VBoxContainer();
        rows.MouseFilter = MouseFilterEnum.Ignore;
        rows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rows);
        rows.AddChild(CreateSectionTitle(T("ui.recent_events"), "!"));

        _eventFeed = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _eventFeed.AddThemeConstantOverride("separation", 8);
        rows.AddChild(_eventFeed);
    }

    private OptionButton CreateLanguageSelector()
    {
        var language = new OptionButton();
        language.AddItem("EN", (int)GameLanguage.English);
        language.AddItem("RU", (int)GameLanguage.Russian);
        language.Select(_localization.CurrentLanguage == GameLanguage.Russian ? 1 : 0);
        language.ItemSelected += index =>
        {
            var selected = (GameLanguage)language.GetItemId((int)index);
            _localization.SetLanguage(selected);
            BuildInterface();
            Refresh();
        };
        UiTheme.ApplyButton(language);
        return language;
    }

    private void ToggleAutoRun()
    {
        _isRunning = !_isRunning;
        _autoTickAccumulator = 0;
        RefreshRunState();
    }

    private void SetSpeed(int multiplier)
    {
        _speedMultiplier = Math.Max(1, multiplier);
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
        _projectMessage = null;
        _eventMessage = null;
        _systemMessage = null;
        _bridge.ResetWorld();
        Refresh();
    }

    private void Refresh()
    {
        var world = _bridge.World;
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
        _districtMap.SetSelectedDistrict(_selectedDistrictId);
        RebuildContext(world);
        RebuildProjects(world);
        RebuildDiagnostics(world);
        RebuildEventFeed(world);
    }

    private void RefreshRunState()
    {
        _runStateLabel.Text = _isRunning
            ? $"{T("ui.running")} {_speedMultiplier}x"
            : $"{T("ui.paused")} {_speedMultiplier}x";
        if (_playPauseButton != null)
        {
            _playPauseButton.Text = _isRunning ? "||" : ">";
        }
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

        if (selectedEvent != null)
        {
            RebuildEventDetails(world, selectedEvent);
            return;
        }

        if (selectedDistrict == null)
        {
            AddPanelRows(_contextList, T("ui.city_status"), "I")
                .AddChild(CreateWrappedLabel(T("ui.no_district_selected")));
            var rows = AddPanelRows(_contextList, T("ui.overview"), "O");
            rows.AddChild(CreateStatRow(T("ui.population"), world.GetTotalPopulation().ToString(CultureInfo.InvariantCulture)));
            rows.AddChild(CreateStatRow(T("ui.budget"), FormatMoney(world.Budget)));
            rows.AddChild(CreateStatRow(T("ui.support"), FormatPercent(world.SupportRating)));
            rows.AddChild(CreateStatRow(T("ui.satisfaction"), FormatPercent(world.GetAverageSatisfaction())));
            rows.AddChild(CreateStatRow(T("ui.unemployment"), FormatPercent(world.LastUnemploymentRate)));
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
        AddDistrictIssueCards(_contextList, selectedDistrict);
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

    private void RebuildProjects(WorldState world)
    {
        ClearChildren(_projectList);

        var intro = AddPanelRows(_projectList, T("ui.quick_actions"), "A");
        intro.AddChild(CreateWrappedLabel(_selectedDistrictId.HasValue ? DistrictName(world, _selectedDistrictId.Value) : T("ui.city_wide"), UiTheme.TextMuted));
        _projectActionLabel = CreateWrappedLabel(_projectMessage ?? T("ui.choose_project"));
        intro.AddChild(_projectActionLabel);

        foreach (var type in Enum.GetValues<ProjectType>().Where(t => t != ProjectType.Custom))
        {
            var project = GovernmentProject.CreateTyped(type, _selectedDistrictId);
            var rows = AddPanelRows(_projectList, ProjectTypeLabel(type), ProjectIcon(type));
            rows.AddChild(CreateStatRow(T("ui.project_upfront_cost"), FormatMoney(project.Cost)));
            rows.AddChild(CreateStatRow(T("ui.project_ongoing_cost"), FormatMoney(_bridge.World.ProjectOperatingExpensePerTick)));
            rows.AddChild(CreateStatRow(T("ui.time"), project.DurationTicks.ToString(CultureInfo.InvariantCulture)));
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

    private void RebuildEventDetails(WorldState world, GameEvent gameEvent)
    {
        var rows = AddPanelRows(_contextList, T("ui.event_details"), EventIcon(gameEvent));
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
            button.Disabled = gameEvent.IsResolved || world.Budget + choice.BudgetEffect < 0f;
            choiceContent.AddChild(button);
        }
    }

    private void RebuildDiagnostics(WorldState world)
    {
        ClearChildren(_diagnosticsList);

        var districtRows = AddPanelRows(_diagnosticsList, T("ui.districts"), "D");
        foreach (var district in world.Districts.OrderBy(d => d.Id))
        {
            districtRows.AddChild(CreateWrappedLabel(Tf("ui.district_summary", district.Population, FormatPercent(district.SupportRating))));
        }

        var businessRows = AddPanelRows(_diagnosticsList, T("ui.businesses"), "B");
        businessRows.AddChild(CreateStatRow(T("ui.active"), $"{world.Businesses.Count(b => b.Status == BusinessStatus.Active)}/{world.Businesses.Count}"));
        foreach (var business in world.Businesses.OrderBy(b => b.Id).Take(6))
        {
            businessRows.AddChild(CreateWrappedLabel($"{business.Name}: {BusinessStatusLabel(business.Status)}", UiTheme.TextMuted));
        }

        var citizenRows = AddPanelRows(_diagnosticsList, T("ui.citizens"), "P");
        citizenRows.AddChild(CreateWrappedLabel(Tf("ui.showing_citizens", Math.Min(8, world.Citizens.Count), world.Citizens.Count), UiTheme.TextMuted));
        foreach (var citizen in world.Citizens.OrderBy(c => c.Name).Take(8))
        {
            citizenRows.AddChild(CreateWrappedLabel($"{citizen.Name}: {FormatPercent(citizen.Satisfaction)}", UiTheme.TextMuted));
        }
    }

    private void RebuildEventFeed(WorldState world)
    {
        ClearChildren(_eventFeed);

        var events = world.Events.OrderByDescending(e => e.CreatedAtTick).Take(4).ToList();
        if (events.Count == 0)
        {
            var empty = CreatePanel(UiTheme.Event);
            empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var rows = new VBoxContainer();
            rows.MouseFilter = MouseFilterEnum.Ignore;
            empty.AddChild(rows);
            rows.AddChild(CreateWrappedLabel(T("ui.no_events"), UiTheme.TextMuted));
            _eventFeed.AddChild(empty);
            return;
        }

        foreach (var gameEvent in events)
        {
            var card = CreateEventCard(world, gameEvent, compact: true);
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _eventFeed.AddChild(card);
        }
    }

    private void ResolveSelectedEventChoice(int eventId, string choiceId)
    {
        var resolved = _bridge.ResolveEventChoice(eventId, choiceId);
        _eventMessage = resolved ? T("ui.choice_applied") : T("ui.choice_failed");
        Refresh();
    }

    private void StartProject(ProjectType type)
    {
        var project = GovernmentProject.CreateTyped(type, _selectedDistrictId);
        var started = _bridge.StartProject(type, _selectedDistrictId);
        _projectMessage = started
            ? Tf("ui.started_project", project.Name, FormatMoney(project.Cost))
            : Tf("ui.not_enough_budget", project.Name, FormatMoney(project.Cost));
        Refresh();
    }

    private void SaveWorld()
    {
        try
        {
            var path = SavePath();
            _bridge.SaveWorld(path);
            _systemMessage = Tf("ui.save_success", path);
        }
        catch (Exception ex)
        {
            _systemMessage = Tf("ui.save_failed", ex.Message);
        }

        Refresh();
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
                _projectMessage = null;
                _eventMessage = null;
                _systemMessage = Tf("ui.load_success", path);
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

        Refresh();
    }

    private void ClearSelection()
    {
        _selectedDistrictId = null;
        _selectedEventId = null;
        _eventMessage = null;
        Refresh();
    }

    private void CycleContextTab()
    {
        if (_tabs.GetTabCount() <= 0) return;

        _tabs.CurrentTab = (_tabs.CurrentTab + 1) % _tabs.GetTabCount();
    }

    private static string SavePath()
    {
        return ProjectSettings.GlobalizePath("user://green_district_save.json");
    }

    private void AddCityIssueCards(Container parent, WorldState world)
    {
        var issues = AddPanelRows(parent, T("ui.priorities"), "!");
        var added = false;

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

    private void AddDistrictIssueCards(Container parent, District district)
    {
        var issues = AddPanelRows(parent, T("ui.priorities"), "!");
        var added = false;

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
        rows.AddChild(CreateWrappedLabel($"{done}/{total} ticks · {percent:F0}%", UiTheme.TextMuted));
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

    private static string FormatPercent(float value)
    {
        return value.ToString("F1", CultureInfo.InvariantCulture) + "%";
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

    private string DistrictName(WorldState world, int districtId)
    {
        return world.Districts.FirstOrDefault(d => d.Id == districtId)?.Name ?? T("ui.unknown_district");
    }

    private static string FormatSigned(float value)
    {
        return value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }
}
