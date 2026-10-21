using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Story events, Paradox style: a card pops up when something in town needs the mayor, and its choices are
/// d20 checks of the mayor's own sheet (<see cref="Stories"/>). The card shows each choice's skill, DC and
/// odds; picking one rolls the die on screen, then tells what came of it. "Later" leaves the story on the
/// alert strip until it runs its course. The Mayor window is the character sheet the rolls come from.
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
        if (_story.Visible || _find.Visible) return;
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
        head.AddChild(Ui.Label("·  " + (sending ? "SENDING STONE" : d.Source == Stories.LostSource ? "THE FALLEN" : d.Source.ToUpperInvariant()), sending ? Ui.Accent : Ui.Ink2, Ui.Bold, 11));
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(spacer);
        if (sending && !s.Done && Sendings.Pauses(t, s)) head.AddChild(Ui.Label("Game paused until you answer", Ui.Ink2, Ui.Medium, 11));
        else if (!s.Done)
        {
            int days = Math.Max(1, (int)Math.Ceiling((s.DueTick - t.Tick) / 24.0));
            head.AddChild(Ui.Label($"Runs its course in {Ui.Days(days)}", Ui.Ink2, Ui.Medium, 11));
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
        if (m.Inspiration > 0)
        {
            var inspire = new CheckBox { Text = $"Reroll with Inspiration if it fails ({m.Inspiration} left)", ButtonPressed = _inspire, FocusMode = FocusModeEnum.None, TooltipText = "If the d20 misses, spend one Inspiration to throw it again. It is only spent on a miss. Running the town well and natural 20s earn it." };
            inspire.Toggled += on =>
            {
                _inspire = on;
                RenderStory(s);
            };
            foot.AddChild(inspire);
        }
        string withParty = Sendings.PartyOf(t, s) is { } party ? $", with the {Ui.Esc(party.Name)} on the stone" : "";
        var who = Ui.Rich(Ui.C(Ui.Ink2, $"[font_size=12]Rolling as [url=mayor]{Ui.C(Ui.Accent, Ui.Esc(m.Name))}[/url], level {m.Level} {Ui.Esc(Stories.Title(t).ToLowerInvariant())}{withParty}[/font_size]"), 12);
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
        string sub;
        if (!o.Rolls) sub = o.Cost > 0 ? $"A sure thing · costs {Js.Str(o.Cost)}g" : "A sure thing";
        else
        {
            var (mod, prof, ab) = Stories.Bonus(t, o.Skill, o.Ability);
            string name = o.Skill != "" ? t.Content.Stories.Skill(o.Skill)?.Name ?? o.Skill : ab.ToUpperInvariant();
            int bonus = mod + prof;
            string adv = Stories.Advantage(t, s, o);
            string help = string.Concat(Sendings.Helpers(t, s, o).Select(h => $" + {h.Name} {(h.Bonus >= 0 ? "+" : "−")}{Math.Abs(h.Bonus)}"));
            sub = $"{name} {(bonus >= 0 ? "+" : "−")}{Math.Abs(bonus)} ({ab.ToUpperInvariant()}{Edge(o.Skill)}){help} vs DC {Stories.DcOf(t, o)}"
                + (adv != "" ? $" · advantage: {adv}" : "") + (o.Cost > 0 ? $" · costs {Js.Str(o.Cost)}g" : "");
        }
        if (blocked != null) sub = $"{Ui.Cap(blocked)}.";
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
        if (pleased.Count + wardsPleased.Count > 0) feel.Add(Ui.C(Ui.Good, $"Pleases {Ui.Esc(Who(pleased, wardsPleased))}"));
        if (angered.Count + wardsAngered.Count > 0) feel.Add(Ui.C(Ui.Bad, $"Angers {Ui.Esc(Who(angered, wardsAngered))}"));
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
        var subl = Ui.Rich(Ui.Coined(Ui.C(Ui.Ink2, Ui.Esc(sub))), 12);
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
            if (touched.Count > 0) row.TooltipText = $"What is at stake: {string.Join(", ", touched)}. Which way each goes depends on the roll.";
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

    private void RenderMayor()
    {
        var p = _panes[Tab.Mayor];
        var t = _sim.Town;
        var r = t.Content.Stories;
        var m = t.Stories.Mayor;
        var bg = r.Background(m.Background);
        p.Begin();
        int? next = Stories.NextLevelXp(t);
        int from = r.Levels.Count >= m.Level ? r.Levels[m.Level - 1] : 0;
        p.Card($"[font_size=19][b]{Ui.Esc(m.Name)}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"Level {m.Level} {Ui.Esc(Stories.Title(t).ToLowerInvariant())} of {Ui.Esc(t.Name)} · {Ui.Esc(bg.Name.ToLowerInvariant())}")}", Ui.Gold);
        if (next is int n) p.Meter("Experience", 100.0 * (m.Xp - from) / Math.Max(1, n - from), $"{m.Xp}/{n}");
        else p.Meter("Experience", 100, $"{m.Xp}");
        p.Note("You gain experience by seeing stories through. Each level lets you choose how you grow, and raises your proficiency bonus every four levels.");
        if (Sheet.Pending(t) > 0) LevelUp(p);

        p.Heading("Abilities");
        var a = Stories.Scores(t);
        string sig = $"ab{a}";
        p.Add(sig, () => AbilityRow(a));
        int prof = Stories.ProficiencyBonus(m.Level);
        int max = Sheet.InspirationMax(t);
        string pips = new string('●', Math.Min(m.Inspiration, max)) + new string('○', Math.Max(0, max - m.Inspiration));
        p.Text($"{Tag($"Proficiency +{prof}", Ui.Ink)}   {Tag($"Inspiration {pips}", m.Inspiration > 0 ? Ui.Gold : Ui.Ink2)}   {Ui.C(Ui.Ink2, $"[font_size=12]{m.Successes} won, {m.Failures} lost[/font_size]")}", 13);

        p.Heading("Inspiration");
        p.Note($"Spend one to reroll a failed d20 in a story. You hold up to {max}. Natural 20s earn it, and so does running the town well:");
        foreach (var d in r.Deeds)
        {
            var (have, need) = Sheet.Progress(t, d);
            string unit = d.Id == "content" ? $"days at {Js.Str(d.Threshold)}+ Mandate" : "";
            p.Text($"{Ui.C(Ui.Gold, "✦")} {Ui.Esc(d.Name)}  {Ui.C(Ui.Ink2, $"[font_size=12]{(need > 1 ? $"{Math.Max(0, have)} of {need}{(unit != "" ? " " + unit : "")}" : "each one")}[/font_size]")}", 13);
        }
        var feats = Sheet.Feats(t);
        if (feats.Count > 0)
        {
            p.Heading("Feats");
            foreach (var f in feats) p.Text($"[b]{Ui.Esc(f.Name)}[/b]  {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(f.Text)}[/font_size]")}", 13);
        }

        p.Heading("Background");
        if (Stories.CanChooseBackground(t))
        {
            p.Note("Who were you before the charter? Your background sets your abilities, your two trained skills, and the kind of story you have advantage in. You can choose until you first step into a story.");
            foreach (var b in r.Backgrounds)
            {
                var skills = string.Join(", ", b.Skills.Select(k => r.Skill(k)?.Name ?? k));
                string id = b.Ref;
                bool current = b.Ref == m.Background;
                p.Row($"[b]{Ui.Esc(b.Name)}[/b]{(current ? "  " + Tag("now", Ui.Gold) : "")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(skills)} · best at {Ui.Esc(string.Join(", ", b.Abilities.Take(2).Select(x => x.ToUpperInvariant())))}. {Ui.Esc(b.Text)}[/font_size]")}",
                    ("Choose", true, current, () => Act(() => _sim.ChooseBackground(id))));
            }
        }
        else p.Text($"[b]{Ui.Esc(bg.Name)}[/b]  {Tag(bg.Feature, Ui.Gold)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(bg.Text)}[/font_size]")}", 13);

        p.Heading("Skills");
        var lines = r.Skills.Select(sk =>
        {
            var (mod, pr, ab) = Stories.Bonus(t, sk.Id);
            int bonus = mod + pr;
            string dot = Stories.Expert(t, sk.Id) ? Ui.C(Ui.Gold, "◆") : Stories.Proficient(t, sk.Id) ? Ui.C(Ui.Gold, "●") : Ui.C(Ui.Line, "○");
            return $"{dot} {(pr > 0 ? $"[b]{Ui.Esc(sk.Name)}[/b]" : Ui.Esc(sk.Name))} {Ui.C(Ui.Ink2, $"[font_size=11]{ab.ToUpperInvariant()}[/font_size]")}  [b]{(bonus >= 0 ? "+" : "−")}{Math.Abs(bonus)}[/b]";
        }).ToList();
        int half = (lines.Count + 1) / 2;
        p.Add("sk" + string.Join("|", lines), () =>
        {
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 16);
            grid.AddThemeConstantOverride("v_separation", 2);
            for (int i = 0; i < half; i++)
            {
                grid.AddChild(SkillCell(lines[i]));
                grid.AddChild(SkillCell(i + half < lines.Count ? lines[i + half] : ""));
            }
            return grid;
        });

        var open = Stories.Pending(t);
        p.Heading("Waiting on you");
        if (open.Count == 0) p.Note("No story needs you now. They come up as the town lives: in its taverns, its council, its heroes and its wars.");
        foreach (var s in open)
        {
            var d = Stories.DefOf(t, s);
            var (word, color) = StakesLook(d.Stakes);
            int days = Math.Max(1, (int)Math.Ceiling((s.DueTick - t.Tick) / 24.0));
            int id = s.Id;
            p.Row($"[b]{Ui.Esc(Stories.Fill(t, s, d.Title))}[/b]\n{Tag(word, color)} {Ui.C(Ui.Ink2, $"[font_size=12]· runs its course in {Ui.Days(days)}[/font_size]")}", ("Step in", true, true, () => OpenStory(id)));
        }
        var decrees = Decrees.Active(t).ToList();
        if (decrees.Count > 0)
        {
            p.Heading("Decrees in force");
            foreach (var d in decrees)
                p.Text($"[b]{Ui.Esc(d.Name)}[/b]  {Tag(Ui.Days(Decrees.DaysLeft(t, d.Id)) + " left", Ui.Ink2)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(d.Text)}[/font_size]")}", 13);
        }
        var called = Callings.Holders(t).ToList();
        if (called.Count > 0)
        {
            p.Heading("Callings");
            p.Note("Parts heroes have taken up in the town through the stories of their class. Each lasts while its hero stays and stays loyal.");
            foreach (var (hero, c) in called)
                p.Text($"[b]{Ui.Esc(c.Name)}[/b]  {AgentLink(hero)} {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(hero.Hero!.Cls.Name.ToLowerInvariant())}[/font_size]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(c.Text)}[/font_size]")}", 13);
        }
        var remembered = t.Stories.Precedents.Keys.Select(id => r.Precedent(id)).OfType<PrecedentDef>().ToList();
        if (remembered.Count > 0)
        {
            p.Heading("The town remembers");
            p.Note("Hard choices are not forgotten. Later stories turn on them.");
            foreach (var pr in remembered)
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {t.Stories.Precedents[pr.Id]}[/font_size]")}  {Ui.Esc(pr.Text)}", 13);
        }
        var past = t.Stories.All.Where(s => s.Done).OrderByDescending(s => s.Id).Take(8).ToList();
        if (past.Count > 0)
        {
            p.Heading("Deeds");
            foreach (var s in past)
            {
                var b = s.Beats[^1];
                var color = b.Result switch { StoryResult.Critical or StoryResult.Success or StoryResult.Certain => Ui.Good, StoryResult.Partial => Ui.Gold, StoryResult.Ignored => Ui.Ink2, _ => Ui.Bad };
                string what = b.Result == StoryResult.Ignored ? "let it be" : b.Result == StoryResult.Certain ? "settled" : Stories.Word(b.Result);
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {b.Tick / 24 + 1}[/font_size]")}  {Ui.Esc(Stories.Fill(t, s, Stories.DefOf(t, s).Title))}  {Tag(what, color)}", 13);
            }
        }
        p.Commit();
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

    private static Control AbilityRow(Abilities a)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        foreach (var (key, name) in new[] { (Ability.Str, "STR"), (Ability.Dex, "DEX"), (Ability.Con, "CON"), (Ability.Int, "INT"), (Ability.Wis, "WIS"), (Ability.Cha, "CHA") })
        {
            int score = a[key], mod = Rng.Modifier(score);
            var cell = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
}
