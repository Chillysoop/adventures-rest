using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Explorers' reports (<see cref="Explore"/>). When a company out exploring finds something it sends word
/// and waits; the report pops up as a card and pauses the clock. The card tells the find's story so far
/// (the first sighting, each chapter investigated, each roll), and offers what can be done: investigate
/// further a chapter at a time, go in, deal with it as its kind allows, keep exploring, or come home.
/// A check the company rolls throws the die on the card, like a story's.
/// </summary>
public partial class Main
{
    private PanelContainer _find = null!;
    private VBoxContainer _findBox = null!;
    private int? _findId;

    /// <summary>Each find's report the player has been shown, by find and how many lines its story had then.</summary>
    private readonly Dictionary<int, int> _findSeen = [];
    private StoryBeat? _findBeat;

    private void BuildFind()
    {
        _find = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(620, 0), ZIndex = 20, MouseFilter = MouseFilterEnum.Stop };
        _find.AddThemeStyleboxOverride("panel", Castle.Oak(26, 22));
        _findBox = new VBoxContainer();
        _findBox.AddThemeConstantOverride("separation", 10);
        _find.AddChild(_findBox);
        AddChild(_find);
        _find.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        _find.GrowHorizontal = GrowDirection.Both;
        _find.GrowVertical = GrowDirection.Both;
    }

    private void OpenFind(int id, StoryBeat? roll = null)
    {
        if (Explore.FindOf(_sim.Town, id) is not { } f) return;
        _findId = id;
        _findSeen[id] = f.Log.Count;
        // A fresh report with a roll in it throws the die first.
        _findBeat = roll ?? (f.Beat != null && !_findShownBeat.Contains(f.Beat) ? f.Beat : null);
        if (_findBeat != null) _findShownBeat.Add(_findBeat);
        RenderFind(f);
        _find.Visible = true;
        MoveChild(_find, GetChildCount() - 1);
    }

    private readonly HashSet<StoryBeat> _findShownBeat = [];

    private void CloseFind()
    {
        _find.Visible = false;
        _findId = null;
        _findBeat = null;
        _dirty = true;
    }

    /// <summary>A report waiting on the mayor pops up on its own and stops the clock.</summary>
    private void SyncFinds()
    {
        if (_find.Visible && _findId is int open && Explore.FindOf(_sim.Town, open) is { } shown && (shown.State != FindState.Open || _sim.Town.Tick < shown.ReportTick) && _findBeat == null)
            CloseFind();
        if (_title.Visible || _menu.Visible || _story.Visible || _find.Visible) return;
        var next = Explore.Waiting(_sim.Town).FirstOrDefault(f => _findSeen.GetValueOrDefault(f.Id, -1) < f.Log.Count);
        if (next == null) return;
        _findSeen[next.Id] = next.Log.Count;
        if (_opts.Smoke) return;
        OpenFind(next.Id);
        if (_speed > 0 && !_opts.NoPause) SetSpeed(0);
    }

    private void RenderFind(Find f)
    {
        Ui.Clear(_findBox);
        var t = _sim.Town;
        var s = Explore.Survey(t, f.SurveyId)!;
        var def = Explore.Def(t, f);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new TextureRect { Texture = Ui.Icon("compass"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        head.AddChild(Ui.Label("DISCOVERY", new Color("8cc084"), Ui.Bold, 11));
        head.AddChild(Ui.Label("·  " + def.Name.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 11));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (f.State == FindState.Open)
        {
            int hours = Math.Max(1, f.DueTick - t.Tick);
            head.AddChild(Ui.Label(hours >= 24 ? $"They wait {Ui.Days((int)Math.Ceiling(hours / 24.0))} for word" : $"They wait {hours} more hours for word", Ui.Ink2, Ui.Medium, 11));
        }
        _findBox.AddChild(head);

        var title = Ui.Label(Explore.Title(t, f), Ui.Ink, Ui.Display, 24);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _findBox.AddChild(title);
        var members = string.Join(", ", s.Members.Select(t.Agent).Where(a => !a.Dead).Select(a => $"{AgentLink(a, a.GivenName)} the {Ui.Esc(a.Hero!.Cls.Name.ToLowerInvariant())}"));
        _findBox.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"[font_size=12]The {Ui.Esc(s.Name)} in {Ui.Esc(t.Regions[f.RegionId].Name)}: {members}[/font_size]"), 12));

        // Chapters done, like a dig site's progress.
        if (def.Chapters.Count > 0)
        {
            var steps = new HBoxContainer();
            steps.AddThemeConstantOverride("separation", 6);
            for (int i = 0; i < def.Chapters.Count; i++)
            {
                bool done = i < f.Chapter;
                var chip = Ui.Label($"{(done ? "✓" : (i + 1).ToString())}  {def.Chapters[i].Title}", done ? Ui.Good : i == f.Chapter ? Ui.Ink : Ui.Ink2, Ui.Medium, 11);
                var panel = new PanelContainer();
                panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color(done ? Ui.Good : Ui.Panel2, done ? 0.14f : 0.8f), done ? Ui.Good : Ui.Line, 6, 1, 8, 3));
                panel.AddChild(chip);
                steps.AddChild(panel);
            }
            _findBox.AddChild(steps);
        }

        if (_findBeat != null)
        {
            RenderFindRoll(f, _findBeat);
            return;
        }
        // The story so far: older lines quietly, the newest plainly.
        var log = f.Log.Where(l => !l.StartsWith("Mayor ", StringComparison.Ordinal)).ToList();
        foreach (var line in log.Take(log.Count - 1).TakeLast(3)) _findBox.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"[i]{Ui.Esc(line)}[/i]"), 13));
        if (log.Count > 0) _findBox.AddChild(Ui.Rich(Ui.Esc(log[^1]), 15));

        if (f.State == FindState.Open && t.Tick >= f.ReportTick)
        {
            var choices = Explore.Choices(t, f);
            for (int i = 0; i < choices.Count; i++) _findBox.AddChild(FindRow(f, choices[i], i));
        }
        else _findBox.AddChild(Ui.Rich(Ui.C(Ui.Ink2, f.State == FindState.Busy ? "They are at work on it. Word will come when they are done." : "This is behind them now."), 13));

        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var note = Ui.Rich(Ui.C(Ui.Ink2, $"[font_size=12]{(Explore.Sends(t, s) ? "Each report costs one of their casters' spell slots for a sending." : "No sendings left: their reports come by bird, a few hours late.")}[/font_size]"), 12);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        note.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        foot.AddChild(note);
        var later = new Button { Text = "Later", TooltipText = "Leave it on the alert strip; if no word comes in time, their leader decides", FocusMode = FocusModeEnum.None };
        later.Pressed += CloseFind;
        foot.AddChild(later);
        _findBox.AddChild(foot);
    }

    private Control FindRow(Find f, FindChoice c, int index)
    {
        string sub = c.Detail + (c.Chance < 1 ? $" · {Math.Round(c.Chance * 100)}%" : "");
        if (c.Blocked != null) sub = $"{Ui.Cap(c.Blocked)}.";
        var row = new Button { Disabled = c.Blocked != null, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 54), MouseDefaultCursorShape = CursorShape.PointingHand };
        var normal = Ui.Box(new Color(Ui.Panel2, 0.9f), Ui.Line, 8, 1, 14, 8);
        var hover = Ui.Box(new Color(new Color("8cc084"), 0.14f), new Color("8cc084"), 8, 1, 14, 8);
        hover.BorderWidthLeft = 4;
        row.AddThemeStyleboxOverride("normal", normal);
        row.AddThemeStyleboxOverride("hover", hover);
        row.AddThemeStyleboxOverride("pressed", hover);
        row.AddThemeStyleboxOverride("disabled", Ui.Box(new Color(Ui.Panel, 0.6f), Ui.Line, 8, 1, 14, 8));
        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 2);
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        v.OffsetLeft = 14;
        v.OffsetTop = 8;
        var label = Ui.Label($"{index + 1}.  {c.Label}", c.Blocked != null ? Ui.Ink2 : Ui.Ink, Ui.Medium, 15);
        label.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(label);
        var subl = Ui.Rich(Ui.Coined(Ui.C(Ui.Ink2, Ui.Esc(sub))), 12);
        subl.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(subl);
        row.AddChild(v);
        int id = f.Id;
        string act = c.Act;
        row.Pressed += () => OrderFind(id, act);
        return row;
    }

    /// <summary>Send the company word. A check made on the spot (a parley, trading news) throws its die on the card.</summary>
    private void OrderFind(int id, string act)
    {
        var t = _sim.Town;
        if (Explore.FindOf(t, id) is not { } f) return;
        var before = f.Beat;
        if (!_sim.OrderExplorers(id, act)) return;
        GuideEvent("explorers");
        _dirty = true;
        _findSeen[id] = f.Log.Count;
        if (f.Beat != null && f.Beat != before)
        {
            OpenFind(id, f.Beat);
            return;
        }
        CloseFind();
    }

    private void RenderFindRoll(Find f, StoryBeat b)
    {
        var t = _sim.Town;
        var after = new VBoxContainer { Visible = false };
        after.AddThemeConstantOverride("separation", 8);
        // The rolling hero's own bonus; Advantage carries who rolled on an explorers' check.
        var roll = new DiceRoll(b, [new DiceRoll.Term(b.Modifier, b.Advantage.Split(' ')[0])], $"{b.Advantage} rolls {Explore.SkillName(t, b.Skill)}", false, InstantRolls, advantage: false) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        roll.Landed += () => after.Visible = true;
        _findBox.AddChild(roll);
        after.AddChild(Ui.Rich(Ui.Esc(f.Log.LastOrDefault(l => !l.StartsWith("Mayor ", StringComparison.Ordinal)) ?? ""), 15));
        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var go = new Button { Text = f.State == FindState.Open ? "Continue" : "Close", ThemeTypeVariation = "Post", FocusMode = FocusModeEnum.None };
        int id = f.Id;
        go.Pressed += () =>
        {
            _findBeat = null;
            if (Explore.FindOf(_sim.Town, id) is { State: FindState.Open } open && _sim.Town.Tick >= open.ReportTick) RenderFind(open);
            else CloseFind();
        };
        foot.AddChild(go);
        after.AddChild(foot);
        _findBox.AddChild(after);
    }
}
