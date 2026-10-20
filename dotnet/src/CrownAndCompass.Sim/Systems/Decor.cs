namespace CrownAndCompass.Sim.Systems;

/// <summary>One kind of decoration the player can set down (content/game/decor.json).</summary>
public sealed record DecorDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Greenery, Street, Trade, Farm, Faith, Monument or Martial: the tab it sits under.</summary>
    public string Group { get; init; } = "";
    public string Blurb { get; init; } = "";
    public double Cost { get; init; }
    public double Timber { get; init; }

    /// <summary>Gold a day from the treasury (lamp oil, a fountain's keeper).</summary>
    public double Upkeep { get; init; }

    /// <summary>Residents the town needs before it can raise one.</summary>
    public int Population { get; init; }

    /// <summary>At most this many in town; 0 for no limit.</summary>
    public int Max { get; init; }

    /// <summary>Radius of the piece on the ground, in lots; nothing else may stand inside it.</summary>
    public double Size { get; init; } = 0.2;

    /// <summary>How far its effect on nearby buildings reaches, fading to nothing at the edge.</summary>
    public double Radius { get; init; } = 2;

    /// <summary>Days a planted tree takes to reach full size (and full effect); 0 for at once.</summary>
    public int GrowDays { get; init; }

    /// <summary>Planted in a row along a drag, this far apart (fences, hedges); 0 for one at a time.</summary>
    public double Line { get; init; }

    /// <summary>Can be painted with the scatter brush (trees, flowers, crates).</summary>
    public bool Scatter { get; init; }

    /// <summary>"god" or "hero": the piece is dedicated to one, chosen when placed.</summary>
    public string Honors { get; init; } = "";

    // What it does. Local effects (near buildings) fade with distance; the rest count town-wide.

    /// <summary>Local beauty: residents' mood at home, and the price of land.</summary>
    public double Appeal { get; init; }

    /// <summary>Local: residents nearby rest and feel less alone when they idle.</summary>
    public double Rest { get; init; }

    /// <summary>Local: buildings it lights by night; the lit share of the town helps the watch.</summary>
    public double Light { get; init; }

    /// <summary>Local: extra sales for workshops nearby.</summary>
    public double Trade { get; init; }

    /// <summary>Local: extra harvest for farms nearby.</summary>
    public double Harvest { get; init; }

    /// <summary>Local: rumours spread faster from a tavern nearby.</summary>
    public double Talk { get; init; }

    /// <summary>Food a day, sold at market for the treasury, once grown.</summary>
    public double Food { get; init; }

    /// <summary>Favour a day for the god it honours.</summary>
    public double Favor { get; init; }

    /// <summary>Counts toward the honours every hero sees the town pay its own.</summary>
    public double Honor { get; init; }

    /// <summary>Settlers drawn: added to the daily chance of arrivals.</summary>
    public double Draw { get; init; }

    /// <summary>Mandate a day.</summary>
    public double Mandate { get; init; }

    /// <summary>Strength at the walls from townsfolk who drill.</summary>
    public double Drill { get; init; }

    /// <summary>Added to how much each watchman counts against crime and raiders.</summary>
    public double Deter { get; init; }
}

public sealed record DecorMayorRules
{
    public int EveryDays { get; init; } = 4;
    public double Reserve { get; init; } = 350;
    public double PerResident { get; init; } = 4;
}

/// <summary>Numbers for decorations and set dressing (content/game/decor.json).</summary>
public sealed record DecorRules
{
    /// <summary>How far from the heart of town (in tiles) decorations may be placed.</summary>
    public double Reach { get; init; } = 17;

    /// <summary>Share of the cost back when a piece is cleared.</summary>
    public double Refund { get; init; } = 0.5;
    public double AppealMood { get; init; } = 0.6;
    public double MoodCap { get; init; } = 6;
    public double MoodFloor { get; init; } = -5;
    public double RestMood { get; init; } = 1.5;
    public double RestCap { get; init; } = 4;

    /// <summary>Loneliness eased an hour per point of rest, while idling near home.</summary>
    public double RestLoneliness { get; init; } = 0.6;
    public double LandPerAppeal { get; init; } = 0.8;
    public double LandCap { get; init; } = 8;

    /// <summary>Contentment a household gains per point of appeal round its house (helping it grow a storey and keep it), within floor and cap.</summary>
    public double ContentPerAppeal { get; init; } = 0.015;
    public double ContentCap { get; init; } = 0.1;
    public double ContentFloor { get; init; } = -0.05;

    /// <summary>Land dearer in a ward per point of its buildings' average appeal, up to a share.</summary>
    public double WardPerAppeal { get; init; } = 0.02;
    public double WardCap { get; init; } = 0.15;

    /// <summary>Watch strength when every building is lit.</summary>
    public double LightWatch { get; init; } = 0.2;
    public double DeterCap { get; init; } = 0.15;
    public double TradeCap { get; init; } = 0.12;
    public double HarvestCap { get; init; } = 0.15;

    /// <summary>Daily rumour chance per point of talk near a tavern, up to TalkCap.</summary>
    public double TalkChance { get; init; } = 0.03;
    public double TalkCap { get; init; } = 0.12;
    public double FavorCap { get; init; } = 1.5;
    public double DrawCap { get; init; } = 0.25;
    public double MandateCap { get; init; } = 0.15;
    public double DrillCap { get; init; } = 30;

    /// <summary>Loyalty points every hero gets per point of honours, up to HonorCap; and for their own statue.</summary>
    public double HonorPerStatue { get; init; } = 1.5;
    public double HonorCap { get; init; } = 4;
    public double StatueLoyalty { get; init; } = 8;

    /// <summary>Price the town's orchards and hives sell at, as a share of market.</summary>
    public double FoodPrice { get; init; } = 1;
    public DecorMayorRules Mayor { get; init; } = new();
    public IReadOnlyList<DecorDef> Pieces { get; init; } = [];

    public DecorDef Piece(string id) => Pieces.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"Unknown decoration {id}");
}

/// <summary>A decoration standing in town: where it is, which way it faces, and who it honours.</summary>
public sealed class Decoration(int id, DecorDef def, double x, double y, double angle, int day)
{
    public int Id { get; } = id;
    public DecorDef Def { get; } = def;

    /// <summary>Position in tiles from the town's corner.</summary>
    public double X { get; } = x;
    public double Y { get; } = y;

    /// <summary>Facing, in radians, any angle.</summary>
    public double Angle { get; } = angle;

    public int PlacedDay { get; } = day;

    /// <summary>The hero a statue honours.</summary>
    public int? HeroId { get; set; }

    /// <summary>The god (library deity id) a statue or shrine honours.</summary>
    public string? Deity { get; set; }
}

/// <summary>The town's decorations and the effect each building feels from them.</summary>
public sealed class DecorState
{
    public List<Decoration> Pieces { get; } = [];
    public int NextId { get; set; } = 1;

    /// <summary>Bumped on every change, so the per-building fields are worked out again.</summary>
    public int Version { get; set; }

    [NotSaved] private Fields? _fields;

    internal Fields? Cached { get => _fields; set => _fields = value; }

    /// <summary>Per-building local effects, by building id.</summary>
    internal sealed class Fields(int buildings, int version, int day)
    {
        public readonly int Buildings = buildings, Version = version, Day = day;
        public readonly double[] Appeal = new double[buildings];
        public readonly double[] Rest = new double[buildings];
        public readonly double[] Light = new double[buildings];
        public readonly double[] Trade = new double[buildings];
        public readonly double[] Harvest = new double[buildings];
        public readonly double[] Talk = new double[buildings];
        public double LitShare;

        /// <summary>Each ward's buildings' average appeal, by ward id.</summary>
        public readonly Dictionary<string, double> Wards = [];
    }
}

/// <summary>
/// Decorations and set dressing, after Ostriv, Foundation and Pharaoh: trees, benches, lamps,
/// statues and the like, set down anywhere at any angle. Each one makes its corner of town more
/// pleasant (appeal) and does one small useful thing for another system: lamps help the watch,
/// stalls help trade, scarecrows the harvest, statues of gods and heroes win favour and loyalty.
/// </summary>
public static class Decor
{
    public static DecorRules Rules(Town town) => town.Content.Decor;

    public static IReadOnlyList<Decoration> All(Town town) => town.Decor.Pieces;

    /// <summary>Where a building stands, in tiles (the middle of its lot).</summary>
    public static (double X, double Y) PosOf(Town town, Building b)
    {
        var lot = town.Lot(b.LotId);
        return (lot.X, lot.Y);
    }

    /// <summary>The heart of town, in tiles.</summary>
    public static (double X, double Y) Heart => (Town.Center, Town.Center);

    /// <summary>How far a tree has grown, 0.3 to 1; anything else is full size at once.</summary>
    public static double Growth(Town town, Decoration d) =>
        d.Def.GrowDays <= 0 ? 1 : Math.Min(1, 0.3 + 0.7 * (town.Day - d.PlacedDay) / d.Def.GrowDays);

    // Placing ----------------------------------------------------------------------------------

    /// <summary>Why the town can't raise this piece at all right now, or null.</summary>
    public static string? Blocked(Town town, DecorDef def)
    {
        if (town.Population < def.Population) return $"needs {def.Population} residents";
        if (def.Max > 0 && town.Decor.Pieces.Count(p => p.Def == def) >= def.Max) return $"the town has {def.Max}";
        if (town.Treasury < Cost(town, def)) return "not enough gold";
        return null;
    }

    public static double Cost(Town town, DecorDef def) => Js.Round(def.Cost + (def.Timber > 0 ? town.Market.Quote(Good.Timber, def.Timber) : 0));

    /// <summary>Why this spot can't take the piece, or null when it can.</summary>
    public static string? SpotBlocked(Town town, DecorDef def, double x, double y)
    {
        var (hx, hy) = Heart;
        if (Math.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy)) > Rules(town).Reach) return "too far from town";
        double r = def.Size;
        int cx = (int)Math.Floor(x * Land.Res), cy = (int)Math.Floor(y * Land.Res);
        // Off the lanes and the square's rim; the open middle of the square is the place for a fountain.
        if (cx >= 0 && cy >= 0 && cx < Land.Size && cy < Land.Size && town.Plan.Paved[cy * Land.Size + cx] != 0
            && Streets.Nearest(town, x, y) is { } lane && lane.D < Streets.Width / 2 + r * 0.5) return "that's the street";
        foreach (var b in town.Buildings)
            if (Overlaps(town.Lot(b.LotId), x, y, r)) return "a building stands there";
        foreach (var p in town.Decor.Pieces)
        {
            double min = (r + p.Def.Size) * 0.85;
            if ((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < min * min) return "something stands there";
        }
        return null;
    }

    /// <summary>A circle against a building's footprint, turned however the building is.</summary>
    private static bool Overlaps(Lot lot, double x, double y, double r)
    {
        double s = Math.Sin(lot.Yaw), c = Math.Cos(lot.Yaw), dx = x - lot.X, dy = y - lot.Y;
        // Into the building's own frame: u along its front, v front to back.
        double u = dx * c + dy * s, v = -dx * s + dy * c;
        double hw = lot.W / (2.0 * Land.Res), hd = lot.D / (2.0 * Land.Res);
        double cu = Math.Clamp(u, -hw, hw), cv = Math.Clamp(v, -hd, hd);
        return (cu - u) * (cu - u) + (cv - v) * (cv - v) < r * r;
    }

    /// <summary>Set a piece down, paid from the treasury. A statue honours the given hero or god, or the obvious one.</summary>
    public static Decoration? Place(Town town, string defId, double x, double y, double angle, int? heroId = null, string? deity = null, bool quiet = false)
    {
        var def = Rules(town).Piece(defId);
        if (Blocked(town, def) != null || SpotBlocked(town, def, x, y) != null) return null;
        double cost = Cost(town, def);
        town.Book(TreasuryAccount.Decorations, -cost);
        if (def.Timber > 0) town.Market.Buy(Good.Timber, def.Timber);
        var d = new Decoration(town.Decor.NextId++, def, x, y, NormalAngle(angle), town.Day);
        if (def.Honors == "hero") d.HeroId = heroId is int h && town.Agent(h).IsHero && !town.Agent(h).Departed ? h : DefaultHero(town)?.Id;
        if (def.Honors == "god") d.Deity = deity != null && Faith.God(town, deity) is { Evil: false } ? deity : DefaultGod(town);
        town.Decor.Pieces.Add(d);
        Changed(town);
        if (!quiet && (def.Cost >= 40 || def.Honors != ""))
            town.Log(LedgerCategory.Town, Importance.Notable, $"The town raises {Describe(town, d)} for {Js.ToFixed(cost, 0)} gold.", d.HeroId is int id ? [id] : null);
        return d;
    }

    public static double NormalAngle(double a)
    {
        double tau = Math.PI * 2;
        a %= tau;
        return a < 0 ? a + tau : a;
    }

    /// <summary>The piece's name with who it honours: "a statue of Ser Brann", "a wayside shrine to Pelor".</summary>
    public static string Describe(Town town, Decoration d)
    {
        string name = d.Def.Name.ToLowerInvariant();
        if (d.HeroId is int h)
            return $"a {(name.StartsWith("statue of", StringComparison.Ordinal) ? "statue" : name)} of {town.Agent(h).Name}";
        if (d.Deity != null && Faith.God(town, d.Deity) is { } god)
            return name.StartsWith("statue of", StringComparison.Ordinal) ? $"a statue of {god.Name}" : $"{Agent.Article(name)} {name} to {god.Name}";
        return $"{Agent.Article(name)} {name}";
    }

    /// <summary>The champion, else the most renowned hero in town.</summary>
    public static Agent? DefaultHero(Town town) =>
        town.Heroes.Where(a => a.Hero!.Champion).FirstOrDefault()
        ?? town.Heroes.OrderByDescending(a => a.Hero!.Renown).ThenBy(a => a.Id).FirstOrDefault();

    /// <summary>The good god the most townsfolk keep (the charter's god when nobody keeps one).</summary>
    public static string DefaultGod(Town town)
    {
        var counts = new OrderedMap<string, int>();
        foreach (var a in town.Residents)
            if (Faith.Of(town, a) is { Evil: false } g) counts[g.Id] = counts.Get(g.Id, 0) + 1;
        return counts.Count == 0
            ? Faith.CharterGod(town).Id
            : counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
    }

    /// <summary>Clear a piece away; half its price comes back.</summary>
    public static bool Remove(Town town, int decorationId)
    {
        int i = town.Decor.Pieces.FindIndex(p => p.Id == decorationId);
        if (i < 0) return false;
        var d = town.Decor.Pieces[i];
        town.Decor.Pieces.RemoveAt(i);
        Changed(town);
        town.Book(TreasuryAccount.Decorations, Js.Round(d.Def.Cost * Rules(town).Refund));
        return true;
    }

    /// <summary>A piece went up or came down: the effects are worked out again, and builders and lanes look again where to go.</summary>
    private static void Changed(Town town)
    {
        town.Decor.Version++;
        town.Plan.Changed();
    }

    /// <summary>
    /// The plan cells decorations stand on (null when there are none), worked out again after any change: builders
    /// leave them be (<see cref="Land.Fits"/>), lanes go round them, and the brush leaves unpainted ones as gardens.
    /// </summary>
    public static bool[]? Cells(Town town)
    {
        var s = town.Decor;
        var plan = town.Plan;
        if (s.Pieces.Count == 0) return plan.Decor = null;
        if (plan.Decor != null && plan.DecorSeen == s.Version) return plan.Decor;
        int n = Land.Size;
        var mask = new bool[n * n];
        foreach (var p in s.Pieces)
        {
            // Any cell the piece's circle reaches into (half a cell's diagonal past its rim).
            double r = p.Def.Size + 0.18;
            for (int cy = (int)Math.Floor((p.Y - r) * Land.Res); cy <= (int)Math.Floor((p.Y + r) * Land.Res); cy++)
                for (int cx = (int)Math.Floor((p.X - r) * Land.Res); cx <= (int)Math.Floor((p.X + r) * Land.Res); cx++)
                {
                    if (cx < 0 || cy < 0 || cx >= n || cy >= n) continue;
                    double dx = (cx + 0.5) / Land.Res - p.X, dy = (cy + 0.5) / Land.Res - p.Y;
                    if (dx * dx + dy * dy < r * r) mask[cy * n + cx] = true;
                }
        }
        plan.DecorSeen = s.Version;
        return plan.Decor = mask;
    }

    /// <summary>Builders raising something on a lot clear any decoration in their way (a piece set down by hand on the spot is kept off by <see cref="Land.Fits"/>).</summary>
    public static void ClearLot(Town town, Lot lot)
    {
        var gone = town.Decor.Pieces.Where(p => Overlaps(lot, p.X, p.Y, p.Def.Size)).Select(p => p.Id).ToList();
        foreach (int id in gone) Remove(town, id);
    }

    // What decorations do ----------------------------------------------------------------------

    /// <summary>The local fields every building feels, worked out again after any change and once a day (trees grow).</summary>
    private static DecorState.Fields Fields(Town town)
    {
        var s = town.Decor;
        var f = s.Cached;
        if (f != null && f.Buildings == town.Buildings.Count && f.Version == s.Version && f.Day == town.Day) return f;
        f = new DecorState.Fields(town.Buildings.Count, s.Version, town.Day);
        if (s.Pieces.Count > 0)
        {
            var pos = town.Buildings.Select(b => PosOf(town, b)).ToArray();
            foreach (var d in s.Pieces)
            {
                double grow = Growth(town, d), r = d.Def.Radius;
                for (int i = 0; i < pos.Length; i++)
                {
                    double dx = pos[i].X - d.X, dy = pos[i].Y - d.Y;
                    double dist2 = dx * dx + dy * dy;
                    if (dist2 >= r * r) continue;
                    double w = grow * (1 - Math.Sqrt(dist2) / r);
                    f.Appeal[i] += d.Def.Appeal * w;
                    f.Rest[i] += d.Def.Rest * w;
                    f.Light[i] += d.Def.Light * w;
                    f.Trade[i] += d.Def.Trade * w;
                    f.Harvest[i] += d.Def.Harvest * w;
                    f.Talk[i] += d.Def.Talk * w;
                }
            }
            int lit = 0, n = 0;
            foreach (var b in town.Buildings)
            {
                if (!b.Complete) continue;
                n++;
                if (f.Light[b.Id] >= 0.3) lit++;
            }
            f.LitShare = n > 0 ? (double)lit / n : 0;
            foreach (var g in town.Buildings.Where(b => b.Complete).GroupBy(b => City.WardOf(town, town.Lot(b.LotId)).Id))
                f.Wards[g.Key] = g.Average(b => f.Appeal[b.Id]);
        }
        s.Cached = f;
        return f;
    }

    /// <summary>
    /// Unpainted ground with a decoration on it is a garden: the brush passes it by, so nothing is built there.
    /// On painted land builders leave decorations standing and build round them (<see cref="Land.Fits"/>).
    /// </summary>
    /// <param name="pad">How far round the piece counts, in tiles (half a plan cell's width takes in any cell it touches).</param>
    public static bool Garden(Town town, double x, double y, double pad = 0)
    {
        foreach (var p in town.Decor.Pieces)
        {
            double r = p.Def.Size + pad;
            if ((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < r * r) return true;
        }
        return false;
    }

    /// <summary>How pleasant it is around a building.</summary>
    public static double AppealOf(Town town, Building b) => Fields(town).Appeal[b.Id];

    /// <summary>How pleasant it is at any point, for the client's overlay and the price of land.</summary>
    public static double AppealAt(Town town, double x, double y)
    {
        double a = 0;
        foreach (var d in town.Decor.Pieces)
        {
            double dx = x - d.X, dy = y - d.Y, r = d.Def.Radius;
            double dist2 = dx * dx + dy * dy;
            if (dist2 < r * r) a += d.Def.Appeal * Growth(town, d) * (1 - Math.Sqrt(dist2) / r);
        }
        return a;
    }

    /// <summary>Mood a resident draws this hour from the decorations round their home.</summary>
    public static double Mood(Town town, Agent a)
    {
        if (a.HomeId is not int home || town.Decor.Pieces.Count == 0) return 0;
        var r = Rules(town);
        var f = Fields(town);
        double mood = Math.Clamp(f.Appeal[home] * r.AppealMood, r.MoodFloor, r.MoodCap);
        if (a.Activity is Activity.Relaxing or Activity.Wandering)
        {
            double rest = Math.Min(r.RestCap, f.Rest[home]);
            mood += rest * r.RestMood;
            a.Loneliness = Math.Max(0, a.Loneliness - rest * r.RestLoneliness);
        }
        return mood;
    }

    /// <summary>Gold a lot fetches on top of its price for the beauty around it.</summary>
    public static double LandValue(Town town, Lot lot)
    {
        if (town.Decor.Pieces.Count == 0) return 0;
        var r = Rules(town);
        return Math.Clamp(AppealAt(town, lot.X, lot.Y) * r.LandPerAppeal, 0, r.LandCap);
    }

    /// <summary>Contentment a household takes from a pleasant street (or loses beside the stocks): houses on fine streets grow a storey sooner.</summary>
    public static double Comfort(Town town, Building house)
    {
        if (town.Decor.Pieces.Count == 0) return 0;
        var r = Rules(town);
        return Math.Clamp(AppealOf(town, house) * r.ContentPerAppeal, r.ContentFloor, r.ContentCap);
    }

    /// <summary>How much dearer land is in a ward its buildings make pleasant, as a multiplier (1 for none): the town sells its plots for more.</summary>
    public static double Prestige(Town town, string wardId)
    {
        if (town.Decor.Pieces.Count == 0 || !Fields(town).Wards.TryGetValue(wardId, out double appeal)) return 1;
        var r = Rules(town);
        return 1 + Math.Clamp(appeal * r.WardPerAppeal, 0, r.WardCap);
    }

    /// <summary>Added to how much each watchman counts: lit streets and the stocks.</summary>
    public static double Watch(Town town)
    {
        if (town.Decor.Pieces.Count == 0) return 0;
        var r = Rules(town);
        return Fields(town).LitShare * r.LightWatch + Math.Min(r.DeterCap, Sum(town, d => d.Deter));
    }

    /// <summary>Multiplier on what a workshop or farm sells: stalls and carts, scarecrows and hives nearby.</summary>
    public static double Yield(Town town, Building b)
    {
        if (town.Decor.Pieces.Count == 0) return 1;
        var r = Rules(town);
        var f = Fields(town);
        return b.Def.Zone == Zone.Farmland ? 1 + Math.Min(r.HarvestCap, f.Harvest[b.Id]) : 1 + Math.Min(r.TradeCap, f.Trade[b.Id]);
    }

    /// <summary>Added to a tavern's daily chance of passing on a rumour: notice boards and bandstands nearby.</summary>
    public static double Talk(Town town, Building tavern)
    {
        if (town.Decor.Pieces.Count == 0) return 0;
        var r = Rules(town);
        return Math.Min(r.TalkCap, Fields(town).Talk[tavern.Id] * r.TalkChance);
    }

    /// <summary>Favour a day for a god from the statues and shrines raised to it.</summary>
    public static double Favor(Town town, string deity) =>
        Math.Min(Rules(town).FavorCap, Js.Sum(town.Decor.Pieces.Where(p => p.Deity == deity), p => p.Def.Favor));

    /// <summary>Added to the daily chance of settlers coming: a fine town is talked of on the roads.</summary>
    public static double Draw(Town town) => Math.Min(Rules(town).DrawCap, Sum(town, d => d.Draw));

    /// <summary>Mandate a day from civic pride.</summary>
    public static double Mandate(Town town) => Math.Min(Rules(town).MandateCap, Sum(town, d => d.Mandate));

    /// <summary>Strength at the walls from the drill yard.</summary>
    public static double Defense(Town town) => Math.Min(Rules(town).DrillCap, Sum(town, d => d.Drill));

    /// <summary>What the town's statues and memorials mean to a hero, as loyalty reasons.</summary>
    public static IEnumerable<(string Why, double Points)> Loyalty(Town town, Agent a)
    {
        var r = Rules(town);
        if (town.Decor.Pieces.Any(p => p.HeroId == a.Id)) yield return ("their statue stands in town", r.StatueLoyalty);
        double honours = Math.Min(r.HonorCap, Sum(town, d => d.Honor) * r.HonorPerStatue);
        if (honours > 0) yield return ("the town honours its heroes", honours);
    }

    private static double Sum(Town town, Func<DecorDef, double> effect)
    {
        double s = 0;
        foreach (var d in town.Decor.Pieces)
        {
            double e = effect(d.Def);
            if (e != 0) s += e * Growth(town, d);
        }
        return s;
    }

    // The stand-in mayor -------------------------------------------------------------------------

    private static readonly string[] Dressing = ["oak", "fruit_tree", "pine", "bench", "flowers", "birch", "beehive", "banner", "fruit_tree", "well", "bush", "haystack"];

    /// <summary>
    /// Headless towns dress themselves a little, so the sim's numbers include what decorations do:
    /// every few days, with gold to spare, the stand-in mayor sets down the piece the town most lacks
    /// beside the building that needs it.
    /// </summary>
    public static void StandIn(Town town, bool defend = true)
    {
        var r = Rules(town);
        var m = r.Mayor;
        if (town.Day < 8 || town.Day % m.EveryDays != 0) return;
        if (town.Treasury < m.Reserve + m.PerResident * town.Population) return;
        if (town.Decor.Pieces.Count >= 8 + town.Population / 2) return;
        var (hx, hy) = Heart;
        foreach (var (id, near, within) in Wants(town, defend))
        {
            var def = r.Piece(id);
            if (Blocked(town, def) != null) continue;
            if (Spot(town, def, near.X, near.Y, true, within ?? def.Radius * 0.8) is not { } spot) continue;
            // Statues and benches face the heart of town; the rest turn as they fall.
            double angle = def.Honors != "" || def.Group == "Monument" || id == "bench"
                ? Math.Atan2(hx - spot.X, hy - spot.Y)
                : town.Decor.NextId * 2.39996;
            Place(town, id, spot.X, spot.Y, angle);
            return;
        }
    }

    /// <summary>What the town lacks, most wanted first, and beside which building.</summary>
    private static IEnumerable<(string Id, (double X, double Y) Near, double? Within)> Wants(Town town, bool defend)
    {
        var f = Fields(town);
        var hall = town.Buildings[0];
        int Count(string id) => town.Decor.Pieces.Count(p => p.Def.Id == id);
        bool Served(Building b, string id)
        {
            var (x, y) = PosOf(town, b);
            double r = Rules(town).Piece(id).Radius;
            return town.Decor.Pieces.Any(p => p.Def.Id == id && (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < r * r);
        }

        foreach (var farm in town.Buildings.Where(b => b.Complete && b.Def.Zone == Zone.Farmland && !Served(b, "scarecrow")))
            yield return ("scarecrow", PosOf(town, farm), null);
        foreach (var tavern in town.Taverns.Where(b => !Served(b, "notice_board")))
            yield return ("notice_board", PosOf(town, tavern), null);
        if (town.Watch > 0 && f.LitShare < 0.6)
            foreach (var dark in town.Buildings.Where(b => b.Complete && f.Light[b.Id] < 0.3).OrderBy(b => Town.DistanceFromCenter(town.Lot(b.LotId))).ThenBy(b => b.Id).Take(8))
                yield return ("lamp", PosOf(town, dark), null);
        var holy = town.Buildings.FirstOrDefault(b => b.Complete && b.Def.Holy) ?? hall;
        if (town.Population >= 25 && Count("god_statue") == 0) yield return ("god_statue", PosOf(town, holy), null);
        if (town.Population >= 30 && Count("hero_statue") == 0 && DefaultHero(town) is { } h && h.Hero!.Renown >= 5) yield return ("hero_statue", PosOf(town, hall), null);
        if (town.Population >= 40 && Count("fountain") == 0) yield return ("fountain", PosOf(town, hall), null);
        if (town.Population >= 60 && Count("founder_statue") == 0) yield return ("founder_statue", PosOf(town, hall), null);
        if (defend && town.Enemy.Stage != ThreatStage.Quiet && Count("training_dummy") + Count("archery_butts") < 3)
        {
            var post = town.Buildings.FirstOrDefault(b => b.Def.Id == "watch_house") ?? hall;
            yield return (Count("archery_butts") < Count("training_dummy") ? "archery_butts" : "training_dummy", PosOf(town, post), null);
        }
        string id = Dressing[town.Decor.Pieces.Count % Dressing.Length];
        var dull = town.Buildings
            .Where(b => b.Complete && (id is "beehive" or "haystack" ? b.Def.Zone == Zone.Farmland : b.Def.Housing > 0))
            .OrderBy(b => f.Appeal[b.Id])
            .ThenBy(b => Town.DistanceFromCenter(town.Lot(b.LotId)))
            .ThenBy(b => b.Id)
            .Take(12);
        // Greenery and the like go on the land round the town plan, as near the dullest homes as they can.
        foreach (var b in dull) yield return (id, PosOf(town, b), 9);
    }

    /// <summary>The first open spot spiralling out from a point, no further than within, or null. Sparing land keeps off land the town will build on.</summary>
    public static (double X, double Y)? Spot(Town town, DecorDef def, double x, double y, bool spareLand = false, double within = 4)
    {
        for (int k = 0; k < 3000; k++)
        {
            double r = 0.45 + 0.16 * Math.Sqrt(k), a = k * 2.39996;
            if (r > within) break;
            double px = Js.Round((x + r * Math.Sin(a)) * 100) / 100, py = Js.Round((y + r * Math.Cos(a)) * 100) / 100;
            if (spareLand && Zoned(town, px, py)) continue;
            if (SpotBlocked(town, def, px, py) == null) return (px, py);
        }
        return null;
    }

    /// <summary>Land the town means to build on: ground painted with a zone or built on. The stand-in keeps to the land around it.</summary>
    private static bool Zoned(Town town, double x, double y)
    {
        var plan = town.Plan;
        foreach (double dx in (double[])[-0.3, 0.3])
            foreach (double dy in (double[])[-0.3, 0.3])
            {
                double px = x + dx, py = y + dy;
                if (px < 0 || py < 0 || px >= Town.Grid || py >= Town.Grid) continue;
                int i = (int)(py * Land.Res) * Land.Size + (int)(px * Land.Res);
                if (plan.Zones[i] != (byte)Zone.None || plan.Taken[i] != 0) return true;
            }
        return false;
    }

    // The day ----------------------------------------------------------------------------------

    /// <summary>Upkeep, and the harvest of the orchards and hives.</summary>
    public static void Daily(Town town)
    {
        if (town.Decor.Pieces.Count == 0) return;
        var r = Rules(town);
        double upkeep = Js.Sum(town.Decor.Pieces, p => p.Def.Upkeep);
        if (upkeep > 0) town.Book(TreasuryAccount.Decorations, -upkeep);
        double food = Sum(town, d => d.Food);
        if (food > 0) town.Book(TreasuryAccount.Orchards, town.Market.Sell(Good.Food, food) * r.FoodPrice);
    }
}
