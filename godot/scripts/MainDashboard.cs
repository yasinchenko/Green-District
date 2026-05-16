using System;
using System.Globalization;
using System.IO;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Godot.Scripts;
using GreenDistrict.Simulation.Localization;
using Godot;

namespace GreenDistrict.Godot.Scripts;

public partial class MainDashboard : Control
{
    private const int AutoTicksPerFrameAt1X = 60;

    private readonly SimulationBridge _bridge = new();
    private readonly LocalizationSystem _localization = new();
    private bool _isRunning;
    private int _speedMultiplier = 1;
    private Control? _uiRoot;
    private Label _timeLabel = null!;
    private Label _budgetLabel = null!;
    private Label _populationLabel = null!;
    private Label _supportLabel = null!;
    private Label _satisfactionLabel = null!;
    private Label _unemploymentLabel = null!;
    private Label _businessLabel = null!;
    private Label _eventLabel = null!;
    private Label _runStateLabel = null!;
    private OptionButton _projectTypeOption = null!;
    private OptionButton _projectDistrictOption = null!;
    private Label _projectActionLabel = null!;
    private DistrictMapView _districtMap = null!;
    private VBoxContainer _districtList = null!;
    private VBoxContainer _districtPanelList = null!;
    private VBoxContainer _businessPanelList = null!;
    private VBoxContainer _citizenPanelList = null!;
    private VBoxContainer _projectPanelList = null!;
    private VBoxContainer _eventList = null!;

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

        _bridge.StepTicks(AutoTicksPerFrameAt1X * _speedMultiplier);
        Refresh();
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

        var root = new MarginContainer();
        _uiRoot = root;
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("margin_left", 18);
        root.AddThemeConstantOverride("margin_top", 18);
        root.AddThemeConstantOverride("margin_right", 18);
        root.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(root);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        root.AddChild(layout);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        layout.AddChild(header);

        var title = new Label { Text = T("ui.title") };
        title.AddThemeFontSizeOverride("font_size", 28);
        header.AddChild(title);

        header.AddChild(CreateSpacer());
        header.AddChild(new Label
        {
            Text = T("ui.language"),
            CustomMinimumSize = new Vector2(68, 36),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.AddChild(CreateLanguageSelector());
        _runStateLabel = new Label { Text = "Paused", CustomMinimumSize = new Vector2(84, 36), VerticalAlignment = VerticalAlignment.Center };
        header.AddChild(_runStateLabel);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 8);
        layout.AddChild(controls);

        controls.AddChild(CreateButton(T("ui.play_pause"), ToggleAutoRun, 112));
        controls.AddChild(CreateButton(T("ui.speed_1x"), () => SetSpeed(1), 74));
        controls.AddChild(CreateButton(T("ui.speed_5x"), () => SetSpeed(5), 74));
        controls.AddChild(CreateButton(T("ui.speed_20x"), () => SetSpeed(20), 74));
        controls.AddChild(CreateButton(T("ui.one_day"), () => StepAndRefresh(1440), 92));
        controls.AddChild(CreateButton(T("ui.thirty_days"), () => StepAndRefresh(1440 * 30), 104));
        controls.AddChild(CreateButton(T("ui.reset"), () =>
        {
            _isRunning = false;
            _bridge.ResetWorld();
            Refresh();
        }, 82));

        var metrics = new GridContainer { Columns = 4 };
        metrics.AddThemeConstantOverride("h_separation", 10);
        metrics.AddThemeConstantOverride("v_separation", 10);
        layout.AddChild(metrics);

        _timeLabel = AddMetric(metrics, T("ui.time"));
        _budgetLabel = AddMetric(metrics, T("ui.budget"));
        _populationLabel = AddMetric(metrics, T("ui.population"));
        _supportLabel = AddMetric(metrics, T("ui.support"));
        _satisfactionLabel = AddMetric(metrics, T("ui.satisfaction"));
        _unemploymentLabel = AddMetric(metrics, T("ui.unemployment"));
        _businessLabel = AddMetric(metrics, T("ui.businesses"));
        _eventLabel = AddMetric(metrics, T("ui.events"));

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 12);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        layout.AddChild(body);

        var cityColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 0.58f
        };
        cityColumn.AddThemeConstantOverride("separation", 12);
        body.AddChild(cityColumn);

        var mapSection = CreateSection(cityColumn, T("ui.city_map"), 0.46f);
        _districtMap = new DistrictMapView
        {
            CustomMinimumSize = new Vector2(420, 180),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mapSection.AddChild(_districtMap);

        _districtList = CreateSection(cityColumn, T("ui.districts"), 0.54f, scrollContent: true);

        var detailTabs = CreateDetailTabs(body, 0.46f);
        CreateProjectControls(detailTabs);
        _districtPanelList = CreateTabList(detailTabs, T("ui.districts"));
        _businessPanelList = CreateTabList(detailTabs, T("ui.businesses"));
        _citizenPanelList = CreateTabList(detailTabs, T("ui.citizens"));
        _projectPanelList = CreateTabList(detailTabs, T("ui.projects"));
        _eventList = CreateTabList(detailTabs, T("ui.events"));
    }

    private OptionButton CreateLanguageSelector()
    {
        var language = new OptionButton { CustomMinimumSize = new Vector2(110, 36) };
        language.AddItem(T("ui.language.english"), (int)GameLanguage.English);
        language.AddItem(T("ui.language.russian"), (int)GameLanguage.Russian);
        language.Select(_localization.CurrentLanguage == GameLanguage.Russian ? 1 : 0);
        language.ItemSelected += index =>
        {
            var selected = (GameLanguage)language.GetItemId((int)index);
            _localization.SetLanguage(selected);
            BuildInterface();
            Refresh();
        };
        return language;
    }

    private void ToggleAutoRun()
    {
        _isRunning = !_isRunning;
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

    private void Refresh()
    {
        var world = _bridge.World;
        _timeLabel.Text = Tf("ui.day_time", world.Clock.Day, world.Clock.GetTimeString());
        _budgetLabel.Text = FormatMoney(world.Budget);
        _populationLabel.Text = world.GetTotalPopulation().ToString(CultureInfo.InvariantCulture);
        _supportLabel.Text = FormatPercent(world.SupportRating);
        _satisfactionLabel.Text = FormatPercent(world.GetAverageSatisfaction());
        _unemploymentLabel.Text = FormatPercent(world.LastUnemploymentRate);
        _businessLabel.Text = $"{world.Businesses.Count(b => b.Status == BusinessStatus.Active)}/{world.Businesses.Count}";
        _eventLabel.Text = world.Events.Count.ToString(CultureInfo.InvariantCulture);

        RefreshRunState();
        RefreshProjectDistrictOptions(world);
        _districtMap.SetWorld(world);
        RebuildDistricts(world);
        RebuildDistrictPanels(world);
        RebuildBusinessPanels(world);
        RebuildCitizenPanels(world);
        RebuildProjectPanels(world);
        RebuildEvents(world);
    }

    private void RefreshRunState()
    {
        _runStateLabel.Text = _isRunning
            ? $"{T("ui.running")} {_speedMultiplier}x"
            : $"{T("ui.paused")} {_speedMultiplier}x";
    }

    private void CreateProjectControls(TabContainer parent)
    {
        var scroll = new ScrollContainer
        {
            Name = "Actions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        parent.AddChild(scroll);

        var rows = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rows.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(rows);

        var title = new Label { Text = T("ui.start_project") };
        title.AddThemeFontSizeOverride("font_size", 18);
        rows.AddChild(title);

        _projectTypeOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var type in Enum.GetValues<ProjectType>().Where(t => t != ProjectType.Custom))
        {
            _projectTypeOption.AddItem(ProjectTypeLabel(type), (int)type);
        }
        rows.AddChild(_projectTypeOption);

        _projectDistrictOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddChild(_projectDistrictOption);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        rows.AddChild(actions);
        actions.AddChild(CreateButton(T("ui.start"), StartSelectedProject));

        _projectActionLabel = CreateWrappedLabel(T("ui.choose_project"));
        rows.AddChild(_projectActionLabel);
        rows.AddChild(CreateWrappedLabel(T("ui.project_hint")));
    }

    private void RefreshProjectDistrictOptions(WorldState world)
    {
        var selectedDistrictId = _projectDistrictOption.Selected >= 0
            ? _projectDistrictOption.GetItemId(_projectDistrictOption.Selected)
            : 0;

        _projectDistrictOption.Clear();
        _projectDistrictOption.AddItem(T("ui.city_wide"), 0);
        foreach (var district in world.Districts.OrderBy(d => d.Id))
        {
            _projectDistrictOption.AddItem(district.Name, district.Id);
        }

        var indexToSelect = 0;
        for (var i = 0; i < _projectDistrictOption.ItemCount; i++)
        {
            if (_projectDistrictOption.GetItemId(i) == selectedDistrictId)
            {
                indexToSelect = i;
                break;
            }
        }

        _projectDistrictOption.Select(indexToSelect);
    }

    private void StartSelectedProject()
    {
        if (_projectTypeOption.Selected < 0)
        {
            _projectActionLabel.Text = "Select a project type first.";
            _projectActionLabel.Text = T("ui.select_project_first");
            return;
        }

        var type = (ProjectType)_projectTypeOption.GetItemId(_projectTypeOption.Selected);
        var selectedDistrictId = _projectDistrictOption.Selected >= 0
            ? _projectDistrictOption.GetItemId(_projectDistrictOption.Selected)
            : 0;
        var districtId = selectedDistrictId <= 0 ? (int?)null : selectedDistrictId;
        var project = GovernmentProject.CreateTyped(type, districtId);
        var started = _bridge.StartProject(type, districtId);

        _projectActionLabel.Text = started
            ? Tf("ui.started_project", project.Name, FormatMoney(project.Cost))
            : Tf("ui.not_enough_budget", project.Name, FormatMoney(project.Cost));
        Refresh();
    }

    private void RebuildDistricts(WorldState world)
    {
        ClearChildren(_districtList);

        foreach (var district in world.Districts.OrderBy(d => d.Id))
        {
            var panel = CreatePanel();
            _districtList.AddChild(panel);

            var rows = new VBoxContainer();
            rows.AddThemeConstantOverride("separation", 4);
            panel.AddChild(rows);

            var name = new Label { Text = district.Name };
            name.AddThemeFontSizeOverride("font_size", 18);
            rows.AddChild(name);
            rows.AddChild(new Label { Text = Tf("ui.district_summary", district.Population, FormatPercent(district.SupportRating)) });
            rows.AddChild(new Label { Text = Tf("ui.jobs_housing_summary", district.TotalJobs - district.OpenJobs, district.TotalJobs, district.OccupiedHousing, district.HousingCapacity) });
            rows.AddChild(new Label { Text = Tf("ui.services_safety_summary", FormatPercent(district.ServiceLevel), FormatPercent(district.AverageSafetySatisfaction)) });
        }
    }

    private void RebuildDistrictPanels(WorldState world)
    {
        ClearChildren(_districtPanelList);

        foreach (var district in world.Districts.OrderBy(d => d.Id))
        {
            var rows = AddPanelRows(_districtPanelList, district.Name);
            rows.AddChild(CreateWrappedLabel(Tf("ui.population_field", district.Population)));
            rows.AddChild(CreateWrappedLabel(Tf("ui.support_crisis", FormatPercent(district.SupportRating), FormatPercent(district.CrisisRisk))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.employment_jobs", FormatPercent(district.EmploymentRate), district.TotalJobs - district.OpenJobs, district.TotalJobs)));
            rows.AddChild(CreateWrappedLabel(Tf("ui.housing_available", district.OccupiedHousing, district.HousingCapacity, district.AvailableHousing)));
            rows.AddChild(CreateWrappedLabel(Tf("ui.services_economy", FormatPercent(district.ServiceLevel), FormatPercent(district.EconomicLevel))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.projects_status", district.ActiveProjects, district.CompletedProjects)));
        }
    }

    private void RebuildBusinessPanels(WorldState world)
    {
        ClearChildren(_businessPanelList);

        foreach (var business in world.Businesses.OrderBy(b => b.DistrictId ?? int.MaxValue).ThenBy(b => b.Id))
        {
            var districtName = business.DistrictId.HasValue
                ? world.Districts.FirstOrDefault(d => d.Id == business.DistrictId.Value)?.Name ?? T("ui.unknown_district")
                : T("ui.no_district");
            var rows = AddPanelRows(_businessPanelList, business.Name);
            rows.AddChild(CreateWrappedLabel(Tf("ui.type_status", business.Type, BusinessStatusLabel(business.Status))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.district_field", districtName)));
            rows.AddChild(CreateWrappedLabel(Tf("ui.staff_wage", business.EmployeeIds.Count, business.MaxEmployees, FormatMoney(business.WagePerEmployee))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.revenue_expenses", FormatMoney(business.Revenue), FormatMoney(business.Expenses))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.profit_loss_ticks", FormatMoney(business.GetProfit()), business.ConsecutiveLossTicks)));
            rows.AddChild(CreateWrappedLabel(Tf("ui.output_summary", business.LastProducedUnits, business.LastSoldUnits)));
        }
    }

    private void RebuildCitizenPanels(WorldState world)
    {
        ClearChildren(_citizenPanelList);

        foreach (var citizen in world.Citizens.OrderBy(c => c.DistrictId ?? int.MaxValue).ThenBy(c => c.Name).Take(24))
        {
            var districtName = citizen.DistrictId.HasValue
                ? world.Districts.FirstOrDefault(d => d.Id == citizen.DistrictId.Value)?.Name ?? T("ui.unknown_district")
                : T("ui.no_district");
            var rows = AddPanelRows(_citizenPanelList, citizen.Name);
            rows.AddChild(CreateWrappedLabel(Tf("ui.age_stage_status", citizen.Age, LifeStageLabel(citizen.LifeStage), EmploymentStatusLabel(citizen.EmploymentStatus))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.district_job", districtName, citizen.Job ?? T("ui.none"))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.income_health", FormatMoney(citizen.Income), FormatPercent(citizen.Health))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.satisfaction_mood", FormatPercent(citizen.Satisfaction), FormatPercent(citizen.Mood))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.needs_summary", FormatPercent(citizen.FoodSatisfaction), FormatPercent(citizen.HousingSatisfaction), FormatPercent(citizen.SafetySatisfaction))));
        }

        if (world.Citizens.Count > 24)
        {
            _citizenPanelList.AddChild(CreateWrappedLabel(Tf("ui.showing_citizens", 24, world.Citizens.Count)));
        }
    }

    private void RebuildProjectPanels(WorldState world)
    {
        ClearChildren(_projectPanelList);

        foreach (var project in world.Projects.OrderBy(p => p.Completed).ThenBy(p => p.Name))
        {
            var districtName = project.DistrictId.HasValue
                ? world.Districts.FirstOrDefault(d => d.Id == project.DistrictId.Value)?.Name ?? T("ui.unknown_district")
                : T("ui.city_wide");
            var rows = AddPanelRows(_projectPanelList, project.Name);
            rows.AddChild(CreateWrappedLabel(Tf("ui.type_status", ProjectTypeLabel(project.Type), project.Completed ? T("ui.completed") : T("ui.active"))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.district_field", districtName)));
            rows.AddChild(CreateWrappedLabel(Tf("ui.cost_benefit", FormatMoney(project.Cost), FormatMoney(project.Benefit))));
            rows.AddChild(CreateWrappedLabel(Tf("ui.progress_ticks", project.DurationTicks - project.RemainingTicks, project.DurationTicks)));
            rows.AddChild(CreateWrappedLabel(Tf("ui.effects_summary", FormatSigned(project.SupportEffect), FormatSigned(project.HousingSatisfactionEffect), FormatSigned(project.SafetySatisfactionEffect))));
        }

        if (world.Projects.Count == 0)
        {
            _projectPanelList.AddChild(new Label { Text = T("ui.no_projects") });
        }
    }

    private void RebuildEvents(WorldState world)
    {
        ClearChildren(_eventList);

        foreach (var gameEvent in world.Events.OrderByDescending(e => e.CreatedAtTick).Take(8))
        {
            var rows = AddPanelRows(_eventList, gameEvent.Title);
            rows.AddChild(CreateWrappedLabel(Tf("ui.tick_type", gameEvent.CreatedAtTick, gameEvent.Type)));
            rows.AddChild(CreateWrappedLabel(gameEvent.Description));
            if (gameEvent.HasChoices)
            {
                rows.AddChild(CreateWrappedLabel(Tf("ui.choices_resolved", gameEvent.Choices.Count, gameEvent.IsResolved)));
            }
        }

        if (_eventList.GetChildCount() == 0)
        {
            _eventList.AddChild(new Label { Text = T("ui.no_events") });
        }
    }

    private static Label AddMetric(GridContainer parent, string title)
    {
        var panel = CreatePanel();
        panel.CustomMinimumSize = new Vector2(180, 78);
        parent.AddChild(panel);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 3);
        panel.AddChild(rows);

        var caption = new Label { Text = title };
        caption.AddThemeFontSizeOverride("font_size", 12);
        rows.AddChild(caption);

        var value = new Label { Text = "-" };
        value.AddThemeFontSizeOverride("font_size", 22);
        rows.AddChild(value);
        return value;
    }

    private TabContainer CreateDetailTabs(Container parent, float stretchRatio)
    {
        var panel = CreatePanel();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = stretchRatio;
        parent.AddChild(panel);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 8);
        panel.AddChild(rows);

        var label = new Label { Text = T("ui.panels") };
        label.AddThemeFontSizeOverride("font_size", 20);
        rows.AddChild(label);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rows.AddChild(tabs);
        return tabs;
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
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(list);
        return list;
    }

    private static VBoxContainer CreateSection(Container parent, string title, float stretchRatio, bool scrollContent = false)
    {
        var panel = CreatePanel();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = stretchRatio;
        parent.AddChild(panel);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 8);
        panel.AddChild(rows);

        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 20);
        rows.AddChild(label);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.SizeFlagsVertical = SizeFlags.ExpandFill;

        if (scrollContent)
        {
            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            scroll.AddChild(content);
            rows.AddChild(scroll);
        }
        else
        {
            rows.AddChild(content);
        }

        return content;
    }

    private static VBoxContainer AddPanelRows(Container parent, string title)
    {
        var panel = CreatePanel();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(panel);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 4);
        panel.AddChild(rows);

        var label = new Label
        {
            Text = title,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        rows.AddChild(label);
        return rows;
    }

    private static Label CreateWrappedLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
    }

    private static PanelContainer CreatePanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeConstantOverride("margin_left", 10);
        panel.AddThemeConstantOverride("margin_top", 8);
        panel.AddThemeConstantOverride("margin_right", 10);
        panel.AddThemeConstantOverride("margin_bottom", 8);
        return panel;
    }

    private static Button CreateButton(string text, Action action, float width = 88f)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 36) };
        button.Pressed += action;
        return button;
    }

    private static Control CreateSpacer()
    {
        return new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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

    private string LifeStageLabel(LifeStage stage)
    {
        return T($"status.life.{stage.ToString().ToLowerInvariant()}");
    }

    private string EmploymentStatusLabel(EmploymentStatus status)
    {
        return T($"status.employment.{status.ToString().ToLowerInvariant()}");
    }

    private string BusinessStatusLabel(BusinessStatus status)
    {
        return status switch
        {
            BusinessStatus.Active => T("ui.active"),
            _ => status.ToString()
        };
    }

    private static string FormatSigned(float value)
    {
        return value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }
}
