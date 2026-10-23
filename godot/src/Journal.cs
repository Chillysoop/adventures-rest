using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The Journal (Chronicles › Journal): one entry for each long struggle the town is in, the way Victoria's journal
/// and Stellaris' situations show them. Each says where it stands, what pushes it each way and where to act. It only
/// reads the town; no countdown for the big bad's host (<see cref="Horde.Word"/>).
/// </summary>
public partial class Main
{
    private void RenderJournal(Pane p, Town t)
    {
        int shown = 0;
        void Entry(string icon, string title, string state, Color color, string meter, IEnumerable<(string Icon, string Text, bool Good)> pushes, Tab window, string act)
        {
            shown++;
            string lines = string.Join("\n", pushes.Select(x => $"[font_size=12]{Ui.Img(x.Icon, 14)} {Ui.C(x.Good ? Ui.Good : Ui.Bad, Ui.Esc(x.Text))}[/font_size]"));
            p.Row($"{Ui.Img(icon, 18)} [b]{Ui.Esc(title)}[/b]  {Tag(state, color)}" + (meter != "" ? $"\n{meter}" : "") + (lines != "" ? $"\n{lines}" : ""),
                (act, true, false, () => OpenWindow(window)));
        }
        static string Bar(double share, Color c)
        {
            int full = (int)Math.Round(Math.Clamp(share, 0, 1) * 12);
            return $"[font_size=12]{Ui.C(c, new string('█', full))}{Ui.C(Ui.Ink2, new string('░', 12 - full))}[/font_size]";
        }

        // The story's act, and what opens the next.
        if (t.Saga.Enabled && t.Content.Saga.Acts.Count > 0)
        {
            var next = Saga.NextAct(t);
            if (next != null)
            {
                int level = Saga.LevelFor(t, next), pop = Saga.PopulationFor(t, next);
                double share = Math.Min(level > 0 ? Saga.ChampionLevel(t) / (double)level : 1, pop > 0 ? t.Population / (double)pop : 1);
                Entry("tab-campaign", Saga.ActTitle(t, t.Saga.Act), "the story", Ui.Gold, Bar(share, Ui.Gold) + Ui.Dim("  next act"),
                    [("heroes", $"Champion level {Saga.ChampionLevel(t)} of {level}", Saga.ChampionLevel(t) >= level), ("population", $"Households {t.Population} of {pop}", t.Population >= pop)],
                    Tab.Campaign, "Campaign");
            }
        }

        // The big bad's host: where it stands in a word, how full it looks if the town has eyes on it.
        if (Saga.BigBad(t) is { } bb && Horde.On(t, bb))
        {
            double? seen = Horde.Seen(t, bb);
            Entry("threat", bb.Leader == "" ? "The big bad" : bb.Leader, Horde.Word(t, bb), Ui.Bad, seen is double s ? Bar(s, Ui.Bad) + Ui.Dim("  as your eyes see it") : Ui.Dim("[font_size=12]No eyes on its camps[/font_size]"),
                [("ui-eye", "Eyes on its camps: towers, scouts, scrying", true), ("order-strike", "Fights at a camp thin it", true), ("act-slay", "A cleared holding scatters it", true)],
                Tab.Campaign, "Campaign");
        }

        // Crises: each menace's end-game blow.
        foreach (var c in Crises.Active(t))
        {
            var m = Crises.MenaceOf(t, c);
            Entry("warband", Crises.Title(t, c), c.Phase.ToString().ToLowerInvariant(), Ui.Bad, "",
                [("defence", $"Blows stopped: {c.BlowsStopped}", true), ("ui-skull", $"Blows landed: {c.BlowsLanded}", false), ("threat", $"Break {(m.Leader == "" ? "its master" : m.Leader)} to end it", true)],
                Tab.War, "War");
        }

        // Fear: the town gives up if it stays too high.
        var dread = t.Content.Saga.Dread;
        if (t.Saga.Dread >= dread.WarnAt * 0.5)
        {
            var worst = Saga.WorstFear(t);
            Entry("suspicion", "Fear in the streets", t.Saga.Dread >= dread.WarnAt ? "dire" : "rising", t.Saga.Dread >= dread.WarnAt ? Ui.Bad : Ui.Gold, Bar(t.Saga.Dread / dread.AbandonAt, Ui.Bad),
                [.. worst != null ? [("threat", $"{worst.Leader} frightens them most", false)] : Array.Empty<(string, string, bool)>(), ("order-strike", "A won fight or a broken menace", true), ("faith", "Funded temples and festivals", true)],
                Tab.Campaign, "Campaign");
        }

        // The guilds and the mages.
        if (t.Union.Formed)
        {
            var u = t.Union;
            var pulls = Union.Pulls(t).OrderByDescending(x => Math.Abs(x.Pull)).Take(3)
                .Select(x => (x.Pull > 0 ? "books" : "favour", $"{Ui.Cap(x.Why)} → {(x.Pull > 0 ? "mages" : "movement")}", Math.Abs(u.Bar + x.Pull) < Math.Abs(u.Bar)));
            Entry("favour", "Guilds and mages", Union.Mood(t).ToLowerInvariant(), Math.Abs(u.Bar) >= Union.Rules(t).Breaking ? Ui.Bad : Ui.Gold,
                Bar((u.Bar + 100) / 200, Ui.Gold) + Ui.Dim("  movement ↔ mages"), pulls, Tab.Council, "Council");
        }

        // Sickness.
        if (Sickness.Outbreak(t))
        {
            int sick = Sickness.Sick(t).Count;
            Entry("sickness", "Sickness", $"{sick} {(sick == 1 ? "house" : "houses")}", Ui.Bad, "",
                [("tend", "Healers door to door", true), ("quarantine", "Close the sick lanes", true), ("ui-skull", "Rats and the dead bring more", false)],
                Tab.City, "City");
        }

        // Unrest.
        if (t.Taxes.Unrest >= 20)
            Entry("suspicion", "Unrest", t.Taxes.Unrest >= 60 ? "boiling" : "simmering", t.Taxes.Unrest >= 60 ? Ui.Bad : Ui.Gold, Bar(t.Taxes.Unrest / 100, Ui.Bad),
                [("gold", "High taxes and unpaid services", false), ("ui-heart", "Festivals, edicts and lower taxes", true)],
                Tab.Council, "Council");

        if (shown == 0) p.Note("No great struggle yet. They will come.");
    }
}
