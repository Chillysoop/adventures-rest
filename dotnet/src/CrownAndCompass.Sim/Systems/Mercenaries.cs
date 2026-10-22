namespace CrownAndCompass.Sim.Systems;

/// <summary>A company of sellswords the town can hire (content/game/mercenaries.json).</summary>
public sealed record MercCompanyDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>The charter the town needs before the company will sign with it.</summary>
    public string Tier { get; init; } = "";

    /// <summary>Its full strength, in the units of the town's defence at the walls.</summary>
    public double Strength { get; init; }

    /// <summary>Gold to sign the contract, and its wage a day after that.</summary>
    public double Fee { get; init; }
    public double Wage { get; init; }

    /// <summary>What its soldiers are called in the Ledger ("pikemen").</summary>
    public string People { get; init; } = "sellswords";
    public string Blurb { get; init; } = "";
}

/// <summary>How sellswords are hired and how their campaigns go (content/game/mercenaries.json).</summary>
public sealed record MercRules
{
    public int CampaignWages { get; init; } = 12;
    public int MarchPerDistance { get; init; } = 1;
    public double HitShare { get; init; } = 0.6;
    public double StormRatio { get; init; } = 2;
    public double RoutBelow { get; init; } = 0.5;
    public double Recover { get; init; } = 0.02;
    public double BreakAt { get; init; } = 0.25;
    public double RoutFeeds { get; init; } = 0.2;
    public IReadOnlyList<MercCompanyDef> Companies { get; init; } = [];
}

/// <summary>A company under contract to the town: home on the walls, or away on a campaign.</summary>
public sealed class Hire
{
    public required string CompanyId { get; init; }

    /// <summary>Soldiers left fit to fight, in strength; losses refill slowly at home.</summary>
    public double Strength { get; set; }
    public int HiredDay { get; init; }

    /// <summary>The menace it marches on, and when it gets there; then when it is home again.</summary>
    public int? TargetId { get; set; }
    public int? ArriveTick { get; set; }
    public int? HomeTick { get; set; }
    public int Campaigns { get; set; }

    public bool Away => ArriveTick != null || HomeTick != null;
}

/// <summary>
/// Hired armies, a sink for a rich town's gold: sellsword companies signed for a fee and paid a daily wage
/// stand on the walls with the watch, and the council can fund campaigns against the menaces of the wilds.
/// A campaign weakens a menace in the field and takes its holdings, or storms its seat outright when the
/// company far outnumbers it, but only heroes can end the big bad. Unpaid companies march off.
/// </summary>
public static class Mercenaries
{
    public static MercRules Rules(Town town) => town.Content.Mercenaries;

    public static MercCompanyDef Def(Town town, string id) =>
        Rules(town).Companies.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"Unknown mercenary company {id}");

    public static Hire? Hired(Town town, string id) => town.Hires.FirstOrDefault(h => h.CompanyId == id);

    /// <summary>Daily wages of every company under contract.</summary>
    public static double Wages(Town town) => town.Hires.Sum(h => Def(town, h.CompanyId).Wage);

    /// <summary>Strength the companies at home add at the walls.</summary>
    public static double Defense(Town town)
    {
        double d = 0;
        foreach (var h in town.Hires) if (!h.Away) d += h.Strength;
        return d;
    }

    // Hiring ----------------------------------------------------------------------------------

    /// <summary>Why the town can't sign this company now, or null when it can.</summary>
    public static string? HireBlocked(Town town, string id)
    {
        var c = Def(town, id);
        if (Hired(town, id) != null) return "already under contract";
        if (c.Tier != "" && !City.Reached(town, c.Tier)) return $"needs the town to be a {City.TierName(town, c.Tier).ToLowerInvariant()}";
        if (town.Treasury < c.Fee) return "not enough gold";
        return null;
    }

    public static bool Hire(Town town, string id)
    {
        if (HireBlocked(town, id) != null) return false;
        var c = Def(town, id);
        if (Stewards.Intercept(town, "defence", new StewardAsk { Area = "defence", Kind = "hire", Title = $"Hire {c.Name} · {Js.Str(c.Fee)}g", Why = "• The town is short of swords\n• Paid by the day after the fee", S1 = id })) return false;
        town.Book(TreasuryAccount.Mercenaries, -c.Fee);
        town.Hires.Add(new Hire { CompanyId = id, Strength = c.Strength, HiredDay = town.Day });
        town.Log(LedgerCategory.War, Importance.Notable, $"The council signs {c.Name} for {Js.Str(c.Fee)} gold and {Js.Str(c.Wage)} a day. Their {c.People} march in under their banners and take the walls with the watch.");
        return true;
    }

    /// <summary>End a contract. A company away on campaign can't be paid off until it is home.</summary>
    public static bool Dismiss(Town town, string id)
    {
        var h = Hired(town, id);
        if (h == null || h.Away) return false;
        town.Hires.Remove(h);
        town.Log(LedgerCategory.War, Importance.Notable, $"{Saga.Cap(Def(town, id).Name)} take their last pay and march off down the trade road.");
        return true;
    }

    // Campaigns -------------------------------------------------------------------------------

    /// <summary>The war chest a campaign takes: days of wages for the march, the fighting and the road home.</summary>
    public static double CampaignCost(Town town, string id) => Def(town, id).Wage * Rules(town).CampaignWages;

    /// <summary>Menaces a company could march on: named, alive, with a seat the town knows of. The warband is the War tab's affair.</summary>
    public static List<Menace> Targets(Town town) =>
        Menaces.Alive(town)
            .Where(m => m.Origin != Menaces.FromWarband && m.Named && Quests.Site(town, m.SeatId) is { Known: not SiteKnowledge.Unheard, Cleared: false })
            .OrderByDescending(m => m.Power)
            .ThenBy(m => m.Id)
            .ToList();

    /// <summary>Days the march takes one way.</summary>
    public static int MarchDays(Town town, Menace m) => 1 + Rules(town).MarchPerDistance * (Quests.Site(town, m.SeatId)?.Def.Distance ?? 1);

    public static string? MarchBlocked(Town town, string id, int menaceId)
    {
        var h = Hired(town, id);
        if (h == null) return "not under contract";
        if (h.Away) return "away on campaign";
        if (!Targets(town).Any(m => m.Id == menaceId)) return "no such foe the town knows where to find";
        if (town.Treasury < CampaignCost(town, id)) return "not enough gold";
        return null;
    }

    /// <summary>Fund a campaign: the company marches on the menace's seat.</summary>
    public static bool March(Town town, string id, int menaceId)
    {
        if (MarchBlocked(town, id, menaceId) != null) return false;
        var h = Hired(town, id)!;
        var c = Def(town, id);
        var m = Menaces.Alive(town).First(x => x.Id == menaceId);
        double cost = CampaignCost(town, id);
        town.Book(TreasuryAccount.Mercenaries, -cost);
        int days = MarchDays(town, m);
        h.TargetId = m.Id;
        h.ArriveTick = town.Tick + days * SimTime.HoursPerDay;
        h.Campaigns++;
        town.Log(LedgerCategory.War, Importance.Major, $"With a war chest of {Js.Str(cost)} gold, {c.Name} march on {m.SeatName} to break {m.Name}. They should reach it in {days} days.");
        return true;
    }

    /// <summary>Daily: pay the wages, bring companies home and refill their ranks, fight the battles due, and let the stand-in mayor hire.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        foreach (var h in town.Hires.ToList())
        {
            var c = Def(town, h.CompanyId);
            if (town.Treasury < c.Wage)
            {
                // Paid in full or not at all.
                town.Hires.Remove(h);
                town.Log(LedgerCategory.War, Importance.Major, $"The treasury can't meet their wages, so {c.Name} strike their tents and leave {town.Name} to its fate.");
                continue;
            }
            town.Book(TreasuryAccount.Mercenaries, -c.Wage);
            if (h.ArriveTick is int at && town.Tick >= at) Battle(town, h, c);
            else if (h.HomeTick is int home && town.Tick >= home)
            {
                h.HomeTick = null;
                town.Log(LedgerCategory.War, Importance.Notable, $"{Saga.Cap(c.Name)} march home to {town.Name}, {Js.Round(100 * h.Strength / c.Strength)}% of their {c.People} fit to fight.");
            }
            else if (!h.Away) h.Strength = Math.Min(c.Strength, h.Strength + c.Strength * r.Recover);
        }
        if (autoMayor) Steward(town);
    }

    private static void Battle(Town town, Hire h, MercCompanyDef c)
    {
        var r = Rules(town);
        var m = h.TargetId is int tid ? town.Saga.Menaces.FirstOrDefault(x => x.Id == tid) : null;
        int back = m != null ? MarchDays(town, m) : 2;
        h.ArriveTick = null;
        h.TargetId = null;
        h.HomeTick = town.Tick + back * SimTime.HoursPerDay;
        var seat = m != null ? Quests.Site(town, m.SeatId) : null;
        if (m == null || !m.Alive || seat == null || seat.Cleared)
        {
            town.Log(LedgerCategory.War, Importance.Notable, $"{Saga.Cap(c.Name)} find their foe already gone and turn for home.");
            return;
        }
        double ours = h.Strength * (0.8 + town.Rng.Next() * 0.4);
        double theirs = Math.Max(1, m.Power);
        double ratio = ours / theirs;
        string text;
        double lost;
        if (ratio >= r.StormRatio && !m.BigBad)
        {
            // They far outnumber it: the seat falls, and the menace with it (Menaces breaks it tomorrow).
            Clear(town, seat);
            lost = 0.15;
            text = $"{Saga.Cap(c.Name)} storm {seat.Def.Name} and put {m.Leader}'s guards to the sword. {Saga.Cap(m.Name)} has lost its seat.";
        }
        else if (ratio >= 1)
        {
            m.Power = Math.Max(0, m.Power - ours * r.HitShare);
            var holding = m.Holdings.Select(id => Quests.Site(town, id)).Where(s => s is { Cleared: false }).OrderBy(s => s!.Def.Tier).ThenBy(s => s!.Def.Id, StringComparer.Ordinal).FirstOrDefault();
            if (holding != null) Clear(town, holding);
            lost = 0.25 / ratio;
            text = $"{Saga.Cap(c.Name)} meet the host of {m.Name} before {seat.Def.Name} and break it in the field{(holding != null ? $", then burn its hold at {holding.Def.Name}" : "")}. {m.Leader} is left licking wounds.";
        }
        else if (ratio >= r.RoutBelow)
        {
            m.Power = Math.Max(0, m.Power - ours * r.HitShare * 0.5);
            lost = 0.4;
            text = $"{Saga.Cap(c.Name)} and the host of {m.Name} fight a bloody day outside {seat.Def.Name}. Both sides draw off; {m.Leader} keeps the seat.";
        }
        else
        {
            Menaces.Feed(town, m, ours * r.RoutFeeds);
            lost = 0.7;
            text = $"{Saga.Cap(m.Name)} routs {c.Name} outside {seat.Def.Name}. Their {c.People} scatter, and {m.Leader}'s banners grow bolder for it.";
        }
        h.Strength *= 1 - lost;
        town.Log(LedgerCategory.War, Importance.Major, text);
        if (h.Strength < c.Strength * r.BreakAt)
        {
            town.Hires.Remove(h);
            town.Log(LedgerCategory.War, Importance.Notable, $"Too few of {c.Name} are left to call it a company. The survivors take their pay and go their own ways.");
        }
    }

    /// <summary>A site the sellswords took: cleared as if heroes had, and any bounty on it handed back.</summary>
    private static void Clear(Town town, SiteState s)
    {
        s.Cleared = true;
        s.ThoughtCleared = true;
        s.ClearedTick = town.Tick;
        foreach (var b in town.Bounties.Where(b => b.SiteId == s.Def.Id && b.Status == BountyStatus.Open))
        {
            b.Status = BountyStatus.Failed;
            Quests.Refund(town, b);
        }
    }

    // The stand-in mayor ----------------------------------------------------------------------

    /// <summary>
    /// A rich town under threat signs the strongest company it can keep paying for a long while, sends it
    /// against a menace it can beat, and pays it off when the money runs short.
    /// </summary>
    private static void Steward(Town town)
    {
        foreach (var h in town.Hires.ToList())
            if (!h.Away && town.Treasury < Def(town, h.CompanyId).Wage * 20) Dismiss(town, h.CompanyId);

        var targets = Targets(town);
        bool threatened = targets.Any(m => m.Stage >= MenaceStage.Rising) || (Threat.IsActive(town) && town.Enemy.Stage >= ThreatStage.Raids);
        int most = City.Reached(town, "city") ? 2 : 1;
        if (threatened && town.Hires.Count < most)
        {
            var pick = Rules(town).Companies
                .Where(c => HireBlocked(town, c.Id) == null && town.Treasury >= c.Fee + c.Wage * 120 + 2000)
                .OrderByDescending(c => c.Strength)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (pick != null) Hire(town, pick.Id);
        }

        foreach (var h in town.Hires.ToList())
        {
            var c = Def(town, h.CompanyId);
            if (h.Away || h.Strength < c.Strength * 0.9 || town.Treasury < CampaignCost(town, c.Id) + c.Wage * 60 + 1000) continue;
            // The strongest risen foe it can beat in the field. The big bad is the heroes' to end.
            var target = targets
                .Where(m => !m.BigBad && m.Stage >= MenaceStage.Rising && m.Power <= h.Strength * 0.9 && !town.Hires.Any(o => o.TargetId == m.Id))
                .OrderByDescending(m => m.Power)
                .ThenBy(m => m.Id)
                .FirstOrDefault();
            if (target != null) March(town, c.Id, target.Id);
        }
    }
}
