using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The minor powers in the Diplomacy window: a leader card for each, with its goal, what it thinks of the town,
/// whose side it is on, its price, and the gifts that win it over or outbid an enemy's envoys.
/// </summary>
public partial class Main
{
    private static (string Word, Color Color) PowerWord(Town t, PowerState p, PowerDef d) =>
        p.Side == PowerSide.Town ? ("Allied", Ui.Good)
        : p.Side == PowerSide.Enemy ? ("With the enemy", Ui.Bad)
        : p.CourtedBy != null ? ("Courted", Ui.Gold)
        : p.Standing >= d.AllyAt ? ("Friendly", Ui.Good)
        : p.Standing >= 0 ? ("Cordial", Ui.Ink)
        : p.Standing >= -30 ? ("Wary", Ui.Gold)
        : ("Hostile", Ui.Bad);

    /// <summary>The envoy's business in a few words.</summary>
    private static string EnvoyLine(Town t, PowerState p)
    {
        string price = Powers.PriceText(t, p);
        return p.Envoy switch
        {
            "alliance" => $"Offers alliance for {price}",
            "pact" => $"Offers truce for {price}",
            "tribute" => $"Demands {price}",
            "favour" => $"Asks: {Powers.TaskText(t, p, "{task}")}",
            "aid" => $"Asks {price} for its feud",
            _ => "",
        };
    }

    private void RenderPowers(Pane p, Town t)
    {
        p.Heading("Minor powers");
        foreach (var ps in t.Powers.OrderBy(x => x.Envoy != "" ? 0 : x.Side == PowerSide.Town ? 1 : x.CourtedBy != null ? 1 : x.Side == PowerSide.Enemy ? 2 : 3).ThenByDescending(x => x.Standing).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            var d = Powers.Def(t, ps.Id);
            var lore = t.Content.Library.Faction(d.Ref);
            var (word, color) = PowerWord(t, ps, d);
            string id = ps.Id;
            string name = Ui.Cap(Powers.Name(t, d));
            string standing = $"{(ps.Standing >= 0 ? "+" : "−")}{Math.Abs(Math.Round(ps.Standing))}";
            string text = $"[b]{Ui.Esc(name)}[/b]  {Tag(word, color)} {Ui.C(Ui.Ink2, $"[font_size=12]{standing}[/font_size]")}\n" +
                          $"[font_size=12]{Ui.Esc(d.Leader)}, {Ui.Esc(d.Title)}[/font_size]" + (lore != null ? $"  {Tag(Ui.Cap(lore.Kind))}" : "");
            // One short line per fact, each behind its icon.
            void Line(string icon, Color c, string what) => text += $"\n[font_size=12]{Ui.Img(icon, 14)} {Ui.C(c, what)}[/font_size]";
            int left(int day) => Math.Max(1, day - t.Day);
            if (ps.Envoy != "") Line("standing", Ui.Gold, $"{Ui.Esc(EnvoyLine(t, ps))} · {Ui.Days(left(ps.EnvoyUntil))}");
            if (Powers.Suitor(t, ps) is { } suitor)
            {
                bool holds = Powers.Resolve(t, ps) >= ps.Offer;
                Line("rivals", holds ? Ui.Gold : Ui.Bad, $"{Ui.Esc(Ui.Cap(suitor.Name))} bids {Js.Str(ps.Offer)} vs your {Js.Str(Js.Round(Powers.Resolve(t, ps)))} · {Ui.Days(left(ps.DecideDay))}");
            }
            if (ps.FeudWith != "" && Powers.Find(t, ps.FeudWith) is { } foe)
                Line("rivals", Ui.Bad, $"Feud with {Ui.Esc(Powers.Name(t, foe))} · {Ui.Days(left(ps.FeudUntil))}");
            if (ps.TaskTaken) Line("decree", Ui.Gold, $"Promised: {Ui.Esc(Powers.TaskText(t, ps, "{task}"))} · {Ui.Days(left(ps.TaskUntil))}");
            if (Powers.Pacted(t, ps)) Line("defence", Ui.Good, $"Truce · {Ui.Days(left(ps.PactUntil))}");
            if (ps.Owed != "") Line("food", Ui.Bad, $"Farms −25% · {Ui.Days(left(ps.OwedUntil))}");
            if (ps.Side == PowerSide.Town)
                Line("favour", Ui.Good, $"{Ui.Esc(Ui.Cap(Powers.BoonText(t, d)))} · {Js.Str(d.Tithe)}g/day");
            else if (ps.Side == PowerSide.Enemy && Powers.Patron(t, ps) is { } patron)
                Line("threat", Ui.Bad, $"Serves {Ui.Esc(patron.Name)}: {Ui.Esc(Powers.BaneText(t, ps))}");
            else
            {
                Line("favour", Ui.Ink2, $"Ally at {Js.Str(d.AllyAt)}: {Ui.Esc(Powers.BoonText(t, d))}");
                if (d.Serves == "any") Line("threat", Ui.Ink2, $"If bought: {Ui.Esc(Powers.BaneText(t, ps))}");
            }

            double gift = Powers.GiftCost(t, id);
            var buttons = new List<(string, bool, bool, Action)>
            {
                ($"Send gifts · {Js.Str(gift)}g (+{Js.Str(Powers.GiftWorth(t, ps))})", t.Treasury >= gift, ps.CourtedBy != null, () => Act(() => _sim.CourtPower(id))),
            };
            if (ps.Envoy != "")
            {
                string verb = ps.Envoy switch { "tribute" => "Pay", "aid" => "Send aid", "favour" => "Promise", _ => "Accept" };
                string price = Powers.Strange(t, ps) != null ? "strange price" : $"{Js.Str(ps.EnvoyGold)}g";
                buttons.Insert(0, ("Refuse", true, false, () => Act(() => _sim.AnswerEnvoy(id, false))));
                buttons.Insert(0, (ps.Envoy == "favour" ? verb : $"{verb} · {price}", Powers.AnswerBlocked(t, ps) == null, true, () => Act(() => _sim.AnswerEnvoy(id, true))));
            }
            if (ps.Side == PowerSide.Town) buttons.Add(("Break", true, false, () => Act(() => _sim.BreakWithPower(id))));
            else if (ps.Side == PowerSide.Neutral) buttons.Add(($"Ally · {Js.Str(d.Price)}g", Powers.AllyBlocked(t, id) == null, true, () => Act(() => _sim.AllyWithPower(id))));
            string face = d.Face;
            var ring = ps.Side == PowerSide.Town ? Ui.Good : ps.Side == PowerSide.Enemy ? Ui.Bad : ps.CourtedBy != null ? Ui.Gold : new Color("5a5347");
            p.Row(text, ($"{face}{ring.ToHtml()}", () => new Portrait(56) { Glyph = face, Tint = d.Evil ? new Color("b5523b") : new Color("6f8fb5"), Ring = ring }, null), buttons.ToArray());
        }
        p.Note("Gifts raise standing · allies anger rivals · outbid enemy bids before they answer.");
    }
}
