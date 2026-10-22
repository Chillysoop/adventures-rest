using System.Text.Json.Serialization;

namespace CrownAndCompass.Sim.Systems;

/// <summary>A year omen (content/game/omens.json): what the seers foretell, the season it may fall in, and what it does while it lasts.</summary>
public sealed record OmenDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>A boon (true) or a trial, for colour.</summary>
    public bool Good { get; init; }
    public double Weight { get; init; } = 1;

    /// <summary>Season ids it may fall in.</summary>
    public IReadOnlyList<string> Seasons { get; init; } = [];

    /// <summary>The seers' advice when it is foretold, and the Ledger's line as it comes.</summary>
    public string Text { get; init; } = "";
    public string Comes { get; init; } = "";

    /// <summary>Folded into the season's own numbers while it lasts (<see cref="SeasonDef"/>).</summary>
    public double Mood { get; init; }
    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Yields { get; init; } = [];
    public double Arrivals { get; init; }
    public double Tourists { get; init; } = 1;
    public double Build { get; init; } = 1;
    public double Menace { get; init; } = 1;
    public double Warband { get; init; } = 1;
    public int MarchDays { get; init; }

    /// <summary>Added to the daily chance a hero walks into town.</summary>
    public double Heroes { get; init; }

    /// <summary>How fast sickness spreads, times its usual (<see cref="Sickness"/>).</summary>
    public double Sick { get; init; } = 1;

    public double YieldOf(Good g)
    {
        foreach (var (k, v) in Yields) if (k == g) return v;
        return 1;
    }
}

public sealed record OmenRules
{
    /// <summary>The Ledger as a year's omen is foretold ({town}, {omen}, {when}, {text}) and as it comes ({omen}, {comes}).</summary>
    public string Foretold { get; init; } = "";
    public string Begins { get; init; } = "";
    public List<OmenDef> Omens { get; init; } = [];
}

/// <summary>This year's omen: which, the season of the year it falls in (0 is spring), and whether it has come.</summary>
public sealed class OmenState
{
    public int Year { get; set; }
    public string Id { get; set; } = "";
    public int Season { get; set; } = -1;
    public bool Come { get; set; }

    /// <summary>Every year's omen so far, oldest first ("year:id").</summary>
    public List<string> Past { get; } = [];
}

/// <summary>
/// Year omens, after Northgard's harsh years: as each year turns, the seers foretell one omen (a harsh winter, a bumper
/// harvest, the blood moon, fair winds, a plague year...) for a season still to come, so the town can get ready. While
/// that season lasts its numbers fold into the season's own (<see cref="Seasons"/>). Omens are picked on their own dice,
/// seeded by the year, so the town's stream is untouched.
/// </summary>
public static class Omens
{
    public static OmenRules Rules(Town town) => town.Content.Omens;

    public static OmenDef? Def(Town town, string id) => Rules(town).Omens.FirstOrDefault(o => o.Id == id);

    /// <summary>The omen foretold for this year, come or still to come.</summary>
    public static OmenDef? Foretold(Town town) =>
        town.Omens.Year == Seasons.Year(town) && town.Omens.Id != "" ? Def(town, town.Omens.Id) : null;

    /// <summary>The omen in force now, or null.</summary>
    public static OmenDef? Now(Town town) =>
        Foretold(town) is { } o && SeasonOfYear(town) == town.Omens.Season ? o : null;

    /// <summary>The season of the year it is, 0 for spring.</summary>
    public static int SeasonOfYear(Town town) => Seasons.Index(town) % Math.Max(1, town.Content.Seasons.Seasons.Count);

    /// <summary>Days until the foretold omen comes; 0 while it is here, null when it has passed or none is foretold.</summary>
    public static int? DaysUntil(Town town)
    {
        if (Foretold(town) == null) return null;
        int now = SeasonOfYear(town);
        if (town.Omens.Season < now) return null;
        if (town.Omens.Season == now) return 0;
        return Seasons.DaysLeft(town) + (town.Omens.Season - now - 1) * Seasons.Rules(town).Days;
    }

    /// <summary>A new year: the seers foretell its omen. The season it falls in comes: the Ledger says so.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.Omens.Count == 0) return;
        var st = town.Omens;
        int year = Seasons.Year(town);
        int now = SeasonOfYear(town);
        var seasons = town.Content.Seasons.Seasons.ToList();
        if (st.Year != year)
        {
            st.Year = year;
            st.Id = "";
            st.Season = -1;
            st.Come = false;
            var dice = new Rng(unchecked((int)Voices.Hash("omen", town.Seed, year)));
            // Only seasons still to come: the seers foretell, they don't report.
            var open = r.Omens
                .Select(o => (O: o, Later: o.Seasons.Select(id => seasons.FindIndex(s => s.Id == id)).Where(i => i > now).ToList()))
                .Where(x => x.Later.Count > 0)
                .ToList();
            if (open.Count == 0) return;
            double total = open.Sum(x => x.O.Weight);
            double roll = dice.Next() * total;
            var pick = open[^1];
            foreach (var x in open)
            {
                roll -= x.O.Weight;
                if (roll < 0)
                {
                    pick = x;
                    break;
                }
            }
            st.Id = pick.O.Id;
            st.Season = pick.Later[dice.Range(0, pick.Later.Count)];
            st.Past.Add($"{year}:{st.Id}");
            string when = $"in the {seasons[st.Season].Name.ToLowerInvariant()}";
            string told = r.Foretold.Replace("{town}", town.Name).Replace("{omen}", pick.O.Name.ToLowerInvariant()).Replace("{when}", when).Replace("{text}", pick.O.Text);
            town.Log(LedgerCategory.Town, Importance.Notable, Saga.Cap(told));
        }
        if (!st.Come && st.Season == now && Def(town, st.Id) is { } o)
        {
            st.Come = true;
            town.Log(LedgerCategory.Town, Importance.Major, Saga.Cap(r.Begins.Replace("{town}", town.Name).Replace("{omen}", o.Name).Replace("{comes}", o.Comes)));
            Records.Pin(town, "omen", o.Name);
        }
    }

    // Effects, read through the season's own ----------------------------------------------------

    public static double Mood(Town town) => Now(town)?.Mood ?? 0;

    public static double Yield(Town town, Good g) => Now(town)?.YieldOf(g) ?? 1;

    public static double Arrivals(Town town) => Now(town)?.Arrivals ?? 0;

    public static double Tourists(Town town) => Now(town)?.Tourists ?? 1;

    public static double Build(Town town) => Now(town)?.Build ?? 1;

    public static double Menace(Town town) => Now(town)?.Menace ?? 1;

    public static double Warband(Town town) => Now(town)?.Warband ?? 1;

    public static int MarchDays(Town town) => Now(town)?.MarchDays ?? 0;

    public static double Heroes(Town town) => Now(town)?.Heroes ?? 0;

    public static double Sick(Town town) => Now(town)?.Sick ?? 1;
}
