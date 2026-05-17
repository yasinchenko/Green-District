using Godot;

namespace GreenDistrict.Godot.Scripts;

public static class UiTheme
{
    public static readonly Color UiBackground = FromHex("E7E1D2");
    public static readonly Color Panel = FromHex("F3EEE3");
    public static readonly Color PanelAlt = FromHex("F7F2E7");
    public static readonly Color Border = FromHex("CFC6B4");
    public static readonly Color Text = FromHex("2F2B27");
    public static readonly Color TextMuted = FromHex("6A655D");
    public static readonly Color TextWeak = FromHex("8E887E");
    public static readonly Color Hud = FromHex("F1EBDD");
    public static readonly Color Button = FromHex("D9CFBB");
    public static readonly Color ButtonHover = FromHex("C9BEA7");
    public static readonly Color ButtonPressed = FromHex("B9AD96");
    public static readonly Color Event = FromHex("F7F2E7");
    public static readonly Color EventHover = FromHex("EEE5D4");
    public static readonly Color BuildMenu = FromHex("EAE3D4");
    public static readonly Color BuildCard = FromHex("F5F0E5");
    public static readonly Color Success = FromHex("5E8C61");
    public static readonly Color Trend = FromHex("78A06F");
    public static readonly Color Warning = FromHex("C89A45");
    public static readonly Color Danger = FromHex("B85B4F");
    public static readonly Color Critical = FromHex("8E3E36");
    public static readonly Color Info = FromHex("5D7E9A");
    public static readonly Color MapLand = FromHex("CFCB9A");
    public static readonly Color MapGrass = FromHex("9FB27D");
    public static readonly Color MapPark = FromHex("7D9465");
    public static readonly Color MapWater = FromHex("7FA9B8");
    public static readonly Color MapRoad = FromHex("B8B1A3");
    public static readonly Color MapRoadShadow = FromHex("9A9388");
    public static readonly Color MapBridge = FromHex("9E754E");
    public static readonly Color MapBridgeShadow = FromHex("6F543B");

    public const int Radius = 6;

    public static StyleBoxFlat PanelStyle(Color? color = null, Color? border = null, int radius = Radius, int borderWidth = 1)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color ?? Panel,
            BorderColor = border ?? Border
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(radius);
        return style;
    }

    public static StyleBoxFlat ButtonStyle(Color color)
    {
        var style = PanelStyle(color, Border, 5, 1);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 5;
        style.ContentMarginBottom = 5;
        return style;
    }

    public static void ApplyButton(Button button)
    {
        button.MouseFilter = Control.MouseFilterEnum.Stop;
        button.AddThemeStyleboxOverride("normal", ButtonStyle(Button));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(ButtonHover));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(ButtonPressed));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(Border));
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Text);
        button.AddThemeColorOverride("font_disabled_color", TextWeak);
        button.FocusMode = Control.FocusModeEnum.None;
    }

    public static void ApplyTabs(TabContainer tabs)
    {
        tabs.AddThemeStyleboxOverride("panel", PanelStyle(PanelAlt));
        tabs.AddThemeStyleboxOverride("tab_selected", TabStyle(ButtonPressed));
        tabs.AddThemeStyleboxOverride("tab_hovered", TabStyle(ButtonHover));
        tabs.AddThemeStyleboxOverride("tab_unselected", TabStyle(Button));
        tabs.AddThemeStyleboxOverride("tab_disabled", TabStyle(Border));
        tabs.AddThemeColorOverride("font_selected_color", Text);
        tabs.AddThemeColorOverride("font_hovered_color", Text);
        tabs.AddThemeColorOverride("font_unselected_color", TextMuted);
        tabs.AddThemeColorOverride("font_disabled_color", TextWeak);
        tabs.AddThemeConstantOverride("side_margin", 4);
    }

    public static void ApplyLabel(Label label, int fontSize = 14, Color? color = null)
    {
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.AddThemeColorOverride("font_color", color ?? Text);
        label.AddThemeFontSizeOverride("font_size", fontSize);
    }

    public static Label Icon(string text, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(24, 24)
        };
        ApplyLabel(label, 13, color ?? TextMuted);
        return label;
    }

    private static Color FromHex(string hex)
    {
        return Color.FromHtml("#" + hex);
    }

    private static StyleBoxFlat TabStyle(Color color)
    {
        var style = PanelStyle(color, Border, 4, 1);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 5;
        style.ContentMarginBottom = 5;
        return style;
    }
}
