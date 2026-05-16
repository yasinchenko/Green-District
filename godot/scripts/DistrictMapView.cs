using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GreenDistrict.Simulation.Core;
using Godot;

namespace GreenDistrict.Godot.Scripts;

public partial class DistrictMapView : Control
{
    private readonly List<PanelContainer> _tiles = new();
    private WorldState? _world;

    public override void _Ready()
    {
        ClipContents = true;
        Resized += LayoutTiles;
    }

    public void SetWorld(WorldState world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        RebuildTiles();
    }

    private void RebuildTiles()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _tiles.Clear();
        if (_world == null) return;

        foreach (var district in _world.Districts.OrderBy(d => d.Id))
        {
            var tile = CreateDistrictTile(district);
            AddChild(tile);
            _tiles.Add(tile);
        }

        LayoutTiles();
    }

    private PanelContainer CreateDistrictTile(District district)
    {
        var tile = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = BuildTooltip(district)
        };
        tile.AddThemeStyleboxOverride("panel", CreateTileStyle(district));
        tile.AddThemeConstantOverride("margin_left", 10);
        tile.AddThemeConstantOverride("margin_top", 8);
        tile.AddThemeConstantOverride("margin_right", 10);
        tile.AddThemeConstantOverride("margin_bottom", 8);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 3);
        tile.AddChild(rows);

        var name = new Label
        {
            Text = district.Name,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        name.AddThemeFontSizeOverride("font_size", 18);
        rows.AddChild(name);

        rows.AddChild(new Label { Text = $"Pop {district.Population} | Jobs {district.TotalJobs - district.OpenJobs}/{district.TotalJobs}" });
        rows.AddChild(new Label { Text = $"Housing {district.OccupiedHousing}/{district.HousingCapacity}" });
        rows.AddChild(new Label { Text = $"Support {FormatPercent(district.SupportRating)}" });

        return tile;
    }

    private void LayoutTiles()
    {
        if (_tiles.Count == 0) return;

        var available = Size;
        if (available.X <= 0f || available.Y <= 0f) return;

        var count = _tiles.Count;
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        var rows = Math.Max(1, (int)Math.Ceiling(count / (float)columns));
        const float gap = 10f;
        var cellWidth = Math.Max(120f, (available.X - gap * (columns - 1)) / columns);
        var cellHeight = Math.Max(96f, (available.Y - gap * (rows - 1)) / rows);

        for (var i = 0; i < _tiles.Count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var tile = _tiles[i];
            tile.Position = new Vector2(column * (cellWidth + gap), row * (cellHeight + gap));
            tile.Size = new Vector2(cellWidth, cellHeight);
        }
    }

    private static StyleBoxFlat CreateTileStyle(District district)
    {
        var support = Math.Clamp(district.SupportRating / 100f, 0f, 1f);
        var safety = Math.Clamp(district.AverageSafetySatisfaction / 100f, 0f, 1f);
        var economic = Math.Clamp(district.EconomicLevel / 100f, 0f, 1f);
        var color = new Color(
            0.20f + (1f - support) * 0.35f,
            0.26f + support * 0.32f,
            0.32f + safety * 0.22f,
            1f);

        var border = new Color(
            0.28f + economic * 0.28f,
            0.36f + support * 0.24f,
            0.42f + safety * 0.24f,
            1f);

        var style = new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = border
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        return style;
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
}
