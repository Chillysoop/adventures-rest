using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Story events, Paradox style: a card pops up when something in town needs the mayor, and its choices are
/// d20 checks of the mayor's own sheet (<see cref="Stories"/>). The card shows each choice's skill, DC and
/// odds; picking one rolls the die on screen, then tells what came of it. "Later" leaves the story on the
/// alert strip until it runs its course. The Mayor window shows what needs the mayor now, with the character sheet the rolls come from a button away.
/// </summary>
public partial class Main
{
    private PanelContainer _story = null!;
    private VBoxContainer _storyBox = null!;
    private int? _storyId;

    /// <summary>The newest story the player has been shown, so each new one pops up once.</summary>
    private int _storySeen;
    private bool _inspire;

    /// <summary>The roll on screen: the beat it shows and when the die was thrown.</summary>
    private StoryBeat? _beat;
    private Control? _beatResult;

    private void BuildStory()
    {
        _story = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(600, 0), ZIndex = 20, MouseFilter = MouseFilterEnum.Stop };
        _story.AddThemeStyleboxOverride("panel", Castle.Oak(26, 22));
        _storyBox = new VBoxContainer();
        _storyBox.AddThemeConstantOverride("separation", 10);
        _story.AddChild(_storyBox);
        AddChild(_story);
        _story.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        _story.GrowHorizontal = GrowDirection.Both;
        _story.GrowVertical = GrowDirection.Both;
        _story.MinimumSizeChanged += () => Ui.Refit(_story);
    }

    /// <summary>Show a story's card at its current stage.</summary>
    private void OpenStory(int id)
    {
        if (Stories.Find(_sim.Town, id) is not { } s) return;
        _storyId = id;
        _beat = null;
        _inspire = false;
        _storySeen = Math.Max(_storySeen, id);
        RenderStory(s);
        _story.Visible = true;
        MoveChild(_story, GetChildCount() - 1);
    }

    private void CloseStory()
    {
        _story.Visible = false;
        _storyId = null;
        _beat = null;
        _dirty = true;
    }

    /// <summary>
    /// A new story pops up on its own (while the player is playing, not watching the stand-in); high stakes
    /// pause the clock. A party's sending pauses it, and the clock stays stopped until it is answered. Each is
    /// a toggle in Settings ("stories", "sendings"): a sending let run pops up like any story.
    /// </summary>
    private void SyncStories(double delta)
    {
        if (_title.Visible || _menu.Visible || _settings.Visible) return;
        bool holdSendings = ShouldStop("sendings");
        if (!_opts.Smoke && holdSendings && Sendings.Waiting(_sim.Town).FirstOrDefault(w => Sendings.Pauses(_sim.Town, w)) is { } sending)
        {
            StopClock("sendings");
            if (!_story.Visible)
            {
                _storySeen = Math.Max(_storySeen, sending.Id);
                OpenStory(sending.Id);
            }
            return;
        }
        // A card whose story ran its course (or was answered by the stand-in) while it sat unanswered closes,
        // so it neither takes a dead click nor holds back the stories behind it.
        if (_story.Visible && _beat == null && _storyId is int open && Stories.Find(_sim.Town, open) is not { Done: false }) CloseStory();
        // One card at a time: a discovery's report on screen is answered first.
        if (_story.Visible || _find.Visible || _intro.Visible) return;
        // Quiet sendings (late in the campaign, not high stakes) wait on the alert strip rather than popping up.
        var next = Stories.Pending(_sim.Town).FirstOrDefault(s => s.Id > _storySeen && (!Sendings.Is(_sim.Town, s) || (!holdSendings && Sendings.Pauses(_sim.Town, s))));
        if (next == null) return;
        _storySeen = next.Id;
        if (_opts.Smoke) return;
        OpenStory(next.Id);
        if (Stories.DefOf(_sim.Town, next).Stakes == "high") StopClock(Sendings.Is(_sim.Town, next) ? "sendings" : "stories");
    }

    private static (string Word, Color Color) StakesLook(string stakes) => stakes switch
    {
        "high" => ("HIGH STAKES", Ui.Bad),
        "medium" => ("RISING STAKES", Ui.Gold),
        _ => ("LOW STAKES", Ui.Ink2),
    };

    private void RenderStory(Story s)
    {
        Ui.Clear(_storyBox);
        _beatResult = null;
        var t = _sim.Town;
        var d = Stories.DefOf(t, s);
        var (word, color) = StakesLook(d.Stakes);
        // The stakes' colour as a rule across the top of the oak card.
        _storyBox.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, 3), MouseFilter = MouseFilterEnum.Ignore });

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new TextureRect { Texture = Ui.Icon("d20"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        bool sending = Sendings.Is(d);
        head.AddChild(Ui.Label(word, color, Ui.Bold, 11));
        head.AddChild(Ui.Label("·  " + (sending ? "SENDING STONE" : d.Source == Stories.LostSource ? "THE FALLEN" : d.Source == Powers.ContactSource ? "FIRST CONTACT" : d.Source.ToUpperInvariant()), sending ? Ui.Accent : Ui.Ink2, Ui.Bold, 11));
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(spacer);
        if (sending && !s.Done && Sendings.Pauses(t, s)) head.AddChild(Ui.Label("❚❚ Paused", Ui.Ink2, Ui.Medium, 11));
        else if (!s.Done)
        {
            int days = Math.Max(1, (int)Math.Ceiling((s.DueTick - t.Tick) / 24.0));
            head.AddChild(Ui.IconChip("ui-time", Ui.Days(days), Ui.Ink2, "Runs its course, then lapses", 14));
        }
        _storyBox.AddChild(head);

        var title = Ui.Label(Stories.Fill(t, s, d.Title), Ui.Ink, Ui.Display, 24);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _storyBox.AddChild(title);
        if (FoeStrip(s) is { } foe) _storyBox.AddChild(foe);

        if (_beat != null)
        {
            RenderBeat(s, _beat);
            return;
        }
        var stage = Stories.StageOf(t, s);
        if (s.Beats.Count > 0) _storyBox.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"[i]{Ui.Esc(s.Beats[^1].Text)}[/i]"), 13));
        _storyBox.AddChild(Ui.Rich(LinkStory(s, Stories.Fill(t, s, stage.Text)), 15));

        var m = t.Stories.Mayor;
        for (int i = 0; i < stage.Options.Count; i++) _storyBox.AddChild(OptionRow(s, stage.Options[i], i));

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 10);
        // Inspiration only rerolls a check, so it is offered only when some choice here rolls.
        if (m.Inspiration > 0 && stage.Options.Any(o => o.Rolls))
        {
            var inspire = new CheckBox { Text = $"Reroll a miss  ✦{m.Inspiration}", Icon = Ui.Icon("ui-star"), ExpandIcon = false, ButtonPressed = _inspire, FocusMode = FocusModeEnum.None, TooltipText = "If the d20 misses, spend one Inspiration to throw it again. It is only spent on a miss. Running the town well and natural 20s earn it." };
            inspire.AddThemeConstantOverride("icon_max_width", 18);
            inspire.Toggled += on =>
            {
                _inspire = on;
                RenderStory(s);
            };
            foot.AddChild(inspire);
        }
        string withParty = Sendings.PartyOf(t, s) is { } party ? $"  {Ui.Img("ui-eye", 13)} {Ui.Esc(party.Name)}" : "";
        var who = Ui.Rich(Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Img("tab-mayor", 14)} [url=mayor]{Ui.C(Ui.Accent, Ui.Esc(m.Name))}[/url] {Ui.Dim($"lv {m.Level}")}{withParty}[/font_size]"), 12);
        who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        who.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        foot.AddChild(who);
        // A sending must be answered: the party is waiting on the stone.
        if (!Sendings.Is(d))
        {
            var later = new Button { Text = "Later", TooltipText = "Leave it on the alert strip for now", FocusMode = FocusModeEnum.None };
            later.Pressed += CloseStory;
            foot.AddChild(later);
        }
        _storyBox.AddChild(foot);
    }

    /// <summary>The enemy or the site a story is about: its portrait, its name and how dangerous the town thinks it is.</summary>
    private Control? FoeStrip(Story s)
    {
        var t = _sim.Town;
        Portrait face;
        string name;
        Control? bar = null;
        if (s.MenaceId is int mid && mid >= 1 && mid <= t.Saga.Menaces.Count)
        {
            var m = t.Saga.Menaces[mid - 1];
            face = Foes.Of(t, m, 52);
            name = Ui.Cap(m.Name);
            if (m.Alive) bar = Foes.Threat(t, m);
        }
        else if (s.Warband)
        {
            face = Foes.Warband(t, 52);
            name = Ui.Cap(t.Content.Enemy.Name);
            if (Assess.WarbandThreat(t) is Banded b) bar = Foes.Bar("Threat", b, Foes.Worse(b));
        }
        else if (s.SiteId is string sid && Quests.Site(t, sid) is { } site && !s.Institutions.Any())
        {
            face = Foes.Of(t, site, 52);
            name = site.Def.Name;
            if (!site.ThoughtCleared) bar = Foes.Bar("Danger", Assess.Danger(t, site), Foes.Worse(Assess.Danger(t, site)), "How hard it looks for the town's heroes, from what the town believes is there.");
        }
        else return null;
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        h.AddChild(face);
        var v = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        v.AddThemeConstantOverride("separation", 4);
        v.AddChild(Ui.Label(name, Ui.Ink, Ui.Bold, 14));
        if (bar != null) v.AddChild(bar);
        h.AddChild(v);
        return h;
    }

    /// <summary>The hero a story is about, as a link.</summary>
    private string LinkStory(Story s, string text)
    {
        string esc = Ui.Esc(text);
        if (s.HeroId is int h)
        {
            var a = _sim.Town.Agent(h);
            int i = esc.IndexOf(a.Name, StringComparison.Ordinal);
            if (i >= 0) esc = $"{esc[..i]}{AgentLink(a)}{esc[(i + a.Name.Length)..]}";
        }
        return esc;
    }

    private Control OptionRow(Story s, StoryOption o, int index)
    {
        var t = _sim.Town;
        string? blocked = Stories.Blocked(t, s, o);
        // The roll as one figure against the DC; where the bonus comes from is on hover.
        string sub, breakdown = "";
        string cost = o.Cost > 0 ? $"  {Ui.Chip("gold", Js.Str(o.Cost), Ui.Gold, "Costs")}" : "";
        if (!o.Rolls) sub = $"{Ui.Img("ui-yes", 14)} {Ui.Dim("Sure thing")}{cost}";
        else
        {
            var (mod, prof, ab) = Stories.Bonus(t, o.Skill, o.Ability);
            string name = o.Skill != "" ? t.Content.Stories.Skill(o.Skill)?.Name ?? o.Skill : ab.ToUpperInvariant();
            int bonus = mod + prof;
            string adv = Stories.Advantage(t, s, o);
            var helpers = Sendings.Helpers(t, s, o);
            int total = bonus + helpers.Sum(h => h.Bonus);
            string help = string.Concat(helpers.Select(h => $" + {h.Name} {(h.Bonus >= 0 ? "+" : "−")}{Math.Abs(h.Bonus)}"));
            breakdown = $"{name} {(bonus >= 0 ? "+" : "−")}{Math.Abs(bonus)} ({ab.ToUpperInvariant()}{Edge(o.Skill)}){help}" + (adv != "" ? $"\nAdvantage: {adv}" : "");
            sub = $"{Ui.Img("d20", 15)} {Ui.C(Ui.Ink, $"[b]{name} {(total >= 0 ? "+" : "−")}{Math.Abs(total)}[/b]")} {Ui.Dim("vs")} {Ui.C(Ui.Ink, $"[b]DC {Stories.DcOf(t, o)}[/b]")}"
                + (adv != "" ? $"  {Ui.Img("ui-up", 14)} {Ui.C(Ui.Good, "[b]advantage[/b]")}" : "") + cost;
        }
        if (blocked != null) sub = $"{Ui.Img("ui-lock", 14)} {Ui.Dim(Ui.Esc(Ui.Cap(blocked)))}";
        // The heroes in town who would like or resent the choice, by their tempers.
        // So do the wards, each by what it cares about.
        var (pleased, angered) = Regard.Reactions(t, o.Tags);
        var (wardsPleased, wardsAngered) = Wards.Reactions(t, o.Tags);
        string Who(List<Agent> heroes, List<WardDef> wards)
        {
            var names = heroes.Take(3).Select(h => h.GivenName).ToList();
            string more = heroes.Count > 3 ? $" +{heroes.Count - 3}" : "";
            var all = new List<string>();
            if (names.Count > 0) all.Add(string.Join(", ", names) + more);
            all.AddRange(wards.Select(w => w.Name));
            return string.Join(", ", all);
        }
        var feel = new List<string>();
        if (pleased.Count + wardsPleased.Count > 0) feel.Add($"{Ui.Img("ui-yes", 13)} {Ui.C(Ui.Good, Ui.Esc(Who(pleased, wardsPleased)))}");
        if (angered.Count + wardsAngered.Count > 0) feel.Add($"{Ui.Img("ui-no", 13)} {Ui.C(Ui.Bad, Ui.Esc(Who(angered, wardsAngered)))}");
        double chance = Stories.Chance(t, s, o, _inspire);
        var odds = Assess.Odds(t, chance);
        var oddsColor = blocked != null ? Ui.Ink2 : Foes.Better(odds);
        var row = new Button { Disabled = blocked != null, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, feel.Count > 0 ? 94 : 76), MouseDefaultCursorShape = CursorShape.PointingHand };
        var normal = Ui.Box(new Color(Ui.Panel2, 0.9f), Ui.Line, 8, 1, 14, 8);
        var hover = Ui.Box(new Color(Ui.Gold, 0.14f), Ui.Gold, 8, 1, 14, 8);
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
        var label = Ui.Label($"{index + 1}.  {Stories.Fill(t, s, o.Label)}", blocked != null ? Ui.Ink2 : Ui.Ink, Ui.Medium, 15);
        label.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(label);
        var subl = Ui.Rich(sub, 12);
        subl.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(subl);
        if (feel.Count > 0)
        {
            var feell = Ui.Rich(string.Join(Ui.C(Ui.Ink2, "  ·  "), feel), 12);
            feell.MouseFilter = MouseFilterEnum.Ignore;
            v.AddChild(feell);
        }
        if (blocked == null)
        {
            v.AddChild(Gauges(o, chance, odds, oddsColor));
            var touched = Assess.Impact(t, o).Touches.Select(w => w.Name).Distinct().ToList();
            row.TooltipText = string.Join("\n", new[] { breakdown, touched.Count > 0 ? $"At stake: {string.Join(", ", touched)} (which way depends on the roll)" : "", feel.Count > 0 ? "Pleases / angers the heroes and wards named" : "" }.Where(x => x != ""));
        }
        row.AddChild(v);
        // The odds' colour down the left edge: green for a safe bet, red for a long shot.
        var edge = new ColorRect { Color = oddsColor, MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(edge);
        edge.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        edge.OffsetLeft = 1;
        edge.OffsetRight = 5;
        edge.OffsetTop = 8;
        edge.OffsetBottom = -8;
        int id = s.Id;
        row.Pressed += () => Choose(id, index);
        return row;
    }

    /// <summary>
    /// A choice's bars: its odds (colour, segments and a word, with the exact chance), what it could win and what it
    /// could cost as rough bands, and the icons of what it touches, without saying which way each would go.
    /// </summary>
    private HBoxContainer Gauges(StoryOption o, double chance, Banded odds, Color oddsColor)
    {
        var t = _sim.Town;
        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 14);
        if (o.Rolls) h.AddChild(Foes.Bar("Odds", odds.Step + 1, odds.Steps, $"{odds.Name} · {Math.Round(chance * 100)}%", oddsColor, width: 60));
        else h.AddChild(Foes.Bar("Odds", odds.Steps, odds.Steps, "Sure thing", oddsColor, width: 60));
        var impact = Assess.Impact(t, o);
        // A choice that only moves the story on has its stakes in what comes next.
        if (impact.Reward.Step == 0 && o.Success.Next != "") h.AddChild(Foes.Bar("Reward", 0, impact.Reward.Steps - 1, "Comes later", Ui.Ink2, width: 33));
        else h.AddChild(Foes.Stakes("Reward", impact.Reward, Foes.Scale(0)));
        if (o.Rolls || impact.Risk.Step > 0) h.AddChild(Foes.Stakes(o.Rolls ? "Risk" : "Cost", impact.Risk, Foes.Scale(1)));
        if (impact.Touches.Count > 0)
        {
            var at = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            at.AddThemeConstantOverride("separation", 3);
            at.AddChild(Ui.Label("Touches", Ui.Ink2, Ui.Medium, 11));
            foreach (var w in impact.Touches.Take(5))
                at.AddChild(new TextureRect { Texture = Ui.Icon(w.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(15, 15), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
            h.AddChild(at);
        }
        return h;
    }

    /// <summary>What the mayor brings to a skill beyond the ability: training, expertise, a feat.</summary>
    private string Edge(string skill)
    {
        var t = _sim.Town;
        if (skill == "") return "";
        var bits = new List<string>();
        if (Stories.Expert(t, skill)) bits.Add("expert");
        else if (Stories.Proficient(t, skill)) bits.Add("proficient");
        foreach (var f in Sheet.Feats(t))
            if (f.Skills.GetValueOrDefault(skill) is int n and > 0) bits.Add($"{f.Name} +{n}");
        return bits.Count == 0 ? "" : ", " + string.Join(", ", bits);
    }

    /// <summary>Take an option: the sim rolls, and the card throws the die.</summary>
    private void Choose(int storyId, int option)
    {
        var beat = _sim.ResolveStory(storyId, option, _inspire);
        if (beat == null) return;
        GuideEvent("roll");
        _beat = beat;
        _dirty = true;
        if (Stories.Find(_sim.Town, storyId) is { } s) RenderStory(s);
    }

    private void RenderBeat(Story s, StoryBeat b)
    {
        var t = _sim.Town;
        bool rolled = b.Result is not (StoryResult.Certain or StoryResult.Ignored);
        var result = new VBoxContainer { Visible = !rolled };
        result.AddThemeConstantOverride("separation", 8);
        if (rolled)
        {
            string skill = t.Content.Stories.Skill(b.Skill)?.Name ?? b.Skill.ToUpperInvariant();
            var roll = new DiceRoll(b, DiceRoll.MayorTerms(t, b), $"{t.Stories.Mayor.Name} rolls {skill}", false, InstantRolls) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            roll.Landed += () => result.Visible = true;
            _storyBox.AddChild(roll);
            string adv = string.Join("  ·  ", new[] { b.Advantage != "" ? $"advantage: {Ui.Esc(b.Advantage)}" : "", b.Rerolled is int first ? $"rerolled a {first} with Inspiration" : "" }.Where(x => x != ""));
            if (adv != "") result.AddChild(Ui.Rich($"[center]{Ui.C(Ui.Ink2, adv)}[/center]", 13));
        }
        result.AddChild(Ui.Rich(LinkStory(s, b.Text), 15));
        if (b.Table != "") result.AddChild(Ui.Rich(Ui.C(Ui.Ink2, $"[i]{Ui.Esc(b.Table)}[/i]"), 13));
        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        // A quest waiting on the party's next sending closes until the stone wakes again.
        bool waits = s.WaitTick is int wait && wait > t.Tick;
        if (waits) result.AddChild(Ui.Rich(Ui.C(Ui.Ink2, "[i]The stone goes quiet. They will send again when there is news.[/i]"), 13));
        var go = new Button { Text = s.Done || waits ? "Close" : "Continue", ThemeTypeVariation = "Post", FocusMode = FocusModeEnum.None };
        int id = s.Id;
        go.Pressed += () =>
        {
            if (Stories.Find(_sim.Town, id) is { Done: false } open && (open.WaitTick ?? 0) <= _sim.Town.Tick) OpenStory(id);
            else CloseStory();
        };
        foot.AddChild(go);
        result.AddChild(foot);
        _storyBox.AddChild(result);
        _beatResult = result;
    }

    /// <summary>Screenshots and the smoke test land every die at once.</summary>
    private bool InstantRolls => _opts.Screenshot != null || _opts.Smoke;

    // ---------- The mayor's sheet ----------

    /// <summary>Whether the Mayor window shows the full character sheet rather than what needs the mayor now.</summary>
    private bool _mayorSheet;

    /// <summary>
    /// The Mayor window: what is timely (a level to choose, stories waiting, the regalia worn and to wear,
    /// decrees and callings in force), with the full character sheet one button away.
    /// </summary>
    private void RenderMayor()
    {
        var p = _panes[Tab.Mayor];
        var t = _sim.Town;
        var r = t.Content.Stories;
        var m = t.Stories.Mayor;
        var bg = r.Background(m.Background);
        p.Begin();
        int max = Sheet.InspirationMax(t);
        string pips = new string('●', Math.Min(m.Inspiration, max)) + new string('○', Math.Max(0, max - m.Inspiration));
        p.Card($"{Ui.Head(Ui.Esc(m.Name), "tab-mayor", Ui.Ink, 19)}\n{Ui.Chips(Ui.Chip("ui-star", $"Level {m.Level}", Ui.Gold), Ui.Chip("ui-star", pips, m.Inspiration > 0 ? Ui.Gold : Ui.Ink2, "Inspiration"), Ui.Dim($"{Ui.Esc(Stories.Title(t))} · {Ui.Esc(bg.Name)}"))}", Ui.Gold);
        p.Buttons(
            (_mayorSheet ? "◂ Now" : $"{(Sheet.Pending(t) > 0 ? "▲ " : "")}Character sheet", true, !_mayorSheet, () =>
            {
                _mayorSheet = !_mayorSheet;
                _dirty = true;
            }));
        if (_mayorSheet) MayorSheetView(p);
        else MayorNow(p);
        p.Commit();
    }

    /// <summary>What needs the mayor now.</summary>
    private void MayorNow(Pane p)
    {
        var t = _sim.Town;
        var r = t.Content.Stories;
        var m = t.Stories.Mayor;
        if (Mayoralty.Dead(t))
        {
            MayorFallen(p);
            return;
        }
        MayorLife(p);
        int? next = Stories.NextLevelXp(t);
        int from = r.Levels.Count >= m.Level ? r.Levels[m.Level - 1] : 0;
        if (next is int n) p.Meter("XP", 100.0 * (m.Xp - from) / Math.Max(1, n - from), $"{m.Xp}/{n}");
        if (Sheet.Pending(t) > 0) LevelUp(p);

        var open = Stories.Pending(t);
        p.Heading("Waiting on you");
        foreach (var s in open)
        {
            var d = Stories.DefOf(t, s);
            var (word, color) = StakesLook(d.Stakes);
            int days = Math.Max(1, (int)Math.Ceiling((s.DueTick - t.Tick) / 24.0));
            int id = s.Id;
            p.Row($"[b]{Ui.Esc(Stories.Fill(t, s, d.Title))}[/b]\n{Tag(word, color)}  {Ui.Chip("ui-time", Ui.Days(days), Ui.Ink2, "Runs its course, then lapses", 14)}", ("Step in", true, true, () => OpenStory(id)));
        }

        RegaliaRows(p);

        var decrees = Decrees.Active(t).ToList();
        if (decrees.Count > 0)
        {
            p.Heading("Decrees in force");
            foreach (var d in decrees)
                p.Text($"{Ui.Img("decree", 15)} {Ui.Tip($"[b]{Ui.Esc(d.Name)}[/b] {Ui.Img("ui-info", 12)}", d.Text)}  {Ui.Chip("ui-time", Ui.Days(Decrees.DaysLeft(t, d.Id)), Ui.Ink2, "Left", 14)}", 13);
        }
        var called = Callings.Holders(t).ToList();
        if (called.Count > 0)
        {
            p.Heading("Callings");
            foreach (var (hero, c) in called)
                p.Text($"{Ui.Img("ui-renown", 15)} {Ui.Tip($"[b]{Ui.Esc(c.Name)}[/b] {Ui.Img("ui-info", 12)}", c.Text)}  {AgentLink(hero)} {Ui.Dim($"[font_size=12]{Ui.Esc(hero.Hero!.Cls.Name.ToLowerInvariant())}[/font_size]")}", 13);
        }
    }

    /// <summary>The mayor's health and where they spend their days.</summary>
    private void MayorLife(Pane p)
    {
        var t = _sim.Town;
        int hp = Mayoralty.Hp(t), max = Mayoralty.MaxHp(t);
        p.Meter("HP", 100.0 * hp / Math.Max(1, max), $"{hp}/{max}", hp * 2 < max);
        if (t.Mayoralty.Hardcore) p.Note($"{Ui.Img("face-death-skull", 12)} Hardcore: if the mayor dies, the town is lost.");
        if (Mayoralty.Away(t) && t.Mayoralty.PartyId is int pid)
            p.Note($"{Ui.Img("compass", 12)} Riding with the {Ui.Esc(t.Parties[pid - 1].Name)}");
        var stances = t.Content.Mayoralty.Stances;
        string now = Mayoralty.Stance(t).Id;
        p.Buttons(stances.Select(st =>
        {
            string id = st.Id;
            return (st.Name, true, id == now, (Action)(() => Act(() => _sim.SetMayorStance(id))));
        }).ToArray());
        var cur = Mayoralty.Stance(t);
        var chips = new List<string>();
        if (cur.Id == "keep") chips.Add($"{Ui.Img("defence", 12)} safe");
        if (cur.Mandate > 0) chips.Add($"{Ui.Img("mandate", 12)} +{Js.Str(cur.Mandate)}/day");
        if (cur.Loyalty > 0) chips.Add($"{Ui.Img("heroes", 12)} +{Js.Str(cur.Loyalty)} loyalty");
        if (cur.Edge > 0) chips.Add($"{Ui.Img("d20", 12)} +{cur.Edge} company checks · XP");
        if (cur.Id != "keep") chips.Add($"{Ui.Img("warband", 12)} danger");
        p.Text(Ui.C(Ui.Ink2, $"[font_size=12]{string.Join("   ", chips)}[/font_size]"), 13);
    }

    /// <summary>The mayor is dead: raise them at the temple, or name a successor.</summary>
    private void MayorFallen(Pane p)
    {
        var t = _sim.Town;
        var s = t.Mayoralty;
        int days = Math.Max(1, t.Content.Mayoralty.DecideDays - (t.Day - (s.FallenDay ?? t.Day)));
        p.Card($"{Ui.Img("face-death-skull", 16)} [b]Mayor {Ui.Esc(t.Stories.Mayor.Name)} is dead[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(s.Cause)} · council decides in {Ui.Days(days)}[/font_size]")}", Ui.Bad);
        if (s.Hardcore) return;
        string? refused = Mayoralty.RaiseRefused(t);
        p.Row($"{Ui.Img("tab-faith", 14)} [b]Raise at the temple[/b]  {Ui.Coined($"{Js.Str(Mayoralty.RaiseCost(t))}g")}" + (refused != null ? $"\n{Ui.C(Ui.Bad, $"[font_size=12]{Ui.Esc(refused)}[/font_size]")}" : ""),
            ("Raise", refused == null, true, () => Act(() => _sim.RaiseMayor())));
        p.Heading("Or name a new mayor");
        var r = t.Content.Stories;
        var options = Mayoralty.Candidates(t);
        for (int i = 0; i < options.Count; i++)
        {
            var c = options[i];
            var bg = r.Background(c.Background);
            var a = Stories.Deal(r, c.Background, c.Scores);
            string best = string.Join(" ", new[] { "str", "dex", "con", "int", "wis", "cha" }.OrderByDescending(k => a[k]).ThenBy(k => k, StringComparer.Ordinal).Take(2).Select(k => $"{k.ToUpperInvariant()} {a[k]}"));
            int k2 = i;
            p.Row($"[b]{Ui.Esc(c.Name)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(bg.Name)} · {best}[/font_size]")}", ("Name mayor", true, i == 0, () => Act(() => _sim.NameSuccessor(k2))));
        }
    }

    /// <summary>What a piece of regalia does, as short chips.</summary>
    private string RegaliaChips(RegaliaDef d)
    {
        var r = _sim.Town.Content.Stories;
        var chips = new List<string>();
        foreach (var (skill, n) in d.Skills.OrderBy(kv => kv.Key, StringComparer.Ordinal)) chips.Add($"{Ui.Img("d20", 12)} +{n} {Ui.Esc(r.Skill(skill)?.Name ?? skill)}");
        if (d.Inspiration > 0) chips.Add($"✦ +{d.Inspiration} inspiration");
        if (d.Mandate > 0) chips.Add($"{Ui.Img("mandate", 12)} +{Js.Str(d.Mandate)}/day");
        if (d.Council > 0) chips.Add($"{Ui.Img("tab-council", 12)} +{Js.Str(d.Council)} seats/day");
        if (d.Loyalty > 0) chips.Add($"{Ui.Img("heroes", 12)} +{Js.Str(d.Loyalty)} loyalty");
        if (d.March > 0) chips.Add($"{Ui.Img("order-strike", 12)} +{d.March} to hit on the march");
        return Ui.C(Ui.Ink2, $"[font_size=12]{string.Join("   ", chips)}[/font_size]");
    }

    /// <summary>The regalia: each slot with what's worn, pieces in the armory to put on, and pieces to commission.</summary>
    private void RegaliaRows(Pane p)
    {
        var t = _sim.Town;
        var rules = t.Content.Regalia;
        p.Heading("Regalia");
        foreach (var slot in rules.Slots)
        {
            var worn = Regalia.In(t, slot.Id);
            string sl = slot.Id;
            var lead = (Func<Control>)(() => SlotIcon(slot.Icon, worn != null));
            if (worn != null && Regalia.Def(t, worn.Item) is { } d)
                p.Row($"[b]{Ui.Esc(worn.Item.Name)}[/b]\n{RegaliaChips(d)}", ($"rg{slot.Icon}1", lead, null), ("Take off", true, false, () => Act(() => _sim.TakeOffRegalia(sl))));
            else
                p.Row(Ui.C(Ui.Ink2, $"{Ui.Esc(slot.Name)} · empty"), ($"rg{slot.Icon}0", lead, null));
        }
        var spare = t.Armory.Stock.Where(s => Regalia.Is(t, s.Item)).OrderBy(s => s.Id).ToList();
        if (spare.Count > 0)
        {
            p.Heading("In the armory");
            foreach (var s in spare)
            {
                var d = Regalia.Def(t, s.Item)!;
                int id = s.Id;
                string icon = rules.Slot(d.Slot)?.Icon ?? "";
                p.Row($"[b]{Ui.Esc(s.Item.Name)}[/b]\n{RegaliaChips(d)}", ($"rga{icon}", () => SlotIcon(icon, true), null), ("Wear", true, true, () => Act(() => _sim.WearRegalia(id))));
            }
        }
        // What the craftsmen could make: what the charter allows now, and the next one or two beyond it.
        var commissions = Regalia.Commissions(t).Where(c => c.Refused is null or "not enough gold").Take(4).ToList();
        if (commissions.Count > 0)
        {
            p.Heading("Commission");
            foreach (var (item, d, refused) in commissions)
            {
                string id = item.Id;
                string icon = rules.Slot(d.Slot)?.Icon ?? "";
                p.Row($"[b]{Ui.Esc(item.Name)}[/b]  {Ui.Coined($"{Js.Str(Regalia.Price(t, item))}g")}\n{RegaliaChips(d)}", ($"rgc{icon}", () => SlotIcon(icon, false), null), ("Commission", refused == null, false, () => Act(() => _sim.CommissionRegalia(id))));
            }
        }
    }

    private static Control SlotIcon(string icon, bool filled)
    {
        var box = new PanelContainer { CustomMinimumSize = new Vector2(40, 40), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        box.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, filled ? Ui.Gold : Ui.Line, 8, 1, 4, 4));
        var img = new TextureRect { Texture = Ui.Icon(icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(30, 30), Modulate = filled ? Colors.White : new Color(1, 1, 1, 0.35f) };
        box.AddChild(img);
        return box;
    }

    /// <summary>The full character sheet: abilities, skills, background, feats, inspiration and the mayor's record.</summary>
    private void MayorSheetView(Pane p)
    {
        var t = _sim.Town;
        var r = t.Content.Stories;
        var m = t.Stories.Mayor;
        if (Sheet.Pending(t) > 0) LevelUp(p);
        var spec = MayorSheetSpec(t);
        p.Add("sheet" + spec.Sig(), () => SheetView.Build(spec));

        // Until the first story, the background can still be changed.
        if (Stories.CanChooseBackground(t) && !m.Chosen)
        {
            p.Heading("Background");
            foreach (var b in r.Backgrounds)
            {
                var skills = string.Join(", ", b.Skills.Select(k => r.Skill(k)?.Name ?? k));
                string id = b.Ref;
                bool current = b.Ref == m.Background;
                p.Row($"{Ui.Tip($"[b]{Ui.Esc(b.Name)}[/b] {Ui.Img("ui-info", 12)}", b.Text)}{(current ? "  " + Tag("now", Ui.Gold) : "")}\n[font_size=12]{Ui.Chips(Ui.Chip("d20", Ui.Esc(skills), Ui.Ink2, "Trained skills"), Ui.Chip("ui-up", string.Join(" ", b.Abilities.Take(2).Select(x => x.ToUpperInvariant())), Ui.Gold, "Best abilities"))}[/font_size]",
                    ("Choose", true, current, () => Act(() => _sim.ChooseBackground(id))));
            }
        }
    }

    /// <summary>The mayor's sheet in the same layout as every hero's (<see cref="SheetView"/>).</summary>
    private SheetSpec MayorSheetSpec(Town t)
    {
        var r = t.Content.Stories;
        var m = t.Stories.Mayor;
        var bg = r.Background(m.Background);
        int? next = Stories.NextLevelXp(t);
        int from = r.Levels.Count >= m.Level ? r.Levels[m.Level - 1] : 0;
        int prof = Stories.ProficiencyBonus(m.Level);
        int max = Sheet.InspirationMax(t);
        string pips = new string('●', Math.Min(m.Inspiration, max)) + new string('○', Math.Max(0, max - m.Inspiration));
        int hp = Mayoralty.Hp(t), maxHp = Mayoralty.MaxHp(t);

        var badge = new List<string> { Ui.Chip("ui-star", pips, m.Inspiration > 0 ? Ui.Gold : Ui.Ink2, "Inspiration: reroll a failed d20") };
        badge.Add(Tag(bg.Feature, Ui.Gold));
        if (t.Mayoralty.Hardcore) badge.Add(Ui.Chip("face-death-skull", "Hardcore", Ui.Bad, "If the mayor dies, the town is lost"));

        var vitals = new List<SheetStat>
        {
            new("ui-heart", "HIT POINTS", $"{hp}/{maxHp}", "", maxHp > 0 ? hp / (double)maxHp : 0),
            new("d20", "PROFICIENCY", $"+{prof}", "Added to trained skills"),
            new("ui-star", "INSPIRATION", $"{m.Inspiration}/{max}", "Rerolls a failed d20 in a story"),
            new("ui-yes", "STORIES WON", m.Successes.ToString(), $"{m.Failures} lost"),
        };

        var regalia = Regalia.Worn(t).Select(w => Line(t.Content.Regalia.Slot(w.Def.Slot)?.Icon ?? "regalia-chain", Ui.Esc(w.Piece.Item.Name), Ui.Gold)).ToList();
        if (regalia.Count == 0) regalia.Add(Ui.Dim("None worn"));
        var feats = Sheet.Feats(t).Select(f => Ui.Tip(Line("ui-star", $"[b]{Ui.Esc(f.Name)}[/b]"), f.Text)).ToList();
        var deeds = r.Deeds.Select(d =>
        {
            var (have, need) = Sheet.Progress(t, d);
            string unit = d.Id == "content" ? $"days at {Js.Str(d.Threshold)}+ Mandate" : "";
            return Line("ui-star", $"{Ui.Esc(d.Name)}  {(need > 1 ? Ui.Tip(Ui.C(Ui.Gold, $"[b]{Math.Max(0, have)}/{need}[/b]"), unit) : Ui.Dim("each"))}");
        }).ToList();

        var story = new List<string> { Ui.Tip(Line("lore", $"[b]{Ui.Esc(bg.Name)}[/b]"), bg.Text) };
        story.AddRange(t.Mayoralty.Past.Select(pm => Line("face-death-skull", $"{Ui.Esc(pm.Name)} {Ui.Dim($"level {pm.Level} · day {pm.From}-{pm.To} · {Ui.Esc(pm.Cause)}")}", Ui.Ink2)));
        foreach (var pr in t.Stories.Precedents.Keys.Select(id => r.Precedent(id)).OfType<PrecedentDef>())
            story.Add(Line("ui-day", $"{Ui.Esc(pr.Text)} {Ui.Dim($"day {t.Stories.Precedents[pr.Id]}")}"));
        var record = new List<string>();
        foreach (var s in t.Stories.All.Where(s => s.Done).OrderByDescending(s => s.Id).Take(8))
        {
            var b = s.Beats[^1];
            var color = b.Result switch { StoryResult.Critical or StoryResult.Success or StoryResult.Certain => Ui.Good, StoryResult.Partial => Ui.Gold, StoryResult.Ignored => Ui.Ink2, _ => Ui.Bad };
            string what = b.Result == StoryResult.Ignored ? "let it be" : b.Result == StoryResult.Certain ? "settled" : Stories.Word(b.Result);
            record.Add($"{Ui.Img(color == Ui.Good ? "ui-yes" : color == Ui.Bad ? "ui-no" : "ui-day", 14)} {Ui.Esc(Stories.Fill(t, s, Stories.DefOf(t, s).Title))}  {Tag(what, color)}  {Ui.Dim($"[font_size=11]day {b.Tick / 24 + 1}[/font_size]")}");
        }

        var right = new List<SheetSection> { new("Regalia", "regalia-chain", regalia) };
        if (feats.Count > 0) right.Add(new("Feats", "ui-star", feats));
        right.Add(new($"Inspiration · up to {max}", "ui-star", deeds));
        var wide = new List<SheetSection> { new("Story", "lore", story) };
        if (record.Count > 0) wide.Add(new("Deeds", "ui-day", record));

        return new SheetSpec
        {
            Name = $"Mayor {m.Name}",
            Subtitle = $"Level {m.Level} {Ui.Esc(Stories.Title(t).ToLowerInvariant())} · {Ui.Esc(bg.Name.ToLowerInvariant())}",
            Badge = string.Join("  ", badge),
            Glyph = "crown",
            Tint = Ui.Gold,
            Ring = Ui.Gold,
            Crowned = true,
            Xp = next is int n ? (m.Xp - from) / (double)Math.Max(1, n - from) : 1,
            XpText = next is int n2 ? $"{m.Xp:#,0} / {n2:#,0} XP" : $"{m.Xp:#,0} XP",
            Vitals = vitals,
            Abilities = Stories.Scores(t),
            Skills = r.Skills.Select(sk =>
            {
                var (mod, pr, _) = Stories.Bonus(t, sk.Id);
                return new SheetSkill(sk.Name, mod + pr, Stories.Proficient(t, sk.Id), Stories.Expert(t, sk.Id));
            }).ToList(),
            SkillsNote = $"● trained +{prof} · ◆ expert",
            Right = right,
            Wide = wide,
        };
    }

    /// <summary>The first ability picked for a +1/+1 improvement, until the second is.</summary>
    private string? _abilityPick;

    /// <summary>The level-up choice: two ability points or a feat at improvement levels, a skill or expertise at the others.</summary>
    private void LevelUp(Pane p)
    {
        var t = _sim.Town;
        var r = t.Content.Stories;
        var (level, improvement) = Sheet.NextAdvance(t);
        int pending = Sheet.Pending(t);
        p.Card($"[font_size=16][b]Level {level}: choose how you grow[/b][/font_size]{(pending > 1 ? Ui.C(Ui.Ink2, $"  ({pending} choices waiting)") : "")}\n{Ui.C(Ui.Ink2, improvement ? "Raise two abilities by 1 (or one by 2), or take a feat." : $"Learn a new skill{(level >= r.ExpertiseFrom ? ", or become expert in one you know (double proficiency)" : "")}.")}", Ui.Gold);
        if (improvement)
        {
            var scores = Stories.Scores(t);
            string sig = $"lvab{level}{_abilityPick}{scores}";
            p.Add(sig, () =>
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                foreach (var ab in Ability.All)
                {
                    string adv = _abilityPick == null ? "" : $"ability:{_abilityPick},{ab}";
                    bool ok = _abilityPick == null ? scores[ab] < 20 : Sheet.Refuse(t, adv) == null;
                    var b = new Button { Text = $"{ab.ToUpperInvariant()} {scores[ab]}{(_abilityPick == ab ? " +1" : "")}", Disabled = !ok, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = _abilityPick == ab ? "Post" : "" };
                    string pick = ab;
                    b.Pressed += () =>
                    {
                        if (_abilityPick == null) _abilityPick = pick;
                        else
                        {
                            Act(() => _sim.AdvanceMayor($"ability:{_abilityPick},{pick}"));
                            _abilityPick = null;
                        }
                        _dirty = true;
                    };
                    row.AddChild(b);
                }
                return row;
            });
            if (_abilityPick != null) p.Note($"+1 {_abilityPick.ToUpperInvariant()}. Pick a second ability, or {_abilityPick.ToUpperInvariant()} again for +2.");
            foreach (var f in r.Feats)
            {
                string id = "feat:" + f.Id;
                bool ok = Sheet.Refuse(t, id) == null;
                p.Row($"[b]{Ui.Esc(f.Name)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(f.Text)}[/font_size]")}", ("Take", ok, false, () => Act(() => _sim.AdvanceMayor(id))));
            }
            return;
        }
        var choices = r.Skills
            .Select(sk => (Skill: sk, Advance: Stories.Proficient(t, sk.Id) ? "expertise:" + sk.Id : "skill:" + sk.Id))
            .Where(c => Sheet.Refuse(t, c.Advance) == null)
            .ToList();
        p.Add($"lvsk{level}{string.Join("|", choices.Select(c => c.Advance))}", () =>
        {
            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            foreach (var (sk, adv) in choices)
            {
                bool expert = adv.StartsWith("expertise:", StringComparison.Ordinal);
                var b = new Button { Text = $"{(expert ? "◆ " : "+ ")}{sk.Name}", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = expert ? $"Become expert in {sk.Name}: double proficiency." : $"Learn {sk.Name} ({sk.Ability.ToUpperInvariant()})." };
                string a = adv;
                b.Pressed += () => Act(() => _sim.AdvanceMayor(a));
                grid.AddChild(b);
            }
            return grid;
        });
    }

    /// <summary>The newest everyday-decision check shown, so each roll toasts once.</summary>
    private int _checksSeen = -1;

    /// <summary>The small roll for the newest everyday decision, and when it finished.</summary>
    private PanelContainer? _check;

    /// <summary>
    /// A d20 rolled on one of the player's decisions throws a small die under the top bar without stopping
    /// play: the bonuses add up, the verdict and what came of it show, and it fades a few seconds later.
    /// </summary>
    private void SyncChecks()
    {
        var st = _sim.Town.Stories;
        if (_checksSeen < 0 || _checksSeen > st.Checks) _checksSeen = st.Checks;
        if (st.Checks == _checksSeen || st.LastCheck is not { } b) return;
        _checksSeen = st.Checks;
        if (_sim.Options.AutoStories) return;
        var t = _sim.Town;
        string skill = t.Content.Stories.Skill(b.Skill)?.Name ?? b.Skill;
        _check?.QueueFree();
        var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Pass, ZIndex = 15 };
        pill.AddThemeStyleboxOverride("panel", Ui.Flat(new Color(0.08f, 0.06f, 0.04f, 0.9f), 14, 8, 14));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Label($"{skill.ToUpperInvariant()} CHECK", Ui.Ink2, Ui.Bold, 10));
        var roll = new DiceRoll(b, DiceRoll.MayorTerms(t, b), "", true, InstantRolls);
        v.AddChild(roll);
        var text = Ui.Label(b.Text, new Color("f6eedb"), Ui.Medium, 12);
        text.Visible = false;
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(Math.Max(roll.CustomMinimumSize.X, 260), 0);
        if (b.Text != "") v.AddChild(text);
        pill.AddChild(v);
        AddChild(pill);
        pill.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize, TopBar + 56);
        pill.GrowHorizontal = GrowDirection.Both;
        _check = pill;
        roll.Landed += () =>
        {
            text.Visible = true;
            if (!IsInstanceValid(pill)) return;
            var tw = pill.CreateTween();
            tw.TweenInterval(3.2);
            tw.TweenProperty(pill, "modulate:a", 0.0, 0.6);
            tw.TweenCallback(Callable.From(() =>
            {
                if (_check == pill) _check = null;
                pill.QueueFree();
            }));
        };
    }

    private static Control SkillCell(string bbcode)
    {
        var r = Ui.Rich(bbcode, 13);
        r.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return r;
    }

    private static Control AbilityRow(Abilities a) => SheetView.AbilityRow(a);
}
