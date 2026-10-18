using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The diplomacy window: every people the town has heard of out in the wilds, what they think of the
/// town and what an alliance would bring, envoys with gifts, and the trade routes the town runs to them.
/// </summary>
public partial class Main
{
    private static (string Word, Color Color) StandingWord(Settlement s, double ally) =>
        s.Razed ? ("Gone", Ui.Ink2)
        : s.Allied ? ("Allied", Ui.Good)
        : s.Standing >= ally * 0.5 ? ("Friendly", Ui.Good)
        : s.Standing >= 10 ? ("Cordial", Ui.Ink)
        : s.Standing >= -20 ? ("Wary", Ui.Gold)
        : ("Hostile", Ui.Bad);

    private void RenderDiplomacy()
    {
        var p = _panes[Tab.Diplomacy];
        var t = _sim.Town;
        var w = t.Content.World.Standing;
        var tr = t.Content.Trade;
        p.Begin();
        var known = t.Settlements.Where(s => t.Regions[s.RegionId].Known).OrderBy(s => s.Razed).ThenByDescending(s => s.Allied).ThenByDescending(s => s.Standing).ThenBy(s => s.Id).ToList();
        int allies = known.Count(s => s.Allied);
        var open = Trade.Open(t).OrderBy(r => r.Id).ToList();
        p.Card($"[font_size=17][b]{known.Count} {(known.Count == 1 ? "people" : "peoples")} known[/b][/font_size]\n" +
               Ui.C(Ui.Ink2, $"{allies} allied · {open.Count} of {Trade.Cap(t)} trade routes running · the {City.Tier(t).Name.ToLowerInvariant()}'s charter allows {Trade.Cap(t)}"), Ui.Gold);

        p.Heading("Trade routes");
        if (open.Count == 0)
            p.Note(Trade.Cap(t) == 0
                ? "A hamlet's charter lets the town run its first trade route. Caravans sell the town's surplus to a people that wants it and bring back what the town is short of."
                : $"No caravans on the road. Any people at {Js.Str(tr.MinStanding)} standing or better will trade: open a route below.");
        foreach (var r in open)
        {
            var s = Trade.SettlementOf(t, r);
            int days = Math.Max(0, r.NextDay - t.Day);
            double net = r.Earned - r.Spent;
            int id = r.Id;
            p.Row($"[b]{Ui.Esc(s.Name)}[/b]  {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(s.People)}[/font_size]")}\n" +
                  $"[font_size=13]{Ui.Esc(r.Export.ToString())} out, {Ui.Esc(r.Import.ToString())} back · next caravan home in {Ui.Days(days)}[/font_size]\n" +
                  Ui.C(Ui.Ink2, $"[font_size=12]{r.Caravans} {(r.Caravans == 1 ? "run" : "runs")}{(r.Raided > 0 ? $", {r.Raided} waylaid" : "")} · sold {Js.Str(Js.Round(r.Earned))}g, bought {Js.Str(Js.Round(r.Spent))}g · road danger {Math.Round(Trade.RaidChance(t, s) * 100)}%[/font_size]"),
                ("Close", true, false, () => Act(() => _sim.CloseTradeRoute(id))));
        }

        p.Heading("Peoples of the wilds");
        if (known.Count == 0) p.Note("The town hasn't heard of anyone out there yet. Parties bring back word of the peoples in the regions they visit.");
        foreach (var s in known)
        {
            var region = t.Regions[s.RegionId];
            var (word, color) = StandingWord(s, w.Ally);
            int id = s.Id;
            var trade = s.Kin.Trade;
            string goods = trade.Offers.Count == 0 ? "They don't trade."
                : $"Want {string.Join(", ", trade.Wants)} · sell {string.Join(", ", trade.Offers)}.";
            int pleas = t.Pleas.Count(x => x.SettlementId == s.Id && x.Status is PleaStatus.Open or PleaStatus.Taken);
            string boon = s.Kin.Boon.Text != "" ? Ui.Cap(s.Kin.Boon.Text.Replace("{S}", s.Name)) + (s.Kin.Boon.Troops > 0 ? ", and their warriors come if the walls are attacked." : ".") : "";
            string head = $"[b]{Ui.Esc(s.Name)}[/b]  {Tag(word, color)}{(s.Razed ? "" : $" {Ui.C(Ui.Ink2, $"[font_size=12]{(s.Standing >= 0 ? "+" : "−")}{Math.Abs(Math.Round(s.Standing))}[/font_size]")}")}" +
                          $"{(pleas > 0 ? "  " + Tag(pleas == 1 ? "1 plea" : $"{pleas} pleas", Ui.Gold) : "")}\n" +
                          Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(s.People))}, a {Ui.Esc(s.Kin.Kind)} in {Ui.Esc(region.Name)}, {Ui.Days(region.Days)} out[/font_size]") +
                          (s.Razed ? "" : $"\n[font_size=12]{Ui.Esc(goods)}[/font_size]") +
                          (s.Razed || boon == "" ? "" : "\n" + Ui.C(Ui.Ink2, $"[font_size=12]{(s.Allied ? "" : $"Allied at {Js.Str(w.Ally)}: ")}{Ui.Esc(boon)}[/font_size]"));
            if (s.Razed)
            {
                p.Row(head);
                continue;
            }
            var buttons = new List<(string, bool, bool, Action)>();
            if (!s.Allied) buttons.Add(($"Envoy · {Js.Str(w.EnvoyCost)}g", t.Treasury >= w.EnvoyCost, false, () => Act(() => _sim.SendEnvoy(id))));
            if (!open.Any(r => r.SettlementId == s.Id) && trade.Offers.Count > 0)
            {
                string? no = Trade.Refuse(t, s);
                var (export, import) = Trade.Suggest(t, s);
                buttons.Add(no == null
                    ? ($"Trade {export} for {import} · {Js.Str(Trade.OpenCost(t, s))}g", true, true, () => Act(() => _sim.OpenTradeRoute(id, export, import)))
                    : ("Trade", false, false, () => { }));
                if (no != null) head += "\n" + Ui.C(Ui.Ink2, $"[font_size=11]No route yet: {Ui.Esc(no)}.[/font_size]");
            }
            p.Row(head, buttons.ToArray());
        }
        p.Note($"Envoys carry gifts, and each one pleases a little less than the last. Clearing trouble near a people, answering their pleas and every caravan that gets through all raise standing. At {Js.Str(w.Ally)} they ally with the town.");
        p.Commit();
    }
}
