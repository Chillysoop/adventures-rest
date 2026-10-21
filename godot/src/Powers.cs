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
                          $"[font_size=13]Led by [b]{Ui.Esc(d.Leader)}[/b], {Ui.Esc(d.Title)}[/font_size]" +
                          (lore != null ? $"  {Tag(Ui.Cap(lore.Kind))}" : "") +
                          $"\n{Ui.C(Ui.Ink2, $"[font_size=12]It {Ui.Esc(d.Goal)}. From {Ui.Esc(d.Seat)}.[/font_size]")}";
            if (ps.Side == PowerSide.Town)
                text += $"\n[font_size=12]{Ui.C(Ui.Good, $"Gives the town {Ui.Esc(Powers.BoonText(t, d))}.")} Tithe {Js.Str(d.Tithe)}g a day.[/font_size]";
            else if (ps.Side == PowerSide.Enemy && Powers.Patron(t, ps) is { } patron)
                text += $"\n[font_size=12]{Ui.C(Ui.Bad, $"Serves {Ui.Esc(patron.Name)}: {Ui.Esc(Powers.BaneText(t, ps))}. Destroy {Ui.Esc(patron.Name)} to free it, or raise its standing to {Js.Str(t.Content.Powers.WinBack)} to win it back.")}[/font_size]";
            else
            {
                text += $"\n[font_size=12]{Ui.C(Ui.Ink2, $"As an ally: {Ui.Esc(Powers.BoonText(t, d))}. Allies at {Js.Str(d.AllyAt)} for {Js.Str(d.Price)}g, then {Js.Str(d.Tithe)}g a day.")}[/font_size]";
                if (d.Serves == "any")
                    text += $"\n[font_size=12]{Ui.C(Ui.Ink2, $"If an enemy buys it: {Ui.Esc(Powers.BaneText(t, ps))}.")}[/font_size]";
            }
            if (Powers.Suitor(t, ps) is { } suitor)
            {
                bool holds = Powers.Resolve(t, ps) >= ps.Offer;
                text += $"\n[font_size=12]{Ui.C(holds ? Ui.Gold : Ui.Bad, $"{Ui.Esc(Ui.Cap(suitor.Name))} offers {Js.Str(ps.Offer)} against the town's {Js.Str(Js.Round(Powers.Resolve(t, ps)))}. It answers in {Ui.Days(Math.Max(0, ps.DecideDay - t.Day))}{(holds ? "; for now it would refuse." : ": send gifts to outbid them.")}")}[/font_size]";
            }
            if (Powers.Pacted(t, ps))
                text += $"\n[font_size=12]{Ui.C(Ui.Good, $"Truce: it takes no enemy's gold for {Ui.Days(ps.PactUntil - t.Day)} more.")}[/font_size]";
            if (ps.Owed != "")
                text += $"\n[font_size=12]{Ui.C(Ui.Bad, $"Still taking its price: the farms yield less for {Ui.Days(ps.OwedUntil - t.Day)}.")}[/font_size]";
            if (ps.Envoy != "")
                text += $"\n[font_size=12]{Ui.C(Ui.Gold, $"Its envoy {Ui.Esc(Powers.EnvoyText(t, ps))}. Answer within {Ui.Days(Math.Max(1, ps.EnvoyUntil - t.Day))}.")}[/font_size]";
            if (ps.LastHeard != "") text += $"\n{Ui.C(Ui.Ink2, $"[font_size=11]Last heard: {Ui.Esc(ps.LastHeard)}[/font_size]")}";

            double gift = Powers.GiftCost(t, id);
            var buttons = new List<(string, bool, bool, Action)>
            {
                ($"Send gifts · {Js.Str(gift)}g (+{Js.Str(Powers.GiftWorth(t, ps))})", t.Treasury >= gift, ps.CourtedBy != null, () => Act(() => _sim.CourtPower(id))),
            };
            if (ps.Envoy != "")
            {
                string verb = ps.Envoy == "tribute" ? "Pay" : "Accept";
                string price = Powers.Strange(t, ps) != null ? "strange price" : $"{Js.Str(ps.EnvoyGold)}g";
                buttons.Insert(0, ("Refuse", true, false, () => Act(() => _sim.AnswerEnvoy(id, false))));
                buttons.Insert(0, ($"{verb} · {price}", Powers.AnswerBlocked(t, ps) == null, true, () => Act(() => _sim.AnswerEnvoy(id, true))));
            }
            if (ps.Side == PowerSide.Town) buttons.Add(("Break", true, false, () => Act(() => _sim.BreakWithPower(id))));
            else if (ps.Side == PowerSide.Neutral) buttons.Add(($"Ally · {Js.Str(d.Price)}g", Powers.AllyBlocked(t, id) == null, true, () => Act(() => _sim.AllyWithPower(id))));
            string face = d.Face;
            var ring = ps.Side == PowerSide.Town ? Ui.Good : ps.Side == PowerSide.Enemy ? Ui.Bad : ps.CourtedBy != null ? Ui.Gold : new Color("5a5347");
            p.Row(text, ($"{face}{ring.ToHtml()}", () => new Portrait(56) { Glyph = face, Tint = d.Evil ? new Color("b5523b") : new Color("6f8fb5"), Ring = ring }, null), buttons.ToArray());
        }
        p.Note("Each power likes and dislikes what the town does: the Ledger loves gold and trade, the Lanternbearers hate infamous heroes and dark allies, the League wants walls. Gifts buy favour that fades slowly, each one lately worth less. Allying with a power angers its rivals. Your clever enemies court the powers too; when one does, outbid it before the answer is due, and each power already with an enemy makes the next likelier to join it. Powers send envoys of their own: an alliance at a discount, a truce to take no enemy's gold, or a demand for tribute.");
    }
}
