namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The town's land, after Foundation: the player paints zones with a round brush on a fine raster
/// (<see cref="Res"/> cells to a tile), streets grow out to the paint on their own (<see cref="Streets"/>), and
/// every new building finds its own spot inside the paint fronting on a street, with a lane of open
/// ground between it and its neighbours. A lot is the patch one building stands on; it exists only once something is built there.
/// </summary>
public static class Land
{
    /// <summary>Plan cells to a tile.</summary>
    public const int Res = 4;

    /// <summary>Cells to a side of the town plan.</summary>
    public const int Size = Town.Grid * Res;

    /// <summary>A building's footprint in cells (width along its front, depth front to back).</summary>
    public static (int W, int D) Footprint(BuildingDef d)
    {
        if (d.Size is [int w, int dd]) return (w, dd);
        return d.Zone == Zone.Farmland || d.Id == "lumber_camp" ? (5, 5) : (3, 3);
    }

    public static Zone ZoneAt(Town town, int cx, int cy) => (Zone)town.Plan.Zones[cy * Size + cx];

    /// <summary>The building standing on a cell, if any.</summary>
    public static int? BuildingAt(Town town, double x, double y)
    {
        int cx = (int)Math.Floor(x * Res), cy = (int)Math.Floor(y * Res);
        if (cx < 0 || cy < 0 || cx >= Size || cy >= Size) return null;
        int lot = town.Plan.Taken[cy * Size + cx] - 1;
        return lot >= 0 ? town.Lot(lot).BuildingId : null;
    }

    // Painting --------------------------------------------------------------------------------

    /// <summary>
    /// Paint a stroke: every open cell within <paramref name="radius"/> tiles of a dab takes the zone
    /// (Zone.None clears it). Built-on ground keeps its zone. Returns how many cells changed.
    /// </summary>
    /// <param name="overwrite">False leaves zoned land alone and paints only open ground.</param>
    /// <param name="density">How tightly the paint lets the zone be built (<see cref="Density"/>); built-on ground of the same zone takes it too, so an old street can be zoned to grow taller.</param>
    /// <param name="crop">Farmland only: the crop the fields are sown with (<see cref="Farming"/>, 0 for any); open fields of another crop are resown.</param>
    public static int Brush(Town town, IEnumerable<(double X, double Y)> dabs, double radius, Zone zone, bool overwrite = true, int density = 0, byte crop = 0)
    {
        var plan = town.Plan;
        int n = 0;
        byte dens = (byte)(Density.Takes(zone) ? density : 0);
        if (zone != Zone.Farmland) crop = 0;
        double r2 = radius * radius * Res * Res;
        var gardens = Gardens(town);
        foreach (var (x, y) in dabs)
        {
            double px = x * Res, py = y * Res;
            int x0 = Math.Max(0, (int)Math.Floor(px - radius * Res)), x1 = Math.Min(Size - 1, (int)Math.Ceiling(px + radius * Res));
            int y0 = Math.Max(0, (int)Math.Floor(py - radius * Res)), y1 = Math.Min(Size - 1, (int)Math.Ceiling(py + radius * Res));
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    double dx = cx + 0.5 - px, dy = cy + 0.5 - py;
                    if (dx * dx + dy * dy > r2) continue;
                    int i = cy * Size + cx;
                    if (plan.Taken[i] != 0 || plan.Zones[i] == (byte)zone)
                    {
                        // Painted or built land of the zone only changes its density, and open fields their crop.
                        if (zone == Zone.None || plan.Zones[i] != (byte)zone || !overwrite || !Open(town, i)) continue;
                        bool resow = plan.Taken[i] == 0 && plan.Crops[i] != crop;
                        if (plan.Density[i] == dens && !resow) continue;
                        plan.Density[i] = dens;
                        if (resow) plan.Crops[i] = crop;
                        n++;
                        continue;
                    }
                    // A company's bastion grounds take no paint.
                    if (zone != Zone.None && plan.Grounds[i] != 0) continue;
                    if (!overwrite && plan.Zones[i] != (byte)Zone.None) continue;
                    // Only ground in a ward the town has laid out takes paint.
                    if (zone != Zone.None && !Open(town, i)) continue;
                    // Unpainted ground with a decoration on it is a garden, and stays one.
                    if (zone != Zone.None && plan.Zones[i] == (byte)Zone.None && gardens != null && gardens[i]) continue;
                    plan.Zones[i] = (byte)zone;
                    plan.Density[i] = dens;
                    plan.Crops[i] = crop;
                    n++;
                }
        }
        if (n > 0)
        {
            plan.Changed();
            plan.Painted++;
            // New paint far from any street gets one.
            if (zone != Zone.None) Streets.Serve(town);
        }
        return n;
    }

    /// <summary>Unpainted, unbuilt cells in the wards the town has laid out.</summary>
    public static int OpenGround(Town town)
    {
        var plan = town.Plan;
        int n = 0;
        for (int i = 0; i < plan.Zones.Length; i++)
            if (plan.Zones[i] == (byte)Zone.None && plan.Taken[i] == 0 && Open(town, i)) n++;
        return n;
    }

    /// <summary>Open (unbuilt) cells painted with any zone a player paints.</summary>
    public static int PaintedCells(Town town)
    {
        var plan = town.Plan;
        int n = 0;
        for (int i = 0; i < plan.Zones.Length; i++)
            if (plan.Taken[i] == 0 && plan.Zones[i] is not ((byte)Zone.None or (byte)Zone.Civic)) n++;
        return n;
    }

    /// <summary>Open (unbuilt) cells painted with a zone.</summary>
    public static int OpenCells(Town town, Zone zone)
    {
        var plan = town.Plan;
        int n = 0;
        for (int i = 0; i < plan.Zones.Length; i++)
            if (plan.Zones[i] == (byte)zone && plan.Taken[i] == 0) n++;
        return n;
    }

    /// <summary>How many more of the zone's largest buildings its open land holds (counted up to <paramref name="cap"/>), by siting them in turn.</summary>
    public static int OpenPlots(Town town, Zone zone, int cap = 30)
    {
        town.Plan.Fresh();
        if (town.Plan.Plots.TryGetValue(zone, out int known)) return known;
        // The largest the townsfolk build in the zone, so a count of one means anything of theirs fits.
        var probe = town.Content.Buildings.Where(d => d.Zone == zone && d.Buildable && d.Wards.Count == 0 && d.Density == 0).OrderByDescending(d => Footprint(d).W * Footprint(d).D).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault()
            ?? new BuildingDef { Id = "", Name = "", Zone = zone };
        var plan = town.Plan;
        var (w, d) = Footprint(probe);
        var marked = new List<int>();
        int n = 0;
        // Each probe takes the first spot down the street list that it fits, as Find would, so one walk down the list counts them all.
        foreach (var (_, x, y, yaw) in Streets.Fronts(town, d, zone))
        {
            if (n >= cap) break;
            if (!City.Allows(town, probe, x, y) || !Fits(town, x, y, w, d, yaw, zone)) continue;
            n++;
            foreach (int i in Cells(x, y, w, d, yaw))
                if (i >= 0)
                {
                    plan.Taken[i] = -1;
                    marked.Add(i);
                }
        }
        foreach (int i in marked) plan.Taken[i] = 0;
        plan.Plots[zone] = n;
        return n;
    }

    // Siting ----------------------------------------------------------------------------------

    /// <summary>The heading (radians) that turns a building's front toward the hall. Heading 0 faces +y; heading a faces (−sin a, cos a).</summary>
    public static double FaceHall(double x, double y) => FaceToward(x, y, Town.Center, Town.Center);

    /// <summary>The heading that turns a building at (x, y) to face the point (tx, ty).</summary>
    public static double FaceToward(double x, double y, double tx, double ty)
    {
        double dx = tx - x, dy = ty - y;
        return dx * dx + dy * dy < 1e-6 ? 0 : Math.Atan2(-dx, dy);
    }

    /// <summary>The cells a footprint centred at (x, y) in tiles covers, turned to <paramref name="yaw"/> (any angle).</summary>
    /// <param name="grow">Cells added on every side (1 makes the lane around a building).</param>
    public static IEnumerable<int> Cells(double x, double y, int w, int d, double yaw, int grow = 0)
    {
        double hw = w / 2.0 + grow, hd = d / 2.0 + grow;
        double px = x * Res, py = y * Res;
        double s = Math.Sin(yaw), c = Math.Cos(yaw);
        double reach = Math.Sqrt(hw * hw + hd * hd);
        int x0 = (int)Math.Floor(px - reach), x1 = (int)Math.Ceiling(px + reach);
        int y0 = (int)Math.Floor(py - reach), y1 = (int)Math.Ceiling(py + reach);
        for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                double dx = cx + 0.5 - px, dy = cy + 0.5 - py;
                // Into the building's own frame: u along its front, v front to back.
                double u = dx * c + dy * s, v = -dx * s + dy * c;
                if (Math.Abs(u) <= hw + 1e-9 && Math.Abs(v) <= hd + 1e-9) yield return cx < 0 || cy < 0 || cx >= Size || cy >= Size ? -1 : cy * Size + cx;
            }
    }

    /// <summary>Whether a building fits here: every cell on the plan, open, off the streets, and in the zone; a lane of unbuilt ground around it.</summary>
    /// <param name="zone">Null takes any land that isn't built on (the council's own works).</param>
    /// <param name="ground">The bastion grounds it may stand on (<see cref="LandPlan.Grounds"/>); nothing else builds on a company's grounds.</param>
    /// <param name="crop">The crop a farm grows (<see cref="Farming.CropOf"/>): it stands only in fields sown with it, or with none.</param>
    public static bool Fits(Town town, double x, double y, int w, int d, double yaw, Zone? zone, int ground = 0, byte crop = 0)
    {
        var plan = town.Plan;
        // Decorations are the player's: builders leave them standing.
        var decor = Decor.Cells(town);
        // Most spots fail at the middle: try that first.
        int cx = (int)(x * Res), cy = (int)(y * Res), mid = cy * Size + cx;
        if (cx < 0 || cy < 0 || cx >= Size || cy >= Size || plan.Taken[mid] != 0 || plan.Paved[mid] != 0 || (zone is Zone zm && plan.Zones[mid] != (byte)zm)) return false;
        foreach (int i in Cells(x, y, w, d, yaw))
        {
            if (i < 0 || plan.Taken[i] != 0 || plan.Paved[i] != 0 || (decor != null && decor[i])) return false;
            if (plan.Grounds[i] != 0 && plan.Grounds[i] != ground) return false;
            if (zone is Zone z && plan.Zones[i] != (byte)z) return false;
            if (crop != 0 && plan.Crops[i] != 0 && plan.Crops[i] != crop) return false;
        }
        // Dense land builds wall to wall.
        int lane = Density.Lane(town, plan.Density[mid]);
        if (lane > 0)
            foreach (int i in Cells(x, y, w, d, yaw, lane))
                if (i >= 0 && plan.Taken[i] != 0) return false;
        // Not on ground too steep to prop up.
        return Hills.Span(town, x, y, w, d, yaw) <= Hills.MaxSpan;
    }

    /// <summary>
    /// Where a new building of this kind would go in the zone: the open spot on a street nearest the hall
    /// that it fits, a little shuffled so streets don't fill in order. Not yet on the plan: <see cref="Town.AddBuilding"/> claims it.
    /// </summary>
    public static Lot? Find(Town town, BuildingDef def, Zone? zone)
    {
        var (w, d) = Footprint(def);
        var plan = town.Plan;
        plan.Fresh();
        // Buildings kept to certain wards search on their own; the rest share a search per zone and size.
        byte crop = Farming.CropOf(town, def);
        var key = (zone, w, d, def.Wards.Count == 0 && def.Density == 0 && crop == 0 ? "" : def.Id);
        if (!plan.Found.TryGetValue(key, out var spot)) plan.Found[key] = spot = Search(town, def, w, d, zone, crop);
        // A fresh lot each time: the caller may set its zone, and claims it under the next id.
        return spot == null ? null : new Lot(town.Lots.Count, spot.X, spot.Y) { W = w, D = d, Yaw = spot.Yaw, Zone = zone ?? Zone.Civic };
    }

    /// <summary>
    /// As <see cref="Find"/>, but in the cheapest ward to live in that has room: where the council puts the houses it lets
    /// to working folk, so they can pay the rent.
    /// </summary>
    public static Lot? FindCheapest(Town town, BuildingDef def, Zone zone)
    {
        var (w, d) = Footprint(def);
        var fronts = Streets.Fronts(town, d, zone);
        foreach (double rent in town.Content.City.Wards.Select(x => x.Rent).Distinct().Order())
            foreach (var (_, x, y, yaw) in fronts)
                if (WardAt(town, (int)(y * Res) * Size + (int)(x * Res)).Rent == rent && City.Allows(town, def, x, y) && Fits(town, x, y, w, d, yaw, zone))
                    return new Lot(town.Lots.Count, x, y) { W = w, D = d, Yaw = yaw, Zone = zone };
        return null;
    }

    private static Lot? Search(Town town, BuildingDef def, int w, int d, Zone? zone, byte crop)
    {
        // Fronting on a street, the way the townsfolk build.
        foreach (var (_, x, y, yaw) in Streets.Fronts(town, d, zone))
            if (City.Allows(town, def, x, y) && Fits(town, x, y, w, d, yaw, zone, crop: zone == Zone.Farmland ? crop : (byte)0))
                return new Lot(town.Lots.Count, x, y) { W = w, D = d, Yaw = yaw, Zone = zone ?? Zone.Civic };
        // The council's works take any open ground when no street has room, facing the nearest street.
        return zone == null ? Anywhere(town, def, w, d) : null;
    }

    private static Lot? Anywhere(Town town, BuildingDef def, int w, int d)
    {
        var plan = town.Plan;
        Zone? zone = null;
        // Centres sit on cell centres for odd sizes and on cell corners for even ones, so a square building fills whole cells.
        double ox = w % 2 == 1 ? 0.5 : 0, oy = d % 2 == 1 ? 0.5 : 0;
        var candidates = new List<(double Score, int Cell)>();
        for (int i = 0; i < plan.Zones.Length; i++)
        {
            if (plan.Taken[i] != 0) continue;
            if (zone is Zone z ? plan.Zones[i] != (byte)z : plan.Zones[i] is not ((byte)Zone.None or (byte)Zone.Civic)) continue;
            int cx = i % Size, cy = i / Size;
            double x = (cx + ox) / Res, y = (cy + oy) / Res;
            double dist = Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center));
            candidates.Add((dist + Jitter(i) * 0.6, i));
        }
        foreach (var (_, i) in candidates.OrderBy(c => c.Score).ThenBy(c => c.Cell))
        {
            // Nudged off the cell centre, so nothing lines up on the plan's raster.
            double x = (i % Size + ox + (Jitter(i * 7 + 1) - 0.5) * 0.8) / Res, y = (i / Size + oy + (Jitter(i * 7 + 2) - 0.5) * 0.8) / Res;
            if (!City.Allows(town, def, x, y)) continue;
            double face = Streets.Facing(town, x, y);
            // Turned a little off it when the straight heading doesn't fit.
            foreach (double off in Bends)
                if (Fits(town, x, y, w, d, face + off, zone)) return new Lot(town.Lots.Count, x, y) { W = w, D = d, Yaw = face + off, Zone = zone ?? Zone.Civic };
        }
        return null;
    }

    private static readonly double[] Bends = [0, 0.2, -0.2, 0.4, -0.4, 0.7, -0.7];

    /// <summary>A building placed by hand at any point and heading (null faces the nearest street); it takes any unbuilt ground off the streets.</summary>
    public static Lot? At(Town town, BuildingDef def, double x, double y, double? yaw = null)
    {
        var (w, d) = Footprint(def);
        double a = yaw ?? Streets.Facing(town, x, y);
        if (!City.Allows(town, def, x, y)) return null;
        return Fits(town, x, y, w, d, a, null) ? new Lot(town.Lots.Count, x, y) { W = w, D = d, Yaw = a } : null;
    }

    /// <summary>Put a lot on the plan: its cells are taken, and painted with its zone.</summary>
    public static void Claim(Town town, Lot lot)
    {
        town.Plan.Changed();
        foreach (int i in Cells(lot.X, lot.Y, lot.W, lot.D, lot.Yaw))
            if (i >= 0)
            {
                town.Plan.Taken[i] = lot.Id + 1;
                town.Plan.Zones[i] = (byte)lot.Zone;
            }
    }

    /// <summary>A fixed shuffle per cell, the same every run, so towns don't all grow alike in rings.</summary>
    public static double Jitter(int i)
    {
        uint h = (uint)i * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xffff) / 65536.0;
    }

    // The council's own hand -------------------------------------------------------------------

    /// <summary>
    /// Zone a round patch of open ground for the zone at about <paramref name="ring"/> tiles from the hall
    /// (0: as near as it gets), no farther than <paramref name="maxDist"/>. Returns false when there's no room.
    /// </summary>
    public static bool PaintPatch(Town town, Zone zone, double radius, double maxDist, double ring, byte crop = 0) =>
        PaintPatch(town, zone, radius, (x, y, dist) => dist > maxDist || (ring > 0 && dist < ring - 0.5) ? null : ring == 0 ? dist : Math.Abs(dist - ring), crop);

    /// <summary>The ward a cell's middle lies in (<see cref="City.WardOf(Town, double, double)"/>), kept for the plan's life.</summary>
    public static WardDef WardAt(Town town, int i)
    {
        var plan = town.Plan;
        if (plan.Wards == null)
        {
            plan.Wards = new WardDef[Size * Size];
            for (int c = 0; c < plan.Wards.Length; c++) plan.Wards[c] = City.WardOf(town, (c % Size + 0.5) / Res, (c / Size + 0.5) / Res);
        }
        return plan.Wards[i];
    }

    /// <summary>The cells decorations stand on (null when there are none): the brush leaves those unpainted as gardens.</summary>
    private static bool[]? Gardens(Town town) => Decor.Cells(town);

    /// <summary>Whether a cell lies in a ward the town has laid out (<see cref="City.IsOpen(Town, double, double)"/> at its middle).</summary>
    public static bool Open(Town town, int i) => town.City.OpenWards.Contains(WardAt(town, i).Id);

    /// <summary>
    /// Zone a round patch of open ground, centred on the open cell in a laid-out ward with the lowest
    /// <paramref name="key"/> (x, y, distance from the hall; null passes the cell over). Returns false when there's no room.
    /// </summary>
    public static bool PaintPatch(Town town, Zone zone, double radius, Func<double, double, double, double?> key, byte crop = 0)
    {
        var plan = town.Plan;
        int best = -1;
        double bestKey = double.MaxValue;
        var gardens = Gardens(town);
        for (int i = 0; i < plan.Zones.Length; i++)
        {
            if (plan.Zones[i] != (byte)Zone.None || plan.Taken[i] != 0 || plan.Grounds[i] != 0) continue;
            double x = (i % Size + 0.5) / Res, y = (i / Size + 0.5) / Res;
            double dist = Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center));
            if (key(x, y, dist) is not double k) continue;
            if (!Open(town, i)) continue;
            if (gardens != null && gardens[i]) continue;
            k += Jitter(i) * 0.5;
            if (k < bestKey)
            {
                bestKey = k;
                best = i;
            }
        }
        if (best < 0) return false;
        double bx = (best % Size + 0.5) / Res, by = (best / Size + 0.5) / Res;
        // As dense as the stand-in mayor zones that ward by now.
        return Brush(town, [(bx, by)], radius, zone, overwrite: false, density: Density.Target(town, bx, by), crop: crop) > 0;
    }
}

/// <summary>The painted town plan: a zone per cell and the lot (id + 1) built on it, row by row.</summary>
public sealed class LandPlan
{
    public byte[] Zones { get; set; } = new byte[Land.Size * Land.Size];
    public int[] Taken { get; set; } = new int[Land.Size * Land.Size];

    /// <summary>How tightly each cell may be built (<see cref="Systems.Density"/>: 0 low, 1 medium, 2 high).</summary>
    public byte[] Density { get; set; } = new byte[Land.Size * Land.Size];

    /// <summary>The crop each farmland cell is sown with (<see cref="Farming.Code"/>, 0 for any).</summary>
    public byte[] Crops { get; set; } = new byte[Land.Size * Land.Size];

    /// <summary>Ground held for a company's bastion (its grounds number), 0 for none: only its own facilities go there (<see cref="Bastions"/>).</summary>
    public byte[] Grounds { get; set; } = new byte[Land.Size * Land.Size];

    /// <summary>The streets, and the cells they cover (1 where paved).</summary>
    public List<Street> Streets { get; set; } = [];
    public byte[] Paved { get; set; } = new byte[Land.Size * Land.Size];

    /// <summary>How worn the ground is by feet off the streets, per cell (a path becomes a lane at <see cref="PathRules.Lay"/>).</summary>
    public float[] Worn { get; set; } = new float[Land.Size * Land.Size];

    /// <summary>Ground height per cell, from the town's seed (<see cref="Hills.Grid"/>).</summary>
    [field: NotSaved]
    public float[]? Heights { get; set; }

    /// <summary>Bumped whenever a cell changes, so searches of an unchanged plan are answered from memory.</summary>
    [field: NotSaved]
    public int Version { get; set; }

    /// <summary>Searches answered for this <see cref="Version"/>: a spot (or none) per zone and footprint, and open plot counts.</summary>
    [field: NotSaved]
    public Dictionary<(Zone?, int, int, string), Lot?> Found { get; } = [];

    [field: NotSaved]
    public Dictionary<Zone, int> Plots { get; } = [];

    /// <summary>Bumped whenever paint changes (<see cref="Streets.Fronts"/>).</summary>
    [field: NotSaved]
    public int Painted { get; set; }

    [field: NotSaved]
    public (int, int) FrontsKey { get; set; } = (-1, -1);

    [field: NotSaved]
    public Dictionary<(int, Zone?), List<(double Score, double X, double Y, double Yaw)>> Fronts { get; } = [];

    /// <summary>The ward each cell lies in (<see cref="Land.WardAt"/>).</summary>
    [field: NotSaved]
    public WardDef[]? Wards { get; set; }

    /// <summary>Whether each building's door is off the streets (<see cref="Streets.Offroad"/>).</summary>
    [field: NotSaved]
    public Dictionary<int, bool> Offroad { get; } = [];

    [field: NotSaved]
    public int Seen { get; set; } = -1;

    /// <summary>The cells decorations stand on, and the decorations' version they were worked out for (<see cref="Decor.Cells"/>).</summary>
    [field: NotSaved]
    public bool[]? Decor { get; set; }

    [field: NotSaved]
    public int DecorSeen { get; set; } = -1;

    public void Changed() => Version++;

    /// <summary>Forget answers from an older plan.</summary>
    public void Fresh()
    {
        if (Seen == Version) return;
        Found.Clear();
        Plots.Clear();
        Offroad.Clear();
        Seen = Version;
    }
}
