namespace CrownAndCompass.Sim.Systems;

/// <summary>What a gift does for the hero who gets it (armory.json "gift").</summary>
public sealed record ArmoryGiftRules
{
    /// <summary>Loyalty a gift wins at once, and more for each tier of the item.</summary>
    public double Loyalty { get; init; } = 5;
    public double PerTier { get; init; } = 2;

    /// <summary>Share of what the item would fetch that the hero counts as pay from the town, capped.</summary>
    public double Pay { get; init; } = 0.5;
    public double PayCap { get; init; } = 150;

    /// <summary>A second gift within this many days wins half the loyalty.</summary>
    public int AgainDays { get; init; } = 14;
}

/// <summary>Loot terms on a bounty: the company hands the hoard's magic to the town (armory.json "terms").</summary>
public sealed record ArmoryTermsRules
{
    /// <summary>How much less a hero wants a bounty with the terms (greedy heroes more).</summary>
    public double Cut { get; init; } = 0.15;
    public double GreedyCut { get; init; } = 0.3;

    /// <summary>What the board suggests for a bounty with the terms, times the usual reward.</summary>
    public double Markup { get; init; } = 1.25;
}

/// <summary>A standing loot pact with a company (armory.json "pact").</summary>
public sealed record ArmoryPactRules
{
    /// <summary>Relation a company needs before it will hear of a pact.</summary>
    public double MinRelation { get; init; } = 50;

    /// <summary>The finder's fee asked, as a share of what merchants would give for the item; friends ask less, greed asks more.</summary>
    public double Fee { get; init; } = 0.5;
    public double Friendly { get; init; } = 0.8;
    public double Devoted { get; init; } = 0.6;
    public double Greedy { get; init; } = 1.25;

    /// <summary>Dealings lost, and days of sulking, when a company turns the offer down.</summary>
    public double Refused { get; init; } = -3;
    public int SulkDays { get; init; } = 10;

    /// <summary>Dealings won for each find handed in and paid for, and lost when the town can't pay the fee.</summary>
    public double Handed { get; init; } = 0.5;
    public double Unpaid { get; init; } = -2;
}

/// <summary>Buying loot from heroes (armory.json "buy").</summary>
public sealed record ArmoryBuyRules
{
    /// <summary>Heroes ask the town this much over the passing merchant's price; wary companies more.</summary>
    public double Markup { get; init; } = 1.2;
    public double Wary { get; init; } = 1.5;

    /// <summary>Days the offer stands before they sell it on the road.</summary>
    public int Days { get; init; } = 6;
}

/// <summary>Magic that trade caravans bring back to sell (armory.json "caravan").</summary>
public sealed record ArmoryCaravanRules
{
    public double Chance { get; init; } = 0.35;

    /// <summary>Share of those offers that are a pair of sending stones, and their price.</summary>
    public double Stone { get; init; } = 0.3;
    public double StonePrice { get; init; } = 150;

    /// <summary>Price, times what merchants would give for the item.</summary>
    public double Markup { get; init; } = 2.5;
    public int Days { get; init; } = 12;
}

/// <summary>How the stand-in mayor keeps the armory (armory.json "auto").</summary>
public sealed record ArmoryAutoRules
{
    /// <summary>Gold kept back in the treasury, and the most of the rest one purchase may take.</summary>
    public double Reserve { get; init; } = 400;
    public double Share { get; init; } = 0.3;

    /// <summary>Stock nobody has had a use for in this many days is sold.</summary>
    public int SellAfterDays { get; init; } = 120;
}

/// <summary>The town armory (content/game/armory.json).</summary>
public sealed record ArmoryRules
{
    /// <summary>The library item that is a pair of sending stones.</summary>
    public string Stone { get; init; } = "sending-stone-pair";

    /// <summary>What the town hall holds on charter day.</summary>
    public List<string> Start { get; init; } = [];

    /// <summary>Sendings a stone carries on one venture, without a caster's slot, and the more sending quests it lets the party ask.</summary>
    public int StoneSlots { get; init; } = 3;
    public int StoneTrips { get; init; } = 1;

    public ArmoryGiftRules Gift { get; init; } = new();
    public ArmoryTermsRules Terms { get; init; } = new();
    public ArmoryPactRules Pact { get; init; } = new();
    public ArmoryBuyRules Buy { get; init; } = new();
    public ArmoryCaravanRules Caravan { get; init; } = new();
    public ArmoryAutoRules Auto { get; init; } = new();
}

/// <summary>One thing in the armory: where it came from and when.</summary>
public sealed class StockItem
{
    public int Id { get; init; }
    public required LibItem Item { get; init; }
    public int Day { get; init; }

    /// <summary>"the Iron Wolves' hoard", "a caravan from Highfold", "handed back by Bryn".</summary>
    public string From { get; init; } = "";

    /// <summary>The name a townsperson gave the blade they forged (<see cref="Systems.Moods"/>), "" for none.</summary>
    public string Name { get; set; } = "";

    /// <summary>"Emberfang, a +1 weapon", or the item's own name.</summary>
    public string Title => Name != "" ? $"{Name}, {Gear.Plainly(Item)}" : Gear.Plainly(Item);
}

/// <summary>Something offered to the town for gold: a hero's spare loot, or a caravan's wares.</summary>
public sealed class LootOffer
{
    public int Id { get; init; }
    public required LibItem Item { get; init; }
    public double Price { get; init; }
    public int Day { get; init; }
    public int Until { get; init; }

    /// <summary>The hero selling it; null for a caravan.</summary>
    public int? HeroId { get; init; }
    public string Seller { get; init; } = "";
}

public sealed class ArmoryState
{
    public List<StockItem> Stock { get; } = [];
    public List<LootOffer> Offers { get; } = [];
    public int NextId { get; set; } = 1;

    public int Gifts { get; set; }
    public int Bought { get; set; }
    public int HandedIn { get; set; }
    public double Spent { get; set; }
}

/// <summary>
/// The town armory: what the town hall keeps to arm its heroes. It fills three ways: companies hand
/// in the magic from a hoard, under loot terms on a council bounty (they want a bigger reward for it)
/// or a standing pact with the council (a finder's fee for each find); heroes offer the town their
/// spare loot before they sell it on the road; and trade caravans come home with arms, charms and
/// sending stones to sell. The mayor gifts what it holds to heroes in town: a gift is worn at once
/// (what it replaces comes back to the armory), counts as pay and wins loyalty, and a better-armed
/// company dares sites a level above its own. A sending stone lets its company send word home
/// with no caster's slot to spend, and ask the mayor more on the road.
/// </summary>
public static class Armory
{
    private static ArmoryRules Rules(Town town) => town.Content.Armory;

    private static string Poss(string name) => name.EndsWith('s') ? $"{name}'" : $"{name}'s";

    public static LibItem? StoneItem(Town town) => town.Content.Library.Item(Rules(town).Stone);

    public static bool IsStone(Town town, LibItem it) => it.Id == Rules(town).Stone;

    /// <summary>The charter's gift: the town hall's own sending stones.</summary>
    public static void Init(Town town)
    {
        foreach (var id in Rules(town).Start)
            if (town.Content.Library.Item(id) is LibItem it) Add(town, it, "the town hall's own");
    }

    public static StockItem Add(Town town, LibItem it, string from)
    {
        var a = town.Armory;
        var s = new StockItem { Id = a.NextId++, Item = it, Day = town.Day, From = from };
        a.Stock.Add(s);
        return s;
    }

    // Stones on the road --------------------------------------------------------------------------

    /// <summary>A living member of the party carries one of the town's sending stones.</summary>
    public static bool Carries(Town town, Party party) => party.Members.Any(id => town.Agent(id) is { Dead: false, Hero.Stone: true });

    /// <summary>Sendings a carried stone adds to the party's spell slots.</summary>
    public static int StoneSlots(Town town, Party party) => Carries(town, party) ? Rules(town).StoneSlots : 0;

    public static int StoneTrips(Town town, Party party) => Carries(town, party) ? Rules(town).StoneTrips : 0;

    /// <summary>Stones out with heroes, alive and still with the town.</summary>
    public static int StonesOut(Town town) => town.Heroes.Count(h => h.Hero!.Stone);

    // Gifts ---------------------------------------------------------------------------------------

    /// <summary>What a gift would do for this hero: the bonus over what they wear, 1 for a stone they lack, 0 or less for no use.</summary>
    public static int Use(Town town, Agent a, LibItem it)
    {
        if (a.Hero == null || a.Dead || a.Departed) return 0;
        if (IsStone(town, it)) return a.Hero.Stone ? 0 : 1;
        // A war mage's wand is for casters.
        if (Gear.SlotOf(it) == "trinket" && !a.Hero.Cls.Spell && it.Category == "wand") return 0;
        return Gear.Gain(town.Content, a, it);
    }

    /// <summary>Why this hero can't take a gift now, or null.</summary>
    public static string? Refuse(Town town, Agent a, LibItem it)
    {
        if (a.Hero == null || a.Dead || a.Departed) return "gone";
        if (a.Hero.AwayUntil != null) return "out of town";
        if (Use(town, a, it) <= 0) return IsStone(town, it) ? "already carries one" : "no use for it";
        return null;
    }

    /// <summary>Heroes in town who would get the most from the item, best first.</summary>
    public static List<(Agent Hero, int Use)> Takers(Town town, LibItem it)
    {
        var list = town.Heroes.Where(h => Refuse(town, h, it) == null).Select(h => (h, Use(town, h, it))).ToList();
        if (IsStone(town, it))
            // A stone does most for a company with no caster to send word, and nobody carrying one.
            return list
                .OrderBy(x => CompanySends(town, x.h))
                .ThenByDescending(x => x.h.Hero!.Level)
                .ThenBy(x => x.h.Id)
                .ToList();
        return list.OrderByDescending(x => x.Item2).ThenByDescending(x => x.h.Hero!.Level).ThenBy(x => x.h.Id).ToList();
    }

    /// <summary>Whether the hero's company can already send word: a caster able to, or a stone among them.</summary>
    private static bool CompanySends(Town town, Agent a)
    {
        var mates = a.Hero!.PartyName is string n ? Intel.Members(town, n) : [a];
        return mates.Any(m => m.Hero!.Stone || Knowledge.Sends(town, m));
    }

    /// <summary>The mayor's gift: an item from the armory goes to a hero in town, who wears or carries it from now on.</summary>
    public static bool Gift(Town town, int stockId, int heroId)
    {
        var r = Rules(town);
        var stock = town.Armory.Stock.FirstOrDefault(s => s.Id == stockId);
        if (stock == null || heroId < 1 || heroId > town.Agents.Count) return false;
        var a = town.Agent(heroId);
        if (Refuse(town, a, stock.Item) != null) return false;
        var s = a.Hero!;
        var it = stock.Item;
        town.Armory.Stock.Remove(stock);
        string what;
        if (IsStone(town, it))
        {
            s.Stone = true;
            what = "one of a pair of sending stones (the other stays in the town hall)";
        }
        else
        {
            string slot = Gear.SlotOf(it), oldName = s.Named.Get(slot, "");
            var old = Gear.Fit(town, a, it);
            if (stock.Name != "")
            {
                s.Named[slot] = stock.Name;
                Gear.Relist(s);
            }
            what = stock.Name != "" ? Gear.Label(s, it) : $"the {Gear.Label(s, it)}";
            if (old != null)
            {
                Add(town, old, $"handed back by {a.GivenName}").Name = oldName;
                what += $", and hands the {(oldName != "" ? oldName : Gear.Plainly(old))} back to the armory";
            }
        }
        double pay = Math.Min(r.Gift.PayCap, Gear.SellValue(town, it) * r.Gift.Pay);
        s.Pay += pay;
        double loyalty = r.Gift.Loyalty + r.Gift.PerTier * it.Tier;
        if (town.Day - s.LastGiftDay < r.Gift.AgainDays) loyalty /= 2;
        Loyalty.Sway(a, loyalty);
        s.LastGiftDay = town.Day;
        town.Armory.Gifts++;
        town.Log(LedgerCategory.Quest, Importance.Notable, $"The mayor gives {a.Name} {what}.", [a.Id]);
        return true;
    }

    /// <summary>
    /// Arm a group in one go (a hero, a company, the heroes on guard duty): while the armory holds something one of
    /// them would gain from, the best such gift is made. A sending stone goes only to one whose company can't send word.
    /// Returns how many gifts were made.
    /// </summary>
    public static int Arm(Town town, IEnumerable<int> heroIds)
    {
        var heroes = heroIds.Where(id => id >= 1 && id <= town.Agents.Count).Distinct().Order().Select(town.Agent).ToList();
        int given = 0;
        // Each gift leaves its hero strictly better armed, so this ends; the cap only guards against a surprise.
        for (int round = 0; round < 64; round++)
        {
            StockItem? best = null;
            Agent? to = null;
            int most = 0;
            foreach (var s in town.Armory.Stock.OrderBy(x => x.Id))
                foreach (var h in heroes)
                {
                    if (Refuse(town, h, s.Item) != null) continue;
                    if (IsStone(town, s.Item) && CompanySends(town, h)) continue;
                    int use = Use(town, h, s.Item);
                    if (use > most)
                    {
                        (best, to, most) = (s, h, use);
                    }
                }
            if (best == null || !Gift(town, best.Id, to!.Id)) break;
            given++;
        }
        return given;
    }

    /// <summary>The armory's best gift for a hero now (what <see cref="Arm"/> would hand them first), or null.</summary>
    public static StockItem? BestFor(Town town, Agent a)
    {
        StockItem? best = null;
        int most = 0;
        foreach (var s in town.Armory.Stock.OrderBy(x => x.Id))
        {
            if (Refuse(town, a, s.Item) != null || (IsStone(town, s.Item) && CompanySends(town, a))) continue;
            int use = Use(town, a, s.Item);
            if (use > most) (best, most) = (s, use);
        }
        return best;
    }

    // Selling off ---------------------------------------------------------------------------------

    public static double SellValue(Town town, LibItem it) => IsStone(town, it) ? Math.Floor(Rules(town).Caravan.StonePrice / 2) : Gear.SellValue(town, it);

    /// <summary>Sell an item from the armory to the merchants, for what they give.</summary>
    public static bool Sell(Town town, int stockId)
    {
        var stock = town.Armory.Stock.FirstOrDefault(s => s.Id == stockId);
        if (stock == null) return false;
        double gold = SellValue(town, stock.Item);
        town.Armory.Stock.Remove(stock);
        town.Book(TreasuryAccount.Armory, gold);
        town.Log(LedgerCategory.Town, Importance.Routine, $"The town hall sells the {Gear.Plainly(stock.Item)} from the armory for {Js.Str(gold)} gold.");
        return true;
    }

    // Loot terms and pacts ------------------------------------------------------------------------

    /// <summary>How much less a hero wants a bounty because of its loot terms.</summary>
    public static double TermsCut(Town town, Agent a, Bounty b) =>
        !b.LootToTown ? 0 : a.Has(Trait.Greedy) ? Rules(town).Terms.GreedyCut : Rules(town).Terms.Cut;

    /// <summary>What the board suggests for a bounty that hands the hoard's magic to the town.</summary>
    public static double TermsReward(Town town, double reward) => Math.Ceiling(reward * Rules(town).Terms.Markup);

    /// <summary>A company's standing pact with the council, or null.</summary>
    public static bool HasPact(Town town, string name) => Intel.Find(town, name)?.Pact == true;

    /// <summary>The finder's fee a company would ask under a pact, as a share of what merchants would give.</summary>
    public static double PactFee(Town town, string name)
    {
        var r = Rules(town).Pact;
        var members = Intel.Members(town, name);
        double rel = Intel.Relation(town, name, members);
        double fee = r.Fee * (rel >= 75 ? r.Devoted : rel >= 60 ? r.Friendly : 1);
        if (members.Any(m => m.Has(Trait.Greedy))) fee *= r.Greedy;
        return Math.Round(fee, 2);
    }

    /// <summary>Why a company won't hear of a pact now, or null.</summary>
    public static string? PactRefusal(Town town, string name)
    {
        var members = Intel.Members(town, name);
        if (members.Count == 0) return "no one rides under that name";
        var c = Intel.Find(town, name);
        if (c?.Pact == true) return "already sworn to one";
        if (c?.SulkUntil is int until && town.Tick < until) return "still sore about the last offer";
        if (Intel.Relation(town, name, members) < Rules(town).Pact.MinRelation) return "don't trust the council enough";
        return null;
    }

    /// <summary>Offer a company a standing pact: every magic find comes to the town, for a finder's fee. A wary company says no, and sulks.</summary>
    public static bool OfferPact(Town town, string name)
    {
        var r = Rules(town).Pact;
        if (Intel.Members(town, name).Count == 0 || HasPact(town, name)) return false;
        var c = Intel.Company(town, name);
        string? no = PactRefusal(town, name);
        if (no != null)
        {
            if (c.SulkUntil is int until && town.Tick < until) return false;
            c.Refusals++;
            Intel.Deal(town, name, r.Refused);
            c.SulkUntil = town.Tick + r.SulkDays * SimTime.HoursPerDay;
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The {name} turn down the council's loot pact: they {no}, and take the asking ill.");
            return false;
        }
        c.Pact = true;
        c.PactFee = PactFee(town, name);
        c.PactDay = town.Day;
        town.Log(LedgerCategory.Quest, Importance.Notable,
            $"The {name} swear a loot pact with the council: the magic they find comes to the town armory, for a finder's fee of {Js.Str(Math.Round(c.PactFee * 100))}% of its worth.");
        return true;
    }

    public static bool EndPact(Town town, string name)
    {
        if (Intel.Find(town, name) is not { Pact: true } c) return false;
        c.Pact = false;
        town.Log(LedgerCategory.Quest, Importance.Routine, $"The council releases the {name} from their loot pact.");
        return true;
    }

    /// <summary>
    /// A find from a hoard, at the homecoming: under loot terms or a pact it goes to the armory (the pact's
    /// fee paid to the finder). Returns false when the hero keeps it, as usual.
    /// </summary>
    public static bool HandIn(Town town, Party party, Bounty? bounty, Agent finder, LibItem it)
    {
        var r = Rules(town).Pact;
        // Loot to the town, or a fetching job: the best of the hoard was taken for the town.
        if (bounty != null && (bounty.LootToTown || (bounty.FunderId == null && Terms.Of(town, bounty).Item)))
        {
            Add(town, it, $"the {Poss(party.Name)} find, under the bounty's terms");
            town.Armory.HandedIn++;
            town.Log(LedgerCategory.Quest, Importance.Notable, $"Under the bounty's terms, {finder.Name} hands the {Gear.Plainly(it)} over to the town armory.", [finder.Id]);
            return true;
        }
        if (Intel.Find(town, party.Name) is not { Pact: true } c) return false;
        double fee = Math.Round(Gear.SellValue(town, it) * c.PactFee);
        if (town.Treasury < fee)
        {
            Intel.Deal(town, party.Name, r.Unpaid);
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The town can't pay the {Poss(party.Name)} finder's fee for the {Gear.Plainly(it)}, so {finder.GivenName} keeps it.", [finder.Id]);
            return false;
        }
        town.Book(TreasuryAccount.Armory, -fee);
        finder.Gold += fee;
        finder.Hero!.Pay += fee;
        c.Handed++;
        Intel.Deal(town, party.Name, r.Handed);
        Add(town, it, $"the {Poss(party.Name)} find, under their pact");
        town.Armory.HandedIn++;
        town.Armory.Spent += fee;
        town.Log(LedgerCategory.Quest, Importance.Notable, $"By their pact, {finder.Name} hands the {Gear.Plainly(it)} to the town armory for a finder's fee of {Js.Str(fee)} gold.", [finder.Id]);
        return true;
    }

    // Buying ----------------------------------------------------------------------------------------

    /// <summary>
    /// A hero would sell the item on the road; they offer it to the town first, at a little over the
    /// merchant's price (more from a wary company). Returns false when there's no point (nothing to sell).
    /// </summary>
    public static bool OfferToTown(Town town, Agent a, LibItem it)
    {
        var r = Rules(town).Buy;
        double merchant = Gear.SellValue(town, it);
        if (merchant <= 0) return false;
        double rel = a.Hero!.PartyName is string n ? Intel.Relation(town, n) : a.Hero.Loyalty;
        double price = Math.Ceiling(merchant * (rel < town.Content.World.Intel.RefuseBelow + 20 ? r.Wary : r.Markup));
        town.Armory.Offers.Add(new LootOffer { Id = town.Armory.NextId++, Item = it, Price = price, Day = town.Day, Until = town.Day + r.Days, HeroId = a.Id, Seller = a.GivenName });
        return true;
    }

    /// <summary>A caravan home from a settlement may bring something to sell: a pair of sending stones, or arms and charms.</summary>
    public static void Caravan(Town town, Settlement s)
    {
        var r = Rules(town).Caravan;
        if (!town.Rng.Chance(r.Chance)) return;
        LibItem? it;
        double price;
        if (town.Rng.Chance(r.Stone) && StoneItem(town) is LibItem stone)
        {
            it = stone;
            price = r.StonePrice;
        }
        else
        {
            int top = Math.Clamp(1 + town.City.Tier / 2, 1, 4);
            var pool = town.Content.LootItems.Where(x => Gear.Slots.Contains(Gear.SlotOf(x)) && x.Tier >= top - 1 && x.Tier <= top).ToList();
            if (pool.Count == 0) return;
            it = town.Rng.Pick(pool);
            price = Math.Ceiling(Gear.SellValue(town, it) * r.Markup);
        }
        town.Armory.Offers.Add(new LootOffer { Id = town.Armory.NextId++, Item = it, Price = price, Day = town.Day, Until = town.Day + r.Days, Seller = $"a caravan from {s.Name}" });
        town.Log(LedgerCategory.Town, Importance.Routine, $"The caravan from {s.Name} brings {Agent.Article(Gear.Plainly(it))} {Gear.Plainly(it)} to sell the town hall, for {Js.Str(price)} gold.");
    }

    /// <summary>Buy an offer into the armory.</summary>
    public static bool Buy(Town town, int offerId)
    {
        var o = town.Armory.Offers.FirstOrDefault(x => x.Id == offerId);
        if (o == null || town.Treasury < o.Price) return false;
        town.Armory.Offers.Remove(o);
        town.Book(TreasuryAccount.Armory, -o.Price);
        town.Armory.Bought++;
        town.Armory.Spent += o.Price;
        if (o.HeroId is int hid)
        {
            var a = town.Agent(hid);
            a.Gold += o.Price;
            if (a.Hero is { } s) s.Pay += o.Price - Gear.SellValue(town, o.Item);
            if (a.Hero?.PartyName is string n) Intel.Deal(town, n, town.Content.World.Intel.Fair);
            Add(town, o.Item, $"bought from {a.GivenName}");
            town.Log(LedgerCategory.Quest, Importance.Routine, $"The town hall buys the {Gear.Plainly(o.Item)} from {a.Name} for the armory, {Js.Str(o.Price)} gold.", [a.Id]);
        }
        else
        {
            Add(town, o.Item, $"bought from {o.Seller}");
            town.Log(LedgerCategory.Town, Importance.Routine, $"The town hall buys the {Gear.Plainly(o.Item)} from {o.Seller} for {Js.Str(o.Price)} gold.");
        }
        return true;
    }

    // The day -------------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        Expire(town);
        if (!auto) return;
        var r = Rules(town).Auto;
        double Spare() => (town.Treasury - r.Reserve) * r.Share;
        // Buy what a hero in town would use, cheapest first, while the treasury can spare it.
        foreach (var o in town.Armory.Offers.OrderBy(x => x.Price).ThenBy(x => x.Id).ToList())
        {
            if (o.Price > Spare()) continue;
            if (!Takers(town, o.Item).Any(x => x.Hero.Id != o.HeroId)) continue;
            Buy(town, o.Id);
        }
        // Give what the armory holds to whoever gains most; sell what nobody has used for months.
        foreach (var s in town.Armory.Stock.OrderBy(x => x.Id).ToList())
        {
            var takers = Takers(town, s.Item);
            if (takers.Count > 0) Gift(town, s.Id, takers[0].Hero.Id);
            else if (!IsStone(town, s.Item) && town.Day - s.Day >= r.SellAfterDays) Sell(town, s.Id);
        }
    }

    /// <summary>Offers run out: a hero sells to a passing merchant after all, a caravan moves on.</summary>
    private static void Expire(Town town)
    {
        foreach (var o in town.Armory.Offers.Where(x => town.Day >= x.Until).ToList())
        {
            town.Armory.Offers.Remove(o);
            if (o.HeroId is int hid && town.Agent(hid) is { Dead: false, Departed: false } a) Gear.SellOnRoad(town, a, o.Item);
        }
        // A hero who dies or leaves takes the offer with them.
        town.Armory.Offers.RemoveAll(o => o.HeroId is int h && town.Agent(h) is { } a && (a.Dead || a.Departed));
    }
}
