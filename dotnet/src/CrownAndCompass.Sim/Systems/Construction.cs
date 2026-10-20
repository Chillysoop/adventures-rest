namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// How buildings go up, after Foundation: the townsfolk's own builds on painted land (homes, shops, workshops, farms) are
/// raised a few at a time by the town's builders, as many at once as it has crews (<see cref="ConstructionRules"/>), so a
/// zone fills in house by house rather than all at once. Every site needs its timber and stone carried onto it before the
/// walls can rise past their footings: a builder whose site has run out of materials spends the hour hauling them from the
/// lumber camp or quarry (<see cref="Activity.Hauling"/>), then builds with what is there.
/// </summary>
public static class Construction
{
    public static ConstructionRules Rules(Town town) => town.Content.City.Construction;

    /// <summary>A site the townsfolk raise on painted land, which waits its turn for a crew (the council's works and bastions go up on their own).</summary>
    public static bool Zoned(Town town, Building b) =>
        Planning.Paintable.Contains(b.Def.Zone) && !b.Def.Bastion && !b.Def.IsGrand && !Planning.IsGovernment(town, b.Def);

    /// <summary>How many of the townsfolk's builds the town's crews can have under way at once: more as the town grows.</summary>
    public static int Crews(Town town)
    {
        var r = Rules(town);
        return Math.Min(r.Most, r.Crews + town.Population / Math.Max(1, r.Residents));
    }

    /// <summary>The townsfolk's builds under way.</summary>
    public static int UnderWay(Town town)
    {
        int n = 0;
        foreach (var b in town.Buildings)
            if (b.IsSite && Zoned(town, b)) n++;
        return n;
    }

    /// <summary>Whether a crew is free to break ground on another of the townsfolk's builds.</summary>
    public static bool Room(Town town) => UnderWay(town) < Crews(town);

    /// <summary>Loads of timber and stone a building takes.</summary>
    public static double Materials(BuildingDef d) => Math.Max(0, d.Timber) + Math.Max(0, d.Stone);

    /// <summary>The share of the materials on site a building starts with: all of them when it needs none, or is raised whole (grand works bring theirs stage by stage).</summary>
    public static double Start(BuildingDef d, bool complete) => complete || d.IsGrand || Materials(d) <= 0 ? 1 : 0;

    /// <summary>Builder-hours a building takes: raising it, and an average builder's hauling of its materials (what its labour budget pays for).</summary>
    public static double Hours(Town town, BuildingDef d) => d.BuildHours + (d.IsGrand ? 0 : Materials(d) / Rules(town).Load);

    /// <summary>Builder-hours left on a site, hauling included.</summary>
    public static double HoursLeft(Town town, Building b) => Math.Max(0, b.Def.BuildHours - b.BuildProgress) + (1 - b.Hauled) * Materials(b.Def) / Rules(town).Load;

    /// <summary>How far the builders can take a site with the materials on it, in build hours: its footings, then as far as the timber and stone go.</summary>
    public static double Ready(Town town, Building b) =>
        b.Def.BuildHours * Math.Min(1, Rules(town).Footings + b.Hauled * (1 - Rules(town).Footings));

    /// <summary>Whether the site's builders have built up to what has been carried onto it, so the next hour goes on hauling.</summary>
    public static bool Short(Town town, Building b) => b.IsSite && b.Hauled < 1 && b.BuildProgress >= Ready(town, b) - 1e-9;

    /// <summary>An hour's hauling onto a site by someone of the given strength modifier (day labourers count as average).</summary>
    public static void Haul(Town town, Building b, int strength = 0)
    {
        double loads = Materials(b.Def);
        if (loads <= 0)
        {
            b.Hauled = 1;
            return;
        }
        double carry = Rules(town).Load * Math.Max(0.5, 1 + 0.1 * strength);
        b.Hauled = Math.Min(1, b.Hauled + carry / loads);
    }

    /// <summary>
    /// Where a site's materials come from: the nearest lumber camp or quarry that stands (stone first for a building that is
    /// mostly stone), or null for the market at the square. The client walks its haulers between there and the site.
    /// </summary>
    public static Building? Yard(Town town, Building site)
    {
        string want = site.Def.Stone > site.Def.Timber ? "Stone" : "Timber";
        var lot = town.Lot(site.LotId);
        Building? best = null;
        double bestD = double.MaxValue;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Def.Produces.Count == 0 || !b.Def.Produces.Any(p => p.Key.ToString() == want)) continue;
            var l = town.Lot(b.LotId);
            double d = (l.X - lot.X) * (l.X - lot.X) + (l.Y - lot.Y) * (l.Y - lot.Y);
            if (d < bestD)
            {
                bestD = d;
                best = b;
            }
        }
        return best;
    }
}

/// <summary>
/// Building rules (city.json "construction"): <see cref="Crews"/> crews to begin with and one more per <see cref="Residents"/>
/// residents, up to <see cref="Most"/>; the share of a build its footings take before any materials are needed
/// (<see cref="Footings"/>); and the loads of timber or stone a builder carries onto a site in an hour (<see cref="Load"/>).
/// </summary>
public sealed record ConstructionRules
{
    public int Crews { get; init; } = 2;
    public int Residents { get; init; } = 25;
    public int Most { get; init; } = 40;
    public double Footings { get; init; } = 0.1;
    public double Load { get; init; } = 3;
}
