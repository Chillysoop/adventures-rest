namespace CrownAndCompass.Sim.Systems;

public sealed record SickHealers
{
    /// <summary>Share of spread and of sick days each kind of healer takes off, and the most all of them together can.</summary>
    public double Shrine { get; init; } = 0.15;
    public double Temple { get; init; } = 0.3;
    public double Bathhouse { get; init; } = 0.15;
    public double Most { get; init; } = 0.7;
}

public sealed record SickQuarantine
{
    /// <summary>Spread times this while the sick lanes are closed, and the mood every townsperson carries for it.</summary>
    public double Spread { get; init; } = 0.3;
    public double Mood { get; init; } = -2;
}

public sealed record SickTend
{
    /// <summary>Gold a sick house to send the healers door to door, and the days it leaves the sick abed at most.</summary>
    public double Cost { get; init; } = 8;
    public int Days { get; init; } = 2;
}

/// <summary>Sickness in town (content/game/sickness.json).</summary>
public sealed record SicknessRules
{
    /// <summary>Houses within this many tiles of a sick one are at risk, the nearest first, at most so many.</summary>
    public double Reach { get; init; } = 2.8;
    public int Neighbours { get; init; } = 4;

    /// <summary>Daily chance a sick house passes it to each house at risk, before water and healers.</summary>
    public double Spread { get; init; } = 0.16;

    /// <summary>Days a house stays sick (before healers), and days after it is over that it can't catch it again.</summary>
    public int[] Days { get; init; } = [6, 10];
    public int Immune { get; init; } = 30;

    /// <summary>Days after an outbreak ends before rats, pilgrims or a caravan can start another.</summary>
    public int Rest { get; init; } = 45;

    /// <summary>How much of the wells' and fountains' reach (<see cref="Decor.Douse"/>) counts as clean water against it.</summary>
    public double Water { get; init; } = 0.8;
    public SickHealers Healers { get; init; } = new();
    public SickQuarantine Quarantine { get; init; } = new();

    /// <summary>Mood a townsperson of a sick house carries.</summary>
    public double SickMood { get; init; } = -10;
    public SickTend Tend { get; init; } = new();

    /// <summary>Enemy groups whose sites near town bring it (groups.json ids), how near (days), and the daily chance each does.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];
    public int GroupReach { get; init; } = 3;
    public double GroupChance { get; init; } = 0.012;

    /// <summary>Chance a caravan home from a feverish settlement brings it; and any caravan, in a plague year.</summary>
    public double Caravan { get; init; } = 0.35;
    public double PlagueYearCaravan { get; init; } = 0.1;
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

public sealed class SicknessState
{
    /// <summary>The council has closed the sick lanes.</summary>
    public bool Quarantine { get; set; }

    /// <summary>Houses that have fallen sick, all told, and outbreaks (a first sick house while none was).</summary>
    public int Houses { get; set; }
    public int Outbreaks { get; set; }

    /// <summary>The most houses sick at once in the current outbreak.</summary>
    public int Peak { get; set; }

    /// <summary>The day the last outbreak ended, or -999.</summary>
    public int LastOver { get; set; } = -999;
}

/// <summary>
/// Sickness, after Kingdoms and Castles' plague: like fire, it only ever comes when something brings it, a plague-bringer's
/// night visit, rats or grey pilgrims from a plague group's site near town, or a caravan home from a feverish village.
/// Then it spreads house to house, unless clean water from wells and fountains and the town's healers beat it back.
/// The sick stay home abed and are miserable, and nobody dies of it. The council can close the sick lanes (slower
/// spread, a gloomier town), send the healers door to door for gold, and the alchemists' fever-breakers cure a house
/// outright. Spread rolls its own dice, seeded by the house and the day, so the town's stream is untouched.
/// </summary>
public static class Sickness
{
    public static SicknessRules Rules(Town town) => town.Content.Sickness;

    private static string Text(Town town, string key) => Rules(town).Texts.TryGetValue(key, out var t) ? t : "";

    public static bool IsSick(Town town, Building b) => b.SickUntil > town.Tick;

    /// <summary>The sick houses, by id.</summary>
    public static List<Building> Sick(Town town) => town.Buildings.Where(b => b.SickUntil > town.Tick).ToList();

    public static bool Outbreak(Town town) => town.Buildings.Any(b => b.SickUntil > town.Tick);

    /// <summary>Townsfolk abed in sick houses.</summary>
    public static int Abed(Town town) => Sick(town).Sum(b => b.Residents.Count);

    /// <summary>A townsperson of a sick house stays in by day.</summary>
    public static bool Holds(Town town, Agent a) => a.HomeId is int h && town.Building(h).SickUntil > town.Tick;

    /// <summary>Mood a townsperson carries for it: their own house sick, and the sick lanes closed.</summary>
    public static double Mood(Town town, Agent a)
    {
        var r = Rules(town);
        return (Holds(town, a) ? r.SickMood : 0) + (town.Sickness.Quarantine ? r.Quarantine.Mood : 0);
    }

    /// <summary>Share the town's healers take off the spread and the sick days.</summary>
    public static double Healers(Town town)
    {
        var h = Rules(town).Healers;
        double cover = (Shrines.Temple(town) != null ? h.Temple : Shrines.Shrine(town) != null ? h.Shrine : 0)
            + (town.Buildings.Any(b => b.Def.Id == "bathhouse" && b.Complete && !b.Closed) ? h.Bathhouse : 0);
        return Math.Min(h.Most, cover);
    }

    private static Rng Dice(Town town, int salt) => new(unchecked((int)Voices.Hash("sick", town.Seed, town.Day, salt)));

    private static bool House(Building b) => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0;

    /// <summary>Something brought it to this house. Returns false when the house is sick already or still immune.</summary>
    public static bool Seed(Town town, Building house, string why)
    {
        if (!House(house) || IsSick(town, house) || town.Tick < house.SickUntil + Rules(town).Immune * SimTime.HoursPerDay && house.SickUntil > 0) return false;
        bool first = !Outbreak(town);
        Fall(town, house, Dice(town, house.Id));
        if (first) town.Sickness.Outbreaks++;
        town.Sickness.Peak = Math.Max(town.Sickness.Peak, Sick(town).Count);
        town.Log(LedgerCategory.Hardship, Importance.Major, Text(town, "seed").Replace("{house}", house.Name == "" ? "a house" : house.Name).Replace("{why}", why), house.Residents.OrderBy(id => id).ToList());
        Records.Pin(town, "sickness", house.Name);
        return true;
    }

    /// <summary>A house the plague-bringer visited: whoever lived there, the house falls sick.</summary>
    public static void Night(Town town, int? homeId)
    {
        if (homeId is int h) Seed(town, town.Building(h), Text(town, "night"));
    }

    /// <summary>A caravan home from a settlement: a feverish one may bring it, and any one in a plague year.</summary>
    public static void Caravan(Town town, Settlement s)
    {
        var r = Rules(town);
        if (Resting(town)) return;
        double chance = ((s.Trouble?.Def == "fever" ? r.Caravan : 0) + (Omens.Sick(town) > 1 ? r.PlagueYearCaravan : 0)) * (1 - Regalia.Sick(town));
        if (chance <= 0) return;
        var dice = Dice(town, 7000 + s.Id);
        if (!dice.Chance(chance)) return;
        var houses = town.Buildings.Where(House).ToList();
        if (houses.Count == 0) return;
        // The drovers live nearest the market road: any house will do.
        Seed(town, houses[dice.Range(0, houses.Count)], Text(town, "caravan").Replace("{settlement}", s.Name));
    }

    /// <summary>The day: plague groups near town may bring it, the sick may pass it on, and the sick get better.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        var st = town.Sickness;
        Groups(town);
        var sick = Sick(town);
        if (sick.Count == 0)
        {
            if (st.Peak > 0)
            {
                town.Log(LedgerCategory.Hardship, Importance.Notable, Text(town, "over").Replace("{town}", town.Name));
                st.Peak = 0;
                st.Quarantine = false;
                st.LastOver = town.Day;
            }
            return;
        }

        // Once it has spread, a fever-breaker from the shelf cures the fullest sick house outright.
        var worst = sick.OrderByDescending(b => b.Residents.Count).ThenBy(b => b.Id).First();
        if (sick.Count >= 2 && Alchemy.Cure(town) is string cure)
        {
            worst.SickUntil = town.Tick;
            town.Log(LedgerCategory.Hardship, Importance.Routine, Text(town, "cured").Replace("{cure}", cure).Replace("{house}", worst.Name == "" ? "a house" : worst.Name), worst.Residents.OrderBy(id => id).ToList());
            sick.Remove(worst);
        }

        double healers = Healers(town);
        double chance = r.Spread * (town.Sickness.Quarantine ? r.Quarantine.Spread : 1) * (1 - healers) * Omens.Sick(town);
        var caught = new List<(Building From, Building To)>();
        foreach (var b in sick)
        {
            var o = town.Lot(b.LotId);
            double water = Math.Min(0.9, Decor.Douse(town, o.X, o.Y) * r.Water);
            double c = chance * (1 - water);
            if (c <= 0) continue;
            var dice = Dice(town, b.Id);
            foreach (var n in Near(town, b))
            {
                if (caught.Any(x => x.To == n) || !dice.Chance(c)) continue;
                caught.Add((b, n));
            }
        }
        foreach (var (from, to) in caught) Fall(town, to, Dice(town, 100000 + to.Id));
        foreach (var g in caught.GroupBy(x => x.From.Id).OrderBy(g => g.Key))
        {
            var from = g.First().From;
            var list = g.Select(x => x.To.Name == "" ? "a house" : x.To.Name).ToList();
            var who = g.SelectMany(x => x.To.Residents).Distinct().OrderBy(id => id).ToList();
            town.Log(LedgerCategory.Hardship, Importance.Notable, Text(town, "spread").Replace("{from}", from.Name == "" ? "a house" : from.Name).Replace("{list}", Expedition.ListNames(list)), who);
        }
        st.Peak = Math.Max(st.Peak, Sick(town).Count);
    }

    private static void Fall(Town town, Building house, Rng dice)
    {
        var r = Rules(town);
        int days = dice.Range(r.Days[0], r.Days[^1] + 1);
        days = Math.Max(1, (int)Math.Round(days * (1 - Healers(town) * 0.5)));
        house.SickUntil = town.Tick + days * SimTime.HoursPerDay;
        town.Sickness.Houses++;
    }

    /// <summary>The houses at risk round a sick one, nearest first.</summary>
    private static List<Building> Near(Town town, Building origin)
    {
        var r = Rules(town);
        var o = town.Lot(origin.LotId);
        double Dist(Building b)
        {
            var l = town.Lot(b.LotId);
            return Math.Sqrt((l.X - o.X) * (l.X - o.X) + (l.Y - o.Y) * (l.Y - o.Y));
        }
        return town.Buildings
            .Where(b => b != origin && House(b) && !IsSick(town, b) && !(b.SickUntil > 0 && town.Tick < b.SickUntil + r.Immune * SimTime.HoursPerDay) && Dist(b) <= r.Reach)
            .OrderBy(Dist).ThenBy(b => b.Id)
            .Take(r.Neighbours)
            .ToList();
    }

    /// <summary>Sick now, or too soon after the last outbreak for the roads to bring another.</summary>
    private static bool Resting(Town town) => Outbreak(town) || town.Day - town.Sickness.LastOver < Rules(town).Rest;

    /// <summary>Rats and grey pilgrims from a plague group's site near town.</summary>
    private static void Groups(Town town)
    {
        var r = Rules(town);
        if (r.Groups.Count == 0 || Resting(town)) return;
        foreach (var s in town.Sites)
        {
            if (s.Cleared || !r.Groups.Contains(s.Def.Group) || s.Def.Distance > r.GroupReach) continue;
            var dice = Dice(town, unchecked((int)Voices.Hash(s.Def.Id)));
            if (!dice.Chance(r.GroupChance * Omens.Sick(town) * (1 - Regalia.Sick(town)))) continue;
            var houses = town.Buildings.Where(House).ToList();
            if (houses.Count == 0) return;
            string why = (s.Def.Group == "sewer-infestation" ? Text(town, "rats") : Text(town, "pilgrims")).Replace("{site}", s.Def.Name);
            Seed(town, houses[dice.Range(0, houses.Count)], why);
            return;
        }
    }

    /// <summary>Close or open the sick lanes. Returns false when there is nothing to do.</summary>
    public static bool SetQuarantine(Town town, bool on)
    {
        if (town.Sickness.Quarantine == on || on && !Outbreak(town) || on && Council.Short(town, "quarantine") != null) return false;
        if (on) Council.Spend(town, "quarantine");
        town.Sickness.Quarantine = on;
        town.Log(LedgerCategory.Council, Importance.Notable, Text(town, on ? "quarantine" : "lifted"));
        return true;
    }

    /// <summary>What sending the healers door to door costs now.</summary>
    public static double TendCost(Town town) => Js.Round(Sick(town).Count * Rules(town).Tend.Cost);

    /// <summary>Pay the healers to go door to door: no sick house stays abed past a day or two. Returns why not, or null when done.</summary>
    public static string? Tend(Town town)
    {
        var sick = Sick(town);
        if (sick.Count == 0) return "nobody is sick";
        double cost = TendCost(town);
        if (town.Treasury < cost) return "not enough gold";
        town.Book(TreasuryAccount.Temple, -cost);
        int until = town.Tick + Rules(town).Tend.Days * SimTime.HoursPerDay;
        foreach (var b in sick) b.SickUntil = Math.Min(b.SickUntil, until);
        town.Log(LedgerCategory.Hardship, Importance.Notable, Text(town, "tended").Replace("{gold}", Js.Str(cost)));
        return null;
    }
}
