namespace CrownAndCompass.Sim.Systems;

public sealed record DowntimeLines
{
    public string Land { get; init; } = "";
    public string Train { get; init; } = "";
    public string Stake { get; init; } = "";
}

/// <summary>What heroes do with their gold between jobs (downtime.json).</summary>
public sealed record DowntimeRules
{
    /// <summary>Gold a hero keeps in their purse whatever they buy.</summary>
    public double Keep { get; init; } = 40;
    public double LandCost { get; init; } = 150;
    public double LandPerLevel { get; init; } = 20;
    public double RootLoyalty { get; init; } = 5;
    public double Rooted { get; init; } = 0.03;
    public double TrainPerLevel { get; init; } = 20;
    public double TrainXp { get; init; } = 0.12;
    public int TrainMaxLevel { get; init; } = 19;
    public double Stake { get; init; } = 120;
    public double Dividend { get; init; } = 0.01;
    public List<string> Holdings { get; init; } = [];
    public List<string> Trainers { get; init; } = [];
    public DowntimeLines Lines { get; init; } = new();
}

/// <summary>
/// Heroes' own lives between jobs, with no season set aside for it: once a week each hero in town with gold to spare
/// buys land and a house of their own (and sleeps there instead of the tavern, more rooted in the town), trains for
/// their next level, or buys a stake in a business that pays them back. Marriage comes through the taverns as for
/// anyone, and companies raise bastions once they can afford one (<see cref="Bastions"/>). None of it keeps a hero
/// from answering a bounty. No dice: who does what comes from their purse and a stable hash.
/// </summary>
public static class Downtime
{
    public static DowntimeRules Rules(Town town) => town.Content.Downtime;

    public static double LandCost(Town town, Agent a) => Rules(town).LandCost + Rules(town).LandPerLevel * a.Hero!.Level;

    public static double TrainCost(Town town, Agent a) => Rules(town).TrainPerLevel * a.Hero!.Level;

    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            if (s.Holding != "") s.Loyalty = Math.Min(100, s.Loyalty + r.Rooted);
            if (s.StakeId is int sid) Dividend(town, a, town.Building(sid));
            if (s.AwayUntil != null || (a.Id + town.Day) % 7 != 0) continue;
            double spare = a.Gold - r.Keep;
            if (s.Holding == "" && r.Holdings.Count > 0 && spare >= LandCost(town, a) && (a.SpouseId != null || s.Loyalty >= 60))
                BuyLand(town, a);
            else if (s.Level <= r.TrainMaxLevel && spare >= TrainCost(town, a) && Quests.XpToNext(s.Level) > 0)
                Train(town, a);
            else if (s.StakeId == null && spare >= r.Stake && Business(town, a) is { } b)
                BuyStake(town, a, b);
        }
    }

    private static void BuyLand(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        double cost = Js.Round(LandCost(town, a));
        a.Gold -= cost;
        town.Book(TreasuryAccount.LandSales, cost);
        s.Holding = r.Holdings[(int)(Voices.Hash("holding", a.Id) % (uint)r.Holdings.Count)];
        s.Loyalty = Math.Min(100, s.Loyalty + r.RootLoyalty);
        Log(town, a, r.Lines.Land, ("holding", s.Holding), ("cost", Js.Str(cost)));
    }

    private static void Train(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        double cost = Js.Round(TrainCost(town, a));
        a.Gold -= cost;
        Quests.GainXp(town, a, (int)(Quests.XpToNext(s.Level) * r.TrainXp));
        string trainer = r.Trainers.Count == 0 ? "a trainer" : r.Trainers[(int)(Voices.Hash("trainer", a.Id, town.Day) % (uint)r.Trainers.Count)];
        Log(town, a, r.Lines.Train, ("trainer", trainer), ("cost", Js.Str(cost)), importance: Importance.Routine);
    }

    /// <summary>The business a hero would put gold into: the open shop or workshop in town with the most cash, ties by id.</summary>
    private static Building? Business(Town town, Agent a) =>
        town.Buildings
            .Where(b => b.Complete && !b.Closed && !b.ForSale && b.Def.Jobs > 0 && b.OwnerId is int o && o != a.Id && town.Agent(o).Hero == null)
            .OrderByDescending(b => b.Cash)
            .ThenBy(b => b.Id)
            .FirstOrDefault();

    private static void BuyStake(Town town, Agent a, Building b)
    {
        var r = Rules(town);
        var s = a.Hero!;
        a.Gold -= r.Stake;
        b.Cash += r.Stake;
        s.StakeId = b.Id;
        s.Stake = r.Stake;
        Log(town, a, r.Lines.Stake, ("business", b.Name), ("cost", Js.Str(r.Stake)));
    }

    /// <summary>The business pays its hero their share a day while its till has it; a business that has gone pays nothing more.</summary>
    private static void Dividend(Town town, Agent a, Building b)
    {
        var s = a.Hero!;
        if (!b.Complete || b.Closed)
        {
            s.StakeId = null;
            s.Stake = 0;
            return;
        }
        double pay = Js.Round(s.Stake * Rules(town).Dividend * 100) / 100;
        if (b.Cash < pay) return;
        b.Cash -= pay;
        a.Gold += pay;
    }

    private static void Log(Town town, Agent a, string line, (string Key, string Value) x, (string Key, string Value) y, Importance importance = Importance.Notable)
    {
        if (line == "") return;
        string text = line.Replace("{hero}", a.Name).Replace("{" + x.Key + "}", x.Value).Replace("{" + y.Key + "}", y.Value);
        town.Log(LedgerCategory.Social, importance, Stories.Cap(text), [a.Id]);
    }
}
