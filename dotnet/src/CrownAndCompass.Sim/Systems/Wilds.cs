namespace CrownAndCompass.Sim.Systems;

/// <summary>Painted zones on the campaign map (content/game/wilds.json).</summary>
public sealed record WildsRules
{
    /// <summary>A cell's side, in days of march.</summary>
    public double Cell { get; init; } = 0.5;

    /// <summary>No paint this close to the town (days): that is the town itself.</summary>
    public double Near { get; init; } = 0.5;
    public ExploreZoneRules Explore { get; init; } = new();
    public PatrolZoneRules Patrol { get; init; } = new();
}

public sealed record ExploreZoneRules
{
    public double Base { get; init; } = 0.6;
    public double PerCell { get; init; } = 0.08;
    public int TripCells { get; init; } = 12;
    public double DaysBase { get; init; } = 1;
    public double DaysPerCell { get; init; } = 0.3;
    public double MaxDays { get; init; } = 8;
    public double Sight { get; init; } = 0.45;
}

public sealed record PatrolZoneRules
{
    public double PerCell { get; init; } = 0.15;
    public double SoldierCells { get; init; } = 1.5;
    public double RaidCut { get; init; } = 0.5;
    public double Security { get; init; } = 10;
    public int WarnHours { get; init; } = 12;
    public double MeetPerCell { get; init; } = 0.006;
    public double MeetMost { get; init; } = 0.25;
    public double Chance { get; init; } = 0.65;
    public double PerLevel { get; init; } = 0.06;
    public double SoldierBonus { get; init; } = 0.15;
    public int Xp { get; init; } = 40;
    public double Pelts { get; init; } = 6;
    public int WoundDice { get; init; } = 1;
    public StandInPatrol StandIn { get; init; } = new();
    public List<string> Meets { get; init; } = [];
}

/// <summary>When the stand-in mayor puts riders on its best friend's road: what it would once have paid for a caravan patrol, and what it keeps back.</summary>
public sealed record StandInPatrol
{
    public double Cost { get; init; } = 160;
    public double Reserve { get; init; } = 600;
}

/// <summary>One painted cell: its column and row on the map's grid (x east, y south), and whether a company has walked it.</summary>
public sealed class WildCell
{
    public int X { get; init; }
    public int Y { get; init; }
    public bool Seen { get; set; }

    /// <summary>Walked, but the company that walked it is not home to tell of it yet: the fog holds and the paint stays.</summary>
    public bool Pending { get; set; }
}

/// <summary>A painted zone of the wilds: ground the council pays a company to explore, or riders to patrol.</summary>
public sealed class WildZone
{
    public int Id { get; init; }

    /// <summary><see cref="Wilds.Explore"/> or <see cref="Wilds.Patrol"/>.</summary>
    public string Kind { get; init; } = "";
    public List<WildCell> Cells { get; } = [];
    public int PaintedTick { get; init; }

    /// <summary>Companies sent to explore it so far.</summary>
    public int Trips { get; set; }

    /// <summary>A patrol the treasury could not pay today: its riders have gone home until it can.</summary>
    public bool Unpaid { get; set; }

    /// <summary>Trouble the patrol met and drove off, and trouble that drove it off.</summary>
    public int Won { get; set; }
    public int Lost { get; set; }
}

/// <summary>
/// Painted zones. The mayor paints the wilds on the campaign map with two brushes. Explore paint is ground the council
/// wants walked: it posts a commission on it (an ordinary exploring company, <see cref="Explore"/>), and posts it
/// again each time the company comes home, until every cell has been seen; then the paint fades. Patrol paint stays
/// until the mayor erases it: riders hired for a daily wage ride it (with a horse regiment sent to help, if the mayor
/// sends one), roads through it lose fewer caravans, raiders setting out from or crossing it are seen sooner, and now
/// and then the riders meet trouble and drive it off or come home hurt. Lair patrols stay their own order
/// (<see cref="Lairs"/>). The patrol's rolls use the zone's own dice, never <see cref="Town.Rng"/>.
/// </summary>
public static class Wilds
{
    public const string Explore = "explore";
    public const string Patrol = "patrol";

    public static WildsRules Rules(Town town) => town.Content.Wilds;

    public static WildZone? Of(Town town, int id) => town.Wilds.FirstOrDefault(z => z.Id == id);

    public static IEnumerable<WildZone> OfKind(Town town, string kind) => town.Wilds.Where(z => z.Kind == kind);

    // ---------- The grid ----------

    /// <summary>The cell a spot (days east and south of the town) lies in.</summary>
    public static (int X, int Y) CellOf(Town town, double x, double y)
    {
        double c = Rules(town).Cell;
        return ((int)Math.Floor(x / c), (int)Math.Floor(y / c));
    }

    /// <summary>The middle of a cell, in days east and south of the town.</summary>
    public static (double X, double Y) Centre(Town town, int x, int y)
    {
        double c = Rules(town).Cell;
        return ((x + 0.5) * c, (y + 0.5) * c);
    }

    public static (double X, double Y) Centre(Town town, WildCell c) => Centre(town, c.X, c.Y);

    /// <summary>Why a cell can't be painted, or null when it can.</summary>
    public static string? CellBlocked(Town town, int x, int y)
    {
        var (cx, cy) = Centre(town, x, y);
        double d = Js.Hypot(cx, cy);
        if (d < Rules(town).Near) return "that is the town itself";
        if (town.Regions.Count == 0 || d > town.Regions.Max(r => r.Days) + 1) return "too far for any company to ride";
        return null;
    }

    /// <summary>The zone of this kind that holds a cell, if any.</summary>
    public static WildZone? ZoneAt(Town town, string kind, int x, int y) =>
        town.Wilds.FirstOrDefault(z => z.Kind == kind && z.Cells.Any(c => c.X == x && c.Y == y));

    /// <summary>The middle of the zone's cells (the unseen ones for an explore zone with any left).</summary>
    public static (double X, double Y) Middle(Town town, WildZone z)
    {
        var cells = z.Kind == Explore && z.Cells.Any(c => !c.Seen) ? z.Cells.Where(c => !c.Seen).ToList() : z.Cells;
        if (cells.Count == 0) return (0, 0);
        double sx = 0, sy = 0;
        foreach (var c in cells)
        {
            var (x, y) = Centre(town, c);
            sx += x;
            sy += y;
        }
        return (sx / cells.Count, sy / cells.Count);
    }

    /// <summary>"the ground to the north-east": what the Ledger calls a zone.</summary>
    public static string Name(Town town, WildZone z)
    {
        var (x, y) = Middle(town, z);
        return $"the ground {Systems.Explore.Bearing(x, y)}";
    }

    // ---------- The brushes ----------

    /// <summary>
    /// Paint cells with a brush: they join the zone of that kind they touch (zones they join up become one), or make a
    /// new one. Cells already painted that way, or that can't be painted, are skipped. Returns the zone, or null when
    /// nothing was painted.
    /// </summary>
    public static WildZone? Paint(Town town, string kind, IEnumerable<(int X, int Y)> cells)
    {
        if (kind is not (Explore or Patrol)) return null;
        var fresh = new List<(int X, int Y)>();
        foreach (var c in cells)
            if (!fresh.Contains(c) && ZoneAt(town, kind, c.X, c.Y) == null && CellBlocked(town, c.X, c.Y) == null) fresh.Add(c);
        if (fresh.Count == 0) return null;
        bool Touches(WildZone z) => z.Cells.Any(c => fresh.Any(f => Math.Abs(f.X - c.X) + Math.Abs(f.Y - c.Y) <= 1));
        var joined = OfKind(town, kind).Where(Touches).OrderBy(z => z.Id).ToList();
        var zone = joined.FirstOrDefault();
        bool made = zone == null;
        if (zone == null)
        {
            zone = new WildZone { Id = ++town.WildCount, Kind = kind, PaintedTick = town.Tick };
            town.Wilds.Add(zone);
        }
        foreach (var other in joined.Skip(1))
        {
            zone.Cells.AddRange(other.Cells);
            zone.Won += other.Won;
            zone.Lost += other.Lost;
            zone.Trips += other.Trips;
            foreach (var r in town.Army.Regiments.Where(r => r.WildsId == other.Id)) r.WildsId = zone.Id;
            foreach (var c in town.Commissions.Where(c => c.ZoneId == other.Id)) c.ZoneId = zone.Id;
            foreach (var s in town.Surveys.Where(s => s.ZoneId == other.Id)) s.ZoneId = zone.Id;
            town.Wilds.Remove(other);
        }
        foreach (var (x, y) in fresh) zone.Cells.Add(new WildCell { X = x, Y = y });
        Sort(zone);
        town.WildsStamp++;
        if (made)
            town.Log(LedgerCategory.Town, Importance.Notable, kind == Explore
                ? $"The council paints {Name(town, zone)} on the map: a company will be paid to walk every part of it."
                : $"The council paints {Name(town, zone)} on the map for riders to patrol, at {Js.Str(Js.Round(Wage(town, zone) * 10) / 10)} gold a day.");
        return zone;
    }

    private static void Sort(WildZone z)
    {
        var sorted = z.Cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        z.Cells.Clear();
        z.Cells.AddRange(sorted);
    }

    /// <summary>Rub out cells of a brush's kind. A zone left empty is gone, its regiment comes home and its open offer is withdrawn.</summary>
    public static int Erase(Town town, string kind, IEnumerable<(int X, int Y)> cells)
    {
        int n = 0;
        var set = cells.ToList();
        foreach (var z in OfKind(town, kind).OrderBy(z => z.Id).ToList())
        {
            n += z.Cells.RemoveAll(c => set.Contains((c.X, c.Y)));
            if (z.Cells.Count == 0) Remove(town, z, erased: true);
        }
        town.WildsStamp++;
        return n;
    }

    private static void Remove(Town town, WildZone z, bool erased)
    {
        town.Wilds.Remove(z);
        town.WildsStamp++;
        foreach (var r in town.Army.Regiments.Where(r => r.WildsId == z.Id)) r.WildsId = null;
        foreach (var c in town.Commissions.Where(c => c.ZoneId == z.Id && c.Status == BountyStatus.Open))
        {
            c.Status = BountyStatus.Expired;
            town.Book(TreasuryAccount.Exploring, c.Reward);
        }
        if (erased) town.Log(LedgerCategory.Town, Importance.Routine, z.Kind == Explore ? "The council rubs out ground it had painted for exploring." : "The council calls its riders in from a patrol it rubbed off the map.");
    }

    // ---------- Explore zones ----------

    public static int Unseen(WildZone z) => z.Cells.Count(c => !c.Seen);

    /// <summary>What the council offers a company for one trip into an explore zone.</summary>
    public static double TripReward(Town town, WildZone z)
    {
        var e = Rules(town).Explore;
        var (x, y) = Middle(town, z);
        var region = Systems.Explore.RegionAt(town, x, y);
        double basis = region != null ? Systems.Explore.SuggestedReward(town, region) : town.Content.Explore.Commission.Base;
        return Js.Round(basis * (e.Base + e.PerCell * Math.Min(e.TripCells, Math.Max(1, Unseen(z)))));
    }

    /// <summary>Hours a company spends in the zone on one trip.</summary>
    public static int TripHours(Town town, WildZone z)
    {
        var e = Rules(town).Explore;
        return (int)Js.Round(Math.Min(e.MaxDays, e.DaysBase + e.DaysPerCell * Unseen(z)) * SimTime.HoursPerDay);
    }

    /// <summary>Whether a company of this zone is out, or its offer stands.</summary>
    public static bool Busy(Town town, WildZone z) =>
        town.Commissions.Any(c => c.ZoneId == z.Id && Systems.Explore.Active(c)) || town.Surveys.Any(s => s.ZoneId == z.Id && Systems.Explore.Out(s));

    /// <summary>
    /// A company out exploring sees the painted cells around it (every explore zone, not only its own). They count as walked
    /// for where it goes next, but the town only knows them once the company is home (<see cref="Told"/>).
    /// </summary>
    internal static void See(Town town, Survey s, (double X, double Y) p)
    {
        double sight = Rules(town).Explore.Sight;
        foreach (var z in town.Wilds)
        {
            if (z.Kind != Explore) continue;
            foreach (var c in z.Cells)
            {
                if (c.Seen) continue;
                var (x, y) = Centre(town, c);
                if (Js.Hypot(x - p.X, y - p.Y) > sight) continue;
                c.Seen = true;
                c.Pending = true;
                s.Saw.Add($"{z.Id}:{c.X}:{c.Y}");
            }
        }
    }

    /// <summary>
    /// The company that walked these cells is home (<paramref name="home"/>) and they are the town's; or it never will be
    /// (lost, or wiped out after going in to fight), and nobody has walked them after all.
    /// </summary>
    internal static void Told(Town town, Survey s, bool home)
    {
        foreach (string key in s.Saw)
        {
            var parts = key.Split(':');
            if (Of(town, int.Parse(parts[0])) is not { } z) continue;
            int x = int.Parse(parts[1]), y = int.Parse(parts[2]);
            var c = z.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
            if (c == null || !c.Pending) continue;
            c.Pending = false;
            if (!home) c.Seen = false;
        }
        s.Saw.Clear();
    }

    /// <summary>Cells walked by a company not home yet.</summary>
    public static int Untold(WildZone z) => z.Cells.Count(c => c.Pending);

    /// <summary>The unseen cell of a zone to make for next from where a company stands: one of the nearest three.</summary>
    internal static (double X, double Y)? NextCell(Town town, WildZone z, (double X, double Y) from)
    {
        var near = z.Cells.Where(c => !c.Seen)
            .Select(c => (Cell: c, P: Centre(town, c)))
            .OrderBy(c => Js.Hypot(c.P.X - from.X, c.P.Y - from.Y))
            .ThenBy(c => c.Cell.Y)
            .ThenBy(c => c.Cell.X)
            .Take(3)
            .ToList();
        if (near.Count == 0) return null;
        var pick = near[town.Rng.Range(0, near.Count)].P;
        double half = Rules(town).Cell * 0.3;
        return (pick.X + (town.Rng.Next() - 0.5) * half, pick.Y + (town.Rng.Next() - 0.5) * half);
    }

    // ---------- Patrol zones ----------

    /// <summary>The regiment sent to ride a zone, if any.</summary>
    public static Regiment? Riders(Town town, WildZone z) => town.Army.Regiments.Where(r => r.WildsId == z.Id).OrderBy(r => r.Id).FirstOrDefault();

    /// <summary>Cells a zone's regiment rides itself, so the town hires nobody for them.</summary>
    public static double RegimentCells(Town town, WildZone z) =>
        Riders(town, z) is { } r ? r.Soldiers.Count * Rules(town).Patrol.SoldierCells * Army.Unit(town, r).Patrol : 0;

    /// <summary>What a patrol costs a day: riders hired for every cell its regiment doesn't ride.</summary>
    public static double Wage(Town town, WildZone z) => Rules(town).Patrol.PerCell * Math.Max(0, z.Cells.Count - RegimentCells(town, z));

    /// <summary>Whether riders keep the zone today: paid, or ridden wholly by its regiment.</summary>
    public static double Cover(Town town, WildZone z) => z.Kind == Patrol && z.Cells.Count > 0 && !z.Unpaid ? 1 : 0;

    /// <summary>Whether a regiment can be sent to ride a zone: horse only, home, not already riding one.</summary>
    public static string? SendBlocked(Town town, Regiment r, WildZone z)
    {
        if (z.Kind != Patrol) return "only patrols take riders";
        if (Army.Unit(town, r).Role != "horse") return "only horse can ride the wilds";
        if (r.Soldiers.Count == 0) return "it has nobody in it";
        if (r.WildsId == z.Id) return "already riding it";
        if (town.Army.Sortie?.Regiments.Contains(r.Id) == true || Conquest.Garrisoning(town, r)) return "away with the host";
        return null;
    }

    /// <summary>Send a horse regiment to ride a patrol zone, or (regiment null) call the zone's regiment home.</summary>
    public static bool Send(Town town, int zoneId, int? regimentId)
    {
        if (Of(town, zoneId) is not { Kind: Patrol } z) return false;
        if (regimentId == null)
        {
            if (Riders(town, z) is not { } home) return false;
            home.WildsId = null;
            town.WildsStamp++;
            town.Log(LedgerCategory.War, Importance.Routine, $"{home.Name} ride home from {Name(town, z)}.");
            return true;
        }
        if (Army.Find(town, regimentId.Value) is not { } r || SendBlocked(town, r, z) != null) return false;
        if (Riders(town, z) is { } old) old.WildsId = null;
        r.WildsId = z.Id;
        town.WildsStamp++;
        town.Log(LedgerCategory.War, Importance.Notable, $"{r.Name} ride out to patrol {Name(town, z)}.");
        return true;
    }

    /// <summary>The best cover any paid patrol gives a spot, 0 when no patrol rides there.</summary>
    public static double CoverAt(Town town, double x, double y)
    {
        if (town.Wilds.Count == 0) return 0;
        var (cx, cy) = CellOf(town, x, y);
        double best = 0;
        foreach (var z in town.Wilds)
            if (z.Kind == Patrol && z.Cells.Any(c => c.X == cx && c.Y == cy)) best = Math.Max(best, Cover(town, z));
        return best;
    }

    /// <summary>The share of a straight road (from a spot to another) that patrols keep, by quarter-day steps, weighed by their cover.</summary>
    public static double Along(Town town, (double X, double Y) a, (double X, double Y) b)
    {
        if (!town.Wilds.Any(z => z.Kind == Patrol)) return 0;
        double len = Js.Hypot(b.X - a.X, b.Y - a.Y);
        int steps = Math.Max(1, (int)Math.Ceiling(len / 0.25));
        double sum = 0;
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            sum += CoverAt(town, a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }
        return sum / (steps + 1);
    }

    /// <summary>How much of the road to a settlement patrols keep, 0 to 1.</summary>
    public static double Road(Town town, Settlement s)
    {
        if (!town.Wilds.Any(z => z.Kind == Patrol)) return 0;
        if (s.RoadDay != town.Day || s.RoadStamp != town.WildsStamp)
        {
            s.RoadDay = town.Day;
            s.RoadStamp = town.WildsStamp;
            // The town's own ground takes no paint: the road is kept from where the wilds begin.
            var to = Systems.Explore.SettlementSpot(town, s);
            double len = Math.Max(1e-6, Js.Hypot(to.X, to.Y)), near = Math.Min(len, Rules(town).Near + Rules(town).Cell * 0.5);
            s.RoadCover = Along(town, (to.X * near / len, to.Y * near / len), to);
        }
        return s.RoadCover;
    }

    /// <summary>The raid chance left on the road to a settlement under its patrols.</summary>
    public static double RaidShare(Town town, Settlement s) => 1 - Rules(town).Patrol.RaidCut * Road(town, s);

    /// <summary>What patrolled roads add to a settlement's safety.</summary>
    public static double Security(Town town, Settlement s) => Rules(town).Patrol.Security * Road(town, s);

    /// <summary>
    /// Hours sooner the town hears of raiders setting out from a spot for the town: the patrol cover over the spot and
    /// the road in, whichever is better, as a share of <see cref="PatrolZoneRules.WarnHours"/>.
    /// </summary>
    public static int Warns(Town town, (double X, double Y) from)
    {
        if (!town.Wilds.Any(z => z.Kind == Patrol)) return 0;
        double cover = Math.Max(CoverAt(town, from.X, from.Y), Along(town, from, (0, 0)) * 2);
        return (int)Js.Round(Rules(town).Patrol.WarnHours * Math.Min(1, cover));
    }

    /// <summary>Who of the patrols saw them: the regiment riding nearest, or the hired riders.</summary>
    public static string Spotter(Town town, (double X, double Y) from)
    {
        var (cx, cy) = CellOf(town, from.X, from.Y);
        var z = town.Wilds.Where(z => z.Kind == Patrol && !z.Unpaid)
            .OrderBy(z => z.Cells.Min(c => Math.Abs(c.X - cx) + Math.Abs(c.Y - cy)))
            .ThenBy(z => z.Id)
            .FirstOrDefault();
        return z != null && Riders(town, z) is { } r ? $"riders of {r.Name}" : "riders of the town's patrol";
    }

    // ---------- The daily tick ----------

    /// <summary>
    /// Daily, before the companies form: explore zones post their next trip or fade once walked; patrols are paid,
    /// lapse when the treasury can't, and ride out to meet whatever moves on their ground. Old caravan patrols on
    /// the roads to settlements become painted patrols.
    /// </summary>
    public static void Daily(Town town, bool autoMayor)
    {
        Migrate(town);
        foreach (var z in town.Wilds.OrderBy(z => z.Id).ToList())
        {
            if (z.Kind == Explore) Exploring(town, z);
            else Patrolling(town, z);
        }
        if (autoMayor) StandIn(town);
    }

    private static void Exploring(Town town, WildZone z)
    {
        if (Busy(town, z) || (Unseen(z) == 0 && Untold(z) > 0)) return;
        if (Unseen(z) == 0)
        {
            town.Log(LedgerCategory.Quest, Importance.Notable, $"Every part of {Name(town, z)} has been walked and told; the council's paint comes off the map.");
            Remove(town, z, erased: false);
            return;
        }
        Systems.Explore.PostZone(town, z);
    }

    private static void Patrolling(Town town, WildZone z)
    {
        double wage = Wage(town, z);
        if (town.Treasury < wage)
        {
            if (!z.Unpaid) town.Log(LedgerCategory.War, Importance.Notable, $"The treasury can't pay the riders on {Name(town, z)}; they go home until it can.");
            if (!z.Unpaid) town.WildsStamp++;
            z.Unpaid = true;
            return;
        }
        if (z.Unpaid) town.Log(LedgerCategory.War, Importance.Routine, $"Paid again, the riders go back out to {Name(town, z)}.");
        if (z.Unpaid) town.WildsStamp++;
        z.Unpaid = false;
        town.Book(TreasuryAccount.Patrols, -wage);
        Meet(town, z);
    }

    /// <summary>Its own dice, so the patrol's meetings never shift the town's main stream.</summary>
    private static Rng Dice(Town town, WildZone z) => new(unchecked(town.Seed * 6007 + town.Tick * 92821 + z.Id * 7759 + 41));

    /// <summary>Now and then the riders meet something on their ground: they drive it off, or are driven off and someone comes home hurt.</summary>
    private static void Meet(Town town, WildZone z)
    {
        var p = Rules(town).Patrol;
        var dice = Dice(town, z);
        if (!dice.Chance(Math.Min(p.MeetMost, p.MeetPerCell * z.Cells.Count)) || p.Meets.Count == 0) return;
        var cell = z.Cells[dice.Range(0, z.Cells.Count)];
        var (cx, cy) = Centre(town, cell);
        var region = Systems.Explore.RegionAt(town, cx, cy);
        int tier = Math.Max(1, region?.Levels[0] ?? 1);
        string what = p.Meets[dice.Range(0, p.Meets.Count)];
        var riders = Riders(town, z);
        var hero = riders?.CommanderId is int cid && town.Agent(cid) is { Dead: false } c ? c
            : town.Heroes.Where(a => !a.IsAway && !a.Dead).OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault();
        double chance = Math.Clamp(p.Chance + p.PerLevel * ((hero?.Hero!.Level ?? 0) - 2 * tier) + (riders != null ? p.SoldierBonus : 0), 0.15, 0.95);
        string where = region != null ? $" in {region.Name}" : "";
        string who = riders != null ? riders.Name : hero != null ? $"{hero.Name}'s patrol" : "The patrol";
        if (dice.Chance(chance))
        {
            z.Won++;
            if (hero != null)
            {
                Quests.GainXp(town, hero, p.Xp * tier);
                Notoriety.Deed(town, hero, "patrol", 1);
            }
            double pelts = p.Pelts * tier;
            town.Book(TreasuryAccount.Patrols, pelts);
            town.Log(LedgerCategory.War, Importance.Notable, $"{Saga.Cap(who)} meet {what}{where} and drive them off, and bring back {Js.Str(pelts)} gold in spoils.", hero != null ? [hero.Id] : []);
            return;
        }
        z.Lost++;
        string wound = "";
        if (hero != null)
        {
            int hurt = dice.Roll(p.WoundDice * (tier + 1), 6);
            hero.Hero!.Hp = Math.Max(1, hero.Hero.Hp - hurt);
            wound = $" {hero.Name} comes home wounded ({hurt} hit points).";
        }
        town.Log(LedgerCategory.War, Importance.Notable, $"{Saga.Cap(who)} run into {what}{where} and are driven off.{wound}", hero != null ? [hero.Id] : []);
    }

    /// <summary>Caravan patrols bought before patrols were painted ride on as painted patrols over the same road.</summary>
    private static void Migrate(Town town)
    {
        foreach (var s in town.Settlements)
        {
            if (!s.Projects.Remove(Patrol)) continue;
            PatrolRoad(town, s);
        }
    }

    /// <summary>Paint a patrol along the road to a settlement, cell by cell.</summary>
    public static WildZone? PatrolRoad(Town town, Settlement s)
    {
        if (Stewards.Intercept(town, "realm", new StewardAsk { Area = "realm", Kind = "roadpatrol", Title = $"Patrol the road to {s.Name}", Why = "• A friend's road\n• Riders keep the raiders off it", I1 = s.Id })) return null;
        var to = Systems.Explore.SettlementSpot(town, s);
        double len = Js.Hypot(to.X, to.Y);
        int steps = Math.Max(1, (int)Math.Ceiling(len / (Rules(town).Cell * 0.5)));
        var cells = new List<(int X, int Y)>();
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            var c = CellOf(town, to.X * t, to.Y * t);
            if (!cells.Contains(c)) cells.Add(c);
        }
        return Paint(town, Patrol, cells);
    }

    /// <summary>The stand-in mayor puts riders on its best friend's road once it can spare what a caravan patrol once cost.</summary>
    private static void StandIn(Town town)
    {
        var p = Rules(town).Patrol.StandIn;
        if (Threat.IsActive(town) || town.Treasury < p.Cost + p.Reserve) return;
        var s = town.Settlements
            .Where(x => !x.Razed && Neighbours.Known(town, x) && (x.Allied || x.Standing >= 30) && Road(town, x) < 0.5)
            .OrderByDescending(x => x.Standing)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (s != null) PatrolRoad(town, s);
    }
}
