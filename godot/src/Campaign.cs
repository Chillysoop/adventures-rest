using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>The campaign window: which act the story is in, the threats festering out in the land, the town's dread, and the yoke and the big bad once they exist.</summary>
public partial class Main
{
    private static readonly Dictionary<MenaceStage, (string Word, Color Color)> MenaceStages = new()
    {
        [MenaceStage.Lair] = ("Lair", Ui.Ink2),
        [MenaceStage.Rising] = ("Rising", Ui.Gold),
        [MenaceStage.Power] = ("A power", Ui.Accent),
        [MenaceStage.Dominion] = ("Dominion", Ui.Bad),
        [MenaceStage.Overlord] = ("Overlord", Ui.Bad),
    };

    private static readonly Dictionary<Tone, (string Word, string Text, Color Color)> Tones = new()
    {
        [Tone.Hopeful] = ("Hopeful", "The town believes in its heroes.", Ui.Good),
        [Tone.Uneasy] = ("Uneasy", "People talk about the roads in low voices.", Ui.Gold),
        [Tone.Grim] = ("Grim", "Now and then a family packs a cart and takes the south road.", Ui.Accent),
        [Tone.Dark] = ("Dark", "Families leave often, untried heroes desert, and evil gods gain ground where a cult is active. If dread stays near the top, the town will be abandoned.", Ui.Bad),
    };

    private void RenderCampaign()
    {
        var p = _panes[Tab.Campaign];
        var t = _sim.Town;
        var s = t.Saga;
        var acts = t.Content.Saga.Acts;
        p.Begin();
        var teller = Storyteller.Of(t);
        p.Text($"{Tag(teller.Title, Ui.Ink2)} Told by [b]{Ui.Esc(teller.Name)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(teller.Character)}[/font_size]")}", 13);
        if (!s.Enabled || acts.Count == 0)
        {
            p.Note("This town plays the short campaign: it ends at the Last Stand.");
            p.Commit();
            return;
        }

        int champ = Saga.ChampionLevel(t);
        string next = "";
        if (s.Act + 1 < acts.Count)
        {
            var n = acts[s.Act + 1];
            string gate = s.Act == 0 ? "once the Last Stand is settled and " : "when ";
            next = $"\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(n.Name)} begins {gate}your champions reach level {n.OpensAtLevel} (they are level {champ} now), no sooner than day {n.EarliestDay}. By day {n.LatestDay} it comes anyway.[/font_size]")}";
        }
        else next = $"\n{Ui.C(Ui.Ink2, $"[font_size=12]The last act. Your champions are level {champ}.[/font_size]")}";
        p.Card($"[font_size=17][b]{Ui.Esc(Saga.ActTitle(t, s.Act))}[/b][/font_size]  {Tag($"since day {s.ActDays[^1]}")}{next}");

        if (Menaces.PendingDemand(t) is { } dm)
        {
            var am = Menaces.AmbitionOf(t, dm);
            var y = t.Content.Saga.Yoke;
            int days = dm.DemandDueTick is int due ? (int)Math.Ceiling((due - t.Tick) / 24.0) : 0;
            FoeCard(p, [dm], $"{Tag("A DEMAND FOR FEALTY", Ui.Accent)}\n[b]{Ui.Esc(Ui.Cap(dm.Name))}[/b] wants {Ui.Esc(t.Name)} as its vassal: {Math.Round(y.Share * 100)}% of the treasury every {Ui.Days(y.EveryDays)}, and its raiders stay away.\n{Ui.C(Ui.Ink2, $"[font_size=12]Refuse and its host marches on the walls. Say nothing for {Ui.Days(Math.Max(1, days))} and that is a refusal.[/font_size]")}", Ui.Accent);
            int id = dm.Id;
            p.Buttons(("Bend the knee", true, false, () => Act(() => _sim.AnswerDemand(id, true))), ("Refuse", true, true, () => Act(() => _sim.AnswerDemand(id, false))));
        }

        var big = Saga.BigBad(t);
        if (big is { Alive: true })
        {
            string march = big.ArrivalTick is int at && at > t.Tick ? $"\n{Ui.C(Ui.Bad, $"[b]Its host reaches the walls in {Ui.Days((int)Math.Ceiling((at - t.Tick) / 24.0))}.[/b]")}" : "";
            FoeCard(p, [big], $"{Tag("THE BIG BAD", Ui.Bad)}\n[font_size=16][b]{Ui.Esc(Ui.Cap(big.Name))}[/b][/font_size]  {Tag($"level {big.Level}")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Seat(t, big)} {(big.Marches == 0 ? "It has not marched on the town yet." : $"It has marched on the town {Times(big.Marches)}.")} Kill it to end the Long Night.[/font_size]")}{march}", Ui.Bad);
        }
        if (s.YokeId is int yid && s.Menaces[yid - 1] is { Alive: true } yoke)
        {
            var y = t.Content.Saga.Yoke;
            string missed = s.MissedTribute > 0 ? $" Tribute missed {Times(s.MissedTribute)} of {y.MissedToMarch} before it marches." : "";
            FoeCard(p, [yoke], $"{Tag("UNDER THE YOKE", Ui.Accent)}\n[b]{Ui.Esc(Ui.Cap(yoke.Name))}[/b] takes {Math.Round(y.Share * 100)}% of the treasury every {Ui.Days(y.EveryDays)}.{missed}\n{Ui.C(Ui.Ink2, "[font_size=12]Kill its leader to break the yoke. The walls break a second time, and the town falls.[/font_size]")}", Ui.Accent);
        }

        RenderCrises(p, t);

        p.Heading("Dread");
        var tone = Tones[s.Tone];
        p.Meter(tone.Word, s.Dread, $"{Math.Round(s.Dread)}", s.Tone >= Tone.Grim);
        p.Note($"{tone.Text} Threats growing out in the land, razed settlements, raids and tribute raise it; victories bring it down.{(s.Sacks > 0 ? $" Sacked {Times(s.Sacks)}." : "")}");

        p.Heading("Threats in the land");
        var known = s.Menaces
            .Where(m => m.Alive && !m.BigBad && (m.Named || m.Origin == Menaces.FromWarband))
            .OrderByDescending(m => m.Stage)
            .ThenByDescending(m => m.Power)
            .ThenBy(m => m.Id)
            .ToList();
        if (known.Count == 0) p.Note("No named threat is abroad. Lairs you hear of but leave alone will fester into one.");
        foreach (var m in known)
        {
            var (word, color) = MenaceStages[m.Stage];
            string march = m.ArrivalTick is int at && at > t.Tick ? "  " + Tag($"marching, {Ui.Days((int)Math.Ceiling((at - t.Tick) / 24.0))} out", Ui.Bad) : "";
            string held = m.Holdings.Count > 0 ? $" It holds {m.Holdings.Count} more {(m.Holdings.Count == 1 ? "lair" : "lairs")}." : "";
            FoeCard(p, [m], $"{Ui.KindIcon(m.Kind, 20)}[b]{Ui.Esc(Ui.Cap(m.Name))}[/b]  {Tag(word, color)}  {Tag($"level {m.Level}")}{march}\n{Leader(m)}{Tag(Menaces.CunningOf(t, m).Name)}  {Tag(Menaces.AmbitionOf(t, m).Name)}{Temper(t, m)}{Heirs(t, m)}\n{Scores(t, m)}{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Annals.Motive(t, m))} {Ui.Esc(Wants(t, m))} {Seat(t, m)}{held}[/font_size]")}{Remembers(t, m)}{LastHeard(t, m)}{Plan(t, m)}{Assets(t, m)}{Backers(t, m)}", color);
        }
        p.Note("A threat grows while nobody clears its seat, as far as its wits allow: a beast only hunts, a dull band takes a valley and stops, a cunning or masterful leader builds a domain and can become the big bad. What it wants decides what it does to the town. Clearing the seat breaks it, and it falls back to a lair it holds.");

        var fallen = s.Menaces.Where(m => !m.Alive && m.Named).OrderByDescending(m => m.EndedDay).ThenBy(m => m.Id).Take(5).ToList();
        if (fallen.Count > 0)
        {
            p.Heading("Destroyed or turned");
            foreach (var m in fallen)
            {
                string turned = m.Redeemed ? "  " + Tag($"{m.Leader} joined {t.Name}", Ui.Good) : "";
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=12]Day {m.EndedDay}[/font_size]")}  {Ui.KindIcon(m.Kind, 16)}{Ui.Esc(Ui.Cap(m.Name))}{turned}", 13);
            }
        }
        p.Commit();
    }

    private static readonly Dictionary<string, Color> Severities = new() { ["lesser"] = Ui.Gold, ["great"] = Ui.Accent, ["dire"] = Ui.Bad };

    /// <summary>End-game crises under way, battles between enemies the town can join, and the foes sworn to revenge.</summary>
    private void RenderCrises(Pane p, Town t)
    {
        var crises = Crises.Active(t).OrderBy(c => Crises.DaysLeft(t, c)).ThenBy(c => c.Id).ToList();
        var clashes = Crises.Pending(t).OrderBy(x => x.Id).ToList();
        var grudges = t.Grudges.Where(g => g.Heat > 0 || g.Marked.Count > 0).OrderByDescending(g => g.Heat).ThenBy(g => g.Foe, StringComparer.Ordinal).ToList();
        if (crises.Count == 0 && clashes.Count == 0 && grudges.Count == 0) return;
        p.Heading("Crises");
        foreach (var c in crises)
        {
            var m = Crises.MenaceOf(t, c);
            var sv = Crises.SeverityOf(t, c);
            var color = Severities.GetValueOrDefault(sv.Id, Ui.Accent);
            int days = Crises.DaysLeft(t, c);
            string phase = c.Phase switch
            {
                CrisisPhase.Gathering => "Gathering its strength",
                CrisisPhase.Onslaught => "Striking at the town",
                _ => "Its host is marching on the walls",
            };
            string when = c.Phase == CrisisPhase.Marching ? $"It reaches the walls in {Ui.Days(Math.Max(1, days))}." : $"It culminates in about {Ui.Days(days)}, with its host at the walls.";
            string blows = c.BlowsLanded + c.BlowsStopped == 0 ? "" : $" Blows so far: {c.BlowsLanded} landed, {c.BlowsStopped} stopped.";
            FoeCard(p, [m], $"{Tag($"{sv.Name.ToUpperInvariant()} CRISIS", color)}  {Tag(phase)}\n[font_size=16][b]{Ui.Esc(Ui.Cap(Crises.Title(t, c)))}[/b][/font_size] of {Ui.Esc(m.Name)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Crises.KindOf(t, m).What)} {when}{blows} Break its seat first and the crisis dies with it. {Seat(t, m)}[/font_size]")}", color);
        }
        foreach (var x in clashes)
        {
            var a = t.Saga.Menaces[x.A - 1];
            var b = t.Saga.Menaces[x.B - 1];
            int id = x.Id, aid = a.Id, bid = b.Id;
            FoeCard(p, [a, b], $"{Tag("ENEMIES CLASH", Ui.Accent)}\n[b]{Ui.Esc(Ui.Cap(a.Name))}[/b] and [b]{Ui.Esc(b.Name)}[/b] will meet in battle in {Ui.Esc(x.Where)} in {Ui.Days(Math.Max(1, x.Day - t.Day))}.\n{Ui.C(Ui.Ink2, "[font_size=12]Ride out with the heroes at home and part of the watch and fall on one of them while it fights the other. Some of the watch may not come back, and the one you strike will want revenge; the other holds its hand for a while. Say nothing and the town keeps behind its walls.[/font_size]")}", Ui.Accent);
            p.Buttons(($"Strike {Short(a)}", true, false, () => Act(() => _sim.JoinClash(id, aid))), ($"Strike {Short(b)}", true, false, () => Act(() => _sim.JoinClash(id, bid))), ("Keep behind the walls", true, true, () => Act(() => _sim.JoinClash(id, 0))));
        }
        foreach (var g in grudges)
        {
            var marked = g.Marked.Select(t.Agent).Where(h => !h.Dead && !h.Departed).OrderBy(h => h.Id).Select(h => h.Name).ToList();
            string names = marked.Count <= 4 ? string.Join(", ", marked) : $"{string.Join(", ", marked.Take(3))} and {marked.Count - 3} more";
            string who = marked.Count == 0 ? "" : $" It has marked {Ui.Esc(names)} for death.";
            p.Row($"{Tag("Vengeance", Ui.Bad)} [b]{Ui.Esc(Ui.Cap(g.Name))}[/b] has sworn revenge on {Ui.Esc(t.Name)}.{who}\n{Ui.C(Ui.Ink2, "[font_size=12]Its agents come for marked heroes in town first, then councillors, houses, the treasury and the walls. A strong watch catches them; the grudge cools in time, and dies with its master.[/font_size]")}");
        }
    }

    /// <summary>A card with the enemy's portrait (two, for a clash) beside the text and its threat bar under it.</summary>
    private void FoeCard(Pane p, Menace[] foes, string bbcode, Color border)
    {
        var t = _sim.Town;
        var threats = foes.Select(m => m.Alive ? Assess.Threat(t, m).Id : "").ToList();
        p.Add($"foe{string.Join(",", foes.Select(m => m.Id))}|{string.Join(",", threats)}|{foes.Count(m => m.BigBad)}{bbcode}{border}", () =>
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, border, 10, 1, 12, 10));
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 12);
            var faces = new VBoxContainer();
            faces.AddThemeConstantOverride("separation", 6);
            foreach (var m in foes) faces.AddChild(Foes.Of(t, m, foes.Length > 1 ? 48 : 64));
            h.AddChild(faces);
            var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            v.AddThemeConstantOverride("separation", 6);
            v.AddChild(Ui.Rich(bbcode));
            // The threat bars right under the heading line, where the eye lands first.
            int at = 1;
            foreach (var m in foes.Where(m => m.Alive))
            {
                var bar = Foes.Threat(t, m);
                if (foes.Length > 1) bar.AddChild(Ui.Label(Short(m), Ui.Ink2, Ui.Medium, 11));
                v.AddChild(bar);
                v.MoveChild(bar, at++ - 1);
            }
            h.AddChild(v);
            panel.AddChild(h);
            return panel;
        });
    }

    /// <summary>Who leads it, and their right hand.</summary>
    private static string Leader(Menace m) =>
        m.Leader == "" ? "" : $"[font_size=13]Led by [b]{Ui.Esc(m.Leader)}[/b]{(m.Lieutenant != "" ? $", with {Ui.Esc(m.Lieutenant)} at their side" : "")}[/font_size]  ";

    /// <summary>What it holds against the town: the wrongs it remembers, its grudge and who it has marked.</summary>
    private static string Remembers(Town t, Menace m)
    {
        var parts = m.Memories.AsEnumerable().Reverse().Take(3).ToList();
        if (Vengeance.Of(t, Vengeance.Key(m)) is { } g)
        {
            var marked = g.Marked.Select(t.Agent).Where(h => !h.Dead && !h.Departed).OrderBy(h => h.Id).Select(h => h.Name).Take(3).ToList();
            if (marked.Count > 0) parts.Add($"has marked {string.Join(", ", marked)} for death");
            else if (g.Heat >= t.Content.Crises.Vengeance.StrikeAt) parts.Add("is sending knives");
        }
        if (m.Repulsed > 0) parts.Add(m.Repulsed == 1 ? "will come back bigger after its last raid was driven off" : $"has been driven off {m.Repulsed} times running, and comes bigger each time");
        if (parts.Count == 0) return "";
        return $"\n[font_size=12]{Tag("Remembers", Ui.Bad)} {Ui.Esc(Ui.Cap(string.Join("; ", parts)))}.[/font_size]";
    }

    /// <summary>Its kind's temper this campaign.</summary>
    private static string Temper(Town t, Menace m) => Character.TemperOf(t, m.Kind) is { } tm ? "  " + Tag(tm.Name, Ui.Accent) : "";

    /// <summary>Its six scores, and what its kind is like this age.</summary>
    private static string Scores(Town t, Menace m)
    {
        string line = $"[font_size=12]{string.Join("  ", Character.Sheet(t, m).Select(ScoreTag))}[/font_size]\n";
        if (Character.TemperOf(t, m.Kind) is { Text: not "" } tm) line += Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(tm.Text)}[/font_size]\n");
        if (Menaces.Done(t, m) is string done) line += $"[font_size=12]{Tag("Done")} {Ui.C(Ui.Ink2, Ui.Esc(done))}[/font_size]\n";
        return line;
    }

    /// <summary>The last the town heard of it.</summary>
    private static string LastHeard(Town t, Menace m) =>
        m.LastHeard == "" ? "" : $"\n[font_size=12]{Tag($"Last heard, day {m.LastHeardDay}")} {Ui.C(Ui.Ink2, Ui.Esc(m.LastHeard))}[/font_size]";

    /// <summary>A menace by its leader's name, for a button.</summary>
    private static string Short(Menace m) => m.Leader;

    /// <summary>What it is working toward, once the town has learned of it, and how long the town has to stop it.</summary>
    private static string Plan(Town t, Menace m)
    {
        if (m.Plan is not { Known: true } plan) return "";
        int days = Math.Max(0, plan.DueDay - t.Day);
        return $"\n[font_size=13]{Tag("Plan", Ui.Gold)} {Ui.Esc(Plots.Describe(t, m))} {Ui.C(days <= 8 ? Ui.Bad : Ui.Ink2, $"({Ui.Days(days)} left)")}[/font_size]";
    }

    /// <summary>What its strength rests on, and where each is kept, so the player knows which holding to hit.</summary>
    private static string Assets(Town t, Menace m)
    {
        if (m.Assets.Count == 0) return "";
        var parts = m.Assets.Select(a =>
        {
            string name = Plots.AssetOf(t, a)?.Name ?? a.Def;
            var site = Quests.Site(t, a.SiteId);
            string where = site == null ? "" : site.Known == SiteKnowledge.Unheard ? " somewhere unknown" : $" at {site.Def.Name}";
            return $"{name}{where}";
        });
        return $"\n[font_size=12]{Tag("Rests on")} {Ui.Esc(string.Join(" · ", parts))}[/font_size]";
    }

    /// <summary>The minor powers it has bought, or is courting.</summary>
    private static string Backers(Town t, Menace m)
    {
        var bought = Powers.Backing(t, m).Select(p => Powers.Name(t, p)).ToList();
        var courting = t.Powers.Where(p => p.CourtedBy == m.Id).Select(p => Powers.Name(t, p)).ToList();
        string line = "";
        if (bought.Count > 0) line += $"\n[font_size=12]{Tag("Backed by", Ui.Bad)} {Ui.Esc(string.Join(" · ", bought))}[/font_size]";
        if (courting.Count > 0) line += $"\n[font_size=12]{Tag("Courting", Ui.Gold)} {Ui.Esc(string.Join(" · ", courting))}[/font_size]";
        return line;
    }

    /// <summary>Whose heirs it is in the world's history.</summary>
    private static string Heirs(Town t, Menace m) =>
        Annals.Lineage(t.Content.History, m.Lineage) is HistoryLineage l ? "  " + Tag(Ui.Cap(l.Title)) : "";

    private static string Wants(Town t, Menace m) => Menaces.AmbitionOf(t, m).What.Replace("{town}", t.Name);

    /// <summary>Where it sits, as far as the town knows.</summary>
    private static string Seat(Town t, Menace m)
    {
        var site = Quests.Site(t, m.SeatId);
        if (site == null || site.Known == SiteKnowledge.Unheard) return $"Its seat somewhere in {Ui.Esc(m.Region)} is unknown.";
        return $"Seated at {SiteLink(site.Def)}{(site.Def.Tier > 0 ? $" (tier {site.Def.Tier})" : "")}.";
    }

    private static string Times(int n) => n switch { 1 => "once", 2 => "twice", _ => $"{n} times" };
}
