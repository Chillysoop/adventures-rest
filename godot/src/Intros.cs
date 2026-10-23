using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// First meetings (<see cref="Intros"/>): the first time the town meets anyone of a lineage, a card shows that
/// faction's picture (<see cref="Vignette"/>), its crest and name, a line on what they are, a little of the history
/// they come from and what they want, and stops the clock ("first-contact" in Settings). Meetings on the same day
/// queue and show one after another.
/// </summary>
public partial class Main
{
    private PanelContainer _intro = null!;
    private VBoxContainer _introBox = null!;
    private Vignette _introArt = null!;

    /// <summary>How many of the town's meetings have been looked at for intros; -1 until the first look after a load.</summary>
    private int _introSeen = -1;
    private readonly Queue<(Meeting Meeting, IntroDef Intro)> _introQueue = [];
    private (IntroDef Def, Meeting? Meeting)? _introShown;
    private int _introRedo;

    /// <summary>The card's width, and its text's inside the oak frame: text is laid out at these from the start, so it
    /// is never measured at no width (a column of single letters, a card taller than the screen).</summary>
    private const float IntroWidth = 660, TextWidth = IntroWidth - 36, CrestSize = 58, NamesWidth = TextWidth - CrestSize - 12;

    private void BuildIntro()
    {
        _intro = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(IntroWidth, 0), MouseFilter = MouseFilterEnum.Stop };
        _intro.AddThemeStyleboxOverride("panel", Castle.Oak(18, 18));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        _intro.AddChild(v);
        _introArt = new Vignette(320);
        v.AddChild(_introArt);
        _introBox = new VBoxContainer();
        _introBox.AddThemeConstantOverride("separation", 6);
        v.AddChild(_introBox);
        AddModal(_intro);
    }

    /// <summary>New meetings bring their lineage's intro, the first time each lineage is met; one card at a time.</summary>
    private void SyncIntros()
    {
        var t = _sim.Town;
        // After a load (or a town started mid-campaign for a shot), what was met before is history.
        if (_introSeen < 0 || _introSeen > t.Discovered.Count) _introSeen = t.Discovered.Count;
        if (t.Discovered.Count > _introSeen)
        {
            foreach (var due in Intros.Due(t, _introSeen)) _introQueue.Enqueue(due);
            _introSeen = t.Discovered.Count;
        }
        if (_intro.Visible || _introQueue.Count == 0 || _opts.Smoke) return;
        if (_title.Visible || _menu.Visible || _settings.Visible || _story.Visible || _find.Visible) return;
        var (meeting, intro) = _introQueue.Dequeue();
        OpenIntro(intro, meeting);
        StopClock("first-contact");
    }

    private void OpenIntro(IntroDef d, Meeting? meeting)
    {
        var t = _sim.Town;
        _introArt.Show(d, Intros.Scene(t, d));
        _introShown = (d, meeting);
        _introRedo = 2;
        RenderIntro();
        _intro.Visible = true;
        _intro.MoveToFront();
        Fit(_intro);
    }

    private void CloseIntro()
    {
        _intro.Visible = false;
        _dirty = true;
    }

    /// <summary>For shots: --intro lineage shows its card as if just met.</summary>
    private void StageIntro(string lineage)
    {
        var t = _sim.Town;
        if (Intros.Def(t, lineage) is not { } d) return;
        var met = t.Discovered.FirstOrDefault(c => Intros.LineageOf(t, c.Key) == lineage) ?? new Meeting { Key = "", Day = t.Day, How = Discovery.Sighted };
        OpenIntro(d, met);
    }

    private static readonly Dictionary<string, (string Icon, string Word)> Ways = new()
    {
        [Discovery.Sighted] = ("ui-eye", "Sighted"),
        [Discovery.Reported] = ("ui-eye", "Reported"),
        [Discovery.Scouted] = ("ui-eye", "Scouted"),
        [Discovery.Scried] = ("ui-eye", "Scried"),
        [Discovery.Met] = ("standing", "Met"),
        [Discovery.Attacked] = ("threat", "Attacked us"),
        [Discovery.Heard] = ("lore", "Heard of"),
    };

    private void RenderIntro()
    {
        if (_introShown is not var (d, meeting)) return;
        var t = _sim.Town;
        Ui.Clear(_introBox);
        var lineage = Annals.Lineage(t.Content.History, d.Lineage);
        var menace = meeting != null && meeting.Key.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(meeting.Key.AsSpan(7), out int mid)
            ? t.Saga.Menaces.FirstOrDefault(m => m.Id == mid)
            : t.Saga.Menaces.FirstOrDefault(m => m.Lineage == d.Lineage);
        Color field = new(d.Field), charge = new(d.Charge);

        // The crest beside the name, the way they were met under it.
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        head.AddChild(new Crest(d, CrestSize));
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(NamesWidth, 0) };
        names.AddThemeConstantOverride("separation", 0);
        string how = meeting != null && Ways.TryGetValue(meeting.How, out var w) ? $"{Ui.Img(w.Icon, 13)} {w.Word} · day {meeting.Day}" : "";
        names.AddChild(Sized(Ui.Rich(Ui.C(charge.Lerp(Ui.Ink, 0.4f), $"[font_size=11][b]FIRST MEETING[/b][/font_size]") + (how != "" ? Ui.Dim($"[font_size=11]   {how}[/font_size]") : ""), 11), NamesWidth));
        var title = Ui.Label(Ui.Cap(lineage?.Title ?? d.Lineage), Ui.Ink, Ui.Display, 26);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        names.AddChild(Sized(title, NamesWidth));
        if (d.Line != "") names.AddChild(Sized(Ui.Rich(Ui.C(Ui.Ink2, $"[i]{Ui.Esc(d.Line)}[/i]"), 14), NamesWidth));
        head.AddChild(names);
        _introBox.AddChild(head);

        // A little lore: the power they come from (its tale on hover), then what they want now.
        var points = new List<string>();
        if (Intros.Ancestor(t, d.Lineage) is { } power)
        {
            string years = $"{Annals.Year(t.Content.History, power.From)}–{Annals.Year(t.Content.History, power.To)}";
            string first = FirstSentence(power.Text);
            points.Add(Ui.Bullet(Ui.Tip($"[b]{Ui.Esc(power.Name)}[/b] {Ui.Dim($"[font_size=12]{years}[/font_size]")}", power.Text + (power.Heirs != "" ? "\n\nLeft today: " + power.Heirs : "")) + $"\n    {Ui.Dim(Ui.Esc(first))}", "lore"));
        }
        string motive = Motive(t, lineage, menace);
        if (motive != "") points.Add(Ui.Bullet(Ui.Esc(motive), "ui-goal"));
        if (menace != null && menace.Leader != "") points.Add(Ui.Bullet($"{Ui.Esc(menace.Leader)}" + (menace.SeatName != "" ? Ui.Dim($" · {Ui.Esc(menace.SeatName)}") : ""), "ui-person"));
        if (points.Count > 0) _introBox.AddChild(Sized(Ui.Rich(string.Join("\n", points), 14), TextWidth));

        // Their ways, as chips: how to win them is on each one's hover.
        if (meeting is { Key: not "" } && MannerChips(t, meeting.Key) is { Length: > 0 } ways) _introBox.AddChild(Ui.Rich(ways, 14));

        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        foot.AddThemeConstantOverride("separation", 8);
        string? window = meeting?.Key switch
        {
            null or "" => null,
            var k when k.StartsWith("power:", StringComparison.Ordinal) || k.StartsWith("people:", StringComparison.Ordinal) => "diplomacy",
            _ => "war",
        };
        if (window != null)
        {
            var open = new Button { Text = window == "war" ? "War" : "Diplomacy", Icon = Ui.Icon(window == "war" ? "tab-war" : "tab-diplomacy"), FocusMode = FocusModeEnum.None, ExpandIcon = true };
            open.AddThemeConstantOverride("icon_max_width", 18);
            open.Pressed += () =>
            {
                CloseIntro();
                OpenWindow(window == "war" ? Tab.War : Tab.Diplomacy);
            };
            foot.AddChild(open);
        }
        var onward = new Button { Text = "Onward", FocusMode = FocusModeEnum.None };
        onward.AddThemeStyleboxOverride("normal", Castle.Button("normal", true));
        onward.AddThemeStyleboxOverride("hover", Castle.Button("hover", true));
        onward.Pressed += CloseIntro;
        foot.AddChild(onward);
        _introBox.AddChild(foot);
    }

    private static T Sized<T>(T c, float width) where T : Control
    {
        c.CustomMinimumSize = new Vector2(width, c.CustomMinimumSize.Y);
        return c;
    }

    private static string FirstSentence(string text)
    {
        int i = text.IndexOf(". ", StringComparison.Ordinal);
        return i < 0 ? text : text[..(i + 1)];
    }

    /// <summary>What the lineage wants, told with its leader's names when a menace of it is known, else of them all.</summary>
    private static string Motive(Town t, HistoryLineage? l, Menace? m)
    {
        if (l == null || l.Motive == "") return "";
        if (m != null) return Annals.Motive(t, m);
        return Ui.Cap(l.Motive.Replace("{leader}", "Their chief").Replace("{seat}", "their halls").Replace("{town}", t.Name).Replace("{region}", "the wilds"));
    }
}

/// <summary>A faction's crest: its glyph in its charge colour on a shield of its field, rimmed in iron and gold.</summary>
public partial class Crest : Control
{
    private readonly IntroDef _d;

    public Crest(IntroDef d, float size)
    {
        _d = d;
        CustomMinimumSize = new Vector2(size, size * 1.12f);
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        Vector2[] Shield(float inset)
        {
            var pts = new List<Vector2> { new(inset, inset), new(w - inset, inset), new(w - inset, h * 0.52f) };
            for (int i = 1; i < 10; i++)
            {
                float a = i / 10f * Mathf.Pi;
                pts.Add(new Vector2(w / 2 + Mathf.Cos(a) * (w / 2 - inset), h * 0.52f + Mathf.Sin(a) * (h * 0.48f - inset)));
            }
            pts.Add(new Vector2(inset, h * 0.52f));
            return pts.ToArray();
        }
        Color field = new(_d.Field), charge = new(_d.Charge);
        DrawColoredPolygon(Shield(0), new Color("1d1712"));
        DrawColoredPolygon(Shield(2.5f), new Color("c9a24a"));
        DrawColoredPolygon(Shield(4.5f), field);
        DrawColoredPolygon(Shield(4.5f).Take(3).Append(new Vector2(4.5f, h * 0.52f)).ToArray(), new Color(field.Lightened(0.12f), 0.5f));
        float g = w * 0.66f;
        var icon = Ui.Icon("crest-" + _d.Lineage);
        DrawTextureRect(icon, new Rect2(new Vector2((w - g) / 2 + 1.5f, h * 0.43f - g / 2 + 1.5f), new Vector2(g, g)), false, new Color(0, 0, 0, 0.5f));
        DrawTextureRect(icon, new Rect2(new Vector2((w - g) / 2, h * 0.43f - g / 2), new Vector2(g, g)), false, charge);
    }
}
