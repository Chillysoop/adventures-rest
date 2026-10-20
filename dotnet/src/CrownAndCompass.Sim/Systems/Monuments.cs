namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Monuments, after Foundation: the temple, the keep and the town hall start small and grow by the parts the council adds
/// beside them (a nave, a bell tower, a curtain wall, a great hall), each a building of its own in buildings.json
/// (<see cref="BuildingDef.PartOf"/>) with its own cost, builders and timber and stone, and its own good for the town: room
/// at prayer, defence, mandate, contentment, settlers, and favour with a god or a faction that can cost a rival's. A town
/// can add every part at once and empty its treasury and the market doing it; or let the council grow it a part at a time
/// (<see cref="Building.Grow"/>), which it does only from gold it plainly doesn't need.
/// </summary>
public static class Monuments
{
    /// <summary>Gold the council keeps back, on top of the next charter and a few times the part, before it grows a monument on its own.</summary>
    public const double Reserve = 400;

    /// <summary>The parts a monument can grow, in the content's order.</summary>
    public static IEnumerable<BuildingDef> PartsOf(Town town, BuildingDef core) => town.Content.Buildings.Where(d => d.PartOf == core.Id);

    /// <summary>Whether a building grows parts.</summary>
    public static bool IsMonument(Town town, BuildingDef d)
    {
        foreach (var p in town.Content.Buildings)
            if (p.PartOf == d.Id) return true;
        return false;
    }

    /// <summary>The monuments in town, standing or rising.</summary>
    public static IEnumerable<Building> All(Town town) => town.Buildings.Where(b => !b.Def.IsPart && IsMonument(town, b.Def));

    /// <summary>The parts a monument has, standing or rising.</summary>
    public static List<Building> Parts(Town town, Building core) => town.Buildings.Where(b => b.ParentId == core.Id).ToList();

    public static Building? Part(Town town, Building core, string partId) => town.Buildings.FirstOrDefault(b => b.ParentId == core.Id && b.Def.Id == partId);

    /// <summary>What adding a part costs now: its gold, and its timber and stone at today's prices (paid load by load as they're carried).</summary>
    public static double Cost(Town town, BuildingDef part) => Js.Round(part.Cost + Planning.Materials(town, part));

    /// <summary>Why the council can't add this part to the monument now, or null when it can.</summary>
    public static string? Blocked(Town town, Building core, BuildingDef part)
    {
        if (part.PartOf != core.Def.Id) return "not one of its parts";
        if (Part(town, core, part.Id) != null) return "it has one";
        if (part.After != "" && Part(town, core, part.After) == null) return $"needs its {town.Content.Building(part.After).Name.ToLowerInvariant()} first";
        if (City.Locked(town, part) is { } locked) return locked;
        if (town.Treasury < Cost(town, part)) return "not enough gold";
        if (Spot(town, core, part) == null) return "no room beside it";
        return null;
    }

    /// <summary>
    /// Where a part goes: against the monument's flank, back or front corners in turn, facing the way it faces, or the
    /// nearest spot beside it that fits.
    /// </summary>
    public static Lot? Spot(Town town, Building core, BuildingDef part)
    {
        var lot = town.Lot(core.LotId);
        var (pw, pd) = Land.Footprint(part);
        double a = lot.Yaw, gap = 1.0 / Land.Res;
        // Front (−sin, cos) and right (cos, sin), in tiles.
        double fx = -Math.Sin(a), fy = Math.Cos(a), rx = Math.Cos(a), ry = Math.Sin(a);
        double side = (lot.W + pw) / 2.0 / Land.Res + gap, back = (lot.D + pd) / 2.0 / Land.Res + gap;
        double edge = (lot.W - pw) / 2.0 / Land.Res, deep = (lot.D - pd) / 2.0 / Land.Res;
        (double R, double F)[] places =
        [
            (side, -deep), (-side, -deep), (0, -back), (edge, -back), (-edge, -back),
            (side, deep), (-side, deep), (side, -back), (-side, -back), (side, 0), (-side, 0),
        ];
        foreach (var (r, f) in places)
        {
            double x = lot.X + rx * r + fx * f, y = lot.Y + ry * r + fy * f;
            if (Land.At(town, part, x, y, a) is { } spot) return spot;
        }
        // Hemmed in: the nearest open ground round it, out to a few tiles.
        double reach = Math.Max(side, back);
        for (double r = reach + 0.25; r <= reach + Room; r += 0.25)
        {
            int n = Math.Max(12, (int)Math.Ceiling(r * 2 * Math.PI / 0.25));
            for (int i = 0; i < n; i++)
            {
                double t = Math.PI + i * Math.PI * 2 / n, x = lot.X + Math.Cos(t) * r, y = lot.Y + Math.Sin(t) * r;
                if (Land.At(town, part, x, y, a) is { } spot) return spot;
            }
        }
        return null;
    }

    /// <summary>How far past the monument's own walls a part may stand when the ground beside it is built up, in tiles.</summary>
    public const double Room = 3;

    /// <summary>Add a part to a monument: paid from the treasury, its materials drawn as the builders carry them.</summary>
    public static Building? Add(Town town, Building core, string partId)
    {
        var part = town.Content.Buildings.FirstOrDefault(d => d.Id == partId);
        if (part == null || Blocked(town, core, part) != null) return null;
        var lot = Spot(town, core, part)!;
        lot.Zone = Zone.Civic;
        town.Book(TreasuryAccount.Works, -part.Cost);
        var site = town.AddBuilding(part, lot, null, $"the {part.Name} of {core.Name}", false);
        site.ParentId = core.Id;
        site.Draws = true;
        site.LaborBudget = Construction.Hours(town, part) * Brain.ConstructionWage;
        town.Log(LedgerCategory.Construction, Importance.Notable, $"The council adds {Article(part.Name)} to {core.Name}. {part.Blurb}");
        return site;
    }

    /// <summary>Add every part the monument can take now, as far as the gold goes: a monument built big from the start.</summary>
    public static int AddAll(Town town, Building core)
    {
        int n = 0;
        bool more = true;
        while (more)
        {
            more = false;
            foreach (var part in PartsOf(town, core.Def).ToList())
                if (Blocked(town, core, part) == null && Add(town, core, part.Id) != null)
                {
                    n++;
                    more = true;
                }
        }
        return n;
    }

    /// <summary>The next part the council would add on its own: the cheapest it can.</summary>
    public static BuildingDef? Next(Town town, Building core) =>
        PartsOf(town, core.Def)
            .Where(p => Part(town, core, p.Id) == null && (p.After == "" || Part(town, core, p.After) != null) && City.Locked(town, p) == null && Spot(town, core, p) != null)
            .OrderBy(p => p.Cost)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>Daily: standing parts win favour, and monuments left to grow take their next part when the treasury can spare it.</summary>
    public static void Daily(Town town)
    {
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || !b.Def.IsPart || b.Def.Favour.Count == 0) continue;
            foreach (var (key, amount) in b.Def.Favour.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (key == "god")
                {
                    var core = b.ParentId is int pid ? town.Building(pid) : null;
                    var god = (core != null ? Faith.Of(town, core) : null) ?? Faith.TempleGod(town) ?? Faith.CharterGod(town);
                    town.Faiths.Favor[god.Id] = Math.Max(0, town.Faiths.Favor.Get(god.Id, 0) + amount);
                }
                else if (key.StartsWith("faction:", StringComparison.Ordinal) && Factions.Find(town, key[8..]) is { Active: true } f)
                    f.Standing = Math.Clamp(f.Standing + amount, -100, 100);
            }
        }
        foreach (var core in All(town).Where(c => c.Grow && c.Complete).ToList())
        {
            if (Parts(town, core).Any(p => p.IsSite)) continue;
            if (Next(town, core) is not { } part) continue;
            double reserve = Reserve + 3 * Cost(town, part) + (City.NextTier(town)?.Cost ?? 0);
            if (town.Treasury < reserve) continue;
            Add(town, core, part.Id);
        }
    }

    /// <summary>Every standing part's good for the town, for the card: "+350 at prayer, +1 mandate".</summary>
    public static string Gives(Town town, BuildingDef d)
    {
        var parts = new List<string>();
        foreach (var (service, n) in d.Serves) parts.Add($"room for {n} more at {(service == "shrine" ? "prayer" : service)}");
        if (d.Defense > 0) parts.Add($"walls {Js.Round(d.Defense * 100)}% stronger");
        if (d.Mandate > 0) parts.Add($"+{Js.Str(d.Mandate)} mandate");
        if (d.Comfort > 0) parts.Add($"every household {Js.ToFixed(d.Comfort * 100, 1)}% more content");
        if (d.Attraction > 0) parts.Add($"+{Js.ToFixed(d.Attraction, 2)} draw on settlers");
        if (d.Heroes > 0) parts.Add($"room for {d.Heroes} more {(d.Heroes == 1 ? "hero" : "heroes")}");
        if (d.SalesBoost > 0) parts.Add($"+{Js.Round(d.SalesBoost * 100)}% of sales to the treasury");
        if (d.WatchBoost > 0) parts.Add($"each watchman counts {Js.Round(d.WatchBoost * 100)}% more");
        foreach (var (key, amount) in d.Favour.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            string who = key == "god" ? "the god" : Factions.Find(town, key[8..])?.Name ?? key[8..];
            parts.Add(amount > 0 ? $"favour with {who}" : $"angers {who}");
        }
        return string.Join(", ", parts);
    }

    private static string Article(string name) => ("aeiou".Contains(char.ToLowerInvariant(name[0])) ? "an " : "a ") + name.ToLowerInvariant();
}
