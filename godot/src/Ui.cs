using System.Text;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>The parchment-and-lamplight look shared with the web build: colours, fonts and the theme.</summary>
public static partial class Ui
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
        // Oak planks with iron studs (Castle.cs); the "Post" variation is a red banner for the main action.
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) t.SetStylebox(state, "Button", Castle.Button(state, banner: false, 11, 6));
        t.SetStylebox("hover_pressed", "Button", Castle.Button("pressed", banner: false, 11, 6));
        t.SetStylebox("focus", "Button", new StyleBoxEmpty());
        t.SetColor("font_color", "Button", Ink);
        t.SetColor("font_hover_color", "Button", Ink);
        t.SetColor("font_pressed_color", "Button", Ink);
        t.SetColor("font_disabled_color", "Button", Ink2 with { A = 0.5f });
        t.SetFont("font", "Button", Medium);

        t.SetTypeVariation("Post", "Button");
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) t.SetStylebox(state, "Post", Castle.Button(state, banner: true, 13, 6));
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
    /// <summary>
    /// Empty a container. Children leave the tree now and are freed at the end of the frame, so a button can
    /// rebuild the box it sits in from its own Pressed signal (freeing it outright mid-signal crashes the engine).
    /// </summary>
    /// <summary>
    /// Shrink a centred card back to its content and re-centre it once the frame lays it out. A container
    /// only ever grows on its own, so a card whose wrapped text first measured tall (before it knew its
    /// width) would keep that height as an empty column down the screen.
    /// </summary>
    public static void Refit(Control card) => Callable.From(() =>
    {
        card.ResetSize();
        card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
    }).CallDeferred();

    public static void Clear(Node box)
    {
        foreach (var c in box.GetChildren())
        {
            box.RemoveChild(c);
            c.QueueFree();
        }
    }

    public static RichTextLabel Rich(string bbcode, int size = 14, string? skip = null)
    {
        var r = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MetaUnderlined = false,
            HintUnderlined = false,
        };
        r.Text = Dress(bbcode, skip);
        if (size != 14)
        {
            r.AddThemeFontSizeOverride("normal_font_size", size);
            r.AddThemeFontSizeOverride("bold_font_size", size);
        }
        r.MetaClicked += meta =>
        {
            string m = meta.AsString();
            if (m.StartsWith("kw:", StringComparison.Ordinal)) Keywords?.Pin(m[3..]);
            else Links?.Invoke(m);
        };
        r.MetaHoverStarted += meta =>
        {
            string m = meta.AsString();
            if (m.StartsWith("kw:", StringComparison.Ordinal)) Keywords?.Hover(m[3..], r);
        };
        r.MetaHoverEnded += meta =>
        {
            if (meta.AsString().StartsWith("kw:", StringComparison.Ordinal)) Keywords?.Unhover();
        };
        return r;
    }

    /// <summary>Set a rich label's text, dressed like <see cref="Rich"/>'s: coins and keywords.</summary>
    public static void SetRich(RichTextLabel r, string bbcode) => r.Text = Dress(bbcode);

    /// <summary>Where link clicks go; set by Main.</summary>
    public static Action<string>? Links;

    /// <summary>Shows and pins the glossary's cards for keywords; set by Main.</summary>
    public static IKeywordCards? Keywords;

    /// <summary>Everything rich text gets: the gold icon on sums of money, and links on the glossary's keywords.</summary>
    public static string Dress(string bbcode, string? skip = null) => Keyworded(Coined(bbcode), skip);

    // ---- Keywords: every glossary term in rich text links to its entry (content/game/glossary.json) ----

    public static readonly Color KeywordColor = new("ecd59a");
    private static Dictionary<string, GlossaryEntry>? _byWord;
    private static Dictionary<string, GlossaryEntry>? _byId;
    private static System.Text.RegularExpressions.Regex? _keywords;

    public static GlossaryEntry? Entry(string id)
    {
        LoadGlossary();
        return _byId!.GetValueOrDefault(id);
    }

    public static GlossaryEntry? EntryForWord(string word)
    {
        LoadGlossary();
        return _byWord!.GetValueOrDefault(word.ToLowerInvariant());
    }

    /// <summary>The pattern that finds keywords (see <see cref="Glossary.Pattern"/>).</summary>
    public static System.Text.RegularExpressions.Regex KeywordPattern
    {
        get
        {
            LoadGlossary();
            return _keywords!;
        }
    }

    private static void LoadGlossary()
    {
        if (_keywords != null) return;
        var book = Glossary.Default();
        _byWord = Glossary.Words(book).ToDictionary(p => p.Word, p => p.Entry);
        _byId = book.Entries.ToDictionary(e => e.Id);
        _keywords = Glossary.Pattern(book);
    }

    /// <summary>
    /// Make a control stand for a keyword: hovering it shows the entry's card (with a line of its own, such as the
    /// figure's meaning right now) and a click pins it. Replaces the control's tooltip.
    /// </summary>
    public static void KeywordOn(Control c, string id, Func<string>? extra = null)
    {
        c.TooltipText = "";
        c.MouseFilter = Control.MouseFilterEnum.Stop;
        c.MouseDefaultCursorShape = Control.CursorShape.Help;
        c.MouseEntered += () => Keywords?.Show(id, extra?.Invoke());
        c.MouseExited += () => Keywords?.Unhover();
        c.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Keywords?.Pin(id, extra?.Invoke());
        };
    }

    /// <summary>A kind's icon as a link to its glossary card (an enemy kind, a good), or nothing if it has none.</summary>
    public static string KindIcon(string id, int size = 18) =>
        Entry(id) is { Icon: not "" } e ? $"[url=kw:{e.Id}]{Img(e.Icon, size)}[/url] " : "";

    /// <summary>A keyword's link: its icon (if it has one) and the word in the keyword colour.</summary>
    public static string KeywordLink(GlossaryEntry e, string word, bool icon = true) =>
        $"[url=kw:{e.Id}]{(icon && e.Icon != "" ? Img(e.Icon, 15) : "")}{C(KeywordColor, word)}[/url]";

    /// <summary>
    /// Link the glossary's keywords in a piece of rich text: the first mention of each in a line, outside tags,
    /// links and images. <paramref name="skip"/> leaves one entry plain (its own card).
    /// </summary>
    public static string Keyworded(string bbcode, string? skip = null)
    {
        LoadGlossary();
        var sb = new StringBuilder(bbcode.Length + 64);
        var seen = new HashSet<string>();
        int depth = 0; // inside [url] or [img]: left alone
        int i = 0;
        while (i < bbcode.Length)
        {
            if (bbcode[i] == '[')
            {
                int close = bbcode.IndexOf(']', i);
                if (close < 0) { sb.Append(bbcode, i, bbcode.Length - i); break; }
                string tag = bbcode.Substring(i, close - i + 1);
                // [nk]...[/nk] (Ui.Plainly) keeps its words unlinked and is dropped here.
                if (tag is "[nk]" or "[/nk]")
                {
                    depth = Math.Max(0, depth + (tag == "[nk]" ? 1 : -1));
                    i = close + 1;
                    continue;
                }
                if (tag.StartsWith("[url", StringComparison.Ordinal) || tag.StartsWith("[img", StringComparison.Ordinal)) depth++;
                else if (tag is "[/url]" or "[/img]") depth = Math.Max(0, depth - 1);
                sb.Append(tag);
                i = close + 1;
                continue;
            }
            int next = bbcode.IndexOf('[', i);
            if (next < 0) next = bbcode.Length;
            string run = bbcode.Substring(i, next - i);
            sb.Append(depth > 0 ? run : LinkRun(run, seen, skip));
            i = next;
        }
        return sb.ToString();
    }

    private static bool InName(string run, int at)
    {
        if (!char.IsUpper(run[at]) || at < 2 || run[at - 1] != ' ') return false;
        int end = at - 1;
        int start = end;
        while (start > 0 && !char.IsWhiteSpace(run[start - 1])) start--;
        return start < end && char.IsUpper(run[start]) && char.IsLetter(run[end - 1]);
    }

    private static string LinkRun(string run, HashSet<string> seen, string? skip)
    {
        var sb = new StringBuilder(run.Length + 32);
        int at = 0;
        foreach (System.Text.RegularExpressions.Match m in _keywords!.Matches(run))
        {
            // A new line is a new entry in a list: its keywords link again.
            if (run.IndexOf('\n', at, m.Index - at) >= 0) seen.Clear();
            var e = _byWord![m.Value.ToLowerInvariant()];
            sb.Append(run, at, m.Index - at);
            at = m.Index + m.Length;
            if (e.Id == skip || !seen.Add(e.Id))
            {
                sb.Append(m.Value);
                continue;
            }
            // "120 gold" already wears the coin, and a capitalised word after another is part of a name
            // ("the Drowned Temple", "the Clock Tower"), not the keyword.
            bool counted = m.Index >= 2 && run[m.Index - 1] == ' ' && char.IsDigit(run[m.Index - 2]);
            if (counted || InName(run, m.Index))
            {
                sb.Append(m.Value);
                seen.Remove(e.Id);
                continue;
            }
            sb.Append(KeywordLink(e, m.Value));
        }
        if (run.IndexOf('\n', at) >= 0) seen.Clear();
        sb.Append(run, at, run.Length - at);
        return sb.ToString();
    }

    public static string Esc(string s) => s.Replace("[", "[lb]");

    public static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public static string C(Color c, string text) => $"[color={Hex(c)}]{text}[/color]";

    public static string Days(int n) => $"{n} {(n == 1 ? "day" : "days")}";

    /// <summary>Which glossary entry a meter's label stands for.</summary>
    public static string? MeterKeyword(string label) => label switch
    {
        "Unrest" => "unrest",
        "Warband" => "warband",
        "Your defence" => "defence",
        "Mandate" => "mandate",
        "Favour" => "favour",
        "Suspicion" => "suspicion",
        _ => null,
    };

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

    // A tag first, so sums inside one (a [hint=...] tooltip) are left alone.
    private static readonly System.Text.RegularExpressions.Regex Coins = new(@"\[[^\]]*\]|(?<![\w#=])(\d[\d,]*(?:\.\d+)?)(g\b| gold\b)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Put the gold icon before every sum of money in a piece of rich text: "120g" and "120 gold".</summary>
    public static string Coined(string bbcode) => Coins.Replace(bbcode, m => !m.Groups[1].Success ? m.Value : $"{Img("gold", 14)}{m.Groups[1].Value}{(m.Groups[2].Value == "g" ? "" : " gold")}");

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

/// <summary>The hover and pinned cards that explain keywords (Main implements it in Keywords.cs).</summary>
public interface IKeywordCards
{
    /// <summary>A keyword link in rich text is under the mouse.</summary>
    void Hover(string id, Control from);

    /// <summary>A control that stands for a keyword is under the mouse; <paramref name="extra"/> adds a line of its own.</summary>
    void Show(string id, string? extra);
    void Unhover();
    void Pin(string id);
    void Pin(string id, string? extra);
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

    /// <summary>A section's heading: its icon (<paramref name="icon"/>, or the kit's for that heading) and the words in gold.</summary>
    public void Heading(string text, string? icon = null) =>
        Add("h" + text + icon, () =>
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 6);
            if ((icon ?? Ui.HeadingIcon(text)) is string i) h.AddChild(Ui.IconRect(i, 17));
            var l = Ui.Label(text.ToUpperInvariant(), Ui.KeywordColor, Ui.Bold, 12);
            l.AddThemeConstantOverride("line_spacing", 0);
            l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            h.AddChild(l);
            var m = new MarginContainer();
            m.AddThemeConstantOverride("margin_top", 12);
            m.AddChild(h);
            m.SetMeta("heading", text);
            return m;
        });

    public void Text(string bbcode, int size = 14) => Add("t" + size + bbcode, () => Ui.Rich(bbcode, size));

    /// <summary>A dim note: its first sentence, the rest behind an info mark (<see cref="Ui.Brief"/>).</summary>
    public void Note(string text) => Text(Ui.C(Ui.Ink2, Ui.Brief(text)), 13);

    /// <summary>A list, one point a line, each with an icon or a gold dot.</summary>
    public void Bullets(IEnumerable<string> points, string? icon = null) => Text(Ui.Bullets(points, icon));

    /// <summary>A line of icon chips (<see cref="Ui.Chip"/>).</summary>
    public void Chips(params string[] chips) => Text(Ui.Chips(chips));

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
    public void Row(string bbcode, params (string Label, bool Enabled, bool Primary, Action Press)[] buttons) => Row(bbcode, null, buttons);

    /// <summary>
    /// A row with a picture before the text and a line of controls under it (a portrait and its danger bar);
    /// <paramref name="look"/> is their part of the signature, so they are rebuilt when it changes.
    /// </summary>
    public void Row(string bbcode, (string Sig, Func<Control>? Lead, Func<Control>? Under)? look, params (string Label, bool Enabled, bool Primary, Action Press)[] buttons)
    {
        var sig = new StringBuilder("r").Append(bbcode);
        if (look is { } lk) sig.Append('|').Append(lk.Sig);
        foreach (var b in buttons) sig.Append('|').Append(b.Label).Append(b.Enabled).Append(b.Primary);
        Add(sig.ToString(), () =>
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 8);
            // Three or more buttons would squeeze the text to a letter a line in a narrow window: they wrap below it.
            bool below = buttons.Length > 2;
            Container h = below ? new HFlowContainer() : new HBoxContainer();
            h.AddThemeConstantOverride(below ? "h_separation" : "separation", 8);
            Control text = Ui.Rich(bbcode);
            if (look is { } l && (l.Lead != null || l.Under != null))
            {
                var box = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                box.AddThemeConstantOverride("separation", 10);
                if (l.Lead != null) box.AddChild(l.Lead());
                var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
                col.AddThemeConstantOverride("separation", 4);
                col.AddChild(text);
                if (l.Under != null) col.AddChild(l.Under());
                box.AddChild(col);
                text = box;
            }
            if (below)
            {
                h.AddThemeConstantOverride("v_separation", 6);
                v.AddChild(text);
            }
            else h.AddChild(text);
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

    /// <summary>A labelled drop-down: picking an option calls <paramref name="pick"/> with its index.</summary>
    public void Choice(string label, IReadOnlyList<string> options, int selected, Action<int> pick) =>
        Add($"o{label}|{string.Join("|", options)}|{selected}", () =>
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 8);
            h.AddChild(Ui.Label(label, Ui.Ink2, null, 13));
            var o = new OptionButton();
            foreach (var x in options) o.AddItem(x);
            o.Selected = selected;
            o.ItemSelected += i => pick((int)i);
            h.AddChild(o);
            return h;
        });

    /// <summary>
    /// A table, database style: a header of columns that sort when clicked (the sorted one marked), and rows of
    /// rich-text cells that select when clicked (the selected one lit). Widths are each column's in pixels; the
    /// first column takes what is left.
    /// </summary>
    public void Table(IReadOnlyList<string> headers, IReadOnlyList<int> widths, IReadOnlyList<string[]> rows, int sorted, bool descending, int selected,
        Action<int> sort, Action<int> pick)
    {
        var sig = new StringBuilder("T").Append(string.Join("|", headers)).Append(sorted).Append(descending).Append(selected);
        foreach (var r in rows) sig.Append('\u001e').Append(string.Join("|", r));
        Add(sig.ToString(), () =>
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 0);
            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 4);
            for (int i = 0; i < headers.Count; i++)
            {
                int col = i;
                var b = new Button
                {
                    Text = headers[i] + (i == sorted ? (descending ? " ▼" : " ▲") : ""),
                    Flat = true,
                    Alignment = i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                    CustomMinimumSize = new Vector2(i == 0 ? 0 : widths[i], 26),
                    SizeFlagsHorizontal = i == 0 ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
                    TooltipText = $"Sort by {headers[i].ToLowerInvariant()}",
                };
                b.AddThemeFontSizeOverride("font_size", 12);
                b.AddThemeColorOverride("font_color", i == sorted ? Ui.Gold : Ui.Ink2);
                b.Pressed += () => sort(col);
                head.AddChild(b);
            }
            v.AddChild(head);
            v.AddChild(new ColorRect { Color = Ui.Line, CustomMinimumSize = new Vector2(0, 1) });
            for (int r = 0; r < rows.Count; r++)
            {
                int row = r;
                var lit = Ui.Box(Ui.Panel2, Ui.Gold, 4, 1, 6, 4);
                var plain = Ui.Flat(new Color(0, 0, 0, 0), 6, 4);
                var hover = Ui.Flat(new Color(Ui.Panel2, 0.6f), 6, 4);
                var line = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
                line.AddThemeStyleboxOverride("panel", r == selected ? lit : plain);
                if (r != selected)
                {
                    line.MouseEntered += () => line.AddThemeStyleboxOverride("panel", hover);
                    line.MouseExited += () => line.AddThemeStyleboxOverride("panel", plain);
                }
                line.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) pick(row);
                };
                var cells = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                cells.AddThemeConstantOverride("separation", 4);
                for (int i = 0; i < headers.Count && i < rows[r].Length; i++)
                {
                    var c = Ui.Rich(i == 0 ? rows[r][i] : $"[center]{rows[r][i]}[/center]", 13);
                    c.MouseFilter = Control.MouseFilterEnum.Ignore;
                    c.AutowrapMode = i == 0 ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
                    c.CustomMinimumSize = new Vector2(i == 0 ? 0 : widths[i], 0);
                    c.SizeFlagsHorizontal = i == 0 ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;
                    c.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                    cells.AddChild(c);
                }
                line.AddChild(cells);
                v.AddChild(line);
                v.AddChild(new ColorRect { Color = new Color(Ui.Line, 0.5f), CustomMinimumSize = new Vector2(0, 1) });
            }
            return v;
        });
    }

    /// <summary>A labelled meter, 0 to 100.</summary>
    public void Meter(string label, double value, string figure, bool bad = false) =>
        Add($"m{label}{Math.Round(value)}{figure}{bad}", () =>
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 8);
            if (Ui.MeterIcon(label) is string icon) h.AddChild(new TextureRect { Texture = Ui.Icon(icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(16, 16), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            var l = Ui.Label(label, Ui.Ink2, null, 13);
            l.CustomMinimumSize = new Vector2(84, 0);
            if (Ui.MeterKeyword(label) is string kw) Ui.KeywordOn(l, kw);
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
