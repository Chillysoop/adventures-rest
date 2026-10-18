namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// How tightly the town may build (city.json "densities"), after the way real and storied cities grow:
/// a Phandalin of cottages and gardens, then a walled core like Baldur's Gate's Lower City where tall houses
/// crowd wall to wall, while lower sprawl spills out past the gates. Density is painted with the zone
/// (<see cref="Land.Brush"/>) and kept per plan cell; it decides how tall a house may grow
/// (<see cref="BuildingDef.Density"/>), whether neighbours keep a lane between them, what the land is
/// worth, and the crowding its residents feel. Medium and high density unlock with the charters.
/// </summary>
public static class Density
{
    public const int Low = 0, Medium = 1, High = 2;

    private static readonly DensityDef Fallback = new() { Id = "low", Name = "Low" };

    /// <summary>Only homes and shops are built tall; fields and workshops keep their room.</summary>
    public static bool Takes(Zone zone) => zone is Zone.Residential or Zone.Commercial;

    public static DensityDef Def(Town town, int level)
    {
        var all = town.Content.City.Densities;
        return all.Count == 0 ? Fallback : all[Math.Clamp(level, 0, all.Count - 1)];
    }

    public static string Name(Town town, int level) => Def(town, level).Name;

    /// <summary>The density a plan cell is zoned for.</summary>
    public static int At(Town town, int cell) => town.Plan.Density[cell];

    /// <summary>The density zoned at a point of the town (tiles from its corner); low off the plan.</summary>
    public static int At(Town town, double x, double y)
    {
        int cx = (int)Math.Floor(x * Land.Res), cy = (int)Math.Floor(y * Land.Res);
        return cx < 0 || cy < 0 || cx >= Land.Size || cy >= Land.Size ? Low : town.Plan.Density[cy * Land.Size + cx];
    }

    public static int At(Town town, Lot lot) => At(town, lot.X, lot.Y);

    /// <summary>The densest the town's charter lets it zone.</summary>
    public static int Unlocked(Town town)
    {
        var all = town.Content.City.Densities;
        int best = Low;
        for (int i = 0; i < all.Count; i++)
            if (City.Reached(town, all[i].Tier)) best = i;
        return best;
    }

    /// <summary>Why the town can't zone this density yet, or null when it can.</summary>
    public static string? Blocked(Town town, int level)
    {
        if (level <= Unlocked(town)) return null;
        var d = Def(town, level);
        return $"{d.Name.ToLowerInvariant()} density needs the town to be a {City.TierName(town, d.Tier).ToLowerInvariant()}";
    }

    /// <summary>Whether the land at a point is zoned dense enough for the building.</summary>
    public static bool Allows(Town town, BuildingDef d, double x, double y) => d.Density <= At(town, x, y);

    /// <summary>Cells of open ground kept round a building on land of this density.</summary>
    public static int Lane(Town town, int level) => Def(town, level).Lane;

    /// <summary>What zoning dense does to the price of a lot.</summary>
    public static double LandFactor(Town town, Lot lot) => Def(town, At(town, lot)).Land;

    /// <summary>Mood a household carries for the crowding where its house stands.</summary>
    public static double Mood(Town town, Building home) => Def(town, At(town, town.Lot(home.LotId))).Mood;

    // The stand-in mayor ------------------------------------------------------------------------

    /// <summary>
    /// How far from the hall the stand-in mayor zones each density now (city.json "sprawl"): a reach that grows with the
    /// square root of the town's people, nothing for a density the charter hasn't unlocked.
    /// </summary>
    public static double[] Reaches(Town town)
    {
        var reach = new double[Math.Max(1, town.Content.City.Densities.Count)];
        int unlocked = Unlocked(town);
        double root = Math.Sqrt(town.Population);
        foreach (var step in town.Content.City.Sprawl)
            if (step.Density <= unlocked && step.Density < reach.Length) reach[step.Density] = step.Reach * root;
        return reach;
    }

    /// <summary>
    /// The density the stand-in mayor zones a cell to: the densest whose reach the cell lies within, so the old town
    /// rises first and the dense core spreads outward as the town grows, never past its ward's <see cref="WardDef.Densest"/>.
    /// </summary>
    public static int Target(Town town, int cell, double[] reaches)
    {
        double x = (cell % Land.Size + 0.5) / Land.Res - Town.Center, y = (cell / Land.Size + 0.5) / Land.Res - Town.Center;
        double d = Math.Sqrt(x * x + y * y);
        int level = Low;
        for (int i = reaches.Length - 1; i > Low; i--)
            if (d <= reaches[i])
            {
                level = i;
                break;
            }
        return Math.Min(level, Land.WardAt(town, cell).Densest);
    }

    public static int Target(Town town, double x, double y)
    {
        int cx = Math.Clamp((int)Math.Floor(x * Land.Res), 0, Land.Size - 1), cy = Math.Clamp((int)Math.Floor(y * Land.Res), 0, Land.Size - 1);
        return Target(town, cy * Land.Size + cx, Reaches(town));
    }

    /// <summary>Patches the stand-in mayor zones denser a day: the core fills in first and the change creeps outward.</summary>
    public const int PatchesPerDay = 3;

    /// <summary>
    /// Daily, for the stand-in mayor: zone the homes and shops nearest the hall denser where the charter says the
    /// ward should be, a round patch at a time, so the old core rises first and the sprawl follows it out.
    /// </summary>
    public static void StandIn(Town town)
    {
        if (Unlocked(town) == Low) return;
        var plan = town.Plan;
        var reaches = Reaches(town);
        // Nothing lies beyond the farthest reach.
        double far = reaches.Max() + 1;
        for (int n = 0; n < PatchesPerDay; n++)
        {
            int best = -1;
            double bestKey = double.MaxValue;
            for (int i = 0; i < plan.Density.Length; i++)
            {
                var zone = (Zone)plan.Zones[i];
                if (!Takes(zone)) continue;
                double fx = (i % Land.Size + 0.5) / Land.Res - Town.Center, fy = (i / Land.Size + 0.5) / Land.Res - Town.Center;
                if (fx * fx + fy * fy > far * far || plan.Density[i] >= Target(town, i, reaches)) continue;
                double x = (i % Land.Size + 0.5) / Land.Res - Town.Center, y = (i / Land.Size + 0.5) / Land.Res - Town.Center;
                double key = Math.Sqrt(x * x + y * y) + Land.Jitter(i) * 1.5;
                if (key < bestKey)
                {
                    bestKey = key;
                    best = i;
                }
            }
            if (best < 0) return;
            Upzone(town, (best % Land.Size + 0.5) / Land.Res, (best / Land.Size + 0.5) / Land.Res, 1.6, reaches);
        }
    }

    /// <summary>Raise every home and shop cell within the radius to its own target density.</summary>
    private static void Upzone(Town town, double x, double y, double radius, double[] reaches)
    {
        var plan = town.Plan;
        double px = x * Land.Res, py = y * Land.Res, r = radius * Land.Res;
        int x0 = Math.Max(0, (int)(px - r)), x1 = Math.Min(Land.Size - 1, (int)(px + r));
        int y0 = Math.Max(0, (int)(py - r)), y1 = Math.Min(Land.Size - 1, (int)(py + r));
        for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                double dx = cx + 0.5 - px, dy = cy + 0.5 - py;
                if (dx * dx + dy * dy > r * r) continue;
                int i = cy * Land.Size + cx;
                if (!Takes((Zone)plan.Zones[i])) continue;
                int t = Target(town, i, reaches);
                if (plan.Density[i] < t) plan.Density[i] = (byte)t;
            }
        plan.Changed();
    }
}
