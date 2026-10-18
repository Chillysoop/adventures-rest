namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The gods are present (GDD rules W8, C1, C2). The charter brings a shrine; townsfolk stop there to
/// pray, leave offerings and, in time, take the god's vows. Once the town is big enough it can raise a
/// temple, its first institution with followers and wants of its own: it tithes the faithful, keeps
/// holy days, pays bounties on the undead and the schemers it hates, heals heroes, and raises the
/// fallen for a price, so death sticks unless someone pays (W9). The general store sells heroes the
/// healing potions they carry into a delve.
/// Who each house serves, holy days and what each god hates come from the pantheon (see Faith).
/// </summary>
public static class Shrines
{
    private static ShrineRules Rules(Town town) => town.Content.Shrines;

    public static Building? Shrine(Town town) =>
        town.Buildings.FirstOrDefault(b => b.Def.Id == Rules(town).Shrine && b.Complete);

    public static Building? Temple(Town town) =>
        town.Buildings.FirstOrDefault(b => b.Def.Id == Rules(town).Temple && b.Complete);

    /// <summary>Where the town prays: the temple once it stands, else the shrine.</summary>
    public static Building? HolyPlace(Town town) => Temple(town) ?? Shrine(town);

    public static List<Agent> Followers(Town town) => town.Residents.Where(a => a.Devout).ToList();

    /// <summary>Raise the charter shrine on a civic lot beside the hall.</summary>
    public static Building RaiseShrine(Town town)
    {
        var def = town.Content.Building(Rules(town).Shrine);
        var lot = CivicLot(town, def)!;
        var b = town.AddBuilding(def, lot, null, "", true);
        Faith.Dedicate(town, b, Faith.CharterGod(town));
        return b;
    }

    /// <summary>A civic lot near the hall for a house of worship or a guildhall, zoning one if needed.</summary>
    public static Lot? CivicLot(Town town, BuildingDef def) => Planning.CivicLot(town, def);

    public static double TempleCost(Town town)
    {
        var def = town.Content.Building(Rules(town).Temple);
        return Js.Round(def.Cost + town.Market.Quote(Good.Timber, def.Timber));
    }

    /// <summary>Why the town can't raise a temple yet, or null when it can.</summary>
    public static string? TempleBlocked(Town town)
    {
        var r = Rules(town);
        if (town.Buildings.Any(b => b.Def.Id == r.Temple)) return Temple(town) != null ? "built" : "under way";
        if (town.Population < r.TemplePopulation) return $"needs {r.TemplePopulation} residents";
        if (town.Treasury < TempleCost(town)) return "not enough gold";
        return null;
    }

    /// <summary>The council endows a temple; builders put it up like any other site.</summary>
    /// <param name="at">The lot the player picked, else one near the hall.</param>
    public static Building? BuildTemple(Town town, Lot? at = null)
    {
        if (TempleBlocked(town) != null) return null;
        var def = town.Content.Building(Rules(town).Temple);
        var lot = at ?? CivicLot(town, def);
        if (lot == null) return null;
        lot.Zone = Zone.Civic;
        town.Book(TreasuryAccount.Temple, -TempleCost(town));
        town.Market.Buy(Good.Timber, def.Timber);
        var god = Faith.TempleChoice(town);
        var b = town.AddBuilding(def, lot, null, "", false);
        Faith.Dedicate(town, b, god);
        b.LaborBudget = def.BuildHours * Brain.ConstructionWage;
        // The shrine's offerings go to the new house.
        var shrine = Shrine(town);
        if (shrine != null)
        {
            b.Cash += shrine.Cash;
            shrine.Cash = 0;
        }
        int faithful = Faith.Followers(town, god.Id).Count;
        town.Log(
            LedgerCategory.Faith,
            Importance.Notable,
            $"The council endows {b.Name}, for {Faith.Prose(god)} has the largest congregation in town. {(faithful > 0 ? $"{faithful} of the faithful carry stones for it." : "The town hopes the faithful will follow.")}");
        return b;
    }

    // Prayer --------------------------------------------------------------------------------------

    /// <summary>How much an agent wants to pray this hour (0 outside the prayer bells or with nowhere to pray).</summary>
    public static double PrayScore(Town town, Agent a, int hour)
    {
        if (!Rules(town).PrayHours.Contains(hour) || HolyPlace(town) == null) return 0;
        return 0.15 + (a.Devout ? 0.55 : 0) + (a.LastDayMood < 50 ? 0.2 : 0) + a.Loneliness / 100 * 0.3;
    }

    public static void Pray(Town town, Agent a)
    {
        var holy = Faith.PlaceFor(town, a) ?? HolyPlace(town);
        if (holy == null) return;
        a.LocationId = holy.Id;
        a.Loneliness = Math.Max(0, a.Loneliness - 2);
        a.PrayedToday = true;
        double offering = Rules(town).Offering;
        if (a.Gold >= offering * 10)
        {
            a.Gold -= offering;
            holy.Cash += offering;
        }
    }

    // The day -------------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        var r = Rules(town);
        Vows(town);
        var holy = HolyPlace(town);
        if (holy != null && town.Day % r.TitheEveryDays == 0) Tithe(town, holy);
        // Holy days are each god's own, kept by Faith.
        var temple = Temple(town);
        if (temple != null) Crusade(town, temple);
        if (auto && TempleBlocked(town) == null && town.Treasury > TempleCost(town) + 250) BuildTemple(town);
    }

    private static void Vows(Town town)
    {
        var r = Rules(town);
        double chance = r.FollowerChance * (Temple(town) != null ? 2 : 1);
        foreach (var a in town.Residents)
        {
            if (a.PrayedToday && !a.Devout && town.Rng.Chance(chance))
            {
                // The faithless take the vows of whichever god's house they have been praying at.
                if (string.IsNullOrEmpty(a.Faith) && a.LocationId != null) a.Faith = town.Faiths.Dedications.Get(a.LocationId.Value, "");
                var god = Faith.Of(town, a);
                if (god != null)
                {
                    a.Devout = true;
                    town.Log(LedgerCategory.Faith, Importance.Routine, $"{a.Name} takes the vows of {Faith.Prose(god)}.", [a.Id]);
                }
            }
            a.PrayedToday = false;
        }
    }

    /// <summary>The sworn tithe to their own god's house, or to the town's holy place if their god has none here.</summary>
    private static void Tithe(Town town, Building holy)
    {
        var gifts = new OrderedMap<int, double>();
        foreach (var a in Followers(town))
        {
            double give = Math.Floor(a.Gold * Rules(town).Tithe * 10) / 10;
            if (give <= 0) continue;
            a.Gold -= give;
            var to = Faith.PlaceFor(town, a) ?? holy;
            gifts[to.Id] = gifts.Get(to.Id, 0) + give;
        }
        double total = 0;
        foreach (var (id, gift) in gifts)
        {
            town.Building(id).Cash += gift;
            total += gift;
        }
        if (total >= 1)
            town.Log(LedgerCategory.Faith, Importance.Routine, $"The faithful tithe {Js.ToFixed(total, 0)} gold to {(gifts.Count == 1 ? town.Building(gifts.Keys[0]).Name : $"{gifts.Count} houses of the gods")}.");
    }

    /// <summary>The temple's agenda: a bounty from its own coffers on the nearest den of the kinds its god hates.</summary>
    private static void Crusade(Town town, Building temple)
    {
        var r = Rules(town);
        if (temple.Cash < r.CrusadeReward + r.CrusadeReserve) return;
        var god = Faith.Of(town, temple);
        var kinds = god != null ? god.Hates : r.CrusadeKinds;
        if (town.Bounties.Any(b => b.FunderId == temple.Id && b.Status is BountyStatus.Open or BountyStatus.Taken)) return;
        var target = town.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared && kinds.Contains(s.Def.Kind))
            .Where(s => !town.Bounties.Any(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken))
            .OrderBy(s => s.Def.Distance)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null) return;
        var bounty = Quests.PostBounty(town, target.Def.Id, r.CrusadeReward, temple);
        if (bounty == null) return;
        town.Log(
            LedgerCategory.Faith,
            Importance.Notable,
            $"{Cap(temple.Name)} offers {Js.Str(r.CrusadeReward)} gold from its own coffers to whoever cleanses {target.Def.Name}. {Cap(god != null ? Faith.Prose(god) : r.Deity)} will not suffer {target.Believed} so near {town.Name}.");
    }

    // Heroes --------------------------------------------------------------------------------------

    /// <summary>A night's rest heals more under the shrine's lamps; at the temple the priests close every wound, for a fee.</summary>
    public static int Rest(Town town, Agent hero, int healed)
    {
        var s = hero.Hero!;
        var temple = Temple(town);
        double fee = Rules(town).TempleHealFee;
        if (temple != null && s.Hp + healed < s.MaxHp && hero.Gold >= fee * 5)
        {
            hero.Gold -= fee;
            temple.Cash += fee;
            return s.MaxHp - s.Hp;
        }
        if (Shrine(town) != null) healed += (int)Math.Ceiling(s.MaxHp * Rules(town).ShrineRest);
        return healed;
    }

    /// <summary>
    /// Heroes carried home dead can be raised at the temple: paid from their own purse, then the
    /// party's take, then (for the faithful) the temple's coffers. Returns who was raised.
    /// </summary>
    public static List<int> Raise(Town town, IReadOnlyList<int> dead, string partyName, ref double take, List<(string Text, int Id)> rites)
    {
        var raised = new List<int>();
        var temple = Temple(town);
        if (temple == null) return raised;
        double cost = Rules(town).RaiseCost;
        foreach (int id in dead)
        {
            var h = town.Agent(id);
            double coffers = h.Devout && town.Faiths.Dedications.Get(temple.Id, "") == h.Faith ? Math.Max(0, temple.Cash - Rules(town).CrusadeReserve) : 0;
            if (h.Gold + take + coffers < cost) continue;
            double fromOwn = Math.Min(h.Gold, cost);
            double fromTake = Math.Min(take, cost - fromOwn);
            double fromTemple = cost - fromOwn - fromTake;
            h.Gold -= fromOwn;
            take -= fromTake;
            // Half the fee is the diamond the spell consumes; the temple keeps the rest.
            temple.Cash += fromOwn + fromTake - cost * 0.5;
            h.Hero!.Hp = 1;
            raised.Add(id);
            string paid = fromTemple > 0 ? "the temple pays what the purse cannot" : fromTake > 0 ? $"the {partyName} pay from their take" : "the hero's own gold pays for it";
            var god = Faith.Of(town, temple);
            rites.Add(($"The priests of {(god != null ? Faith.Prose(god) : Rules(town).Deity)} lay {h.Name} on the altar and call them back. {h.GivenName} breathes again ({paid}, {Js.Str(cost)} gold).", id));
        }
        return raised;
    }

    /// <summary>Heroes buy healing potions at the general store before they march.</summary>
    public static void Outfit(Town town, IReadOnlyList<Agent> heroes)
    {
        var p = Rules(town).Potion;
        var stores = town.Buildings.Where(b => b.Def.Id == "general_store" && b.Complete && !b.Closed && !b.ForSale).ToList();
        if (stores.Count == 0) return;
        foreach (var h in heroes)
        {
            var s = h.Hero!;
            while (s.Potions < p.Carry && h.Gold >= p.Price * 1.5)
            {
                var store = stores[(h.Id + s.Potions) % stores.Count];
                double tax = p.Price * town.TaxRate;
                h.Gold -= p.Price;
                town.Book(TreasuryAccount.SalesTax, tax);
                store.Cash += p.Price - tax - p.Wholesale;
                store.RevenueToday += p.Price;
                s.Potions++;
            }
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
