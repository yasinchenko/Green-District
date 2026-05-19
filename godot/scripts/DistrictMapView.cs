using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GreenDistrict.Simulation.Core;
using GreenDistrict.Simulation.Map;
using Godot;

namespace GreenDistrict.Godot.Scripts;

public partial class DistrictMapView : Control
{
    private const float MinZoom = 0.58f;
    private const float DefaultZoom = 0.86f;
    private const float MaxZoom = 2.4f;
    private const float ZoomStep = 1.14f;

    [Signal]
    public delegate void DistrictSelectedEventHandler(int districtId);

    private readonly List<MapDistrictShape> _shapes = new();
    private readonly Dictionary<string, Texture2D?> _mapAssetCache = new(StringComparer.OrdinalIgnoreCase);
    private MapGridGenerationResult? _mapGeneration;
    private WorldState? _world;
    private int? _selectedDistrictId;
    private int? _selectedEventId;
    private int? _hoveredDistrictId;
    private Vector2 _lastSize;
    private float _zoom = DefaultZoom;
    private Vector2 _pan;
    private bool _isPanning;
    private bool _showGridDebug;
    private bool _renderGridFallback = true;
    private double _animationTime;
    private double _redrawAccumulator;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        Resized += () =>
        {
            RebuildShapes();
            ClampPan();
            QueueRedraw();
        };
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.MapLand);
        DrawSetTransform(_pan, 0f, new Vector2(_zoom, _zoom));

        if (_renderGridFallback && _mapGeneration != null)
        {
            DrawMapGridFallback();
        }
        else
        {
            DrawMapBackground();

            foreach (var shape in _shapes)
            {
                DrawDistrict(shape);
            }

            DrawWaterGeography();
            DrawRoadNetwork();

            foreach (var shape in _shapes)
            {
                DrawDistrictDetails(shape);
            }
        }

        if (_showGridDebug)
        {
            DrawGridDebugOverlay();
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    public override void _Process(double delta)
    {
        if (_world == null) return;

        _animationTime += delta;
        _redrawAccumulator += delta;
        if (_redrawAccumulator < 1.0 / 18.0) return;

        _redrawAccumulator = 0;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseMotion motion)
        {
            if (_isPanning)
            {
                _pan += motion.Relative;
                ClampPan();
                QueueRedraw();
                AcceptEvent();
                return;
            }

            var mapPosition = ScreenToMap(motion.Position);
            var hover = FindDistrictAt(mapPosition)?.District.Id;
            if (hover != _hoveredDistrictId)
            {
                _hoveredDistrictId = hover;
                TooltipText = hover.HasValue
                    ? BuildTooltip(_shapes.First(s => s.District.Id == hover.Value).District)
                    : string.Empty;
                QueueRedraw();
            }
        }

        if (input is InputEventMouseButton button)
        {
            if ((button.ButtonIndex == MouseButton.WheelUp || button.ButtonIndex == MouseButton.WheelDown) && button.CtrlPressed)
            {
                var factor = button.ButtonIndex == MouseButton.WheelUp ? ZoomStep : 1f / ZoomStep;
                ZoomAt(button.Position, factor);
                AcceptEvent();
                return;
            }

            if (button.ButtonIndex is MouseButton.Right or MouseButton.Middle)
            {
                _isPanning = button.Pressed && CanPan();
                AcceptEvent();
                return;
            }

            if (button is { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                SelectDistrictAt(ScreenToMap(button.Position));
            }
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true, Keycode: Key.G })
        {
            _showGridDebug = !_showGridDebug;
            QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (input is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true, Keycode: Key.M })
        {
            _renderGridFallback = !_renderGridFallback;
            QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button) return;

        var localPosition = GetLocalMousePosition();
        if (localPosition.X < 0f || localPosition.Y < 0f || localPosition.X > Size.X || localPosition.Y > Size.Y) return;
        if (SelectDistrictAt(ScreenToMap(localPosition)))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public void SetWorld(WorldState world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _mapGeneration = new MapGridGenerator().Generate(_world);
        if (_lastSize != Size || !ShapesMatchWorld(_world))
        {
            RebuildShapes();
        }

        ClampPan();
        QueueRedraw();
    }

    private bool ShapesMatchWorld(WorldState world)
    {
        var districts = world.Districts.OrderBy(district => district.Id).ToList();
        if (_shapes.Count != districts.Count) return false;

        for (var i = 0; i < districts.Count; i++)
        {
            if (_shapes[i].District.Id != districts[i].Id) return false;
        }

        return true;
    }

    private void ZoomAt(Vector2 screenPosition, float factor)
    {
        var previousZoom = _zoom;
        var nextZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(nextZoom - previousZoom) < 0.001f) return;

        var mapPosition = ScreenToMap(screenPosition);
        _zoom = nextZoom;
        _pan = screenPosition - mapPosition * _zoom;
        ClampPan();
        QueueRedraw();
    }

    private Vector2 ScreenToMap(Vector2 screenPosition)
    {
        return (screenPosition - _pan) / _zoom;
    }

    private Vector2 MapToScreen(Vector2 mapPosition)
    {
        return mapPosition * _zoom + _pan;
    }

    private Vector2 GridToMap(GridPosition position)
    {
        if (_mapGeneration == null) return Vector2.Zero;

        return new Vector2(
            position.X / (float)_mapGeneration.Grid.WidthMeters * Size.X,
            position.Y / (float)_mapGeneration.Grid.HeightMeters * Size.Y);
    }

    private Rect2 GridCellToMapRect(GridPosition position)
    {
        if (_mapGeneration == null) return new Rect2(Vector2.Zero, Vector2.Zero);

        var cellSize = new Vector2(
            Size.X / _mapGeneration.Grid.WidthMeters,
            Size.Y / _mapGeneration.Grid.HeightMeters);
        return new Rect2(GridToMap(position), cellSize);
    }

    private GridPosition MapToGrid(Vector2 mapPosition)
    {
        if (_mapGeneration == null) return new GridPosition(0, 0);

        var x = (int)MathF.Floor(mapPosition.X / Math.Max(1f, Size.X) * _mapGeneration.Grid.WidthMeters);
        var y = (int)MathF.Floor(mapPosition.Y / Math.Max(1f, Size.Y) * _mapGeneration.Grid.HeightMeters);
        return new GridPosition(
            Math.Clamp(x, 0, _mapGeneration.Grid.WidthMeters - 1),
            Math.Clamp(y, 0, _mapGeneration.Grid.HeightMeters - 1));
    }

    private GridPosition ScreenToGrid(Vector2 screenPosition)
    {
        return MapToGrid(ScreenToMap(screenPosition));
    }

    private void ClampPan()
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            _pan = Vector2.Zero;
            _isPanning = false;
            return;
        }

        _zoom = Math.Clamp(_zoom, MinZoom, MaxZoom);
        var contentSize = Size * _zoom;
        _pan = new Vector2(
            ClampPanAxis(_pan.X, Size.X, contentSize.X),
            ClampPanAxis(_pan.Y, Size.Y, contentSize.Y));

        if (!CanPan())
        {
            _isPanning = false;
        }
    }

    private bool CanPan()
    {
        var contentSize = Size * _zoom;
        return contentSize.X > Size.X + 0.5f || contentSize.Y > Size.Y + 0.5f;
    }

    private static float ClampPanAxis(float value, float viewport, float content)
    {
        if (content <= viewport)
        {
            return (viewport - content) * 0.5f;
        }

        return Math.Clamp(value, viewport - content, 0f);
    }

    public void SetSelectedDistrict(int? districtId)
    {
        _selectedDistrictId = districtId;
        QueueRedraw();
    }

    public void SetSelectedEvent(int? eventId)
    {
        _selectedEventId = eventId;
        QueueRedraw();
    }

    private void RebuildShapes()
    {
        _shapes.Clear();
        _lastSize = Size;
        if (_world == null || Size.X <= 40f || Size.Y <= 40f) return;

        var districts = _world.Districts.OrderBy(d => d.Id).ToList();
        var count = districts.Count;
        if (count == 0) return;

        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        var rows = Math.Max(1, (int)Math.Ceiling(count / (float)columns));
        const float padding = 22f;
        const float gap = 18f;
        var cellWidth = Math.Max(120f, (Size.X - padding * 2f - gap * (columns - 1)) / columns);
        var cellHeight = Math.Max(100f, (Size.Y - padding * 2f - gap * (rows - 1)) / rows);

        for (var i = 0; i < districts.Count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var rect = new Rect2(
                padding + column * (cellWidth + gap),
                padding + row * (cellHeight + gap),
                cellWidth,
                cellHeight);

            var polygon = CreateDistrictPolygon(rect, districts[i].Id);
            _shapes.Add(new MapDistrictShape(districts[i], polygon, rect));
        }
    }

    private void DrawMapBackground()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.MapLand);

        var grass = new Color(UiTheme.MapGrass, 0.22f);
        for (var i = 0; i < 18; i++)
        {
            var x = StableRange(i * 17 + 3, 0f, Size.X);
            var y = StableRange(i * 19 + 7, 0f, Size.Y);
            DrawCircle(new Vector2(x, y), StableRange(i * 23, 18f, 46f), grass);
        }

    }

    private void DrawMapGridFallback()
    {
        if (_mapGeneration == null || Size.X <= 0f || Size.Y <= 0f) return;

        DrawGridSurfaces();
        DrawGridDistrictAreas();
        DrawGridRoads();
        DrawGridObjects();
        DrawGridDistrictLabels();
    }

    private void DrawGridSurfaces()
    {
        if (_mapGeneration == null) return;

        var grid = _mapGeneration.Grid;
        var cellSize = new Vector2(Size.X / grid.WidthMeters, Size.Y / grid.HeightMeters);
        for (var y = 0; y < grid.HeightMeters; y++)
        {
            var runStart = 0;
            var runAssetKey = grid.GetSurfaceAssetKey(new GridPosition(0, y));

            for (var x = 1; x <= grid.WidthMeters; x++)
            {
                var assetKey = x < grid.WidthMeters
                    ? grid.GetSurfaceAssetKey(new GridPosition(x, y))
                    : string.Empty;
                if (assetKey == runAssetKey) continue;

                var rect = new Rect2(
                    new Vector2(runStart * cellSize.X, y * cellSize.Y),
                    new Vector2((x - runStart) * cellSize.X, cellSize.Y));
                if (TryGetMapTexture(runAssetKey, out var texture))
                {
                    DrawTextureRect(texture, rect, tile: true);
                }
                else
                {
                    DrawRect(rect, SurfaceFallbackColor(runAssetKey));
                }

                runStart = x;
                runAssetKey = assetKey;
            }
        }
    }

    private void DrawGridDistrictAreas()
    {
        if (_mapGeneration == null) return;

        foreach (var area in _mapGeneration.DistrictAreas.Values)
        {
            var tint = area.DistrictId % 2 == 0
                ? new Color(UiTheme.Success, 0.08f)
                : new Color(UiTheme.Warning, 0.07f);
            var polygon = BuildVisualDistrictPolygon(area);
            DrawColoredPolygon(polygon, tint);

            var selected = _selectedDistrictId == area.DistrictId;
            var hovered = _hoveredDistrictId == area.DistrictId;
            var border = selected ? UiTheme.Info : hovered ? UiTheme.Warning : new Color(UiTheme.Border, 0.55f);
            var outline = polygon.Append(polygon[0]).ToArray();
            if (selected || hovered)
            {
                DrawPolyline(outline, new Color(border, 0.18f), selected ? 8f : 6f, true);
            }

            DrawPolyline(outline, border, selected ? 2.5f : hovered ? 2f : 1.2f, true);
        }
    }

    private void DrawGridRoads()
    {
        if (_mapGeneration == null) return;

        foreach (var cell in _mapGeneration.Grid.Cells.Where(cell => cell.HasRoad))
        {
            DrawGridRoadCell(cell, GridCellToMapRect(cell.Position));
        }
    }

    private void DrawGridRoadCell(MapCell cell, Rect2 rect)
    {
        if (!string.IsNullOrWhiteSpace(cell.RoadAssetKey) && TryGetMapTexture(cell.RoadAssetKey, out var texture))
        {
            DrawTextureRect(texture, rect, tile: false);
            return;
        }

        var center = rect.GetCenter();
        var half = rect.Size * 0.5f;
        var roadWidth = Math.Max(1.6f, Math.Min(rect.Size.X, rect.Size.Y) * 0.62f);
        var roadColor = cell.RoadKind switch
        {
            RoadKind.Bridge => UiTheme.MapBridge,
            RoadKind.RegionalRoad => Color.FromHtml("#C49A43"),
            RoadKind.AccessRoad => Color.FromHtml("#B9B1A1"),
            _ => UiTheme.MapRoad
        };
        var shadow = cell.RoadKind == RoadKind.Bridge ? UiTheme.MapBridgeShadow : UiTheme.MapRoadShadow;

        DrawRoadConnection(cell, center, half, shadow, roadWidth + 1.5f);
        DrawRoadConnection(cell, center, half, roadColor, roadWidth);
        DrawCircle(center, roadWidth * 0.42f, roadColor);

        if (cell.RoadTileKind is RoadTileKind.TJunction or RoadTileKind.Cross)
        {
            DrawCircle(center, roadWidth * 0.58f, new Color(UiTheme.PanelAlt, 0.18f));
        }
    }

    private void DrawRoadConnection(MapCell cell, Vector2 center, Vector2 half, Color color, float width)
    {
        if (cell.RoadConnections == RoadDirection.None)
        {
            DrawCircle(center, width * 0.42f, color);
            return;
        }

        if (cell.RoadConnections.HasFlag(RoadDirection.North))
        {
            DrawLine(center, center + new Vector2(0f, -half.Y), color, width, true);
        }

        if (cell.RoadConnections.HasFlag(RoadDirection.East))
        {
            DrawLine(center, center + new Vector2(half.X, 0f), color, width, true);
        }

        if (cell.RoadConnections.HasFlag(RoadDirection.South))
        {
            DrawLine(center, center + new Vector2(0f, half.Y), color, width, true);
        }

        if (cell.RoadConnections.HasFlag(RoadDirection.West))
        {
            DrawLine(center, center + new Vector2(-half.X, 0f), color, width, true);
        }
    }

    private void DrawGridObjects()
    {
        if (_mapGeneration == null) return;

        foreach (var mapObject in _mapGeneration.Grid.Objects.Values.OrderBy(mapObject => mapObject.DistrictId).ThenBy(mapObject => mapObject.Id))
        {
            var rect = GridObjectToMapRect(mapObject);
            if (TryGetMapTexture(mapObject.AssetKey, out var texture))
            {
                DrawTextureRect(texture, rect, tile: false);
            }
            else
            {
                var fill = ObjectFallbackColor(mapObject);
                DrawRect(rect.Grow(0.8f), new Color(UiTheme.MapRoadShadow, 0.28f));
                DrawRect(rect, fill);
                DrawRect(rect, new Color(UiTheme.Text, 0.22f), false, 1f);
            }

            if (IsSelectedEventObject(mapObject))
            {
                DrawSelectedEventObjectHighlight(rect);
            }

            if (Math.Min(rect.Size.X, rect.Size.Y) >= 10f)
            {
                DrawText(ObjectFallbackLabel(mapObject), rect.GetCenter() + new Vector2(-4f, 5f), 12, ObjectLabelColor(mapObject));
            }
        }
    }

    private void DrawGridDistrictLabels()
    {
        if (_mapGeneration == null || _world == null) return;

        foreach (var district in _world.Districts)
        {
            if (!_mapGeneration.DistrictAreas.TryGetValue(district.Id, out var area)) continue;

            var pos = GridToMap(area.Origin.Offset(3, 7));
            DrawRect(new Rect2(pos - new Vector2(6f, 17f), new Vector2(190f, 42f)), new Color(UiTheme.MapLand, 0.58f));
            DrawText(district.Name, pos, 17, UiTheme.Text);
            DrawText($"Pop {district.Population} | Support {FormatPercent(district.SupportRating)}", pos + new Vector2(0f, 19f), 12, UiTheme.TextMuted);
        }
    }

    private void DrawGridDebugOverlay()
    {
        if (_mapGeneration == null || Size.X <= 0f || Size.Y <= 0f) return;

        var grid = _mapGeneration.Grid;
        var cellSize = new Vector2(Size.X / grid.WidthMeters, Size.Y / grid.HeightMeters);
        var districtTints = new[]
        {
            new Color(UiTheme.Info, 0.055f),
            new Color(UiTheme.Warning, 0.055f),
            new Color(UiTheme.Success, 0.055f),
            new Color(UiTheme.Danger, 0.045f)
        };

        foreach (var cell in grid.Cells)
        {
            var rect = new Rect2(GridToMap(cell.Position), cellSize);
            if (cell.DistrictId.HasValue)
            {
                DrawRect(rect, districtTints[Math.Abs(cell.DistrictId.Value) % districtTints.Length]);
            }

            if (cell.Surface == MapSurfaceType.Water)
            {
                DrawRect(rect, new Color(UiTheme.MapWater, 0.28f));
            }
            else if (cell.Surface == MapSurfaceType.Blocked)
            {
                DrawRect(rect, new Color(UiTheme.Critical, 0.24f));
            }

            if (cell.HasRoad)
            {
                var roadColor = cell.RoadKind == RoadKind.Bridge
                    ? UiTheme.MapBridge
                    : cell.RoadKind == RoadKind.RegionalRoad
                        ? UiTheme.Warning
                        : UiTheme.Info;
                DrawRect(rect.Grow(-Math.Max(0.2f, Math.Min(cellSize.X, cellSize.Y) * 0.18f)), new Color(roadColor, 0.72f));
            }

            if (cell.HasObject)
            {
                DrawRect(rect.Grow(-Math.Max(0.2f, Math.Min(cellSize.X, cellSize.Y) * 0.08f)), new Color(UiTheme.Danger, 0.58f));
            }
        }

        var lineColor = new Color(UiTheme.Text, 0.10f);
        var majorLineColor = new Color(UiTheme.Text, 0.18f);
        for (var x = 0; x <= grid.WidthMeters; x += 10)
        {
            var mapX = x * cellSize.X;
            DrawLine(new Vector2(mapX, 0f), new Vector2(mapX, Size.Y), x % 50 == 0 ? majorLineColor : lineColor, x % 50 == 0 ? 1.2f : 0.6f);
        }

        for (var y = 0; y <= grid.HeightMeters; y += 10)
        {
            var mapY = y * cellSize.Y;
            DrawLine(new Vector2(0f, mapY), new Vector2(Size.X, mapY), y % 50 == 0 ? majorLineColor : lineColor, y % 50 == 0 ? 1.2f : 0.6f);
        }

        DrawRect(new Rect2(new Vector2(10f, Size.Y - 34f), new Vector2(330f, 25f)), new Color(UiTheme.PanelAlt, 0.88f));
        DrawText("Grid debug: district / water / roads / objects (Ctrl+G), grid render (Ctrl+M)", new Vector2(18f, Size.Y - 16f), 12, UiTheme.Text);
    }

    private void DrawWaterGeography()
    {
        var water = GetWaterPolygon();
        if (water.Length == 0) return;

        DrawColoredPolygon(water, new Color(UiTheme.MapWater, 0.58f));
        DrawPolyline(water.Append(water[0]).ToArray(), new Color(UiTheme.PanelAlt, 0.30f), 7f, true);
        DrawPolyline(water.Append(water[0]).ToArray(), new Color(UiTheme.MapWater, 0.82f), 2.5f, true);
    }

    private Vector2[] GetWaterPolygon()
    {
        if (Size.X <= 260f || Size.Y <= 180f) return Array.Empty<Vector2>();

        return new[]
        {
            new Vector2(Size.X * 0.83f, 0f),
            new Vector2(Size.X, 0f),
            new Vector2(Size.X, Size.Y * 0.17f),
            new Vector2(Size.X * 0.94f, Size.Y * 0.20f),
            new Vector2(Size.X * 0.90f, Size.Y * 0.36f),
            new Vector2(Size.X * 0.96f, Size.Y * 0.54f),
            new Vector2(Size.X, Size.Y),
            new Vector2(Size.X * 0.84f, Size.Y),
            new Vector2(Size.X * 0.76f, Size.Y * 0.76f),
            new Vector2(Size.X * 0.82f, Size.Y * 0.54f),
            new Vector2(Size.X * 0.77f, Size.Y * 0.24f)
        };
    }

    private void DrawDistrict(MapDistrictShape shape)
    {
        var selected = _selectedDistrictId == shape.District.Id;
        var hovered = _hoveredDistrictId == shape.District.Id;
        var fill = DistrictFill(shape.District);
        var border = selected ? UiTheme.Info : hovered ? UiTheme.Warning : UiTheme.Border;

        DrawColoredPolygon(shape.Polygon, fill);
        var outline = shape.Polygon.Append(shape.Polygon[0]).ToArray();
        if (selected || hovered)
        {
            DrawPolyline(outline, new Color(border, 0.22f), selected ? 9f : 7f, true);
        }

        DrawPolyline(outline, new Color(UiTheme.MapRoadShadow, 0.18f), selected ? 5f : hovered ? 4f : 3f, true);
        DrawPolyline(outline, border, selected ? 3.5f : hovered ? 3f : 2f, true);
    }

    private void DrawRoadNetwork()
    {
        if (_shapes.Count == 0) return;

        foreach (var road in GetRegionalRoads())
        {
            DrawRoadSegment(road);
        }
    }

    private void DrawRoadSegment(RoadSegment road)
    {
        DrawRoadSegment(road, _ => true, allowBridge: true);
    }

    private void DrawRoadSegment(RoadSegment road, Func<Vector2, bool> withinBounds, bool allowBridge)
    {
        var offset = new Vector2(2f, 2f);
        DrawConditionedLine(
            road.From + offset,
            road.To + offset,
            new Color(UiTheme.MapRoadShadow, 0.62f),
            road.Width + 4f,
            point => withinBounds(point - offset) && !PointInWater(point - offset));
        DrawConditionedLine(road.From, road.To, UiTheme.MapRoad, road.Width + 1.5f, point => withinBounds(point) && !PointInWater(point));
        DrawConditionedLine(
            road.From,
            road.To,
            new Color(UiTheme.PanelAlt, 0.24f),
            Math.Max(1.5f, road.Width * 0.28f),
            point => withinBounds(point) && !PointInWater(point));

        if (!allowBridge || PointInWater(road.From) || PointInWater(road.To)) return;

        DrawConditionedLine(
            road.From + offset,
            road.To + offset,
            new Color(UiTheme.MapBridgeShadow, 0.70f),
            road.Width + 5f,
            point => withinBounds(point - offset) && PointInWater(point - offset));
        DrawConditionedLine(road.From, road.To, UiTheme.MapBridge, road.Width + 2f, point => withinBounds(point) && PointInWater(point));
        DrawConditionedLine(
            road.From,
            road.To,
            new Color(UiTheme.PanelAlt, 0.28f),
            Math.Max(1.2f, road.Width * 0.25f),
            point => withinBounds(point) && PointInWater(point));
    }

    private void DrawDistrictDetails(MapDistrictShape shape)
    {
        var roads = GetRoadsForDistrict(shape).ToList();
        DrawLocalStreets(shape);
        var projectObjects = DrawProjectObjects(shape, roads);
        var buildings = DrawBuildings(shape, projectObjects, roads);
        buildings.AddRange(projectObjects);
        DrawTrees(shape, buildings);
        DrawMarkers(shape);
        DrawDistrictLabel(shape);
    }

    private void DrawLocalStreets(MapDistrictShape shape)
    {
        foreach (var street in GetLocalStreets(shape))
        {
            DrawRoadSegment(street, point => PointInPolygon(point, shape.Polygon), allowBridge: true);
        }
    }

    private List<Rect2> DrawProjectObjects(MapDistrictShape shape, List<RoadSegment> roads)
    {
        var placed = new List<Rect2>();
        if (_world == null) return placed;

        var projects = _world.Projects
            .Where(p => p.DistrictId == shape.District.Id)
            .OrderBy(p => p.Completed)
            .ThenBy(p => p.Id)
            .ToList();
        if (projects.Count == 0) return placed;

        var index = 0;
        foreach (var project in projects)
        {
            var rect = FindProjectRect(shape, project, roads, placed, index++);
            if (rect == null) continue;

            if (project.Completed)
            {
                DrawCompletedProject(project, rect.Value);
            }
            else
            {
                DrawConstructionProject(project, rect.Value);
            }

            placed.Add(rect.Value);
        }

        return placed;
    }

    private List<Rect2> DrawBuildings(MapDistrictShape shape, IReadOnlyList<Rect2> reserved, List<RoadSegment> roads)
    {
        var placed = reserved.ToList();
        var businessObjects = DrawBusinessObjects(shape, placed, roads);
        placed.AddRange(businessObjects);

        var count = Math.Clamp(shape.District.Population + shape.District.TotalJobs + 8, 8, 24);
        var attempts = count * 8;

        var generatedBuildings = 0;
        for (var i = 0; i < attempts && generatedBuildings < count; i++)
        {
            var seed = shape.District.Id * 97 + i * 17;

            var size = new Vector2(
                StableRange(seed + 1, 12f, 24f),
                StableRange(seed + 2, 10f, 22f));
            var rect = i < roads.Count * 3
                ? RectNearRoad(roads, size, seed + 3)
                : new Rect2(PointInBounds(shape.Bounds.Grow(-20f), seed + 3) - size * 0.5f, size);

            if (!TryPrepareAccessibleRect(rect, shape, roads, placed, 5f)) continue;

            var color = Color.FromHtml("#E6D7BD");
            DrawRect(rect.Grow(1f), new Color(UiTheme.MapRoadShadow, 0.35f));
            DrawRect(rect, color);
            DrawRect(rect, new Color(UiTheme.Text, 0.18f), false, 1f);
            DrawLine(rect.Position + new Vector2(2f, 3f), rect.End - new Vector2(2f, size.Y - 3f), new Color(UiTheme.PanelAlt, 0.42f), 1f);
            placed.Add(rect);
            generatedBuildings++;
        }

        return placed.Where(rect => !reserved.Contains(rect)).ToList();
    }

    private List<Rect2> DrawBusinessObjects(MapDistrictShape shape, IReadOnlyList<Rect2> reserved, List<RoadSegment> roads)
    {
        var placed = reserved.ToList();
        var drawn = new List<Rect2>();
        if (_world == null) return drawn;

        var businesses = _world.Businesses
            .Where(b => b.DistrictId == shape.District.Id && b.Status == BusinessStatus.Active)
            .OrderByDescending(b => b.MaxEmployees)
            .ThenBy(b => b.Id)
            .Take(6)
            .ToList();
        if (businesses.Count == 0) return drawn;

        for (var i = 0; i < businesses.Count; i++)
        {
            var business = businesses[i];
            var size = BusinessFootprint(business);
            Rect2? rect = null;
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var seed = shape.District.Id * 613 + business.Id * 41 + attempt * 17;
                var candidate = attempt < roads.Count * 3
                    ? RectNearRoad(roads, size, seed)
                    : new Rect2(PointInBounds(shape.Bounds.Grow(-24f), seed) - size * 0.5f, size);
                if (!TryPrepareAccessibleRect(candidate, shape, roads, placed, 8f)) continue;

                rect = candidate;
                break;
            }

            if (rect == null) continue;

            DrawBusinessObject(business, rect.Value);
            placed.Add(rect.Value);
            drawn.Add(rect.Value);
        }

        return drawn;
    }

    private void DrawBusinessObject(Business business, Rect2 rect)
    {
        var color = BusinessColor(business);
        DrawRect(rect.Grow(2f), new Color(UiTheme.MapRoadShadow, 0.34f));
        DrawRect(rect, BusinessFill(business));
        DrawRect(rect, color, false, 2f);

        var icon = BusinessIcon(business);
        DrawText(icon, rect.GetCenter() + new Vector2(-5f, 5f), icon == "+" ? 16 : 13, color);

        if (IsIndustryBusiness(business))
        {
            DrawRect(new Rect2(rect.Position + new Vector2(rect.Size.X - 7f, 3f), new Vector2(4f, 9f)), new Color(color, 0.85f));
            var puff = rect.Position + new Vector2(rect.Size.X - 5f, -2f);
            DrawCircle(puff, 2.5f, new Color(UiTheme.TextWeak, 0.28f));
        }
        else if (IsFarmBusiness(business))
        {
            for (var i = 1; i < 4; i++)
            {
                var y = rect.Position.Y + i * rect.Size.Y / 4f;
                DrawLine(new Vector2(rect.Position.X + 3f, y), new Vector2(rect.End.X - 3f, y), new Color(UiTheme.MapPark, 0.45f), 1f);
            }
        }
    }

    private Rect2? FindProjectRect(
        MapDistrictShape shape,
        GovernmentProject project,
        List<RoadSegment> roads,
        IReadOnlyList<Rect2> placed,
        int index)
    {
        var baseSize = ProjectFootprint(project.Type, project.Completed);
        for (var attempt = 0; attempt < 36; attempt++)
        {
            var seed = shape.District.Id * 811 + project.Id * 37 + index * 19 + attempt * 13;
            var rect = attempt < roads.Count * 3
                ? RectNearRoad(roads, baseSize, seed)
                : new Rect2(PointInBounds(shape.Bounds.Grow(-24f), seed) - baseSize * 0.5f, baseSize);
            if (TryPrepareAccessibleRect(rect, shape, roads, placed, 8f))
            {
                return rect;
            }
        }

        var fallback = SafeMarkerPosition(shape, index);
        var fallbackRect = new Rect2(fallback - baseSize * 0.5f, baseSize);
        return TryPrepareAccessibleRect(fallbackRect, shape, roads, placed, 8f) ? fallbackRect : null;
    }

    private void DrawConstructionProject(GovernmentProject project, Rect2 rect)
    {
        var color = ProjectColor(project.Type);
        var pulse = 0.65f + MathF.Sin((float)_animationTime * 4.0f + project.Id) * 0.12f;
        DrawRect(rect.Grow(4f), new Color(color, 0.16f + pulse * 0.08f));
        DrawRect(rect.Grow(1.5f), new Color(UiTheme.MapRoadShadow, 0.36f));
        DrawRect(rect, UiTheme.BuildMenu);
        DrawRect(rect, color, false, 2f);

        var progress = project.DurationTicks <= 0
            ? 0f
            : Math.Clamp((project.DurationTicks - project.RemainingTicks) / (float)project.DurationTicks, 0f, 1f);
        var progressRect = new Rect2(rect.Position + new Vector2(3f, rect.Size.Y - 6f), new Vector2((rect.Size.X - 6f) * progress, 3f));
        DrawRect(new Rect2(rect.Position + new Vector2(3f, rect.Size.Y - 6f), new Vector2(rect.Size.X - 6f, 3f)), new Color(UiTheme.Button, 0.9f));
        DrawRect(progressRect, color);

        DrawLine(rect.Position + new Vector2(4f, 6f), rect.End - new Vector2(4f, rect.Size.Y - 6f), new Color(color, 0.65f), 1.5f);
        DrawLine(new Vector2(rect.End.X - 5f, rect.Position.Y + 5f), new Vector2(rect.Position.X + 5f, rect.End.Y - 9f), new Color(color, 0.45f), 1.2f);
        DrawText(ProjectIcon(project.Type), rect.GetCenter() + new Vector2(-4f, 5f), 12, color);
    }

    private void DrawCompletedProject(GovernmentProject project, Rect2 rect)
    {
        var color = ProjectColor(project.Type);
        DrawRect(rect.Grow(2f), new Color(UiTheme.MapRoadShadow, 0.34f));
        DrawRect(rect, ProjectFill(project.Type));
        DrawRect(rect, color, false, 2f);

        switch (project.Type)
        {
            case ProjectType.Park:
                DrawCircle(rect.GetCenter(), Math.Min(rect.Size.X, rect.Size.Y) * 0.32f, UiTheme.MapPark);
                DrawCircle(rect.GetCenter() + new Vector2(5f, -4f), 4f, new Color(UiTheme.MapGrass, 0.85f));
                break;
            case ProjectType.Road:
                DrawLine(rect.Position + new Vector2(3f, rect.Size.Y * 0.5f), rect.End - new Vector2(3f, rect.Size.Y * 0.5f), UiTheme.MapRoadShadow, 4f, true);
                DrawLine(rect.Position + new Vector2(3f, rect.Size.Y * 0.5f), rect.End - new Vector2(3f, rect.Size.Y * 0.5f), UiTheme.MapRoad, 2.5f, true);
                break;
            case ProjectType.Clinic:
                DrawText("+", rect.GetCenter() + new Vector2(-5f, 5f), 16, UiTheme.Danger);
                break;
            case ProjectType.Police:
                DrawText("P", rect.GetCenter() + new Vector2(-5f, 5f), 14, UiTheme.Info);
                break;
            default:
                DrawText(ProjectIcon(project.Type), rect.GetCenter() + new Vector2(-5f, 5f), 13, color);
                break;
        }
    }

    private void DrawTrees(MapDistrictShape shape, IReadOnlyList<Rect2> buildings)
    {
        var roads = GetRoadsForDistrict(shape).ToList();
        var count = Math.Clamp((int)(shape.District.ServiceLevel / 12f) + 4, 4, 12);
        for (var i = 0; i < count; i++)
        {
            var pos = PointInBounds(shape.Bounds, shape.District.Id * 53 + i * 29);
            if (!PointInPolygon(pos, shape.Polygon)) continue;
            if (roads.Any(road => DistanceToSegment(pos, road.From, road.To) < road.Width + 7f)) continue;
            if (buildings.Any(rect => rect.Grow(8f).HasPoint(pos))) continue;

            DrawCircle(pos, StableRange(shape.District.Id * 59 + i * 7, 3f, 6f), UiTheme.MapPark);
            DrawCircle(pos + new Vector2(1.5f, -1.5f), 2f, new Color(UiTheme.MapGrass, 0.7f));
        }
    }

    private void DrawMarkers(MapDistrictShape shape)
    {
        var district = shape.District;
        var markerIndex = 0;
        var unresolvedDistrictEvents = GetUnresolvedDistrictEvents(district.Id).ToList();

        if (district.HasActiveCrisis || district.CrisisRisk > 65f || unresolvedDistrictEvents.Any(e => e.Type == EventType.Crisis))
        {
            DrawMarker(SafeMarkerPosition(shape, markerIndex++), "!", UiTheme.Danger);
        }

        if (unresolvedDistrictEvents.Any(e => e.Type != EventType.Crisis))
        {
            DrawMarker(SafeMarkerPosition(shape, markerIndex++), "?", UiTheme.Warning);
        }

        if (district.ActiveProjects > 0)
        {
            DrawMarker(SafeMarkerPosition(shape, markerIndex++), "P", UiTheme.Info);
        }

        if (district.AvailableHousing <= 0)
        {
            DrawMarker(SafeMarkerPosition(shape, markerIndex), "H", UiTheme.Warning);
        }
    }

    private void DrawMarker(Vector2 center, string text, Color color)
    {
        var pulse = 1f + MathF.Sin((float)_animationTime * 3.4f + text[0]) * 0.16f;
        var radius = 9f * pulse;
        DrawCircle(center, radius + 4f, new Color(color, 0.18f));
        DrawCircle(center, radius, color);
        DrawCircle(center, radius, new Color(UiTheme.Text, 0.22f), false, 1.5f);
        DrawText(text, center + new Vector2(-4f, 5f), 12, UiTheme.PanelAlt);
    }

    private bool IsSelectedEventObject(PlacedMapObject mapObject)
    {
        return _selectedEventId.HasValue &&
            mapObject.EntityKind == MapObjectEntityKind.GameEvent &&
            mapObject.EntityId == _selectedEventId;
    }

    private void DrawSelectedEventObjectHighlight(Rect2 rect)
    {
        var pulse = 0.55f + 0.45f * MathF.Sin((float)_animationTime * 4.2f);
        var glow = new Color(UiTheme.Warning, 0.18f + pulse * 0.16f);
        var border = UiTheme.Warning.Lerp(UiTheme.Info, pulse * 0.35f);
        DrawRect(rect.Grow(7f + pulse * 2.5f), glow);
        DrawRect(rect.Grow(3f), border, false, 2.2f);
        DrawRect(rect.Grow(6f), new Color(border, 0.38f), false, 1.2f);
    }

    private IEnumerable<GameEvent> GetUnresolvedDistrictEvents(int districtId)
    {
        if (_world == null) yield break;

        foreach (var gameEvent in _world.Events)
        {
            if (gameEvent.IsResolved || !gameEvent.HasChoices) continue;
            if (gameEvent.Choices.Any(choice => choice.DistrictId == districtId))
            {
                yield return gameEvent;
            }
        }
    }

    private static Vector2 SafeMarkerPosition(MapDistrictShape shape, int markerIndex)
    {
        var candidates = new[]
        {
            new Vector2(0.86f, 0.16f),
            new Vector2(0.80f, 0.24f),
            new Vector2(0.72f, 0.18f),
            new Vector2(0.88f, 0.34f),
            new Vector2(0.70f, 0.32f),
            new Vector2(0.62f, 0.20f),
            new Vector2(0.78f, 0.44f),
            new Vector2(0.58f, 0.36f)
        };

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[(markerIndex + i) % candidates.Length];
            var point = shape.Bounds.Position + shape.Bounds.Size * candidate + new Vector2(0f, markerIndex * 20f);
            if (CircleInsidePolygon(point, 12f, shape.Polygon))
            {
                return point;
            }
        }

        return shape.Center;
    }

    private void DrawDistrictLabel(MapDistrictShape shape)
    {
        var pos = shape.Bounds.Position + new Vector2(12f, 22f);
        var district = shape.District;
        DrawRect(new Rect2(pos - new Vector2(7f, 17f), new Vector2(190f, 42f)), new Color(UiTheme.MapLand, 0.58f));
        DrawText(district.Name, pos, 17, UiTheme.Text);
        DrawText($"Pop {district.Population} | Support {FormatPercent(district.SupportRating)}", pos + new Vector2(0f, 19f), 12, UiTheme.TextMuted);
    }

    private void DrawText(string text, Vector2 position, int size, Color color)
    {
        DrawString(ThemeDB.FallbackFont, position, text, HorizontalAlignment.Left, -1f, size, color);
    }

    private bool TryGetMapTexture(string assetKey, out Texture2D texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(assetKey)) return false;

        if (_mapAssetCache.TryGetValue(assetKey, out var cached))
        {
            if (cached == null) return false;
            texture = cached;
            return true;
        }

        foreach (var path in MapAssetCandidates(assetKey))
        {
            if (!ResourceLoader.Exists(path)) continue;

            var loaded = ResourceLoader.Load<Texture2D>(path);
            _mapAssetCache[assetKey] = loaded;
            if (loaded == null) return false;

            texture = loaded;
            return true;
        }

        _mapAssetCache[assetKey] = null;
        return false;
    }

    private static IEnumerable<string> MapAssetCandidates(string assetKey)
    {
        var normalized = assetKey.Trim().Replace('\\', '/');
        yield return $"res://assets/map/{normalized.Replace('.', '/')}.png";
        yield return $"res://assets/map/{normalized}.png";
    }

    private Rect2 GridAreaToMapRect(MapDistrictGridArea area)
    {
        var topLeft = GridToMap(area.Origin);
        var bottomRight = GridToMap(area.Origin.Offset(area.WidthMeters, area.HeightMeters));
        return new Rect2(topLeft, bottomRight - topLeft);
    }

    private Rect2 GridObjectToMapRect(PlacedMapObject mapObject)
    {
        var topLeft = GridToMap(mapObject.Position);
        var bottomRight = GridToMap(mapObject.Position.Offset(mapObject.FootprintWidth, mapObject.FootprintLength));
        return new Rect2(topLeft, bottomRight - topLeft).Grow(-0.8f);
    }

    private Vector2[] BuildVisualDistrictPolygon(MapDistrictGridArea area)
    {
        if (_mapGeneration == null) return RectToPolygon(GridAreaToMapRect(area));

        var points = new List<Vector2>();
        var objectMarginCells = 3;
        foreach (var mapObject in _mapGeneration.Grid.Objects.Values.Where(mapObject => mapObject.DistrictId == area.DistrictId))
        {
            AddGridRectPoints(
                points,
                mapObject.Position.X - objectMarginCells,
                mapObject.Position.Y - objectMarginCells,
                mapObject.Position.X + mapObject.FootprintWidth + objectMarginCells,
                mapObject.Position.Y + mapObject.FootprintLength + objectMarginCells,
                area);
        }

        foreach (var cell in _mapGeneration.Grid.Cells.Where(cell => cell.DistrictId == area.DistrictId && cell.HasRoad))
        {
            AddGridRectPoints(
                points,
                cell.Position.X - 1,
                cell.Position.Y - 1,
                cell.Position.X + 2,
                cell.Position.Y + 2,
                area);
        }

        if (points.Count < 3)
        {
            return RoughenPolygon(RectToPolygon(GridAreaToMapRect(area).Grow(-8f)), area.DistrictId);
        }

        var hull = ConvexHull(points);
        if (hull.Count < 3)
        {
            return RoughenPolygon(RectToPolygon(GridAreaToMapRect(area).Grow(-8f)), area.DistrictId);
        }

        return RoughenPolygon(hull.ToArray(), area.DistrictId);
    }

    private void AddGridRectPoints(List<Vector2> points, int minX, int minY, int maxX, int maxY, MapDistrictGridArea area)
    {
        minX = Math.Clamp(minX, area.MinX, area.MaxX);
        minY = Math.Clamp(minY, area.MinY, area.MaxY);
        maxX = Math.Clamp(maxX, area.MinX + 1, area.MaxX + 1);
        maxY = Math.Clamp(maxY, area.MinY + 1, area.MaxY + 1);
        if (minX >= maxX || minY >= maxY) return;

        points.Add(GridToMap(new GridPosition(minX, minY)));
        points.Add(GridToMap(new GridPosition(maxX, minY)));
        points.Add(GridToMap(new GridPosition(maxX, maxY)));
        points.Add(GridToMap(new GridPosition(minX, maxY)));
    }

    private static Vector2[] RectToPolygon(Rect2 rect)
    {
        return new[]
        {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y)
        };
    }

    private static List<Vector2> ConvexHull(IEnumerable<Vector2> source)
    {
        var points = source
            .Distinct()
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();
        if (points.Count <= 1) return points;

        var lower = new List<Vector2>();
        foreach (var point in points)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], point) <= 0f)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(point);
        }

        var upper = new List<Vector2>();
        for (var i = points.Count - 1; i >= 0; i--)
        {
            var point = points[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], point) <= 0f)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(point);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static float Cross(Vector2 origin, Vector2 a, Vector2 b)
    {
        return (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);
    }

    private static Vector2[] RoughenPolygon(IReadOnlyList<Vector2> polygon, int seed)
    {
        if (polygon.Count < 3) return polygon.ToArray();

        var area = SignedPolygonArea(polygon);
        var result = new List<Vector2>(polygon.Count * 2);
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            var edge = next - current;
            if (edge.LengthSquared() < 0.01f) continue;

            result.Add(current);

            var midpoint = (current + next) * 0.5f;
            var normal = area >= 0f
                ? new Vector2(edge.Y, -edge.X).Normalized()
                : new Vector2(-edge.Y, edge.X).Normalized();
            var offset = StableRange(seed * 101 + i * 17, 1.5f, 5.5f);
            var slide = edge.Normalized() * StableRange(seed * 113 + i * 19, -2.5f, 2.5f);
            result.Add(midpoint + normal * offset + slide);
        }

        return result.Count >= 3 ? result.ToArray() : polygon.ToArray();
    }

    private static float SignedPolygonArea(IReadOnlyList<Vector2> polygon)
    {
        var area = 0f;
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            area += current.X * next.Y - next.X * current.Y;
        }

        return area * 0.5f;
    }

    private static Color SurfaceFallbackColor(string assetKey)
    {
        return assetKey switch
        {
            "terrain.water" => new Color(UiTheme.MapWater, 0.68f),
            "terrain.shoreline" => Color.FromHtml("#B9C2A5"),
            "terrain.park" => new Color(UiTheme.MapPark, 0.72f),
            "terrain.blocked" => new Color(UiTheme.Critical, 0.24f),
            _ => UiTheme.MapLand
        };
    }

    private static Color ObjectFallbackColor(PlacedMapObject mapObject)
    {
        return mapObject.AssetKey switch
        {
            var key when key.Contains("marker.crisis", StringComparison.OrdinalIgnoreCase) => UiTheme.Danger,
            var key when key.Contains("marker.decision", StringComparison.OrdinalIgnoreCase) => UiTheme.Warning,
            var key when key.Contains("marker.", StringComparison.OrdinalIgnoreCase) => UiTheme.Info,
            var key when key.Contains("clinic", StringComparison.OrdinalIgnoreCase) => Color.FromHtml("#E8C8BE"),
            var key when key.Contains("school", StringComparison.OrdinalIgnoreCase) => Color.FromHtml("#C9D8E6"),
            var key when key.Contains("police", StringComparison.OrdinalIgnoreCase) => Color.FromHtml("#C6D2DE"),
            var key when key.Contains("park", StringComparison.OrdinalIgnoreCase) => new Color(UiTheme.MapGrass, 0.92f),
            var key when key.Contains("farm", StringComparison.OrdinalIgnoreCase) => Color.FromHtml("#D8D6A3"),
            var key when key.Contains("workshop", StringComparison.OrdinalIgnoreCase) => Color.FromHtml("#C8B09A"),
            var key when key.Contains("shop", StringComparison.OrdinalIgnoreCase) => Color.FromHtml("#E3C39B"),
            var key when key.Contains("house", StringComparison.OrdinalIgnoreCase) => Color.FromHtml("#E6D7BD"),
            _ => mapObject.Type == PlacedMapObjectType.GovernmentProject ? Color.FromHtml("#DDB16E") : Color.FromHtml("#E6D7BD")
        };
    }

    private static Color ObjectLabelColor(PlacedMapObject mapObject)
    {
        return mapObject.AssetKey.Contains("clinic", StringComparison.OrdinalIgnoreCase)
            ? UiTheme.Danger
            : mapObject.AssetKey.Contains("police", StringComparison.OrdinalIgnoreCase)
                ? UiTheme.Info
                : UiTheme.TextMuted;
    }

    private static string ObjectFallbackLabel(PlacedMapObject mapObject)
    {
        if (mapObject.AssetKey.Contains("marker.crisis", StringComparison.OrdinalIgnoreCase)) return "!";
        if (mapObject.AssetKey.Contains("marker.decision", StringComparison.OrdinalIgnoreCase)) return "?";
        if (mapObject.AssetKey.Contains("marker.", StringComparison.OrdinalIgnoreCase)) return "!";
        if (mapObject.AssetKey.Contains("clinic", StringComparison.OrdinalIgnoreCase)) return "+";
        if (mapObject.AssetKey.Contains("school", StringComparison.OrdinalIgnoreCase)) return "S";
        if (mapObject.AssetKey.Contains("police", StringComparison.OrdinalIgnoreCase)) return "P";
        if (mapObject.AssetKey.Contains("park", StringComparison.OrdinalIgnoreCase)) return "G";
        if (mapObject.AssetKey.Contains("farm", StringComparison.OrdinalIgnoreCase)) return "F";
        if (mapObject.AssetKey.Contains("workshop", StringComparison.OrdinalIgnoreCase)) return "W";
        if (mapObject.AssetKey.Contains("shop", StringComparison.OrdinalIgnoreCase)) return "$";
        return "H";
    }

    private MapDistrictShape? FindDistrictAt(Vector2 point)
    {
        if (_renderGridFallback && _mapGeneration != null)
        {
            foreach (var area in _mapGeneration.DistrictAreas.Values.OrderBy(area => area.DistrictId))
            {
                if (!PointInPolygon(point, BuildVisualDistrictPolygon(area))) continue;
                var district = _world?.Districts.FirstOrDefault(district => district.Id == area.DistrictId);
                if (district != null)
                {
                    return _shapes.LastOrDefault(shape => shape.District.Id == district.Id)
                        ?? new MapDistrictShape(district, BuildVisualDistrictPolygon(area), GridAreaToMapRect(area));
                }
            }

            return null;
        }

        return _shapes.LastOrDefault(shape => PointInPolygon(point, shape.Polygon));
    }

    private bool SelectDistrictAt(Vector2 point)
    {
        var shape = FindDistrictAt(point);
        if (shape == null) return false;

        _selectedDistrictId = shape.District.Id;
        EmitSignal(SignalName.DistrictSelected, shape.District.Id);
        QueueRedraw();
        return true;
    }

    private static Vector2[] CreateDistrictPolygon(Rect2 rect, int seed)
    {
        var inset = Math.Min(rect.Size.X, rect.Size.Y) * 0.04f;
        rect = rect.Grow(-inset);
        var jitterX = Math.Min(28f, rect.Size.X * 0.08f);
        var jitterY = Math.Min(24f, rect.Size.Y * 0.08f);

        return new[]
        {
            new Vector2(rect.Position.X + StableRange(seed + 1, 0f, jitterX), rect.Position.Y + StableRange(seed + 2, 0f, jitterY)),
            new Vector2(rect.Position.X + rect.Size.X * 0.48f + StableRange(seed + 3, -jitterX, jitterX), rect.Position.Y + StableRange(seed + 4, 0f, jitterY)),
            new Vector2(rect.End.X - StableRange(seed + 5, 0f, jitterX), rect.Position.Y + rect.Size.Y * 0.08f + StableRange(seed + 6, -jitterY, jitterY)),
            new Vector2(rect.End.X - StableRange(seed + 7, 0f, jitterX), rect.Position.Y + rect.Size.Y * 0.52f + StableRange(seed + 8, -jitterY, jitterY)),
            new Vector2(rect.End.X - rect.Size.X * 0.08f + StableRange(seed + 9, -jitterX, 0f), rect.End.Y - StableRange(seed + 10, 0f, jitterY)),
            new Vector2(rect.Position.X + rect.Size.X * 0.44f + StableRange(seed + 11, -jitterX, jitterX), rect.End.Y - StableRange(seed + 12, 0f, jitterY)),
            new Vector2(rect.Position.X + StableRange(seed + 13, 0f, jitterX), rect.End.Y - rect.Size.Y * 0.08f + StableRange(seed + 14, -jitterY, jitterY)),
            new Vector2(rect.Position.X + StableRange(seed + 15, 0f, jitterX), rect.Position.Y + rect.Size.Y * 0.48f + StableRange(seed + 16, -jitterY, jitterY))
        };
    }

    private static Vector2 PointInBounds(Rect2 bounds, int seed)
    {
        return new Vector2(
            StableRange(seed + 1, bounds.Position.X + 28f, bounds.End.X - 28f),
            StableRange(seed + 2, bounds.Position.Y + 48f, bounds.End.Y - 26f));
    }

    private static Rect2 RectNearRoad(IReadOnlyList<RoadSegment> roads, Vector2 size, int seed)
    {
        if (roads.Count == 0) return new Rect2(Vector2.Zero, size);

        var road = roads[Math.Abs(seed) % roads.Count];
        var direction = road.To - road.From;
        if (direction.LengthSquared() <= 0.001f) return new Rect2(road.From - size * 0.5f, size);

        direction = direction.Normalized();
        var normal = new Vector2(-direction.Y, direction.X);
        var side = StableRange(seed + 3, 0f, 1f) < 0.5f ? -1f : 1f;
        var t = StableRange(seed + 4, 0.10f, 0.90f);
        var roadPoint = road.From.Lerp(road.To, t);
        var offset = Math.Max(size.X, size.Y) * 0.5f + road.Width * 0.5f + 3f;
        return new Rect2(roadPoint + normal * side * offset - size * 0.5f, size);
    }

    private IEnumerable<RoadSegment> GetRegionalRoads()
    {
        if (_shapes.Count == 0) yield break;

        var ordered = _shapes
            .OrderBy(shape => shape.Center.Y)
            .ThenBy(shape => shape.Center.X)
            .ToList();

        if (ordered.Count <= 1) yield break;

        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var from = FindDistrictRoadGateway(ordered[i], ordered[i + 1].Center);
            var to = FindDistrictRoadGateway(ordered[i + 1], ordered[i].Center);
            if (from == null || to == null) continue;

            foreach (var road in RouteRoad(from.Value, to.Value, 5.2f))
            {
                yield return road;
            }
        }
    }

    private IEnumerable<RoadSegment> RouteRoad(Vector2 from, Vector2 to, float width)
    {
        yield return new RoadSegment(from, to, width);
    }

    private Vector2? FindDistrictRoadGateway(MapDistrictShape shape, Vector2 target)
    {
        var localRoads = GetLocalStreets(shape)
            .SelectMany(road => GetDrawableRoadSegments(road, point => PointInPolygon(point, shape.Polygon) && !PointInWater(point)))
            .ToList();

        if (localRoads.Count == 0) return null;

        return localRoads
            .Select(road => ClosestPointOnSegment(target, road.From, road.To))
            .Where(point => PointInPolygon(point, shape.Polygon) && !PointInWater(point))
            .OrderBy(point => point.DistanceSquaredTo(target))
            .Cast<Vector2?>()
            .FirstOrDefault();
    }

    private static IEnumerable<RoadSegment> GetLocalStreets(MapDistrictShape shape)
    {
        var bounds = shape.Bounds.Grow(-42f);
        if (bounds.Size.X <= 90f || bounds.Size.Y <= 90f) yield break;

        var mainY = bounds.Position.Y + bounds.Size.Y * StableRange(shape.District.Id + 21, 0.46f, 0.56f);
        var mainX = bounds.Position.X + bounds.Size.X * StableRange(shape.District.Id + 31, 0.36f, 0.64f);
        yield return new RoadSegment(new Vector2(bounds.Position.X, mainY), new Vector2(bounds.End.X, mainY), 3f);
        yield return new RoadSegment(new Vector2(mainX, bounds.Position.Y), new Vector2(mainX, bounds.End.Y), 3f);

        if (shape.District.Population + shape.District.TotalJobs < 34) yield break;

        var branchY = bounds.Position.Y + bounds.Size.Y * StableRange(shape.District.Id + 41, 0.26f, 0.78f);
        var branchStart = bounds.Position.X + bounds.Size.X * 0.12f;
        var branchEnd = bounds.Position.X + bounds.Size.X * StableRange(shape.District.Id + 51, 0.58f, 0.82f);
        yield return new RoadSegment(new Vector2(branchStart, branchY), new Vector2(branchEnd, branchY), 2.6f);
    }

    private IEnumerable<RoadSegment> GetRoadsForDistrict(MapDistrictShape shape)
    {
        foreach (var street in GetLocalStreets(shape))
        {
            foreach (var road in GetRoadSegmentsForNetwork(street, point => PointInPolygon(point, shape.Polygon)))
            {
                yield return road;
            }
        }

        foreach (var road in GetRegionalRoads())
        {
            if (PointInPolygon(road.From, shape.Polygon) ||
                PointInPolygon(road.To, shape.Polygon) ||
                PointInPolygon(road.From.Lerp(road.To, 0.5f), shape.Polygon))
            {
                foreach (var visibleRoad in GetRoadSegmentsForNetwork(road, point => PointInPolygon(point, shape.Polygon)))
                {
                    yield return visibleRoad;
                }
            }
        }
    }

    private bool TryPrepareAccessibleRect(
        Rect2 rect,
        MapDistrictShape shape,
        List<RoadSegment> roads,
        IReadOnlyList<Rect2> placed,
        float clearance)
    {
        if (!CanOccupyRect(rect, shape.Polygon, roads, placed, clearance)) return false;
        if (RectHasRoadAccess(rect, roads)) return true;
        if (!TryBuildAccessRoute(rect, shape, roads, placed, out var connectors)) return false;

        foreach (var connector in connectors)
        {
            roads.Add(connector);
            DrawRoadSegment(connector, point => PointInPolygon(point, shape.Polygon), allowBridge: true);
        }

        return true;
    }

    private bool CanOccupyRect(
        Rect2 rect,
        IReadOnlyList<Vector2> polygon,
        IReadOnlyList<RoadSegment> roads,
        IReadOnlyList<Rect2> placed,
        float clearance)
    {
        var protectedRect = rect.Grow(clearance);
        if (!RectInsidePolygon(protectedRect, polygon)) return false;
        if (RectIntersectsWater(protectedRect)) return false;
        if (roads.Any(road => RoadIntersectsRect(road, rect.Grow(1.5f)))) return false;
        if (placed.Any(existing => existing.Grow(clearance).Intersects(protectedRect))) return false;
        return true;
    }

    private static bool RectHasRoadAccess(Rect2 rect, IReadOnlyList<RoadSegment> roads)
    {
        return roads.Any(road =>
            !RoadIntersectsRect(road, rect.Grow(0.5f)) &&
            RoadIntersectsRect(road, rect.Grow(road.Width * 0.5f + 4f)));
    }

    private bool TryBuildAccessRoute(
        Rect2 rect,
        MapDistrictShape shape,
        IReadOnlyList<RoadSegment> roads,
        IReadOnlyList<Rect2> placed,
        out List<RoadSegment> connectors)
    {
        connectors = new List<RoadSegment>();
        if (roads.Count == 0) return false;

        var targetCenter = rect.GetCenter();
        var nearest = roads
            .Select(road => new
            {
                Road = road,
                Point = ClosestPointOnSegment(targetCenter, road.From, road.To),
                Distance = targetCenter.DistanceSquaredTo(ClosestPointOnSegment(targetCenter, road.From, road.To))
            })
            .Where(item => PointInPolygon(item.Point, shape.Polygon) && !PointInWater(item.Point))
            .OrderBy(item => item.Distance)
            .Take(5)
            .ToList();

        foreach (var candidate in nearest)
        {
            var rectAccess = RectAccessPoint(rect, candidate.Point);
            var routes = new[]
            {
                new[] { candidate.Point, rectAccess },
                new[] { candidate.Point, new Vector2(candidate.Point.X, rectAccess.Y), rectAccess },
                new[] { candidate.Point, new Vector2(rectAccess.X, candidate.Point.Y), rectAccess }
            };

            foreach (var route in routes)
            {
                var segments = BuildRouteSegments(route, 2.4f);
                if (segments.Count == 0) continue;
                if (!segments.All(segment => IsValidAccessSegment(segment, shape.Polygon, placed))) continue;

                connectors = segments;
                return true;
            }
        }

        return false;
    }

    private static List<RoadSegment> BuildRouteSegments(IReadOnlyList<Vector2> points, float width)
    {
        var result = new List<RoadSegment>();
        for (var i = 0; i < points.Count - 1; i++)
        {
            if (points[i].DistanceTo(points[i + 1]) > 4f)
            {
                result.Add(new RoadSegment(points[i], points[i + 1], width));
            }
        }

        return result;
    }

    private bool IsValidAccessSegment(RoadSegment segment, IReadOnlyList<Vector2> polygon, IReadOnlyList<Rect2> placed)
    {
        var canBridge = !PointInWater(segment.From) && !PointInWater(segment.To);
        for (var i = 0; i <= 14; i++)
        {
            var point = segment.From.Lerp(segment.To, i / 14f);
            if (!PointInPolygon(point, polygon)) return false;
            if (PointInWater(point) && !canBridge) return false;
        }

        return placed.All(existing => !SegmentIntersectsRect(segment.From, segment.To, existing.Grow(5f)));
    }

    private bool RectIntersectsWater(Rect2 rect)
    {
        var water = GetWaterPolygon();
        if (water.Length == 0) return false;

        var corners = new[]
        {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y)
        };
        if (corners.Append(rect.GetCenter()).Any(point => PointInPolygon(point, water))) return true;
        if (water.Any(rect.HasPoint)) return true;

        for (var i = 0; i < corners.Length; i++)
        {
            var rectA = corners[i];
            var rectB = corners[(i + 1) % corners.Length];
            for (var j = 0; j < water.Length; j++)
            {
                if (SegmentsIntersect(rectA, rectB, water[j], water[(j + 1) % water.Length])) return true;
            }
        }

        return false;
    }

    private bool PointInWater(Vector2 point)
    {
        var water = GetWaterPolygon();
        return water.Length > 0 && PointInPolygon(point, water);
    }

    private static bool RectInsidePolygon(Rect2 rect, IReadOnlyList<Vector2> polygon)
    {
        var points = new[]
        {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y),
            rect.GetCenter()
        };

        return points.All(point => PointInPolygon(point, polygon));
    }

    private static bool CircleInsidePolygon(Vector2 center, float radius, IReadOnlyList<Vector2> polygon)
    {
        var points = new[]
        {
            center,
            center + new Vector2(radius, 0f),
            center + new Vector2(-radius, 0f),
            center + new Vector2(0f, radius),
            center + new Vector2(0f, -radius),
            center + new Vector2(radius * 0.7f, radius * 0.7f),
            center + new Vector2(-radius * 0.7f, radius * 0.7f),
            center + new Vector2(radius * 0.7f, -radius * 0.7f),
            center + new Vector2(-radius * 0.7f, -radius * 0.7f)
        };

        return points.All(point => PointInPolygon(point, polygon));
    }

    private static bool RoadIntersectsRect(RoadSegment road, Rect2 rect)
    {
        if (rect.HasPoint(road.From) || rect.HasPoint(road.To)) return true;

        return SegmentIntersectsRect(road.From, road.To, rect) ||
            DistanceToSegment(rect.GetCenter(), road.From, road.To) < Math.Max(rect.Size.X, rect.Size.Y) * 0.5f;
    }

    private static bool SegmentIntersectsRect(Vector2 from, Vector2 to, Rect2 rect)
    {
        if (rect.HasPoint(from) || rect.HasPoint(to)) return true;

        var corners = new[]
        {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y)
        };

        for (var i = 0; i < corners.Length; i++)
        {
            if (SegmentsIntersect(from, to, corners[i], corners[(i + 1) % corners.Length]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 x, Vector2 y) => x.X * y.Y - x.Y * y.X;

        var ab = b - a;
        var ac = c - a;
        var ad = d - a;
        var cd = d - c;
        var ca = a - c;
        var cb = b - c;
        return Cross(ab, ac) * Cross(ab, ad) <= 0f && Cross(cd, ca) * Cross(cd, cb) <= 0f;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        return point.DistanceTo(ClosestPointOnSegment(point, from, to));
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f) return from;

        var t = Math.Clamp((point - from).Dot(segment) / lengthSquared, 0f, 1f);
        return from + segment * t;
    }

    private static Vector2 RectAccessPoint(Rect2 rect, Vector2 roadPoint)
    {
        var center = rect.GetCenter();
        var delta = roadPoint - center;
        if (Math.Abs(delta.X) > Math.Abs(delta.Y))
        {
            var x = delta.X < 0f ? rect.Position.X : rect.End.X;
            return new Vector2(x, Math.Clamp(roadPoint.Y, rect.Position.Y + 2f, rect.End.Y - 2f));
        }

        var y = delta.Y < 0f ? rect.Position.Y : rect.End.Y;
        return new Vector2(Math.Clamp(roadPoint.X, rect.Position.X + 2f, rect.End.X - 2f), y);
    }

    private IEnumerable<RoadSegment> GetDrawableRoadSegments(RoadSegment road, Func<Vector2, bool> canDraw)
    {
        const int segments = 28;
        Vector2? start = null;
        var previous = road.From;
        var previousInside = canDraw(previous);

        for (var i = 1; i <= segments; i++)
        {
            var current = road.From.Lerp(road.To, i / (float)segments);
            var currentInside = canDraw(current);
            if (previousInside && currentInside)
            {
                start ??= previous;
            }
            else if (start.HasValue)
            {
                if (start.Value.DistanceTo(previous) > 3f)
                {
                    yield return new RoadSegment(start.Value, previous, road.Width);
                }

                start = null;
            }

            previous = current;
            previousInside = currentInside;
        }

        if (start.HasValue && previousInside && start.Value.DistanceTo(previous) > 3f)
        {
            yield return new RoadSegment(start.Value, previous, road.Width);
        }
    }

    private IEnumerable<RoadSegment> GetRoadSegmentsForNetwork(RoadSegment road, Func<Vector2, bool> inBounds)
    {
        foreach (var segment in GetDrawableRoadSegments(road, inBounds))
        {
            if (!PointInWater(segment.From) && !PointInWater(segment.To))
            {
                yield return segment;
                continue;
            }

            foreach (var drySegment in GetDrawableRoadSegments(segment, point => inBounds(point) && !PointInWater(point)))
            {
                yield return drySegment;
            }
        }
    }

    private void DrawConditionedLine(Vector2 from, Vector2 to, Color color, float width, Func<Vector2, bool> canDraw)
    {
        const int segments = 28;
        Vector2? start = null;
        var previous = from;
        var previousInside = canDraw(previous);

        for (var i = 1; i <= segments; i++)
        {
            var current = from.Lerp(to, i / (float)segments);
            var currentInside = canDraw(current);
            if (previousInside && currentInside)
            {
                start ??= previous;
            }
            else if (start.HasValue)
            {
                DrawLine(start.Value, previous, color, width, true);
                start = null;
            }

            previous = current;
            previousInside = currentInside;
        }

        if (start.HasValue && previousInside)
        {
            DrawLine(start.Value, previous, color, width, true);
        }
    }

    private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + 0.0001f) + pi.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float StableRange(int seed, float min, float max)
    {
        var value = MathF.Sin(seed * 12.9898f) * 43758.5453f;
        var normalized = value - MathF.Floor(value);
        return min + (max - min) * normalized;
    }

    private static Color DistrictFill(District district)
    {
        var support = Math.Clamp(district.SupportRating / 100f, 0f, 1f);
        var safety = Math.Clamp(district.AverageSafetySatisfaction / 100f, 0f, 1f);
        var economy = Math.Clamp(district.EconomicLevel / 100f, 0f, 1f);
        var baseColor = UiTheme.MapGrass.Lerp(UiTheme.Success, support * 0.35f);
        var safetyTint = UiTheme.MapWater.Lerp(baseColor, 0.65f + safety * 0.25f);
        var identityTint = (district.Id % 3) switch
        {
            0 => Color.FromHtml("#A7B97F"),
            1 => Color.FromHtml("#B6B982"),
            _ => Color.FromHtml("#8FAE8D")
        };
        return safetyTint.Lerp(identityTint, 0.18f).Lerp(Color.FromHtml("#D29B5E"), economy * 0.10f);
    }

    private static Vector2 ProjectFootprint(ProjectType type, bool completed)
    {
        var size = type switch
        {
            ProjectType.Road => new Vector2(38f, 14f),
            ProjectType.Clinic => new Vector2(28f, 24f),
            ProjectType.School => new Vector2(34f, 24f),
            ProjectType.Police => new Vector2(26f, 22f),
            ProjectType.Housing => new Vector2(34f, 28f),
            ProjectType.Park => new Vector2(32f, 26f),
            _ => new Vector2(28f, 22f)
        };

        return completed ? size : size + new Vector2(4f, 4f);
    }

    private static Color ProjectColor(ProjectType type)
    {
        return type switch
        {
            ProjectType.Road => UiTheme.MapRoadShadow,
            ProjectType.Clinic => UiTheme.Danger,
            ProjectType.School => Color.FromHtml("#7D9BC2"),
            ProjectType.Police => UiTheme.Info,
            ProjectType.Housing => Color.FromHtml("#A7B97F"),
            ProjectType.Park => UiTheme.MapPark,
            _ => UiTheme.Warning
        };
    }

    private static Color ProjectFill(ProjectType type)
    {
        return type switch
        {
            ProjectType.Park => new Color(UiTheme.MapGrass, 0.92f),
            ProjectType.Road => new Color(UiTheme.MapRoad, 0.95f),
            ProjectType.Clinic => Color.FromHtml("#E8C8BE"),
            ProjectType.School => Color.FromHtml("#C9D8E6"),
            ProjectType.Police => Color.FromHtml("#C6D2DE"),
            ProjectType.Housing => Color.FromHtml("#DDE6C5"),
            _ => UiTheme.BuildCard
        };
    }

    private static Vector2 BusinessFootprint(Business business)
    {
        if (IsFarmBusiness(business)) return new Vector2(34f, 22f);
        if (IsIndustryBusiness(business)) return new Vector2(32f, 26f);
        if (IsClinicBusiness(business)) return new Vector2(28f, 24f);
        if (IsShopBusiness(business)) return new Vector2(24f, 20f);
        return new Vector2(24f, 20f);
    }

    private static Color BusinessColor(Business business)
    {
        if (IsFarmBusiness(business)) return UiTheme.MapPark;
        if (IsIndustryBusiness(business)) return Color.FromHtml("#9B7A63");
        if (IsClinicBusiness(business)) return UiTheme.Danger;
        if (IsShopBusiness(business)) return Color.FromHtml("#D29B5E");
        return UiTheme.Warning;
    }

    private static Color BusinessFill(Business business)
    {
        if (IsFarmBusiness(business)) return Color.FromHtml("#D8D6A3");
        if (IsIndustryBusiness(business)) return Color.FromHtml("#C8B09A");
        if (IsClinicBusiness(business)) return Color.FromHtml("#E8C8BE");
        if (IsShopBusiness(business)) return Color.FromHtml("#E3C39B");
        return Color.FromHtml("#E6D7BD");
    }

    private static string BusinessIcon(Business business)
    {
        if (IsFarmBusiness(business)) return "F";
        if (IsIndustryBusiness(business)) return "W";
        if (IsClinicBusiness(business)) return "+";
        if (IsShopBusiness(business)) return "$";
        return "B";
    }

    private static bool IsFarmBusiness(Business business)
    {
        return ContainsBusinessTerm(business, "farm") || ContainsBusinessTerm(business, "food");
    }

    private static bool IsIndustryBusiness(Business business)
    {
        return ContainsBusinessTerm(business, "factory")
            || ContainsBusinessTerm(business, "workshop")
            || ContainsBusinessTerm(business, "industry")
            || ContainsBusinessTerm(business, "goods");
    }

    private static bool IsClinicBusiness(Business business)
    {
        return ContainsBusinessTerm(business, "clinic")
            || ContainsBusinessTerm(business, "health")
            || ContainsBusinessTerm(business, "medical");
    }

    private static bool IsShopBusiness(Business business)
    {
        return ContainsBusinessTerm(business, "shop")
            || ContainsBusinessTerm(business, "trade")
            || ContainsBusinessTerm(business, "store");
    }

    private static bool ContainsBusinessTerm(Business business, string term)
    {
        return business.Type.Contains(term, StringComparison.OrdinalIgnoreCase)
            || business.ProductionType.Contains(term, StringComparison.OrdinalIgnoreCase)
            || business.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static string ProjectIcon(ProjectType type)
    {
        return type switch
        {
            ProjectType.Road => "R",
            ProjectType.Clinic => "+",
            ProjectType.School => "S",
            ProjectType.Police => "P",
            ProjectType.Housing => "H",
            ProjectType.Park => "G",
            _ => "B"
        };
    }

    private static string BuildTooltip(District district)
    {
        return $"{district.Name}\n" +
               $"Population: {district.Population}\n" +
               $"Support: {FormatPercent(district.SupportRating)}\n" +
               $"Safety: {FormatPercent(district.AverageSafetySatisfaction)}\n" +
               $"Services: {FormatPercent(district.ServiceLevel)}";
    }

    private static string FormatPercent(float value)
    {
        return value.ToString("F1", CultureInfo.InvariantCulture) + "%";
    }

    private sealed class MapDistrictShape
    {
        public MapDistrictShape(District district, Vector2[] polygon, Rect2 bounds)
        {
            District = district;
            Polygon = polygon;
            Bounds = bounds;
            Center = new Vector2(polygon.Average(v => v.X), polygon.Average(v => v.Y));
        }

        public District District { get; }
        public Vector2[] Polygon { get; }
        public Rect2 Bounds { get; }
        public Vector2 Center { get; }
    }

    private readonly record struct RoadSegment(Vector2 From, Vector2 To, float Width);
}
