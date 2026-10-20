using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Tales at the tavern: when a company comes home it tells the table what happened, a beat at a time, on an
/// inked map of the place with their figures moving room to room and the rolls that mattered tumbling on the
/// d20 (<see cref="DiceRoll"/>). The beats are the chronicle's own (<see cref="TaleBeat"/>). A tale never
/// stops the clock and can be skipped; any chronicle can be heard again from the Chronicles.
/// Also here: the "Last time in..." recap card (<see cref="Recap"/>) and the cards for heroes' level-ups and
/// magic items (<see cref="Moments"/>), which go into the news feed.
/// </summary>
public partial class Main
{
    private PanelContainer _tale = null!;
    private VBoxContainer _taleBox = null!;
    private TaleMap _taleMap = null!;
    private RichTextLabel _taleLine = null!;
    private Control _taleDice = null!;
    private Label _taleCount = null!;
    private Button _taleNext = null!;
    private Chronicle? _taleOf;
    private List<TaleBeat> _taleSteps = [];
    private int _taleAt;
    private double _taleWait;
    private DiceRoll? _taleRoll;
    private int _talesSeen = -1;
    private int _lastTaleDay = -99;

    /// <summary>Seconds a beat stays before the next, after any die lands.</summary>
    private const double TaleBeatSeconds = 2.8;

    /// <summary>A tale tells itself when a company comes home at most once in this many days; the rest wait in the Chronicles.</summary>
    private const int TaleEveryDays = 2;

    private static readonly HashSet<string> RoomKinds = ["fight", "boss", "trap", "hazard", "puzzle", "parley", "treasure"];

    private void BuildTale()
    {
        _tale = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(720, 0), ZIndex = 19, MouseFilter = MouseFilterEnum.Stop };
        _tale.AddThemeStyleboxOverride("panel", Castle.Oak(24, 20));
        _taleBox = new VBoxContainer();
        _taleBox.AddThemeConstantOverride("separation", 8);
        _tale.AddChild(_taleBox);
        AddChild(_tale);
        _tale.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize);
        _tale.GrowHorizontal = GrowDirection.Both;
        _tale.OffsetTop = 120;
        _tale.MinimumSizeChanged += () => Ui.Refit(_tale);
    }

    /// <summary>The beats worth telling, at most a dozen or so: the big moments first, then what the heroes said about them.</summary>
    private static List<TaleBeat> TaleSteps(Chronicle c)
    {
        static int Score(TaleBeat b) => b.Kind switch
        {
            "setout" or "boss" or "kill" or "death" or "lost" or "cleared" or "goal" or "retreat" => 5,
            "crit" or "fumble" or "down" or "saved" => 4,
            "say" or "parley" or "puzzle" => 3,
            "trap" or "treasure" or "fight" or "heal" => 2,
            "hazard" or "spell" => 1,
            _ => 0,
        };
        var beats = c.Beats.Where(b => Score(b) > 0 && b.Line < c.Lines.Count).ToList();
        const int max = 14;
        if (beats.Count > max)
        {
            var keep = beats.Select((b, i) => (b, i)).OrderByDescending(x => Score(x.b)).ThenBy(x => x.i).Take(max).Select(x => x.i).ToHashSet();
            beats = beats.Where((b, i) => keep.Contains(i)).ToList();
        }
        return beats;
    }

    private void OpenTale(int chronicleId)
    {
        if (_sim.Town.Chronicles.FirstOrDefault(c => c.Id == chronicleId) is not { Beats.Count: > 0 } c) return;
        _taleOf = c;
        _taleSteps = TaleSteps(c);
        _taleAt = 0;
        RenderTale();
        _tale.Visible = true;
        MoveChild(_tale, GetChildCount() - 1);
        ShowTaleStep();
    }

    private void CloseTale()
    {
        _tale.Visible = false;
        _taleOf = null;
        _taleRoll = null;
    }

    /// <summary>A company home with a story worth telling tells it, when the player isn't in the middle of something else.</summary>
    private void SyncTales(double delta)
    {
        var t = _sim.Town;
        if (_talesSeen < 0 || _talesSeen > t.Chronicles.Count) _talesSeen = t.Chronicles.Count;
        if (_tale.Visible) AdvanceTale(delta);
        if (t.Chronicles.Count == _talesSeen) return;
        var fresh = t.Chronicles.Skip(_talesSeen).ToList();
        _talesSeen = t.Chronicles.Count;
        if (_opts.Smoke || _opts.Screenshot != null || _sim.Options.AutoStories && !_opts.Tales) return;
        if (_tale.Visible || _title.Visible || _menu.Visible || _story.Visible || _find.Visible || _recap.Visible) return;
        if (t.Day - _lastTaleDay < TaleEveryDays) return;
        // The one with the most to tell.
        var best = fresh.Where(c => c.MemberIds.Count > 0 && c.Beats.Any(b => b.Kind is "boss" or "kill" or "death" or "lost" or "cleared" or "goal" or "down"))
            .OrderByDescending(c => c.Beats.Count(b => b.Kind is "death" or "kill" or "boss" or "crit")).ThenBy(c => c.Id).FirstOrDefault();
        if (best == null) return;
        _lastTaleDay = t.Day;
        OpenTale(best.Id);
    }

    private void RenderTale()
    {
        var c = _taleOf!;
        var t = _sim.Town;
        Ui.Clear(_taleBox);
        var tavern = t.Buildings.Where(b => b.Complete && b.Def.Id == "tavern").OrderBy(b => b.Id).FirstOrDefault();
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new TextureRect { Texture = Ui.Icon("ale"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(26, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        head.AddChild(Ui.Label($"TOLD AT {(tavern?.Name ?? "the tavern").ToUpperInvariant()}", Ui.Gold, Ui.Bold, 11));
        var verdictColor = c.Outcome == ExpeditionOutcome.Cleared || c.GoalMet ? Ui.Good : c.Outcome == ExpeditionOutcome.Wiped ? Ui.Bad : Ui.Ink2;
        head.AddChild(Ui.Label("·  " + c.Verdict.ToUpperInvariant(), verdictColor, Ui.Bold, 11));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _taleCount = Ui.Label("", Ui.Ink2, Ui.Medium, 11);
        head.AddChild(_taleCount);
        _taleBox.AddChild(head);

        var title = Ui.Label($"The {c.PartyName} at {Ui.Cap(c.SiteName)}", Ui.Ink, Ui.Display, 22);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _taleBox.AddChild(title);
        var members = c.MemberIds.Select(t.Agent).ToList();
        string names = string.Join(", ", members.Select(a => $"{(c.Dead.Contains(a.Id) ? "✝ " : "")}{AgentLink(a, a.GivenName)}{(a.Hero is { Nickname: not "" } h ? Ui.C(Ui.Ink2, $" “{Ui.Esc(h.Nickname)}”") : "")} the {Ui.Esc(a.Hero!.Cls.Name.ToLowerInvariant())}"));
        _taleBox.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"[font_size=12]Day {c.DepartDay} to {c.ReturnDay} · [/font_size]") + $"[font_size=12]{names}[/font_size]", 12));

        _taleMap = new TaleMap(c, members, Rooms(c)) { CustomMinimumSize = new Vector2(672, 230) };
        _taleBox.AddChild(_taleMap);

        _taleLine = Ui.Rich("", 16);
        _taleLine.CustomMinimumSize = new Vector2(672, 52);
        _taleBox.AddChild(_taleLine);
        _taleDice = new CenterContainer { CustomMinimumSize = new Vector2(0, 54) };
        _taleBox.AddChild(_taleDice);

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 8);
        var skip = new Button { Text = "Skip", FocusMode = FocusModeEnum.None, TooltipText = "Let them finish without you (Esc)" };
        skip.Pressed += CloseTale;
        foot.AddChild(skip);
        var read = new Button { Text = "Read the chronicle", FocusMode = FocusModeEnum.None, TooltipText = "Every line of it, in the Chronicles" };
        int id = c.Id;
        read.Pressed += () =>
        {
            CloseTale();
            _openChronicles.Add(id);
            OpenWindow(Tab.Chronicles);
        };
        foot.AddChild(read);
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _taleNext = new Button { Text = "Next", ThemeTypeVariation = "Post", FocusMode = FocusModeEnum.None };
        _taleNext.Pressed += NextTaleStep;
        foot.AddChild(_taleNext);
        _taleBox.AddChild(foot);
    }

    /// <summary>The room each beat happens in: -1 on the road, then one room per encounter inside, the boss's last.</summary>
    private static List<int> Rooms(Chronicle c)
    {
        var rooms = new List<int>();
        int room = -1;
        foreach (var b in c.Beats)
        {
            if (RoomKinds.Contains(b.Kind)) room++;
            else if (b.Kind == "retreat" || b.Kind == "lost") room = Math.Max(room, 0);
            rooms.Add(room);
        }
        return rooms;
    }

    private void NextTaleStep()
    {
        if (_taleOf == null) return;
        if (_taleAt >= _taleSteps.Count)
        {
            CloseTale();
            return;
        }
        _taleAt++;
        ShowTaleStep();
    }

    /// <summary>Jump to a step (for screenshots): the given one, or the most telling (the master's room, a death, a killing blow).</summary>
    private void SkipTaleTo(int? step)
    {
        if (_taleOf == null) return;
        int best = _taleSteps.FindLastIndex(b => b.Kind is "kill" or "death");
        if (best < 0) best = _taleSteps.FindIndex(b => b.Kind == "boss");
        _taleAt = Math.Clamp(step ?? Math.Max(0, best), 0, _taleSteps.Count);
        ShowTaleStep();
        _taleMap.Snap();
        _taleWait = 1e9;
    }

    private void AdvanceTale(double delta)
    {
        if (_taleRoll is { Done: false }) return;
        _taleWait -= delta;
        if (_taleWait <= 0) NextTaleStep();
    }

    private void ShowTaleStep()
    {
        var c = _taleOf!;
        var t = _sim.Town;
        Ui.Clear(_taleDice);
        _taleRoll = null;
        _taleWait = TaleBeatSeconds;
        _taleCount.Text = $"{Math.Min(_taleAt + 1, _taleSteps.Count + 1)} of {_taleSteps.Count + 1}";
        if (_taleAt >= _taleSteps.Count)
        {
            // The end: what they came home with, and who did not.
            _taleMap.Show(-1, c.Outcome == ExpeditionOutcome.Wiped);
            var dead = c.Dead.Select(t.Agent).Select(a => a.GivenName).ToList();
            string end = c.Outcome == ExpeditionOutcome.Wiped
                ? "Nobody came home to tell this one. What you heard, the stones and the crows told."
                : $"Home on day {c.ReturnDay}{(c.Gold > 0 ? $" with {Js.Str(c.Gold)} gold to split" : "")}." + (dead.Count > 0 ? $" {Expedition.ListNames(dead)} did not come home with them." : "");
            Ui.SetRich(_taleLine, $"[i]{Ui.Esc(end)}[/i]");
            _taleNext.Text = "Close";
            _taleWait = TaleBeatSeconds * 2;
            return;
        }
        _taleNext.Text = "Next";
        var b = _taleSteps[_taleAt];
        int index = c.Beats.ToList().IndexOf(b);
        _taleMap.Show(index, false);
        string line = c.Lines[b.Line];
        string text = Ui.Esc(line);
        if (b.HeroId > 0 && b.HeroId <= t.Agents.Count)
        {
            var a = t.Agent(b.HeroId);
            int i = text.IndexOf(a.GivenName, StringComparison.Ordinal);
            if (i >= 0) text = $"{text[..i]}{AgentLink(a, a.GivenName)}{text[(i + a.GivenName.Length)..]}";
        }
        var color = b.Kind switch
        {
            "death" or "lost" or "down" => Ui.Bad,
            "kill" or "cleared" or "goal" or "crit" => Ui.Good,
            "boss" => Ui.Gold,
            _ => Ui.Ink,
        };
        Ui.SetRich(_taleLine, b.Kind == "say" ? Ui.C(Ui.Ink2, $"[i]{text}[/i]") : Ui.C(color, text));
        if (b.Roll > 0 && b.HeroId > 0)
        {
            var who = t.Agent(b.HeroId);
            int dc = b.Dc > 0 ? b.Dc : 10;
            int total = b.Total > 0 ? b.Total : b.Roll;
            var result = b.Roll == 20 ? StoryResult.Critical : b.Roll == 1 ? StoryResult.Fumble : total >= dc ? StoryResult.Success : StoryResult.Failure;
            var beat = new StoryBeat { Tick = c.Id * 997 + b.Line, Die = b.Roll, Total = total, Dc = dc, Result = result };
            string what = b.Kind switch
            {
                "death" or "saved" => "a death save",
                "kill" or "crit" => "to hit",
                "trap" => "to spot the trap",
                "parley" => "to talk",
                "puzzle" => "to work it out",
                _ => "",
            };
            string bonus = b.Kind switch { "death" or "saved" => "", "kill" or "crit" => "attack", _ => "skill" };
            _taleRoll = new DiceRoll(beat, total != b.Roll ? [new DiceRoll.Term(total - b.Roll, bonus)] : [], $"{who.GivenName} rolls {what}", true, InstantRolls, false);
            _taleDice.AddChild(_taleRoll);
        }
    }

    // ---------- "Last time in..." ----------

    private PanelContainer _recap = null!;
    private string _seasonSeen = "";

    private void BuildRecap()
    {
        _recap = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(600, 0), ZIndex = 21, MouseFilter = MouseFilterEnum.Stop };
        _recap.AddThemeStyleboxOverride("panel", Castle.Oak(26, 22));
        AddChild(_recap);
        _recap.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        _recap.GrowHorizontal = GrowDirection.Both;
        _recap.GrowVertical = GrowDirection.Both;
        _recap.MinimumSizeChanged += () => Ui.Refit(_recap);
    }

    /// <summary>The recap a game master opens a session with: the big moments since a day, who is hurt or away, and the open leads.</summary>
    private void OpenRecap(int sinceDay, string why)
    {
        var t = _sim.Town;
        var r = Recap.Build(t, sinceDay);
        Ui.Clear(_recap);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        v.AddChild(Ui.Label(why.ToUpperInvariant(), Ui.Gold, Ui.Bold, 11));
        var title = Ui.Label(r.Title, Ui.Ink, Ui.Display, 24);
        v.AddChild(title);
        void Section(string head, IReadOnlyList<string> lines, string none)
        {
            v.AddChild(Ui.Label(head.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 11));
            var text = lines.Count == 0 ? Ui.C(Ui.Ink2, $"[i]{Ui.Esc(none)}[/i]") : string.Join("\n", lines.Select(l => $"{Ui.C(Ui.Gold, "◆")} {Ui.Esc(l)}"));
            var rich = Ui.Rich(text, 14);
            rich.CustomMinimumSize = new Vector2(548, 0);
            v.AddChild(rich);
        }
        Section("What happened", r.Moments, "A quiet stretch. Nothing the bards would sing about.");
        Section("The company", r.Hurt, "Everyone is home and on their feet.");
        Section("Open leads", r.Leads, "Nothing waits on you. For now.");
        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var go = new Button { Text = "Carry on", ThemeTypeVariation = "Post", FocusMode = FocusModeEnum.None };
        go.Pressed += () => _recap.Visible = false;
        foot.AddChild(go);
        v.AddChild(foot);
        _recap.AddChild(v);
        _recap.Visible = true;
        MoveChild(_recap, GetChildCount() - 1);
    }

    /// <summary>A new season opens with a recap of the last, in a town the player runs.</summary>
    private void SyncRecap()
    {
        var t = _sim.Town;
        string season = $"{Seasons.Year(t)}:{Seasons.Current(t).Id}";
        if (_seasonSeen == "") _seasonSeen = season;
        if (season == _seasonSeen) return;
        _seasonSeen = season;
        if (_opts.Smoke || _opts.Screenshot != null || _sim.Options.AutoStories || _title.Visible || _menu.Visible) return;
        OpenRecap(Math.Max(1, t.Day - Seasons.Rules(t).Days), $"{Seasons.Current(t).Name} comes");
    }

    // ---------- Heroes' moments ----------

    private int _momentsSeen = -1;
    private int _lastMomentDay = -99;

    /// <summary>A hero's level-up or new magic item goes into the news feed as a card of its own, at most one a day.</summary>
    private void SyncMoments()
    {
        var t = _sim.Town;
        var all = t.Moments;
        if (_momentsSeen < 0) _momentsSeen = all.Count == 0 ? 0 : all[^1].Tick;
        var fresh = all.Where(m => m.Tick > _momentsSeen).ToList();
        if (fresh.Count == 0) return;
        _momentsSeen = fresh[^1].Tick;
        if (_opts.Smoke || t.Day == _lastMomentDay) return;
        // An item over a level; a famous level over another.
        var m = fresh.OrderByDescending(x => x.Kind == Moments.Item).ThenByDescending(x => x.Text != "").ThenBy(x => x.Tick).First();
        _lastMomentDay = t.Day;
        FeedMoment(m);
    }

    private void FeedMoment(HeroMoment m)
    {
        var t = _sim.Town;
        var a = t.Agent(m.HeroId);
        var card = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
        var s = Glass(12, 12, 10);
        s.BorderColor = Ui.Gold;
        s.BorderWidthLeft = 3;
        s.BorderWidthTop = 1;
        card.AddThemeStyleboxOverride("panel", s);
        string kind = m.Kind == Moments.Item ? "MAGIC ITEM" : "LEVEL UP";
        string gains = string.Join("\n", m.Gains.Select(g => $"{Ui.C(Ui.Gold, "✦")} {Ui.Esc(g)}"));
        var text = Ui.Rich($"{Ui.C(Ui.Gold, $"[font_size=11][b]{kind}[/b][/font_size]")}  {Ui.C(Ui.Ink2, $"[font_size=11]{Ui.Esc(a.Hero?.Cls.Name ?? "")}[/font_size]")}\n[font_size=15][b]{Ui.Esc(m.Title)}[/b][/font_size]\n[font_size=13]{gains}[/font_size]" + (m.Text != "" ? $"\n{Ui.C(Ui.Ink2, $"[font_size=12][i]{Ui.Esc(m.Text)}[/i][/font_size]")}" : ""));
        text.CustomMinimumSize = new Vector2(300, 0);
        card.AddChild(text);
        card.SetMeta("until", _clockReal + 30);
        int id = a.Id;
        card.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Select(Selection.Agent(id));
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) card.QueueFree();
        };
        _feed.AddChild(card);
        _feed.MoveChild(card, 0);
        while (_feed.GetChildCount() > (_guideCard.Visible ? 3 : 5))
        {
            var old = _feed.GetChild(_feed.GetChildCount() - 1);
            _feed.RemoveChild(old);
            old.QueueFree();
        }
    }
}

/// <summary>
/// The place a tale is told about, inked like a game master's map: the road in from the left, a room for each
/// encounter along a winding passage, the master's room last; the party's figures stand where the beat is.
/// Laid out from the chronicle's id, so a tale always draws the same map.
/// </summary>
public partial class TaleMap : Control
{
    private static readonly Color Paper = new("e8dcc0");
    private static readonly Color Paper2 = new("d9c9a3");
    private static readonly Color Ink = new("4a3a26");
    private static readonly Color Faint = new("8a7656");
    private static readonly Color Blood = new("9b2d20");

    private readonly Chronicle _c;
    private readonly List<Agent> _members;
    private readonly List<int> _rooms;
    private readonly int _count;
    private readonly List<string> _kinds = [];
    private int _at = -1;
    private bool _gone;
    private readonly HashSet<int> _dead = [];
    private readonly Dictionary<int, Vector2> _pos = [];

    public TaleMap(Chronicle c, List<Agent> members, List<int> rooms)
    {
        _c = c;
        _members = members;
        _rooms = rooms;
        _count = Math.Max(1, rooms.Count == 0 ? 1 : rooms.Max() + 1);
        foreach (var b in c.Beats) if (b.Kind is "fight" or "boss" or "trap" or "hazard" or "puzzle" or "parley" or "treasure") _kinds.Add(b.Kind);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Show the party at a beat (by its index in the chronicle's beats); -1 for the end (home, or gone).</summary>
    public void Show(int beat, bool gone)
    {
        _at = beat;
        _gone = gone;
        _dead.Clear();
        var upTo = beat < 0 ? _c.Beats.Count : beat + 1;
        foreach (var b in _c.Beats.Take(upTo)) if (b.Kind == "death") _dead.Add(b.HeroId);
        QueueRedraw();
    }

    private Vector2 Room(int i)
    {
        var size = Size;
        float left = 110, right = size.X - 50;
        float x = _count == 1 ? (left + right) / 2 : left + (right - left) * i / (_count - 1);
        float wave = (float)Math.Sin(i * 1.9 + _c.Id * 0.7) * (size.Y * 0.26f);
        return new Vector2(x, size.Y / 2 + wave);
    }

    private Vector2 Road => new(38, Size.Y / 2);

    /// <summary>Put the figures where they stand now, without walking there (for screenshots).</summary>
    public void Snap() => _snap = true;

    private bool _snap;

    private Vector2 Target(int hero)
    {
        int room = _at < 0 ? -1 : _rooms[Math.Min(_at, _rooms.Count - 1)];
        // At the end they are home, at the road's start, unless nobody came back.
        var at = room < 0 || _at < 0 ? Road : Room(room);
        int i = _members.FindIndex(m => m.Id == hero);
        float angle = i * (float)(Math.Tau / Math.Max(1, _members.Count)) + 0.6f;
        return at + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 15;
    }

    public override void _Process(double delta)
    {
        bool moving = false;
        foreach (var m in _members)
        {
            var target = Target(m.Id);
            var p = _pos.TryGetValue(m.Id, out var now) ? now : Road;
            var next = _snap || Size.X < 10 ? target : p.Lerp(target, (float)Math.Min(1, delta * 4));
            if (next.DistanceTo(p) > 0.05f || !_pos.ContainsKey(m.Id)) moving = true;
            _pos[m.Id] = next;
        }
        if (moving) QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Paper2);
        DrawRect(new Rect2(new Vector2(4, 4), size - new Vector2(8, 8)), Paper);
        // A few stains, for the look of a map that's been on a table.
        var rand = new Random(_c.Id * 31 + 7);
        for (int k = 0; k < 6; k++)
            DrawCircle(new Vector2(rand.Next(20, (int)size.X - 20), rand.Next(20, (int)size.Y - 20)), rand.Next(10, 34), new Color(Paper2, 0.35f));

        // The road in, then the passage room to room.
        var road = Road;
        DrawDashedLine(road, Room(0), Faint, 2, 6);
        DrawString(Ui.Bold, road + new Vector2(-24, -18), "ROAD", HorizontalAlignment.Left, -1, 10, Faint);
        for (int i = 0; i + 1 < _count; i++) DrawLine(Room(i), Room(i + 1), Ink, 3);
        int reached = _at < 0 ? _count - 1 : _rooms[Math.Min(_at, _rooms.Count - 1)];
        for (int i = 0; i < _count; i++)
        {
            string kind = i < _kinds.Count ? _kinds[i] : "fight";
            bool boss = kind == "boss";
            var c = Room(i);
            float w = boss ? 58 : 40, h = boss ? 44 : 30;
            float jx = (float)(rand.NextDouble() * 6 - 3), jy = (float)(rand.NextDouble() * 6 - 3);
            var rect = new Rect2(c - new Vector2(w / 2 + jx, h / 2 + jy), new Vector2(w, h));
            bool seen = i <= reached;
            DrawRect(rect, seen ? new Color(Paper2, 0.9f) : new Color(Paper, 1));
            DrawRect(rect, boss ? Blood : Ink, false, boss ? 3 : 2);
            // What was in each room, as a mark a game master would ink.
            var mark = new Color(boss ? Blood : Ink, seen ? 1 : 0.35f);
            switch (kind)
            {
                case "boss":
                    DrawCircle(c, 7, mark);
                    DrawLine(c + new Vector2(-12, 12), c + new Vector2(12, -12), mark, 2);
                    DrawLine(c + new Vector2(-12, -12), c + new Vector2(12, 12), mark, 2);
                    break;
                case "trap":
                case "hazard":
                    DrawLine(c + new Vector2(-7, -7), c + new Vector2(7, 7), mark, 2);
                    DrawLine(c + new Vector2(-7, 7), c + new Vector2(7, -7), mark, 2);
                    break;
                case "treasure":
                    DrawCircle(c, 6, new Color(Ui.Gold, seen ? 1 : 0.35f));
                    DrawArc(c, 6, 0, (float)Math.Tau, 16, mark, 1.5f);
                    break;
                case "puzzle":
                case "parley":
                    DrawString(Ui.Bold, c + new Vector2(-4, 6), "?", HorizontalAlignment.Left, -1, 16, mark);
                    break;
                default:
                    DrawLine(c + new Vector2(-8, 0), c + new Vector2(8, 0), mark, 2);
                    DrawLine(c + new Vector2(0, -6), c + new Vector2(0, 6), mark, 2);
                    break;
            }
        }

        // The figures: a coin for each hero, their initial on it; a cross for the fallen.
        foreach (var m in _members)
        {
            if (_gone) break;
            var p = _pos.TryGetValue(m.Id, out var now) ? now : Road;
            bool dead = _dead.Contains(m.Id);
            var col = ClassColor(m.Hero!.Cls.Id);
            if (dead)
            {
                DrawLine(p + new Vector2(0, -10), p + new Vector2(0, 10), Blood, 3);
                DrawLine(p + new Vector2(-6, -3), p + new Vector2(6, -3), Blood, 3);
                continue;
            }
            bool speaking = _at >= 0 && _at < _c.Beats.Count && _c.Beats[_at].HeroId == m.Id;
            if (speaking) DrawCircle(p, 13, new Color(Ui.Gold, 0.85f));
            DrawCircle(p, 10, Ink);
            DrawCircle(p, 8.5f, col);
            DrawString(Ui.Bold, p + new Vector2(-4.5f, 5), m.GivenName[..1], HorizontalAlignment.Left, -1, 13, Colors.White);
        }
        DrawString(Ui.Display, new Vector2(size.X - 14 - Ui.Display.GetStringSize(_c.SiteName.ToUpperInvariant(), HorizontalAlignment.Left, -1, 12).X, size.Y - 12), _c.SiteName.ToUpperInvariant(), HorizontalAlignment.Left, -1, 12, Faint);
    }

    private static Color ClassColor(string cls)
    {
        uint h = Voices.Hash(cls);
        return Color.FromHsv(h % 360 / 360f, 0.55f, 0.62f);
    }
}
