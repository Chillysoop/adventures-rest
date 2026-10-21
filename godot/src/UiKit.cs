using System.Text;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The UI-text kit (docs/ui-text.md): an icon before the word, a number in its colour, a line per point, and the
/// explanation in a hover. Rich-text helpers return BBCode for <see cref="Rich"/>; the control helpers build nodes
/// with tooltips. Icons are assets/icons/*.svg (scripts/icons.ts): goods, kinds, gods (god-id) and ui-* glyphs.
/// </summary>
public static partial class Ui
{
    // ---- Colour by meaning: the same hue wherever the same thing shows ----

    public static readonly Color FaithHue = new("c9a0e8");
    public static readonly Color WaterHue = new("7fb0d4");
    public static readonly Color LoreHue = new("d8c08a");
    public static readonly Color WarHue = new("ef7f6f");

    /// <summary>The rest of a line, dimmed: what a figure is, or why.</summary>
    public static string Dim(string s) => C(Ink2, s);

    /// <summary>Text with a hover: a word or an icon whose meaning shows on the mouse.</summary>
    public static string Tip(string bbcode, string tip) => tip == "" ? bbcode : $"[hint={tip.Replace("]", ")").Replace("[", "(")}]{bbcode}[/hint]";

    /// <summary>
    /// An icon and its figure, the figure bold in its colour, with what it means on hover: "[coin] 120".
    /// The building block of a skimmable line; several go side by side with <see cref="Chips"/>.
    /// </summary>
    public static string Chip(string icon, string value, Color? color = null, string tip = "", int size = 16) =>
        Tip($"{Img(icon, size)} {(color is Color c ? C(c, $"[b]{value}[/b]") : $"[b]{value}[/b]")}", tip);

    /// <summary>Chips on one line, spaced apart.</summary>
    public static string Chips(params string[] chips) => string.Join("    ", chips.Where(c => c != ""));

    /// <summary>A signed figure in green or red: "+12", "−5" (a real minus sign).</summary>
    public static string Signed(double n, string format = "N0") =>
        C(n >= 0 ? Good : Bad, $"[b]{(n >= 0 ? "+" : "−")}{Math.Abs(n).ToString(format)}[/b]");

    /// <summary>A heading inside rich text: an icon, the words bold and a little larger, in a colour.</summary>
    public static string Head(string text, string? icon = null, Color? color = null, int size = 15) =>
        $"[font_size={size}]{(icon != null ? Img(icon, size + 3) + " " : "")}{C(color ?? Ink, $"[b]{text}[/b]")}[/font_size]";

    /// <summary>One point of a list: an icon (or a coloured dot) before it.</summary>
    public static string Bullet(string bbcode, string? icon = null, Color? color = null) =>
        $"{(icon != null ? Img(icon, 15) : C(color ?? Gold, "•"))} {bbcode}";

    /// <summary>A list, one point a line. Empty points are left out.</summary>
    public static string Bullets(IEnumerable<string> points, string? icon = null, Color? color = null) =>
        string.Join("\n", points.Where(p => p != "").Select(p => Bullet(p, icon, color)));

    /// <summary>
    /// Long prose cut to its first sentence, the rest in a hover behind an info mark. Use it for explanations that
    /// a player reads once; what they need every time goes in chips and bullets instead.
    /// </summary>
    public static string Brief(string bbcode)
    {
        int cut = FirstSentence(bbcode);
        if (cut < 0) return bbcode;
        string head = bbcode[..cut].TrimEnd(), rest = bbcode[cut..].Trim();
        return $"{head} {Tip(Img("ui-info", 13), Plain(rest))}";
    }

    /// <summary>Where a piece of rich text's first sentence ends, outside tags, or -1 when there is only one.</summary>
    private static int FirstSentence(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length - 2; i++)
        {
            char ch = s[i];
            if (ch == '[') depth++;
            else if (ch == ']') depth = Math.Max(0, depth - 1);
            else if (depth == 0 && ch is '.' or '!' or '?' or ':' && s[i + 1] == ' ' && char.IsUpper(s[i + 2]) && i > 12)
                return i + 1;
        }
        return -1;
    }

    /// <summary>Rich text with its tags taken out, for a tooltip.</summary>
    public static string Plain(string bbcode) => System.Text.RegularExpressions.Regex.Replace(bbcode, @"\[[^\]]*\]", "").Replace("[lb]", "[");

    /// <summary>A god's icon (god-id), or the favour mark for one without.</summary>
    public static string GodIcon(string id) => ResourceLoader.Exists($"res://assets/icons/god-{id}.svg") ? $"god-{id}" : "favour";

    // ---- Controls ----

    /// <summary>An icon as a control, with its meaning on hover.</summary>
    public static TextureRect IconRect(string icon, int size = 18, string tip = "") => new()
    {
        Texture = Icon(icon),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(size, size),
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        TooltipText = tip,
        MouseFilter = tip == "" ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop,
    };

    /// <summary>A chip as a control: icon and figure, the meaning on hover.</summary>
    public static HBoxContainer IconChip(string icon, string value, Color? color = null, string tip = "", int size = 18)
    {
        var h = new HBoxContainer { TooltipText = tip, MouseFilter = tip == "" ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop };
        h.AddThemeConstantOverride("separation", 4);
        h.AddChild(IconRect(icon, size));
        var l = Label(value, color ?? Ink, Bold, 14);
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        h.AddChild(l);
        return h;
    }

    /// <summary>Chips in a row that wraps when the window is narrow.</summary>
    public static HFlowContainer IconRow(params (string Icon, string Value, Color? Color, string Tip)[] chips)
    {
        var row = new HFlowContainer();
        row.AddThemeConstantOverride("h_separation", 14);
        row.AddThemeConstantOverride("v_separation", 4);
        foreach (var c in chips) row.AddChild(IconChip(c.Icon, c.Value, c.Color, c.Tip));
        return row;
    }

    /// <summary>The icon each pane heading wears (<see cref="Pane.Heading"/>); a new heading adds its own here.</summary>
    private static readonly Dictionary<string, string> HeadingIcons = new()
    {
        ["Abilities"] = "d20",
        ["Alchemy bench"] = "potions",
        ["Background"] = "lore",
        ["Bastions"] = "ui-house",
        ["Before the council"] = "decree",
        ["Bounty board"] = "lore",
        ["Callings"] = "heroes",
        ["Champions of the lists"] = "ui-renown",
        ["Character sheet"] = "d20",
        ["Clockwork wardens"] = "tools",
        ["Crises"] = "ui-warn",
        ["Decrees in force"] = "decree",
        ["Deeds"] = "ui-renown",
        ["Density"] = "ui-house",
        ["Destroyed or turned"] = "ui-skull",
        ["Discoveries"] = "ui-search",
        ["Dread"] = "dread",
        ["Expeditions"] = "compass",
        ["Exploring"] = "compass",
        ["Eyes in the wilds"] = "ui-eye",
        ["Eyes on it"] = "ui-eye",
        ["Factions"] = "ui-faction",
        ["Feats"] = "ui-star",
        ["Grand works"] = "stone",
        ["Grant it"] = "ui-yes",
        ["Guilds"] = "tools",
        ["Hall of heroes"] = "ui-renown",
        ["Heroes"] = "heroes",
        ["Hold it"] = "ui-shield",
        ["Household goods"] = "furniture",
        ["Houses"] = "ui-house",
        ["In the armory"] = "tools",
        ["Inspiration"] = "ui-star",
        ["Its parts"] = "stone",
        ["Latest rolls"] = "d20",
        ["Loot deals"] = "ui-bag",
        ["Minor powers"] = "rivals",
        ["Miracles"] = "ui-miracle",
        ["Needs you"] = "ui-warn",
        ["Next issue's front page"] = "lore",
        ["Offered to the town"] = "ui-trade",
        ["On the road"] = "ui-road",
        ["Open threats"] = "threat",
        ["Past motions"] = "decree",
        ["Peoples of the wilds"] = "population",
        ["Recipe book"] = "potions",
        ["Reports for sale"] = "lore",
        ["Roster"] = "heroes",
        ["Seats"] = "mandate",
        ["Sellswords"] = "threat",
        ["Sending stones"] = "ui-eye",
        ["Services"] = "ui-heal",
        ["Skills"] = "d20",
        ["Spellbooks"] = "books",
        ["Still to come"] = "ui-time",
        ["Story so far"] = "lore",
        ["Strength"] = "defence",
        ["Strike at a foe"] = "threat",
        ["Tavern talk"] = "ale",
        ["Taxes"] = "gold",
        ["The airship"] = "compass",
        ["The gods in town"] = "ui-holy",
        ["The scholars"] = "books",
        ["The town remembers"] = "ui-renown",
        ["Threats in the land"] = "threat",
        ["Town guard"] = "defence",
        ["Trade routes"] = "ui-trade",
        ["Under the streets"] = "ui-skull",
        ["Waiting on you"] = "ui-warn",
        ["Walls"] = "defence",
        ["Wards"] = "ui-house",
        ["Why settlers come or don't"] = "population",
        ["Wizards in town"] = "books",
        ["Works"] = "tools",
        ["Yesterday's books"] = "gold",
    };

    public static string? HeadingIcon(string heading) => HeadingIcons.GetValueOrDefault(heading);
}
