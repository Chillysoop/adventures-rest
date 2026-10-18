using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// The library window: the wizards' library and its spellbooks, the wizards it has drawn to town, what the
/// scholars are studying and how the council funds them, and the town's own tree of discoveries, revealed by
/// who lives in town, who it trades with and what it has built.
/// </summary>
public partial class Main
{
    private static readonly string[] Fields = ["arcana", "crafts", "husbandry", "healing", "trade", "war", "lore"];

    private void RenderLibrary()
    {
        var p = _panes[Tab.Library];
        var t = _sim.Town;
        var r = t.Content.Research;
        var st = t.Research;
        p.Begin();

        var rank = Research.Rank(t);
        int circle = Research.Circle(t);
        p.Card($"[font_size=17][b]{Ui.Esc(Ui.Cap(rank.Name))}[/b][/font_size]  " + Ui.C(Ui.Ink2, $"power {Math.Round(Research.Power(t))}") + "\n" +
               Ui.C(Ui.Ink2, $"{st.Books.Count} {(st.Books.Count == 1 ? "spellbook" : "spellbooks")} · " +
                   (circle > 0 ? $"town magic of the {Research.Ordinal(circle)} circle" : "no town magic yet") +
                   $" · {st.Known.Count} of {st.Revealed.Count} discoveries made"), Ui.Gold);

        // What the scholars are working on, and what it costs.
        p.Heading("The scholars");
        double lore = Research.Lore(t);
        if (st.Study is string id)
        {
            var d = Research.Def(t, id);
            double need = Research.LoreCost(t, d), have = st.Progress.Get(id, 0);
            int days = lore > 0 ? (int)Math.Ceiling(Math.Max(0, need - have) / lore) : 0;
            p.Meter($"{d.Name}", have / need, lore > 0 ? $"{Ui.Days(days)} at {Js.Str(Math.Round(lore, 1))} lore a day" : "no lore coming in");
        }
        else p.Note(Research.Open(t).Count > 0 ? "The scholars are idle. Choose what they study below." : "Nothing the scholars could study yet: discoveries are revealed by who lives in town, who it trades with, what it builds and the books in the library.");
        var sources = Research.Sources(t);
        if (sources.Count > 0)
            p.Text(Ui.C(Ui.Ink2, "[font_size=12]Lore a day: " + string.Join(" · ", sources.Select(s => $"{Ui.Esc(s.Why)} {Js.Str(Math.Round(s.Lore, 1))}")) +
                   $", × {Js.Str(r.Funding[st.Funding].Lore)} for funding[/font_size]"), 12);
        var buttons = new List<(string, bool, bool, Action)>();
        for (int i = 0; i < r.Funding.Count; i++)
        {
            int level = i;
            double cost = Research.FundingCost(t, i);
            buttons.Add(($"{Ui.Cap(r.Funding[i].Name)}{(cost > 0 ? $" · {Js.Str(cost)}g" : "")}", st.Funding != i, st.Funding == i, () => Act(() => _sim.FundResearch(level))));
        }
        p.Buttons(buttons.ToArray());

        // The tree: what this town could study, and what it is still missing.
        p.Heading("Discoveries");
        if (st.Revealed.Count == 0)
            p.Note("No ideas have taken hold yet. Smiths, weavers, scribes and other trades, neighbours of other peoples, trade routes, temples and libraries, and spellbooks each reveal discoveries of their own.");
        foreach (string field in Fields)
        {
            var defs = st.Revealed.Keys.Select(k => Research.Def(t, k)).Where(d => d.Field == field).OrderBy(d => Research.IsKnown(t, d.Id)).ThenBy(d => d.Rank).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (defs.Count == 0) continue;
            p.Text($"[b]{Ui.Cap(field == "arcana" ? "town magic" : field)}[/b]", 13);
            foreach (var d in defs)
            {
                string did = d.Id;
                bool known = Research.IsKnown(t, d.Id);
                var missing = Research.Missing(t, d);
                string state = known ? Tag("known", Ui.Good)
                    : st.Study == d.Id ? Tag("studying", Ui.Gold)
                    : missing.Count > 0 ? Tag("needs " + string.Join(", ", missing))
                    : Tag($"rank {d.Rank}");
                string body = $"[b]{Ui.Esc(d.Name)}[/b]  {state}\n" +
                              Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(d.Text)} {Effects(d)} · from {Ui.Esc(st.Revealed[d.Id])}[/font_size]");
                if (known || st.Study == d.Id || missing.Count > 0) p.Row(body);
                else
                {
                    double cost = st.Progress.ContainsKey(d.Id) ? 0 : Research.Commission(t, d);
                    p.Row(body, (cost > 0 ? $"Study · {Js.Str(cost)}g" : "Study", t.Treasury >= cost, false, () => Act(() => _sim.StudyDiscovery(did))));
                }
            }
        }

        p.Heading("Spellbooks");
        if (st.Books.Count == 0) p.Note("The library has no spellbooks yet. They come home from delves, on caravans from the elves, gnomes and far cities, and from wizards who hold a bastion.");
        foreach (var b in st.Books.OrderByDescending(b => b.Circle).ThenBy(b => b.Id).Take(12))
            p.Text($"{Ui.Esc(Ui.Cap(b.Name))}\n" + Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(b.From)}, day {b.Day}[/font_size]"), 13);
        if (st.Books.Count > 12) p.Note($"and {st.Books.Count - 12} more.");

        p.Heading("Wizards in town");
        var wizards = t.Heroes.Where(a => a.Hero!.Cls.Id == "wizard").OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).ToList();
        if (wizards.Count == 0) p.Note("No wizards live in town. A library with books enough draws them, each stronger than the last.");
        foreach (var a in wizards)
            p.Text($"[b]{Ui.Esc(a.Name)}[/b], level {a.Hero!.Level}{(st.Drawn.Contains(a.Id) ? Ui.C(Ui.Ink2, " · drawn by the library") : "")}{(a.IsAway ? Ui.C(Ui.Ink2, " · away") : "")}\n" +
                   Ui.C(Ui.Ink2, $"[font_size=12]reads up to the {Research.Ordinal(Math.Min(9, (a.Hero.Level + 1) / 2))} circle[/font_size]"), 13);
        p.Note("The library's power is the circles of its books and the levels of its wizards. As it grows it draws stronger wizards to town, and the highest circle both a book and a wizard reach is the town magic the scholars can work.");
        p.Commit();
    }

    private static string Effects(DiscoveryDef d)
    {
        var e = new List<string>();
        foreach (var (good, f) in d.Yields) e.Add($"{good} {(f >= 1 ? "+" : "−")}{Math.Round(Math.Abs(f - 1) * 100)}%");
        if (d.Mood != 0) e.Add($"mood {(d.Mood > 0 ? "+" : "−")}{Js.Str(Math.Abs(d.Mood))}");
        if (d.Arrivals != 0) e.Add("more settlers");
        if (d.Build != 0) e.Add($"building {Math.Round(d.Build * 100)}% faster");
        if (d.Gold != 0) e.Add($"{(d.Gold > 0 ? "+" : "−")}{Js.Str(Math.Abs(d.Gold))}g a day");
        if (d.Heroes != 0) e.Add($"+{d.Heroes} {(d.Heroes == 1 ? "hero" : "heroes")}");
        if (d.Defence != 0) e.Add($"defence +{Js.Str(d.Defence)}");
        if (d.Ward != 0) e.Add($"undercity stirs {Math.Round(d.Ward * 100)}% slower");
        if (d.Xp != 0) e.Add("new heroes better trained");
        if (d.Scry != 0) e.Add($"scrying {Math.Round(d.Scry * 100)}% cheaper");
        if (d.Lore != 0) e.Add($"+{Js.Str(d.Lore)} lore a day");
        return e.Count == 0 ? "" : $"({string.Join(", ", e)})";
    }
}
