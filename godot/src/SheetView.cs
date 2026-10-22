using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>A box in a sheet's vitals row: an icon, a big figure, a short label under it, and a bar when it has one.</summary>
public sealed record SheetStat(string Icon, string Label, string Value, string Tip = "", double Bar = -1, Color? Color = null);

/// <summary>One skill line: trained ones marked ●, expert ones ◆.</summary>
public sealed record SheetSkill(string Name, int Bonus, bool Trained, bool Expert);

/// <summary>A titled block of short lines (bbcode, each usually led by an icon).</summary>
public sealed record SheetSection(string Title, string Icon, IReadOnlyList<string> Lines);

/// <summary>
/// What a character sheet shows. Heroes (<c>CharacterSheet.cs</c>) and the mayor fill one, so every sheet in the game
/// reads the same way: portrait and name, a badge line, the XP bar, the vitals boxes, the six abilities, skills on
/// the left and the rest in titled sections.
/// </summary>
public sealed record SheetSpec
{
    public string Name { get; init; } = "";

    /// <summary>"Level 3 dwarf paladin · acolyte" (bbcode).</summary>
    public string Subtitle { get; init; } = "";

    /// <summary>The line under the subtitle: a role chip, inspiration pips... (bbcode).</summary>
    public string Badge { get; init; } = "";

    /// <summary>A portrait glyph (assets/icons/face-*.svg), or empty for none.</summary>
    public string Glyph { get; init; } = "";
    public Color Tint { get; init; } = Ui.Ink2;
    public Color Ring { get; init; } = new("5a5347");
    public bool Crowned { get; init; }

    /// <summary>The way to the next level, 0 to 1, or below 0 for no bar.</summary>
    public double Xp { get; init; } = -1;
    public string XpText { get; init; } = "";

    public IReadOnlyList<SheetStat> Vitals { get; init; } = [];
    public Abilities? Abilities { get; init; }
    public IReadOnlyList<SheetSkill> Skills { get; init; } = [];

    /// <summary>One dim line under the skills: "proficiency +2, half on the rest".</summary>
    public string SkillsNote { get; init; } = "";

    /// <summary>Beside the skills.</summary>
    public IReadOnlyList<SheetSection> Right { get; init; } = [];

    /// <summary>Across the foot of the sheet.</summary>
    public IReadOnlyList<SheetSection> Wide { get; init; } = [];

    /// <summary>Everything shown, so a pane rebuilds the sheet only when it changes.</summary>
    public string Sig()
    {
        var b = new System.Text.StringBuilder();
        b.Append(Name).Append('|').Append(Subtitle).Append('|').Append(Badge).Append('|').Append(Glyph).Append(Tint).Append(Ring).Append(Crowned)
            .Append('|').Append(Math.Round(Xp, 3)).Append(XpText).Append('|').Append(Abilities is Abilities a ? string.Join(",", new[] { Ability.Str, Ability.Dex, Ability.Con, Ability.Int, Ability.Wis, Ability.Cha }.Select(k => a[k])) : "").Append('|').Append(SkillsNote);
        foreach (var v in Vitals) b.Append('|').Append(v);
        foreach (var s in Skills) b.Append('|').Append(s);
        foreach (var s in Right.Concat(Wide)) b.Append('|').Append(s.Title).Append(string.Join("¦", s.Lines));
        return b.ToString();
    }
}

/// <summary>Draws a <see cref="SheetSpec"/> as one framed sheet.</summary>
public static class SheetView
{
    private static readonly Color Frame = new("6b5a3a");

    public static Control Build(SheetSpec s)
    {
        var sheet = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        sheet.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Frame, 6, 1, 10, 10));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        sheet.AddChild(v);

        v.AddChild(Header(s));
        if (s.Vitals.Count > 0) v.AddChild(Vitals(s.Vitals));
        if (s.Abilities is Abilities ab) v.AddChild(AbilityRow(ab));

        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 12);
        if (s.Skills.Count > 0)
        {
            var left = Column(1);
            left.AddChild(Head("Skills", "skill"));
            left.AddChild(Ui.Rich(SkillTable(s.Skills), 12));
            if (s.SkillsNote != "") left.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"[font_size=11]{s.SkillsNote}[/font_size]"), 11));
            cols.AddChild(left);
        }
        if (s.Right.Count > 0)
        {
            var right = Column(1.25f);
            foreach (var sec in s.Right) AddSection(right, sec);
            cols.AddChild(right);
        }
        if (cols.GetChildCount() > 0) v.AddChild(cols);
        foreach (var sec in s.Wide) AddSection(v, sec);
        return sheet;
    }

    /// <summary>The six ability boxes: name, modifier large, score small.</summary>
    public static Control AbilityRow(Abilities a)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        foreach (var (key, name) in new[] { (Ability.Str, "STR"), (Ability.Dex, "DEX"), (Ability.Con, "CON"), (Ability.Int, "INT"), (Ability.Wis, "WIS"), (Ability.Cha, "CHA") })
        {
            int score = a[key], mod = Rng.Modifier(score);
            var cell = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            cell.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, Ui.Line, 8, 1, 4, 6));
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", -2);
            foreach (var (text, color, font, size) in new[] { (name, Ui.Ink2, Ui.Bold, 10), ($"{(mod >= 0 ? "+" : "−")}{Math.Abs(mod)}", Ui.Ink, Ui.Display, 22), (score.ToString(), Ui.Ink2, Ui.Medium, 11) })
            {
                var l = Ui.Label(text, color, font, size);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                v.AddChild(l);
            }
            cell.AddChild(v);
            row.AddChild(cell);
        }
        return row;
    }

    private static Control Header(SheetSpec s)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        if (s.Glyph != "")
            h.AddChild(new Portrait(64) { Glyph = s.Glyph, Tint = s.Tint, Ring = s.Ring, Crowned = s.Crowned });
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Rich($"[font_size=20][b]{Ui.Esc(s.Name)}[/b][/font_size]", 20));
        if (s.Subtitle != "") v.AddChild(Ui.Rich(Ui.C(Ui.Ink2, s.Subtitle), 13));
        if (s.Badge != "") v.AddChild(Ui.Rich(s.Badge, 13));
        if (s.Xp >= 0)
        {
            var xp = new HBoxContainer();
            xp.AddThemeConstantOverride("separation", 6);
            xp.AddChild(Ui.IconRect("sheet-xp", 14));
            var bar = Bar(s.Xp, Ui.Gold, 7);
            bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            xp.AddChild(bar);
            if (s.XpText != "") xp.AddChild(Ui.Label(s.XpText, Ui.Ink2, Ui.Medium, 11));
            v.AddChild(xp);
        }
        h.AddChild(v);
        return h;
    }

    private static Control Vitals(IReadOnlyList<SheetStat> stats)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        foreach (var st in stats)
        {
            var cell = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TooltipText = st.Tip, MouseFilter = st.Tip == "" ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop };
            cell.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, Ui.Line, 6, 1, 4, 4));
            var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            v.AddThemeConstantOverride("separation", 0);
            var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            top.AddThemeConstantOverride("separation", 4);
            top.AddChild(Ui.IconRect(st.Icon, 15));
            var value = Ui.Label(st.Value, st.Color ?? Ui.Ink, Ui.Display, 17);
            value.MouseFilter = Control.MouseFilterEnum.Ignore;
            top.AddChild(value);
            v.AddChild(top);
            if (st.Bar >= 0) v.AddChild(Bar(st.Bar, st.Bar < 0.34 ? Ui.Bad : st.Bar < 0.67 ? Ui.Gold : Ui.Good, 4));
            var label = Ui.Label(st.Label, Ui.Ink2, Ui.Bold, 10);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(label);
            cell.AddChild(v);
            row.AddChild(cell);
        }
        return row;
    }

    private static string SkillTable(IReadOnlyList<SheetSkill> skills)
    {
        var b = new System.Text.StringBuilder("[table=2]");
        foreach (var s in skills)
        {
            var color = s.Trained ? Ui.Ink : Ui.Ink2;
            string mark = s.Expert ? Ui.C(Ui.Gold, "◆") : s.Trained ? Ui.C(Ui.Gold, "●") : "○";
            string bonus = s.Trained ? $"[b]{Sign(s.Bonus)}[/b]" : Sign(s.Bonus);
            b.Append("[cell]").Append(Ui.C(color, $"[font_size=12]{mark} {Ui.Esc(s.Name)}[/font_size]")).Append("[/cell]")
                .Append("[cell padding=8,0,0,0]").Append(Ui.C(color, $"[font_size=12]{bonus}[/font_size]")).Append("[/cell]");
        }
        return b.Append("[/table]").ToString();
    }

    private static string Sign(int n) => n >= 0 ? $"+{n}" : $"−{-n}";

    private static VBoxContainer Column(float ratio)
    {
        var c = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = ratio };
        c.AddThemeConstantOverride("separation", 4);
        return c;
    }

    private static void AddSection(VBoxContainer into, SheetSection sec)
    {
        if (sec.Lines.Count == 0) return;
        into.AddChild(Head(sec.Title, sec.Icon));
        into.AddChild(Ui.Rich(string.Join("\n", sec.Lines.Select(l => $"[font_size=12]{l}[/font_size]")), 12));
    }

    /// <summary>A section's head: its icon and an upper-case title in gold over a hairline.</summary>
    private static Control Head(string title, string icon)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 5);
        h.AddChild(Ui.IconRect(icon, 14));
        h.AddChild(Ui.Label(title.ToUpperInvariant(), Ui.Gold, Ui.Bold, 11));
        v.AddChild(h);
        v.AddChild(new ColorRect { Color = Frame, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
        return v;
    }

    private static ProgressBar Bar(double share, Color fill, int height)
    {
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0, Value = Math.Clamp(share, 0, 1), ShowPercentage = false, CustomMinimumSize = new Vector2(0, height), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", Ui.Box(new Color("120f0b"), Ui.Line, 3));
        bar.AddThemeStyleboxOverride("fill", Ui.Box(fill, fill.Darkened(0.3f), 3));
        return bar;
    }
}
