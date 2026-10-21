namespace CrownAndCompass.Sim.Systems;

/// <summary>One thing adventurers judge a town by (word.json <c>aspects</c>), and what they say when it is good or bad.</summary>
public sealed record WordAspectDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>What a hero says of it: {town}, {tavern}.</summary>
    public IReadOnlyList<string> Good { get; init; } = [];
    public IReadOnlyList<string> Bad { get; init; } = [];
}

/// <summary>Adventurers' word of the town (content/game/word.json).</summary>
public sealed record WordRules
{
    /// <summary>Stars (of five) at which the word neither draws heroes nor keeps them away.</summary>
    public double Neutral { get; init; } = 2.5;

    /// <summary>Added to the daily chance a hero walks in, for each star above neutral (taken off below it).</summary>
    public double Arrivals { get; init; } = 0.06;

    /// <summary>Less chance a newcomer is green, for each star above neutral: seasoned heroes go where the word is good.</summary>
    public double Seasoned { get; init; } = 0.05;

    /// <summary>Days between one hero's say and the next, and the says kept.</summary>
    public int Every { get; init; } = 4;
    public int Keep { get; init; } = 8;

    /// <summary>Recent pay a hero thinks fair, per level, for full stars on pay.</summary>
    public double FairPay { get; init; } = 20;
    public List<WordAspectDef> Aspects { get; init; } = [];
}

/// <summary>What a hero said of the town, and the stars they gave it.</summary>
public sealed class Review
{
    public int Day { get; set; }
    public int HeroId { get; set; }
    public string Aspect { get; set; } = "";
    public int Stars { get; set; }
    public bool Good { get; set; }
    public string Text { get; set; } = "";
}

public sealed class WordState
{
    /// <summary>The latest says, newest last.</summary>
    public List<Review> Reviews { get; } = [];
}

/// <summary>
/// Word of mouth, after MMORPG Tycoon 2: heroes are the town's customers, and they talk. They judge its beds, its pay,
/// the gear in its shops, its healers and how the mayor treats them, and give each up to five stars. Good word on the
/// roads brings heroes in sooner and more of them seasoned; bad word keeps them away. Every few days one hero has
/// their say about the best or the worst of it. Picking who speaks uses its own dice, so the town's stream is untouched.
/// </summary>
public static class Word
{
    public static WordRules Rules(Town town) => town.Content.Word;

    /// <summary>Stars (0 to 5) for each aspect, in the file's order.</summary>
    public static List<(WordAspectDef Def, double Stars)> Aspects(Town town) =>
        Rules(town).Aspects.Select(d => (d, Stars(town, d.Id))).ToList();

    /// <summary>The town's rating, the average of its aspects (0 to 5).</summary>
    public static double Rating(Town town)
    {
        var all = Rules(town).Aspects;
        return all.Count == 0 ? Rules(town).Neutral : all.Average(d => Stars(town, d.Id));
    }

    /// <summary>Stars for one aspect, 0 to 5, to a half star.</summary>
    public static double Stars(Town town, string aspect)
    {
        var home = town.Heroes.Where(a => !a.IsAway).ToList();
        var heroes = town.Heroes;
        double s = aspect switch
        {
            "beds" => Beds(town, home),
            "pay" => heroes.Count == 0 ? 2.5 : 5 * heroes.Average(a => Math.Min(1, a.Hero!.Pay / (Math.Max(1, a.Hero.Level) * Rules(town).FairPay))) + Math.Min(1, town.Bounties.Count(b => b.Status == BountyStatus.Open) * 0.5),
            "gear" => (Built(town, town.Content.Gear.Forge) ? 2.5 : 0) + (Built(town, town.Content.Gear.Store) ? 1 : 0) + Math.Min(1, town.Wares.Count / 3.0) + Math.Min(1, town.Armory.Stock.Count / 2.0),
            "healing" => (Shrines.Temple(town) != null ? 3 : Shrines.Shrine(town) != null ? 1.5 : 0) + (town.Alchemy.Shelf.Values.Sum() > 0 ? 1 : 0) + (home.Any(a => a.Hero!.Cls.Healer) ? 1 : 0),
            "welcome" => heroes.Count == 0 ? 2.5 : heroes.Average(a => a.Hero!.Loyalty) / 20,
            _ => Rules(town).Neutral,
        };
        return Math.Clamp(Math.Round(s * 2) / 2, 0, 5);
    }

    private static double Beds(Town town, List<Agent> home)
    {
        var venues = town.Taverns;
        if (venues.Count == 0) return 0;
        if (home.Count == 0) return Math.Min(5, 1.5 * venues.Count);
        double lodged = home.Count(a => a.Hero!.Lodging) / (double)home.Count;
        int visits = venues.Sum(v => v.VisitsLast + v.TurnedAwayLast);
        double turned = visits == 0 ? 0 : venues.Sum(v => v.TurnedAwayLast) / (double)visits;
        return 5 * lodged - 3 * turned;
    }

    private static bool Built(Town town, string id) => town.Buildings.Any(b => b.Def.Id == id && b.Complete && !b.Closed && !b.ForSale);

    /// <summary>Added to the daily chance a hero walks in.</summary>
    public static double Arrivals(Town town) => (Rating(town) - Rules(town).Neutral) * Rules(town).Arrivals;

    /// <summary>Taken off the chance a newcomer is green.</summary>
    public static double Seasoned(Town town) => (Rating(town) - Rules(town).Neutral) * Rules(town).Seasoned;

    /// <summary>Every few days a hero home has their say about the best or the worst of the town.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.Aspects.Count == 0 || town.Day % Math.Max(1, r.Every) != 0) return;
        var home = town.Heroes.Where(a => !a.IsAway).OrderBy(a => a.Id).ToList();
        if (home.Count == 0) return;
        var dice = new Rng(unchecked((int)Voices.Hash("word", town.Seed, town.Day)));
        var a = home[dice.Range(0, home.Count)];
        var scored = Aspects(town);
        // What sticks with them most: the worst of it, unless the best stands out further from the middle.
        var worst = scored.OrderBy(x => x.Stars).ThenBy(x => x.Def.Id, StringComparer.Ordinal).First();
        var best = scored.OrderByDescending(x => x.Stars).ThenBy(x => x.Def.Id, StringComparer.Ordinal).First();
        bool good = best.Stars - r.Neutral > r.Neutral - worst.Stars;
        var pick = good ? best : worst;
        var lines = good ? pick.Def.Good : pick.Def.Bad;
        if (lines.Count == 0) return;
        var venues = town.Taverns;
        string tavern = venues.Count == 0 ? "the inn" : venues[a.Id % venues.Count].Name;
        string text = lines[dice.Range(0, lines.Count)].Replace("{town}", town.Name).Replace("{tavern}", tavern);
        var reviews = town.Word.Reviews;
        reviews.Add(new Review { Day = town.Day, HeroId = a.Id, Aspect = pick.Def.Id, Stars = (int)Math.Round(Rating(town)), Good = good, Text = text });
        while (reviews.Count > Math.Max(1, r.Keep)) reviews.RemoveAt(0);
    }
}
