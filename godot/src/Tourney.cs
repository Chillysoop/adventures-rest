using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The tourney card (<see cref="Tournaments"/>): the council's proposal with a button a level and its cost, then
/// the four events with their draws, each rider's odds in the game's coloured-odds style and a stake to put on
/// one of them, the heats as they are ridden and the latest pass thrown on two d20s, and the town's past champions.
/// It sits beside the map and never stops the clock: the heats are ridden hour by hour whether it is open or not.
/// </summary>
public partial class Main
{
    private PanelContainer _tourney = null!;
    private Pane _tourneyPane = null!;
    private VBoxContainer _tourneyDice = null!;
    private Label _tourneyTitle = null!;

    /// <summary>The stake picked on the card (an index into the betting stakes), and the heat whose pass the dice show.</summary>
    private int _tourneyStake = 1;
    private (int Event, int Heats) _tourneyRolled = (-1, -1);

    private void BuildTourney()
    {
        _tourney = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(480, 0), ZIndex = 18, MouseFilter = MouseFilterEnum.Stop };
        _tourney.AddThemeStyleboxOverride("panel", Castle.Oak(20, 18));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        _tourney.AddChild(v);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new TextureRect { Texture = Ui.Icon("d20"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(26, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        _tourneyTitle = Ui.Label("THE TOURNEY", Ui.Gold, Ui.Bold, 12);
        _tourneyTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tourneyTitle.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(_tourneyTitle);
        var look = new Button { Text = "Show the lists", FocusMode = FocusModeEnum.None, TooltipText = "Look at the tourney grounds" };
        look.Pressed += LookAtLists;
        head.AddChild(look);
        var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
        close.Pressed += CloseTourney;
        head.AddChild(close);
        v.AddChild(head);

        _tourneyDice = new VBoxContainer { Visible = false };
        _tourneyDice.AddThemeConstantOverride("separation", 2);
        v.AddChild(_tourneyDice);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 520) };
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(box);
        v.AddChild(scroll);
        _tourneyPane = new Pane(box);

        AddChild(_tourney);
        _tourney.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize);
        _tourney.GrowHorizontal = GrowDirection.Begin;
        _tourney.OffsetTop = 96;
        _tourney.OffsetRight = -18;
    }

    private void OpenTourney()
    {
        _tourneyPane.Invalidate();
        _tourney.Visible = true;
        _tourneyRolled = (-1, -1);
        MoveChild(_tourney, GetChildCount() - 1);
        RenderTourney();
        GuideEvent("tourney");
    }

    private void CloseTourney()
    {
        _tourney.Visible = false;
        Ui.Clear(_tourneyDice);
        _tourneyDice.Visible = false;
    }

    private void LookAtLists()
    {
        var st = _sim.Town.Tourney;
        if (st.Stage is TourneyStage.Running or TourneyStage.Over or TourneyStage.Funded && (st.X != 0 || st.Y != 0))
            Look(new Vector2(Geo.Margin + (float)st.X, Geo.Margin + (float)st.Y), 9);
        else Look(Geo.Center, 20);
    }

    /// <summary>Keep the card up to date while it is open: the dice for a new heat at once, the rest as the hours pass.</summary>
    private void SyncTourney()
    {
        if (!_tourney.Visible) return;
        var st = _sim.Town.Tourney;
        int live = st.Events.FindIndex(e => e.Started && !e.Done);
        if (live < 0) live = st.Events.FindLastIndex(e => e.Started);
        if (live >= 0 && (live, st.Events[live].Heats.Count) != _tourneyRolled)
        {
            _tourneyRolled = (live, st.Events[live].Heats.Count);
            RollHeat(st.Events[live]);
        }
        if (_dirty) RenderTourney();
    }

    /// <summary>The latest heat's last pass on two dice, each rider's d20 and bonus against the other's total.</summary>
    private void RollHeat(TourneyEvent e)
    {
        Ui.Clear(_tourneyDice);
        var t = _sim.Town;
        var h = e.Heats[^1];
        if (h.Walkover || h.Passes.Count == 0)
        {
            _tourneyDice.Visible = false;
            return;
        }
        var p = h.Passes[^1];
        var d = Tournaments.DefOf(t, e);
        string round = h.Passes.Count <= d.Rounds.Count ? d.Rounds[h.Passes.Count - 1].Text : "a deciding pass";
        foreach (bool first in new[] { true, false })
        {
            var who = Tournaments.Rider(t, first ? h.A : h.B);
            int die = first ? p.DieA : p.DieB, bonus = first ? p.BonusA : p.BonusB;
            int mine = die + bonus, theirs = first ? p.TotalB : p.TotalA;
            bool won = mine > theirs;
            var beat = new StoryBeat
            {
                Tick = h.Tick + (first ? 0 : 1),
                Die = die,
                Modifier = bonus,
                Total = mine,
                Dc = theirs + 1,
                Result = won ? die == 20 ? StoryResult.Critical : StoryResult.Success : die == 1 ? StoryResult.Fumble : StoryResult.Failure,
            };
            string ability = Tournaments.AbilityFor(t, who, p.Ability).ToUpperInvariant();
            var roll = new DiceRoll(beat, [new DiceRoll.Term(bonus, ability)], $"{Short(who.Name)} · {round}", true, InstantRolls, advantage: false);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var name = Ui.Rich($"[b]{Ui.Esc(Short(who.Name))}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=11]{Ui.Esc(round)}[/font_size]")}", 13);
            name.CustomMinimumSize = new Vector2(120, 0);
            name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(name);
            row.AddChild(roll);
            _tourneyDice.AddChild(row);
        }
        _tourneyDice.Visible = true;
    }

    private static string Short(string name)
    {
        int sp = name.IndexOf(' ');
        return sp > 0 ? name[..sp] : name;
    }

    private void RenderTourney()
    {
        var t = _sim.Town;
        var st = t.Tourney;
        var r = Tournaments.Rules(t);
        var p = _tourneyPane;
        var level = Tournaments.LevelOf(t);
        string season = t.Content.Seasons.Seasons.FirstOrDefault(s => s.Id == r.Season)?.Name ?? "";
        _tourneyTitle.Text = $"THE {season.ToUpperInvariant()} TOURNEY  ·  YEAR {Math.Max(1, st.Year)}{(level != null ? "  ·  " + level.Name.ToUpperInvariant() : "")}";
        p.Begin();
        switch (st.Stage)
        {
            case TourneyStage.None:
                p.Card(Ui.C(Ui.Ink2, $"No tourney yet. Each {Ui.Esc(r.Season)}, once {Ui.Esc(t.Name)} is a {Ui.Esc(City.TierName(t, r.Tier).ToLowerInvariant())}, the council proposes one."));
                break;
            case TourneyStage.Skipped:
                p.Card(Ui.C(Ui.Ink2, "There is no tourney this year. The council will propose one again next summer."));
                break;
            case TourneyStage.Proposed:
                Proposal(p);
                break;
            default:
                Lists(p);
                break;
        }
        if (st.Honours.Count > 0)
        {
            p.Heading("Champions of the lists");
            foreach (var h in st.Honours.AsEnumerable().Reverse().Take(6))
                p.Text($"{Ui.C(Ui.Gold, "◆")} [b]{Ui.Esc(h.Name)}[/b] {Ui.C(Ui.Ink2, $"of {Ui.Esc(h.From)} · {Ui.Esc(h.Event)}, year {h.Year}")}", 12);
        }
        p.Commit();
    }

    private void Proposal(Pane p)
    {
        var t = _sim.Town;
        var st = t.Tourney;
        var r = Tournaments.Rules(t);
        int days = Math.Max(1, st.OpenDay - t.Day);
        p.Card($"[b]The council proposes a tourney.[/b] {Ui.C(Ui.Ink2, $"The lists open in {Ui.Days(days)}: {string.Join(", ", r.Events.Select(e => e.Name))}. Champions come from the neighbouring settlements to ride against the town's heroes. Left unanswered, the council holds a modest one if the treasury can bear it.")}", Ui.Gold);
        bool arena = Tournaments.HasArena(t);
        if (arena) p.Note($"The arena makes it {Js.Str(Math.Round((r.Arena.Scale - 1) * 100))}% bigger: more visitors, more cheer, more renown.");
        for (int i = 0; i < r.Levels.Count; i++)
        {
            var l = r.Levels[i];
            double cost = Tournaments.Cost(t, l);
            int level = i;
            p.Row($"[b]{Ui.Esc(l.Name)}[/b] · {Js.Str(cost)}g\n" +
                  Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(l.Text)}[/font_size]") + "\n" +
                  $"[font_size=12]{l.Entrants} riders an event · visitors ×{Js.ToFixed(l.Tourists, 1)} · +{Js.Str(l.Mood)} mood · winner's renown +{l.Renown[0]} · purse {Js.Str(l.Purse)}g[/font_size]",
                ("Fund", t.Treasury >= cost, i == 1, () => Act(() => _sim.FundTourney(level))));
        }
        p.Row($"[b]Skip it this year[/b]\n" + Ui.C(Ui.Ink2, $"[font_size=12]Saves the gold. The town is let down: −{Js.Str(r.Skip.Mandate)} mandate and a sour week.[/font_size]"),
            ("Skip", true, false, () => Act(() => _sim.SkipTourney())));
    }

    private void Lists(Pane p)
    {
        var t = _sim.Town;
        var st = t.Tourney;
        var r = Tournaments.Rules(t);
        string where = st.Arena ? "in the arena" : "outside the town";
        string state = st.Stage switch
        {
            TourneyStage.Funded => $"The lists go up {where} in {Ui.Days(Math.Max(1, st.OpenDay - t.Day))}.",
            TourneyStage.Running => $"The lists stand {where}. {(st.Visitors > 0 ? $"{Js.Str(st.Visitors)} visitors so far have paid {Js.Str(st.GateGold)}g to watch." : "The crowds are coming in.")}",
            _ => $"The tourney is over. {Js.Str(st.Visitors)} visitors came and paid {Js.Str(st.GateGold)}g to watch.",
        };
        string bets = st.Winnings != 0 ? $" Your bets, all told: {(st.Winnings >= 0 ? Ui.C(Ui.Good, $"+{Js.Str(st.Winnings)}g") : Ui.C(Ui.Bad, $"−{Js.Str(-st.Winnings)}g"))}." : "";
        p.Card($"{Ui.Esc(state)}{bets}\n{Ui.C(Ui.Ink2, $"[font_size=12]Cost {Js.Str(st.Cost)}g. One stake an event, before its first heat. The clock runs on.[/font_size]")}", Ui.Gold);

        // The stake to put on a rider.
        var stakes = r.Betting.Stakes.Where(s => s <= Tournaments.MaxStake(t)).ToList();
        if (stakes.Count > 0 && st.Events.Any(e => Tournaments.BetBlocked(t, e) == null))
        {
            _tourneyStake = Math.Clamp(_tourneyStake, 0, stakes.Count - 1);
            p.Choice("Stake", stakes.Select(s => $"{Js.Str(s)} gold").ToList(), _tourneyStake, i =>
            {
                _tourneyStake = i;
                _dirty = true;
            });
        }
        double stake = stakes.Count > 0 ? stakes[Math.Clamp(_tourneyStake, 0, stakes.Count - 1)] : 0;

        int focus = st.Events.FindIndex(e => !e.Done);
        for (int i = 0; i < st.Events.Count; i++)
        {
            var e = st.Events[i];
            var d = Tournaments.DefOf(t, e);
            string when = e.Done ? "won" : e.Day == t.Day ? e.Started ? "riding now" : "today" : e.Day == t.Day + 1 ? "tomorrow" : $"day {Seasons.DayOf(t) + e.Day - t.Day} of {Seasons.Current(t).Name.ToLowerInvariant()}";
            p.Heading($"{d.Name} · {when}");
            if (e.Done && i != focus)
            {
                var w = Tournaments.Rider(t, e.Winner!.Value);
                p.Text($"{Ui.C(Ui.Gold, "♛")} [b]{Ui.Esc(w.Name)}[/b] {Ui.C(Ui.Ink2, "of " + Ui.Esc(w.From))}{BetLine(e)}", 13);
                continue;
            }
            if (i != focus && i != focus + 1)
            {
                p.Text(Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(d.Blurb)}[/font_size]") + BetLine(e), 12);
                continue;
            }
            p.Text(Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(d.Blurb)} {Ui.Esc(string.Join(", ", d.Rounds.Select(x => $"{x.Text} ({(x.Ability == "cast" ? "casting" : x.Ability.ToUpperInvariant())})")))}.[/font_size]") + BetLine(e), 12);
            Riders(p, i, e, stake);
            foreach (var h in e.Heats) p.Text(HeatLine(e, h), 12);
        }
    }

    /// <summary>What the mayor has on an event, if anything.</summary>
    private string BetLine(TourneyEvent e)
    {
        if (e.Bet is not { } b) return "";
        var t = _sim.Town;
        var on = Tournaments.Rider(t, b.Entrant);
        string how = !b.Settled ? Ui.C(Ui.Gold, $"{Js.Str(b.Stake)}g on {Ui.Esc(Short(on.Name))} at {Js.ToFixed(b.Odds, 1)}×")
            : b.Paid > 0 ? Ui.C(Ui.Good, $"{Js.Str(b.Stake)}g on {Ui.Esc(Short(on.Name))} won {Js.Str(b.Paid)}g")
            : Ui.C(Ui.Bad, $"{Js.Str(b.Stake)}g on {Ui.Esc(Short(on.Name))} lost");
        return $"\n[font_size=12]Your stake: {how}[/font_size]";
    }

    /// <summary>The draw: each rider, where from, their class and bonus, their odds as the game's coloured bar, and a stake button while betting is open.</summary>
    private void Riders(Pane p, int index, TourneyEvent e, double stake)
    {
        var t = _sim.Town;
        var d = Tournaments.DefOf(t, e);
        var chances = Tournaments.Chances(t, e);
        string? blocked = Tournaments.BetBlocked(t, e);
        var lost = e.Heats.Select(h => h.Winner == h.A ? h.B : h.A).ToHashSet();
        foreach (int id in e.Draw)
        {
            var en = Tournaments.Rider(t, id);
            double chance = chances[id];
            var odds = Assess.Odds(t, chance);
            var color = chance <= 0 ? Ui.Ink2 : Foes.Better(odds);
            bool out_ = lost.Contains(id);
            string cls = t.Content.Classes.FirstOrDefault(c => c.Id == en.Class)?.Name ?? en.Class;
            string bonus = string.Join(" ", d.Rounds.Select(x => x.Ability).Distinct().Select(a => $"{Tournaments.AbilityFor(t, en, a).ToUpperInvariant()} {Skills.Sign(Tournaments.Bonus(t, en, a))}"));
            string away = Tournaments.Absent(t, en) ? "  " + Ui.C(Ui.Bad, "away") : "";
            string home = en.AgentId != null ? Ui.C(Ui.Gold, " ★") : "";
            string text = $"[b]{(out_ ? Ui.C(Ui.Ink2, Ui.Esc(en.Name)) : Ui.Esc(en.Name))}[/b]{home}{away}{(e.Winner == id ? "  " + Ui.C(Ui.Gold, "♛ winner") : "")}" +
                          Ui.C(Ui.Ink2, $"[font_size=12]  {Ui.Esc(en.From)} · {en.Level} {Ui.Esc(cls.ToLowerInvariant())}{(en.Fit ? "" : " (out of their art)")}[/font_size]");
            int rider = id;
            string pays = chance > 0 ? $"{Js.ToFixed(Tournaments.Odds(t, chance), 1)}×" : "—";
            (string, bool, bool, Action)[] buttons = blocked == null && chance > 0 && stake > 0
                ? [($"Bet {Js.Str(stake)} · pays {pays}", true, false, () => Act(() => _sim.PlaceBet(index, rider, stake)))]
                : [];
            string sig = $"{Math.Round(chance * 1000)}|{odds.Name}|{out_}";
            p.Row(text, (sig + bonus, (Func<Control>?)null, () =>
            {
                var under = new HBoxContainer();
                under.AddThemeConstantOverride("separation", 12);
                if (out_) under.AddChild(Ui.Label("Out of it", Ui.Ink2, Ui.Bold, 11));
                else
                {
                    under.AddChild(Foes.Bar("Odds", odds, color, $"{Math.Round(chance * 100)}% to win {d.Name}. A winning stake pays {pays}."));
                    under.AddChild(Ui.Label($"{Math.Round(chance * 100)}%", color, Ui.Bold, 11));
                }
                under.AddChild(Ui.Label(bonus, Ui.Ink2, Ui.Medium, 11));
                return under;
            }), buttons);
        }
    }

    /// <summary>"Round 1 · Brannoc beat Ellyjobell 2–1 (14 v 9, 18 v 19, 12 v 7)".</summary>
    private string HeatLine(TourneyEvent e, Heat h)
    {
        var t = _sim.Town;
        int rounds = (int)Math.Round(Math.Log2(Math.Max(2, e.Draw.Count)));
        string stage = h.Round == rounds - 1 ? "Final" : h.Round == rounds - 2 ? "Semi-final" : $"Round {h.Round + 1}";
        var a = Tournaments.Rider(t, h.A);
        var b = Tournaments.Rider(t, h.B);
        var w = h.Winner == h.A ? a : b;
        var l = h.Winner == h.A ? b : a;
        if (h.Walkover) return $"{Ui.C(Ui.Ink2, stage + " ·")} {Ui.Esc(Short(w.Name))} {Ui.C(Ui.Ink2, $"by walkover: {Ui.Esc(Short(l.Name))} never came")}";
        string passes = string.Join(", ", h.Passes.Select(x => h.Winner == h.A ? $"{x.TotalA} v {x.TotalB}" : $"{x.TotalB} v {x.TotalA}"));
        int sw = Math.Max(h.ScoreA, h.ScoreB), sl = Math.Min(h.ScoreA, h.ScoreB);
        return $"{Ui.C(Ui.Ink2, stage + " ·")} [b]{Ui.Esc(Short(w.Name))}[/b] beat {Ui.Esc(Short(l.Name))} {sw}–{sl} {Ui.C(Ui.Ink2, $"({passes})")}";
    }

    /// <summary>--tourney N: put a tourney of that level on now, stake on a rider of the --tourney-day'th event (a hero of the town's if one rides), ride into its heats, and open the card over the lists.</summary>
    private void StageTourney(int level)
    {
        var t = _sim.Town;
        if (!Tournaments.Stage(t, level)) return;
        int ev = Math.Clamp(_opts.TourneyDay, 0, t.Tourney.Events.Count - 1);
        var e = t.Tourney.Events[ev];
        var chances = Tournaments.Chances(t, e);
        int pick = e.Draw.OrderByDescending(id => Tournaments.Rider(t, id).AgentId != null).ThenByDescending(id => chances[id]).ThenBy(id => id).First();
        _sim.PlaceBet(ev, pick, Tournaments.Rules(t).Betting.Stakes.Where(s => s <= Tournaments.MaxStake(t)).DefaultIfEmpty(10).Max());
        // On to that event's day, a few heats in.
        int day = t.Day + ev;
        for (int i = 0; i < 24 * 3 && !(t.Day == day && t.Hour >= _opts.TourneyHour); i++) _sim.Step();
        _dirty = true;
        if (_opts.TourneyCard) OpenTourney();
        var st = t.Tourney;
        // Off to the right of the lists, so they sit clear of the card.
        _opts.Look ??= new Vector2(Geo.Margin + (float)st.X + (_opts.TourneyCard ? (_opts.Cam ?? 12) * 0.2f : 0), Geo.Margin + (float)st.Y);
        _map.LookAt(_opts.Look.Value, _opts.Cam ?? 12);
    }
}
