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
            p.Card($"{Tag("A DEMAND FOR FEALTY", Ui.Accent)}\n[b]{Ui.Esc(Ui.Cap(dm.Name))}[/b] wants {Ui.Esc(t.Name)} as its vassal: {Math.Round(y.Share * 100)}% of the treasury every {Ui.Days(y.EveryDays)}, and its raiders stay away.\n{Ui.C(Ui.Ink2, $"[font_size=12]Refuse and its host marches on the walls. Say nothing for {Ui.Days(Math.Max(1, days))} and that is a refusal.[/font_size]")}", Ui.Accent);
            int id = dm.Id;
            p.Buttons(("Bend the knee", true, false, () => Act(() => _sim.AnswerDemand(id, true))), ("Refuse", true, true, () => Act(() => _sim.AnswerDemand(id, false))));
        }

        var big = Saga.BigBad(t);
        if (big is { Alive: true })
        {
            string march = big.ArrivalTick is int at && at > t.Tick ? $"\n{Ui.C(Ui.Bad, $"[b]Its host reaches the walls in {Ui.Days((int)Math.Ceiling((at - t.Tick) / 24.0))}.[/b]")}" : "";
            p.Card($"{Tag("THE BIG BAD", Ui.Bad)}\n[font_size=16][b]{Ui.Esc(Ui.Cap(big.Name))}[/b][/font_size]  {Tag($"level {big.Level}")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Seat(t, big)} {(big.Marches == 0 ? "It has not marched on the town yet." : $"It has marched on the town {Times(big.Marches)}.")} Kill it to end the Long Night.[/font_size]")}{march}", Ui.Bad);
        }
        if (s.YokeId is int yid && s.Menaces[yid - 1] is { Alive: true } yoke)
        {
            var y = t.Content.Saga.Yoke;
            string missed = s.MissedTribute > 0 ? $" Tribute missed {Times(s.MissedTribute)} of {y.MissedToMarch} before it marches." : "";
            p.Card($"{Tag("UNDER THE YOKE", Ui.Accent)}\n[b]{Ui.Esc(Ui.Cap(yoke.Name))}[/b] takes {Math.Round(y.Share * 100)}% of the treasury every {Ui.Days(y.EveryDays)}.{missed}\n{Ui.C(Ui.Ink2, "[font_size=12]Kill its leader to break the yoke. The walls break a second time, and the town falls.[/font_size]")}", Ui.Accent);
        }

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
            p.Row($"[b]{Ui.Esc(Ui.Cap(m.Name))}[/b]  {Tag(word, color)}  {Tag($"level {m.Level}")}{march}\n{Tag(Menaces.CunningOf(t, m).Name)}  {Tag(Menaces.AmbitionOf(t, m).Name)}{Heirs(t, m)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Annals.Motive(t, m))} {Ui.Esc(Wants(t, m))} {Seat(t, m)}{held}[/font_size]")}{Plan(t, m)}{Assets(t, m)}");
        }
        p.Note("A threat grows while nobody clears its seat, as far as its wits allow: a beast only hunts, a dull band takes a valley and stops, a cunning or masterful leader builds a domain and can become the big bad. What it wants decides what it does to the town. Clearing the seat breaks it, and it falls back to a lair it holds.");

        var fallen = s.Menaces.Where(m => !m.Alive && m.Named).OrderByDescending(m => m.EndedDay).ThenBy(m => m.Id).Take(5).ToList();
        if (fallen.Count > 0)
        {
            p.Heading("Destroyed or turned");
            foreach (var m in fallen)
            {
                string turned = m.Redeemed ? "  " + Tag($"{m.Leader} joined {t.Name}", Ui.Good) : "";
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {m.EndedDay}[/font_size]")}  {Ui.Esc(Ui.Cap(m.Name))}{turned}", 13);
            }
        }
        p.Commit();
    }

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
