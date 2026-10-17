using System.Text;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>The parchment-and-lamplight look shared with the web build: colours, fonts and the theme.</summary>
public static class Ui
{
    public static readonly Color Bg = new("17140f");
    public static readonly Color Panel = new("211d17");
    public static readonly Color Panel2 = new("2a251d");
    public static readonly Color Ink = new("efe6d2");
    public static readonly Color Ink2 = new("b3a68f");
    public static readonly Color Line = new("3a3328");
    public static readonly Color Accent = new("e07a5f");
    public static readonly Color Gold = new("e8b93a");
    public static readonly Color Good = new("8cc084");
    public static readonly Color Bad = new("ef7f6f");

    public static string Hex(Color c) => "#" + c.ToHtml(false);

    public static Font Body { get; private set; } = null!;
    public static Font Medium { get; private set; } = null!;
    public static Font Bold { get; private set; } = null!;
    public static Font Display { get; private set; } = null!;

    private static FontVariation Weight(FontFile file, int weight)
    {
        var tag = TextServerManager.GetPrimaryInterface().NameToTag("wght");
        return new FontVariation { BaseFont = file, VariationOpentype = new Godot.Collections.Dictionary { { tag, weight } } };
    }

    public static Theme BuildTheme()
    {
        var inter = GD.Load<FontFile>("res://assets/fonts/Inter.ttf");
        var cinzel = GD.Load<FontFile>("res://assets/fonts/Cinzel.ttf");
        Body = Weight(inter, 400);
        Medium = Weight(inter, 550);
        Bold = Weight(inter, 650);
        Display = Weight(cinzel, 700);

        var t = new Theme { DefaultFont = Body, DefaultFontSize = 14 };
        t.SetColor("font_color", "Label", Ink);
        t.SetColor("default_color", "RichTextLabel", Ink);
        t.SetFont("bold_font", "RichTextLabel", Bold);
        t.SetFont("italics_font", "RichTextLabel", Body);
        t.SetFont("normal_font", "RichTextLabel", Body);
        t.SetConstant("line_separation", "RichTextLabel", 3);

        t.SetStylebox("panel", "PanelContainer", Box(Panel, Line, 14));
        t.SetStylebox("panel", "Panel", Box(Bg, Bg, 0, 0));

        // Buttons: ghost by default; the "Post" variation is the accent call to action.
        t.SetStylebox("normal", "Button", Box(Panel, Line, 8, 1, 10, 5));
        t.SetStylebox("hover", "Button", Box(Panel2, Ink2, 8, 1, 10, 5));
        t.SetStylebox("pressed", "Button", Box(Panel2, Accent, 8, 1, 10, 5));
        t.SetStylebox("disabled", "Button", Box(Panel, Line, 8, 1, 10, 5));
        t.SetStylebox("focus", "Button", new StyleBoxEmpty());
        t.SetColor("font_color", "Button", Ink);
        t.SetColor("font_hover_color", "Button", Ink);
        t.SetColor("font_pressed_color", "Button", Ink);
        t.SetColor("font_disabled_color", "Button", Ink2 with { A = 0.5f });
        t.SetFont("font", "Button", Medium);

        t.SetTypeVariation("Post", "Button");
        t.SetStylebox("normal", "Post", Box(Accent, Accent, 8, 0, 12, 6));
        t.SetStylebox("hover", "Post", Box(Accent.Lightened(0.1f), Accent, 8, 0, 12, 6));
        t.SetStylebox("pressed", "Post", Box(Accent.Darkened(0.15f), Accent, 8, 0, 12, 6));
        t.SetStylebox("disabled", "Post", Box(Accent with { A = 0.35f }, Accent, 8, 0, 12, 6));
        t.SetColor("font_color", "Post", Colors.White);
        t.SetColor("font_hover_color", "Post", Colors.White);
        t.SetColor("font_pressed_color", "Post", Colors.White);
        t.SetColor("font_disabled_color", "Post", new Color(1, 1, 1, 0.6f));
        t.SetFont("font", "Post", Bold);

        // Tabs and speed buttons: flat until chosen.
        t.SetTypeVariation("Tab", "Button");
        t.SetStylebox("normal", "Tab", Flat(Colors.Transparent, 4, 11));
        t.SetStylebox("hover", "Tab", Flat(Colors.Transparent, 4, 11));
        t.SetStylebox("pressed", "Tab", Underline(Accent));
        t.SetStylebox("hover_pressed", "Tab", Underline(Accent));
        t.SetColor("font_color", "Tab", Ink2);
        t.SetColor("font_hover_color", "Tab", Ink);
        t.SetColor("font_pressed_color", "Tab", Ink);
        t.SetColor("font_hover_pressed_color", "Tab", Ink);
        t.SetFont("font", "Tab", Bold);
        t.SetFontSize("font_size", "Tab", 13);

        t.SetTypeVariation("Speed", "Button");
        t.SetStylebox("normal", "Speed", Flat(Colors.Transparent, 10, 5, 7));
        t.SetStylebox("hover", "Speed", Flat(Panel2, 10, 5, 7));
        t.SetStylebox("pressed", "Speed", Flat(Accent, 10, 5, 7));
        t.SetStylebox("hover_pressed", "Speed", Flat(Accent, 10, 5, 7));
        t.SetColor("font_pressed_color", "Speed", Colors.White);
        t.SetColor("font_hover_pressed_color", "Speed", Colors.White);
        t.SetFont("font", "Speed", Bold);

        t.SetStylebox("panel", "TooltipPanel", Box(Panel2, Line, 6, 1, 8, 5));
        t.SetColor("font_color", "TooltipLabel", Ink);

        var grabber = Flat(Line, 0, 0, 99);
        t.SetStylebox("grabber", "VScrollBar", grabber);
        t.SetStylebox("grabber_highlight", "VScrollBar", Flat(Ink2, 0, 0, 99));
        t.SetStylebox("grabber_pressed", "VScrollBar", Flat(Ink2, 0, 0, 99));
        t.SetStylebox("scroll", "VScrollBar", Flat(Colors.Transparent, 3, 0, 99));
        return t;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int radius, int borderWidth = 1, int padX = 0, int padY = 0) =>
        new()
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padY,
            ContentMarginBottom = padY,
            AntiAliasing = true,
        };

    public static StyleBoxFlat Flat(Color bg, int padX, int padY, int radius = 0) => Box(bg, bg, radius, 0, padX, padY);

    private static StyleBoxFlat Underline(Color c)
    {
        var s = Flat(Colors.Transparent, 4, 11);
        s.BorderColor = c;
        s.BorderWidthBottom = 2;
        return s;
    }

    public static Label Label(string text, Color? color = null, Font? font = null, int size = 14)
    {
        var l = new Label { Text = text };
        if (color != null) l.AddThemeColorOverride("font_color", color.Value);
        if (font != null) l.AddThemeFontOverride("font", font);
        if (size != 14) l.AddThemeFontSizeOverride("font_size", size);
        return l;
    }

    /// <summary>Wrapping BBCode text whose [url] links report to the client (agents, sites, chronicles).</summary>
    public static RichTextLabel Rich(string bbcode, int size = 14)
    {
        var r = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MetaUnderlined = false,
        };
        r.Text = Coined(bbcode);
        if (size != 14)
        {
            r.AddThemeFontSizeOverride("normal_font_size", size);
            r.AddThemeFontSizeOverride("bold_font_size", size);
        }
        r.MetaClicked += meta => Links?.Invoke(meta.AsString());
        return r;
    }

    /// <summary>Where link clicks go; set by Main.</summary>
    public static Action<string>? Links;

    public static string Esc(string s) => s.Replace("[", "[lb]");

    public static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public static string C(Color c, string text) => $"[color={Hex(c)}]{text}[/color]";

    public static string Days(int n) => $"{n} {(n == 1 ? "day" : "days")}";

    /// <summary>Which icon goes with a meter's label.</summary>
    public static string? MeterIcon(string label) => label switch
    {
        "Warband" => "warband",
        "Your defence" => "defence",
        "Mandate" => "mandate",
        "Favour" => "favour",
        "Suspicion" => "suspicion",
        _ => null,
    };

    // ---- Icons: one per resource, used wherever it appears (assets/icons/*.svg) ----

    private static readonly Dictionary<string, Texture2D> Icons = [];

    public static Texture2D Icon(string name)
    {
        if (!Icons.TryGetValue(name, out var t)) Icons[name] = t = GD.Load<Texture2D>($"res://assets/icons/{name}.svg");
        return t;
    }

    /// <summary>An icon inline in rich text.</summary>
    public static string Img(string name, int size = 15) => $"[img={size}x{size} valign=center]res://assets/icons/{name}.svg[/img]";

    private static readonly System.Text.RegularExpressions.Regex Coins = new(@"(?<![\w#=])(\d[\d,]*(?:\.\d+)?)(g\b| gold\b)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Put the gold icon before every sum of money in a piece of rich text: "120g" and "120 gold".</summary>
    public static string Coined(string bbcode) => Coins.Replace(bbcode, m => $"{Img("gold", 14)}{m.Groups[1].Value}{(m.Groups[2].Value == "g" ? "" : " gold")}");

    /// <summary>A button label with its sum of money shown with the gold icon instead of a "g".</summary>
    public static void GoldButton(Button b)
    {
        var m = System.Text.RegularExpressions.Regex.Match(b.Text, @"(\d[\d,]*)g\b");
        if (!m.Success)
        {
            b.Icon = null; // buttons that are reused may have shown a price before
            return;
        }
        b.Text = b.Text.Remove(m.Index + m.Length - 1, 1);
        b.Icon = Icon("gold");
        b.ExpandIcon = false;
        b.AddThemeConstantOverride("icon_max_width", 16);
    }
}

/// <summary>
/// A side pane rebuilt from a description each time the game changes, but only re-instantiated
/// when the description differs, so a running clock never steals a click or a hover.
/// </summary>
public sealed class Pane
{
    public VBoxContainer Box { get; }
    private readonly StringBuilder _sig = new();
    private readonly List<Func<Control>> _items = [];
    private string _shown = "\0";

    public Pane(VBoxContainer box) => Box = box;

    public void Begin()
    {
        _sig.Clear();
        _items.Clear();
    }

    public void Add(string sig, Func<Control> make)
    {
        _sig.Append(sig).Append('\u001f');
        _items.Add(make);
    }

    public void Commit()
    {
        string sig = _sig.ToString();
        if (sig == _shown) return;
        _shown = sig;
        foreach (var c in Box.GetChildren()) c.QueueFree();
        foreach (var make in _items) Box.AddChild(make());
    }

    public void Invalidate() => _shown = "\0";

    // ---- Building blocks ----

    public void Heading(string text) =>
        Add("h" + text, () =>
        {
            var l = Ui.Label(text.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 11);
            l.AddThemeConstantOverride("line_spacing", 0);
            var m = new MarginContainer();
            m.AddThemeConstantOverride("margin_top", 12);
            m.AddChild(l);
            return m;
        });

    public void Text(string bbcode, int size = 14) => Add("t" + size + bbcode, () => Ui.Rich(bbcode, size));

    public void Note(string text) => Text(Ui.C(Ui.Ink2, text), 12);

    /// <summary>A card with a tinted background, for summaries at the top of a pane.</summary>
    public void Card(string bbcode, Color? border = null) =>
        Add("c" + bbcode + border, () =>
        {
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, border ?? Ui.Panel2, 10, border == null ? 0 : 1, 12, 10));
            p.AddChild(Ui.Rich(bbcode));
            return p;
        });

    /// <summary>A row: text on the left, optional buttons on the right, a rule beneath.</summary>
    public void Row(string bbcode, params (string Label, bool Enabled, bool Primary, Action Press)[] buttons)
    {
        var sig = new StringBuilder("r").Append(bbcode);
        foreach (var b in buttons) sig.Append('|').Append(b.Label).Append(b.Enabled).Append(b.Primary);
        Add(sig.ToString(), () =>
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 8);
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 8);
            h.AddChild(Ui.Rich(bbcode));
            foreach (var b in buttons)
            {
                var btn = new Button { Text = b.Label, Disabled = !b.Enabled, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, ThemeTypeVariation = b.Primary ? "Post" : "" };
                Ui.GoldButton(btn);
                var press = b.Press;
                btn.Pressed += () => press();
                h.AddChild(btn);
            }
            v.AddChild(h);
            v.AddChild(new ColorRect { Color = Ui.Line, CustomMinimumSize = new Vector2(0, 1) });
            return v;
        });
    }

    public void Buttons(params (string Label, bool Enabled, bool Primary, Action Press)[] buttons)
    {
        var sig = new StringBuilder("b");
        foreach (var b in buttons) sig.Append('|').Append(b.Label).Append(b.Enabled).Append(b.Primary);
        Add(sig.ToString(), () =>
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 8);
            foreach (var b in buttons)
            {
                var btn = new Button { Text = b.Label, Disabled = !b.Enabled, ThemeTypeVariation = b.Primary ? "Post" : "" };
                Ui.GoldButton(btn);
                var press = b.Press;
                btn.Pressed += () => press();
                h.AddChild(btn);
            }
            return h;
        });
    }

    /// <summary>A labelled meter, 0 to 100.</summary>
    public void Meter(string label, double value, string figure, bool bad = false) =>
        Add($"m{label}{Math.Round(value)}{figure}{bad}", () =>
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 8);
            if (Ui.MeterIcon(label) is string icon) h.AddChild(new TextureRect { Texture = Ui.Icon(icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(16, 16), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            var l = Ui.Label(label, Ui.Ink2, null, 12);
            l.CustomMinimumSize = new Vector2(80, 0);
            h.AddChild(l);
            var bar = new MeterBar { Value = (float)Math.Clamp(value / 100, 0, 1), Fill = bad ? Ui.Bad : Ui.Gold, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(60, 8), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            h.AddChild(bar);
            var f = Ui.Label(figure, Ui.Ink, Ui.Bold, 14);
            f.CustomMinimumSize = new Vector2(34, 0);
            f.HorizontalAlignment = HorizontalAlignment.Right;
            h.AddChild(f);
            return h;
        });
}

/// <summary>A rounded progress bar.</summary>
public partial class MeterBar : Control
{
    public float Value;
    public Color Fill = Ui.Gold;

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Ui.Flat(Ui.Line, 0, 0, 99), r);
        if (Value > 0) DrawStyleBox(Ui.Flat(Fill, 0, 0, 99), new Rect2(Vector2.Zero, new Vector2(Mathf.Max(Size.Y, Size.X * Value), Size.Y)));
    }
}
