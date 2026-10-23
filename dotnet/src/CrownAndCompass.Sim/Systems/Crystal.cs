namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Where the airships' lift-crystal comes from (Conner, 2026-10-22: "Once you get airships going, that should become more
/// abundant as discoveries and even a mining source"). The dock's store (<see cref="SkyFleetState.Crystal"/>) starts with what
/// came with the dock; traders sell a little each month, dear; companies bring shards home from the sky; cleared isles high
/// enough show seams the town can mine; and discoveries make every charge go further (<see cref="Research.Crystal"/>).
/// </summary>
public static class Crystal
{
    private static CrystalRules Rules(Town town) => town.Content.Sky.Crystal;

    public static double Stock(Town town) => town.SkyFleet.Crystal;

    // ---- Traders ----

    /// <summary>Crystal the traders will still sell this month.</summary>
    public static double ForSale(Town town)
    {
        var r = Rules(town);
        var st = town.SkyFleet;
        return town.Day - st.BoughtDay >= r.BuyDays ? r.BuyMost : Math.Max(0, r.BuyMost - st.Bought);
    }

    public static double Price(Town town, double units) => Js.Round(units * Rules(town).Buy);

    /// <summary>Why the town can't buy crystal now, or null.</summary>
    public static string? BuyRefused(Town town, int units)
    {
        if (Sky.Dock(town) == null) return "needs the Sky Dock";
        if (units <= 0 || units > ForSale(town)) return "the traders have no more this month";
        if (town.Treasury < Price(town, units)) return "not enough gold";
        return null;
    }

    /// <summary>Buy crystal from the traders who call at the dock.</summary>
    public static bool Buy(Town town, int units)
    {
        if (BuyRefused(town, units) != null) return false;
        var r = Rules(town);
        var st = town.SkyFleet;
        if (town.Day - st.BoughtDay >= r.BuyDays)
        {
            st.BoughtDay = town.Day;
            st.Bought = 0;
        }
        double cost = Price(town, units);
        town.Book(TreasuryAccount.Works, -cost);
        st.Bought += units;
        st.Crystal += units;
        town.Log(LedgerCategory.Town, Importance.Routine, $"Traders at the Sky Dock sell the town {units} days of lift-crystal for {Js.Str(cost)} gold.");
        return true;
    }

    // ---- The sky's own ----

    private static bool HighEnough(Town town, SiteState site)
    {
        var bands = town.Content.World.Isles.Bands;
        int at = bands.ToList().FindIndex(b => b.Id == site.Def.Band);
        int from = bands.ToList().FindIndex(b => b.Id == Rules(town).SeamFrom);
        return at >= 0 && from >= 0 && at >= from;
    }

    /// <summary>A company home from clearing a site in the sky: shards for the store, and a seam on an isle high enough.</summary>
    public static void Home(Town town, Party party, SiteState site)
    {
        if (!site.Def.Aloft) return;
        double shards = Js.Round(site.Def.Tier * Rules(town).ShardsPerTier * 10) / 10;
        if (Sky.Dock(town) != null && shards > 0)
        {
            town.SkyFleet.Crystal += shards;
            town.Log(LedgerCategory.Quest, Importance.Routine, $"The {party.Name} bring home lift-crystal shards from {site.Def.Name}: {Js.Str(shards)} days of lift for the Sky Dock.", party.Members.ToList());
        }
        if (!site.Seam && HighEnough(town, site))
        {
            site.Seam = true;
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The {party.Name} report a seam of lift-crystal running through {site.Def.Name}. A mine there would keep the airships flying.", party.Members.ToList());
        }
    }

    /// <summary>Seams the town knows of, the mined ones first.</summary>
    public static IEnumerable<SiteState> Seams(Town town) =>
        town.Sites.Where(s => s.Seam).OrderByDescending(s => s.Mined).ThenBy(s => s.Def.Id, StringComparer.Ordinal);

    /// <summary>A mine's day: its yield, more a ring out and with what the town has learned.</summary>
    public static double Yield(Town town, SiteState site)
    {
        var r = Rules(town);
        int ring = site.RegionId is int rid ? town.Regions[rid].Ring : 0;
        return (r.MineYield + r.MineYieldPerRing * ring) * (1 + Research.Seams(town));
    }

    /// <summary>Why the town can't open a mine on the seam, or null.</summary>
    public static string? MineRefused(Town town, SiteState? site)
    {
        if (site == null || !site.Seam) return "no seam there";
        if (site.Mined) return "already mined";
        if (!site.Cleared) return "the site isn't clear";
        if (Sky.Dock(town) == null) return "needs the Sky Dock";
        if (town.Treasury < Rules(town).MineCost) return "not enough gold";
        return null;
    }

    /// <summary>Open a crystal mine on a seam.</summary>
    public static bool Mine(Town town, string siteId)
    {
        var site = town.Sites.FirstOrDefault(s => s.Def.Id == siteId);
        if (MineRefused(town, site) != null) return false;
        town.Book(TreasuryAccount.Works, -Rules(town).MineCost);
        site!.Mined = true;
        town.Log(LedgerCategory.Town, Importance.Notable, $"Miners and riggers go up to {site.Def.Name}: the town's crystal mine there sends down {Js.Str(Js.Round(Yield(town, site) * 10) / 10)} days of lift a day.");
        return true;
    }

    /// <summary>Daily: mines on clear seams fill the store; a seam retaken by its foes yields nothing until it is cleared again. The stand-in buys and mines.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        if (Sky.Dock(town) == null) return;
        foreach (var s in Seams(town).Where(s => s.Mined && s.Cleared)) town.SkyFleet.Crystal += Yield(town, s);
        if (!autoMayor) return;
        var r = Rules(town);
        foreach (var s in Seams(town).Where(s => MineRefused(town, s) == null).ToList())
            if (town.Treasury > r.MineCost + 800) Mine(town, s.Def.Id);
        int want = (int)Math.Floor(Math.Min(ForSale(town), r.BuyMost / 2));
        if (Stock(town) < r.Start / 2 && want > 0 && town.Treasury > Price(town, want) + 600) Buy(town, want);
    }
}
