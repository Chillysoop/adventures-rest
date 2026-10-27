namespace CrownAndCompass.Sim.Systems;

public sealed record DowntimeLines
{
    public string Land { get; init; } = "";
    public string Train { get; init; } = "";
    public string Stake { get; init; } = "";
    public string Forge { get; init; } = "";
    public string Monument { get; init; } = "";
    public string Shrine { get; init; } = "";
    public string Endow { get; init; } = "";
    public string Feast { get; init; } = "";
    public string Manor { get; init; } = "";
    public string Retire { get; init; } = "";
}

/// <summary>What a famous hero with a fortune puts into the town (downtime.json patron).</summary>
public sealed record BenefactionRules
{
    /// <summary>Spare gold (over <see cref="DowntimeRules.Keep"/>) and renown a hero needs before they play patron, and the share of it they give.</summary>
    public double Spare { get; init; } = 800;
    public int Renown { get; init; } = 10;
    public double Share { get; init; } = 0.5;

    /// <summary>Days between one gift and the next from the same hero.</summary>
    public int Every { get; init; } = 60;

    /// <summary>Fame for each gift, and the god's favour a gold of endowment buys at a shrine that already stands.</summary>
    public int Fame { get; init; } = 2;
    public double FavourPerGold { get; init; } = 0.01;
}

/// <summary>
/// Where a rich hero's fortune goes besides the patron's gifts (downtime.json fortunes): a feast at a tavern, a manor in
/// place of their first house, and at the last a rich veteran's retirement with an endowment to the town.
/// </summary>
public sealed record FortuneRules
{
    /// <summary>Spare gold before a hero throws a feast, the share of it they spend (at most most), and days between feasts.</summary>
    public double FeastSpare { get; init; } = 400;
    public double FeastShare { get; init; } = 0.15;
    public double FeastMost { get; init; } = 400;
    public int FeastEvery { get; init; } = 14;

    /// <summary>A manor's price (plus a level), paid to the town as a land sale, and the loyalty it roots.</summary>
    public double ManorCost { get; init; } = 1200;
    public double ManorPerLevel { get; init; } = 80;
    public double ManorLoyalty { get; init; } = 10;
    public List<string> Manors { get; init; } = [];

    /// <summary>A veteran this rich and seasoned may retire to teach, leaving this share of it to the town.</summary>
    public double RetireGold { get; init; } = 8000;
    public int RetireLevel { get; init; } = 10;
    public double RetireShare { get; init; } = 0.5;
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
    public BenefactionRules Patron { get; init; } = new();
    public FortuneRules Fortunes { get; init; } = new();
    public DowntimeLines Lines { get; init; } = new();
}

/// <summary>
/// Heroes' own lives between jobs, with no season set aside for it: once a week each hero in town with gold to spare
/// buys land and a house of their own (and sleeps there instead of the tavern, more rooted in the town), plays patron
/// once famous and rich (<see cref="Patron"/>), trains for
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
            var f = r.Fortunes;
            if (spare >= f.RetireGold && s.Level >= f.RetireLevel && Legacies.Veteran(town, a) && s.FiefId == null && s.BastionId == null)
            {
                RichRetire(town, a, spare);
                continue;
            }
            if (s.Holding != "" && !s.Manor && f.Manors.Count > 0 && spare >= ManorCost(town, a) && s.Loyalty >= 50)
            {
                BuyManor(town, a);
                spare = a.Gold - r.Keep;
            }
            if (spare >= f.FeastSpare && (s.FeastDay is not int fd || town.Day - fd >= f.FeastEvery) && town.Taverns.Count > 0)
            {
                Feast(town, a, Math.Min(f.FeastMost, spare * f.FeastShare));
                spare = a.Gold - r.Keep;
            }
            if (s.Holding == "" && r.Holdings.Count > 0 && spare >= LandCost(town, a) && (a.SpouseId != null || s.Loyalty >= 60))
                BuyLand(town, a);
            else if (spare >= r.Patron.Spare && s.Renown >= r.Patron.Renown && (s.GaveDay is not int g || town.Day - g >= r.Patron.Every))
                Patron(town, a, spare * r.Patron.Share);
            else if (s.Level <= r.TrainMaxLevel && spare >= TrainCost(town, a) && Quests.XpToNext(s.Level) > 0)
                Train(town, a);
            else if (s.StakeId == null && spare >= r.Stake && Business(town, a) is { } b)
                BuyStake(town, a, b);
        }
    }

    public static double ManorCost(Town town, Agent a) => Rules(town).Fortunes.ManorCost + Rules(town).Fortunes.ManorPerLevel * a.Hero!.Level;

    /// <summary>A round for the house and a roast for the street: the gold goes into a tavern's till, ties by id.</summary>
    private static void Feast(Town town, Agent a, double purse)
    {
        var taverns = town.Taverns;
        var hall = taverns[(int)(Voices.Hash("feast", a.Id, town.Day) % (uint)taverns.Count)];
        double cost = Math.Floor(purse);
        a.Gold -= cost;
        hall.Cash += cost;
        a.Hero!.FeastDay = town.Day;
        Log(town, a, Rules(town).Lines.Feast, ("tavern", hall.Name), ("cost", Js.Str(cost)), importance: Importance.Routine);
    }

    /// <summary>The first house gives way to a manor of their own: the price is a land sale to the town, and the hero is rooted for good.</summary>
    private static void BuyManor(Town town, Agent a)
    {
        var f = Rules(town).Fortunes;
        var s = a.Hero!;
        double cost = Js.Round(ManorCost(town, a));
        a.Gold -= cost;
        town.Book(TreasuryAccount.LandSales, cost);
        s.Manor = true;
        s.Holding = f.Manors[(int)(Voices.Hash("manor", a.Id) % (uint)f.Manors.Count)];
        s.Loyalty = Math.Min(100, s.Loyalty + f.ManorLoyalty);
        Log(town, a, Rules(town).Lines.Manor, ("holding", s.Holding), ("cost", Js.Str(cost)));
    }

    /// <summary>A rich veteran hangs up their sword to teach, and leaves a share of their fortune to the town.</summary>
    private static void RichRetire(Town town, Agent a, double spare)
    {
        double gift = Math.Floor(spare * Rules(town).Fortunes.RetireShare);
        a.Gold -= gift;
        town.Book(TreasuryAccount.Patron, gift);
        string name = a.Name;
        Legacies.Retire(town, a);
        town.Log(LedgerCategory.Social, Importance.Major, Stories.Cap(Rules(town).Lines.Retire.Replace("{hero}", name).Replace("{cost}", Js.Str(gift))), [a.Id]);
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

    /// <summary>
    /// A famous hero with a fortune puts it into the town, the first that fits: the next part of a monument, a piece from the
    /// forge made for them from the town's parts, their god's shrine (its fund if it isn't raised, its favour if it
    /// is). The town gets the gold, the hero a name for it.
    /// </summary>
    private static void Patron(Town town, Agent a, double purse)
    {
        var r = Rules(town);
        foreach (var core in Monuments.All(town).Where(c => c.Complete).OrderBy(c => c.Id))
        {
            if (Monuments.Parts(town, core).Any(p => !p.Complete) || Monuments.Next(town, core) is not { } part) continue;
            double cost = Monuments.Cost(town, part);
            if (cost > purse || Monuments.Blocked(town, core, part) is { } why && why != "not enough gold") continue;
            town.Book(TreasuryAccount.Patron, cost);
            if (Monuments.Add(town, core, part.Id) == null)
            {
                town.Book(TreasuryAccount.Patron, -cost);
                continue;
            }
            a.Gold -= cost;
            Gave(town, a);
            Log(town, a, r.Lines.Monument, ("part", part.Name), ("monument", core.Name), ("cost", Js.Str(cost)));
            return;
        }
        if (Spoils.ForHero(town, a, purse) is { } order)
        {
            Gave(town, a);
            Log(town, a, r.Lines.Forge, ("item", order.Item), ("cost", Js.Str(order.Cost)), ("shop", order.Shop));
            return;
        }
        if (Faith.God(town, a.Faith) is { Evil: false } god)
        {
            double gift = Math.Floor(purse);
            a.Gold -= gift;
            bool standing = town.Faiths.Dedications.Values.Contains(god.Id);
            if (standing) town.Faiths.Favor[god.Id] = town.Faiths.Favor.Get(god.Id, 0) + gift * r.Patron.FavourPerGold;
            else town.Faiths.Funds[god.Id] = town.Faiths.Funds.Get(god.Id, 0) + gift;
            Gave(town, a);
            Log(town, a, standing ? r.Lines.Endow : r.Lines.Shrine, ("god", Faith.Prose(god)), ("cost", Js.Str(gift)));
        }
    }

    private static void Gave(Town town, Agent a)
    {
        a.Hero!.GaveDay = town.Day;
        Notoriety.Deed(town, a, "patron", Rules(town).Patron.Fame);
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

    private static void Log(Town town, Agent a, string line, (string Key, string Value) x, (string Key, string Value) y, Importance importance = Importance.Notable) =>
        Log(town, a, line, [x, y], importance);

    private static void Log(Town town, Agent a, string line, (string Key, string Value) x, (string Key, string Value) y, (string Key, string Value) z) =>
        Log(town, a, line, [x, y, z], Importance.Notable);

    private static void Log(Town town, Agent a, string line, (string Key, string Value)[] keys, Importance importance)
    {
        if (line == "") return;
        string text = line.Replace("{hero}", a.Name);
        foreach (var (k, v) in keys) text = text.Replace("{" + k + "}", v);
        town.Log(LedgerCategory.Social, importance, Stories.Cap(text), [a.Id]);
    }
}
