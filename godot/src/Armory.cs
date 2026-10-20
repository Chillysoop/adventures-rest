using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The armory window: what the town hall holds to arm its heroes and who would use it, loot heroes and
/// caravans offer the town, the sending stones out on the road, and loot pacts and purses for companies.
/// </summary>
public partial class Main
{
    private static string Stars(LibItem it) => Ui.C(Ui.Gold, $"[font_size=11]{new string('◆', Math.Clamp(it.Tier, 1, 5))}[/font_size]");

    private void RenderArmory()
    {
        var p = _panes[Tab.Armory];
        var t = _sim.Town;
        var r = t.Content.Armory;
        p.Begin();
        p.Note("The town hall keeps arms, armour, charms and sending stones to give heroes in town. A gift is worn at once, counts as pay, wins loyalty, and a better-armed company dares harder sites.");

        p.Heading("In the armory");
        var stock = t.Armory.Stock.OrderBy(s => Sim.Systems.Armory.IsStone(t, s.Item) ? 0 : 1).ThenByDescending(s => s.Item.Tier).ThenBy(s => s.Id).ToList();
        if (stock.Count == 0) p.Note("The armory is empty. Fill it from loot terms on bounties, loot pacts, or what heroes and caravans offer below.");
        foreach (var s in stock)
        {
            var takers = Sim.Systems.Armory.Takers(t, s.Item);
            int id = s.Id;
            var buttons = new List<(string, bool, bool, Action)>();
            foreach (var (hero, _) in takers.Take(1))
            {
                int hid = hero.Id;
                buttons.Add(($"Give to {hero.GivenName}", true, true, () => Act(() => _sim.Gift(id, hid))));
            }
            double sell = Sim.Systems.Armory.SellValue(t, s.Item);
            buttons.Add(($"Sell {Js.Str(sell)}g", sell > 0, false, () => Act(() => _sim.SellStock(id))));
            string who = takers.Count == 0 ? "No hero in town has a use for it now."
                : Sim.Systems.Armory.IsStone(t, s.Item) ? (CanSend(t, takers[0].Hero) ? "Every company in town can already send word." : (takers[0].Hero.Hero!.PartyName is string n ? $"The {n} have no caster to send word." : $"{takers[0].Hero.GivenName} has no caster to send word."))
                : $"Best for {takers[0].Hero.GivenName} the {takers[0].Hero.Hero!.Cls.Name} (+{takers[0].Use}){(takers.Count > 1 ? $", {takers.Count - 1} more could use it" : "")}.";
            p.Row($"[b]{Ui.Esc(s.Title)}[/b] {Stars(s.Item)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(s.From))}, day {s.Day}[/font_size]")}\n[font_size=12]{Ui.Esc(who)}[/font_size]", buttons.ToArray());
        }

        p.Heading("Offered to the town");
        var offers = t.Armory.Offers.OrderBy(o => o.Until).ThenBy(o => o.Id).ToList();
        if (offers.Count == 0) p.Note("Nothing on offer. Heroes offer the town loot they don't need before they sell it on the road, and trade caravans bring wares home.");
        foreach (var o in offers)
        {
            int id = o.Id;
            var taker = Sim.Systems.Armory.Takers(t, o.Item).FirstOrDefault(x => x.Hero.Id != o.HeroId);
            string seller = o.HeroId is int h ? $"{AgentLink(t.Agent(h))} offers it" : Ui.Esc(Ui.Cap(o.Seller));
            string use = taker.Hero != null ? $"{Ui.Esc(taker.Hero.GivenName)} could use it" : "no hero in town could use it now";
            int days = Math.Max(1, o.Until - t.Day);
            p.Row($"[b]{Ui.Esc(Gear.Plainly(o.Item))}[/b] {Stars(o.Item)}\n[font_size=12]{seller} · {use}[/font_size]\n{Ui.C(Ui.Ink2, $"[font_size=12]{(o.HeroId != null ? "Sold on the road" : "The caravan moves on")} in {Ui.Days(days)}[/font_size]")}",
                ($"Buy {Js.Str(o.Price)}g", t.Treasury >= o.Price, taker.Hero != null, () => Act(() => _sim.BuyLoot(id))));
        }

        p.Heading("Sending stones");
        var carriers = t.Heroes.Where(h => h.Hero!.Stone).OrderBy(h => h.Hero!.PartyName ?? "", StringComparer.Ordinal).ThenBy(h => h.Id).ToList();
        p.Note($"A hero carrying one of the town's sending stones lets their company send word home {r.StoneSlots} times a venture without spending a caster's spell slot, and ask your counsel more often on the road. The town keeps the other half of each pair in the hall.");
        if (carriers.Count == 0) p.Note("No stones are out with heroes.");
        else
            p.Text(string.Join("\n", carriers.Select(h => $"{AgentLink(h)}{(h.Hero!.PartyName is string n ? Ui.C(Ui.Ink2, $" of the {Ui.Esc(n)}") : "")}{(h.IsAway ? Ui.C(Ui.Gold, " · on the road") : "")}")), 13);

        p.Heading("Loot deals");
        p.Note($"Post a bounty with [b]loot terms[/b] on the Bounties board: it costs about {Js.Str(Math.Round((r.Terms.Markup - 1) * 100))}% more, and whatever magic the hoard holds comes to the armory. A company sworn to a [b]loot pact[/b] hands in every find from every trip, for a finder's fee; companies below {Js.Str(r.Pact.MinRelation)} relation turn the offer down and take it ill.");
        var names = Intel.MembersByCompany(t);
        if (names.Count == 0) p.Note("No company has formed yet.");
        foreach (var (name, members) in names)
        {
            var c = Intel.Find(t, name);
            double rel = Intel.Relation(t, name, members);
            var (word, color) = RelationWord(rel, t.Content.World.Intel.RefuseBelow);
            var home = members.Where(m => !m.IsAway).ToList();
            double purse = Js.Round(t.Content.Loyalty.Reward.PerLevel * home.Sum(m => m.Hero!.Level));
            string pact = c is { Pact: true } ? Tag($"Loot pact, {Js.Str(Math.Round(c.PactFee * 100))}% fee{(c.Handed > 0 ? $", {c.Handed} handed in" : "")}", Ui.Good) : "";
            string? no = Sim.Systems.Armory.PactRefusal(t, name);
            var buttons = new List<(string, bool, bool, Action)>();
            string n2 = name;
            if (c is { Pact: true }) buttons.Add(("End the pact", true, false, () => Act(() => _sim.EndPact(n2))));
            else buttons.Add(($"Offer a pact, {Js.Str(Math.Round(Sim.Systems.Armory.PactFee(t, name) * 100))}% fee", c?.SulkUntil is not int u || t.Tick >= u, false, () => Act(() => _sim.OfferPact(n2))));
            if (home.Count > 0)
            {
                var ids = home.Select(m => m.Id).ToList();
                buttons.Add(($"Purse {Js.Str(purse)}g", t.Treasury >= purse, false, () => Act(() =>
                {
                    foreach (int hid in ids) _sim.RewardHero(hid, Js.Round(t.Content.Loyalty.Reward.PerLevel * t.Agent(hid).Hero!.Level));
                })));
            }
            string line = no != null && c is not { Pact: true } ? Ui.C(Ui.Ink2, $" · would refuse a pact: {Ui.Esc(no)}") : "";
            p.Row($"[b]The {Ui.Esc(name)}[/b]  {Tag(word, color)}  {pact}\n{Ui.C(Ui.Ink2, $"[font_size=12]{home.Count} of {members.Count} in town{line}[/font_size]")}", buttons.ToArray());
        }
        p.Note("A purse sends each member in town gold from the treasury with the town's thanks.");
        p.Commit();
    }

    private static bool CanSend(Town t, Agent a)
    {
        var mates = a.Hero!.PartyName is string n ? Intel.Members(t, n) : [a];
        return mates.Any(m => m.Hero!.Stone || Knowledge.Sends(t, m));
    }
}
