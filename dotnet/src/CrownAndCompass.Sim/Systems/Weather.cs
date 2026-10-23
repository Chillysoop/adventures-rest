namespace CrownAndCompass.Sim.Systems;

/// <summary>What the weather of a kind of country leans to (weather.json <c>habitats</c>, <c>themes</c>).</summary>
public sealed record WeatherLean
{
    /// <summary>Added to the storminess: wet country above 0, dry below.</summary>
    public double Wet { get; init; }

    /// <summary>Degrees added to the heat: high and cold ground below 0.</summary>
    public double Heat { get; init; }

    /// <summary>Added to the wind (0 still, 1 a gale).</summary>
    public double Wind { get; init; }

    /// <summary>How readily fog lies there.</summary>
    public double Fog { get; init; }

    /// <summary>Share of the season's heat swing the sea takes off (0 inland, 0.5 open water).</summary>
    public double Mild { get; init; }

    /// <summary>Dry ground that blows: a gale there is a sandstorm.</summary>
    public bool Dust { get; init; }

    /// <summary>Open water: the storms of the season come harder.</summary>
    public bool Sea { get; init; }
}

/// <summary>One kind of weather as the town names it: clear, rain, a blizzard...</summary>
public sealed record WeatherKind
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>One line for the hover.</summary>
    public string Text { get; init; } = "";
}

/// <summary>content/game/weather.json: the weather fronts, the climate they move through and what the town hears of them.</summary>
public sealed record WeatherRules
{
    /// <summary>Days between new pressure systems on the map, on average.</summary>
    public double Every { get; init; } = 0.9;

    /// <summary>Days a system lives, from its birth to its last.</summary>
    public double[] Life { get; init; } = [5, 10];

    /// <summary>Road days a system drifts in a day.</summary>
    public double[] Speed { get; init; } = [1.4, 2.8];

    /// <summary>Days across a system's heart, and how much longer it runs across its path than along it (a front).</summary>
    public double[] Radius { get; init; } = [3, 7];
    public double Stretch { get; init; } = 1.7;

    /// <summary>Share of systems that are lows (cloud, rain, storms); the rest are highs (clear, still).</summary>
    public double Lows { get; init; } = 0.6;

    /// <summary>How strong a low runs, and a high (as a share of a low).</summary>
    public double[] Strength { get; init; } = [0.4, 1.3];
    public double HighShare { get; init; } = 0.7;

    /// <summary>The rare great storm: its share of lows by season, and how much stronger it is.</summary>
    public Dictionary<string, double> Great { get; init; } = [];
    public double[] GreatStrength { get; init; } = [1.8, 2.4];

    /// <summary>Road days from the town that systems are born and live within.</summary>
    public double Reach { get; init; } = 26;

    /// <summary>Degrees the prevailing wind swings over the year, and a system's own heading wanders from it.</summary>
    public double Swing { get; init; } = 35;
    public double Steer { get; init; } = 25;

    /// <summary>Slow wet and dry spells over the whole map: their size and their length in days.</summary>
    public double Spell { get; init; } = 0.14;
    public double SpellDays { get; init; } = 14;

    /// <summary>The year's heat: mean, half its swing, the warmest day of the year, and the day's swing.</summary>
    public double Mean { get; init; } = 9;
    public double Swing2 { get; init; } = 12;
    public double Warmest { get; init; } = 45;
    public double Daily { get; init; } = 4;

    /// <summary>What the map's warm and wet sides (<see cref="World.Climate"/>) add: degrees and storminess at full.</summary>
    public double ClimateHeat { get; init; } = 9;
    public double ClimateWet { get; init; } = 0.22;

    /// <summary>Summer thunder over warm land in the afternoon: storminess at full, the heat it starts above and a degree's worth.</summary>
    public double Thunder { get; init; } = 0.55;
    public double ThunderAbove { get; init; } = 16;
    public double ThunderPer { get; init; } = 0.09;

    /// <summary>Storminess where each step begins: grey, foul, storm, tempest.</summary>
    public double[] Steps { get; init; } = [0.3, 0.62, 1.0, 1.55];

    /// <summary>Wind that makes a storm of a foul day, and the wind of a still day.</summary>
    public double Gale { get; init; } = 0.9;

    /// <summary>How much of a low's strength blows as wind.</summary>
    public double Gusts { get; init; } = 0.35;
    public double Breeze { get; init; } = 0.18;

    /// <summary>Precipitation falls as snow below this heat, and lying snow melts above that, a share a day.</summary>
    public double SnowBelow { get; init; } = 1;
    public double MeltAbove { get; init; } = 3;
    public double Melt { get; init; } = 0.18;

    /// <summary>Snow a day of foul weather lays, and a storm.</summary>
    public double[] Lays { get; init; } = [0, 0.02, 0.12, 0.3, 0.5];

    /// <summary>Days the land remembers rain (wet ground, high rivers), and how fast each day fades.</summary>
    public int WetDays { get; init; } = 14;
    public double WetFade { get; init; } = 0.85;

    /// <summary>Days of warning the town has of the sky: the sky itself, a druid or ranger at home.</summary>
    public int Lead { get; init; } = 1;
    public int LeadReader { get; init; } = 1;
    public IReadOnlyList<string> Readers { get; init; } = [];

    /// <summary>Extra days a march toward the town takes with snow lying (by cover above each mark) and in a storm.</summary>
    public double[] MarchSnow { get; init; } = [0.3, 0.65];
    public int MarchStorm { get; init; } = 1;
    public int MarchStormHours { get; init; } = 9;
    public int MarchMost { get; init; } = 3;

    public Dictionary<string, WeatherLean> Habitats { get; init; } = [];
    public Dictionary<string, WeatherLean> Themes { get; init; } = [];
    public IReadOnlyList<WeatherKind> Kinds { get; init; } = [];

    /// <summary>Ledger lines: the signs of a storm coming, its breaking over the town, a great storm, and clearing.</summary>
    public IReadOnlyList<string> Signs { get; init; } = [];
    public IReadOnlyList<string> Breaks { get; init; } = [];
    public IReadOnlyList<string> Great2 { get; init; } = [];
    public IReadOnlyList<string> Clears { get; init; } = [];
}

/// <summary>The weather at one spot and hour.</summary>
public readonly record struct WeatherNow
{
    /// <summary>0 fair, 1 grey, 2 foul, 3 storm, 4 tempest.</summary>
    public int Step { get; init; }

    /// <summary>The storminess behind the step.</summary>
    public double Storm { get; init; }

    /// <summary>Degrees, about Celsius.</summary>
    public double Heat { get; init; }

    /// <summary>0 still to 1 a gale (more in a tempest).</summary>
    public double Wind { get; init; }

    /// <summary>The bearing the wind blows toward, degrees (0 north, clockwise).</summary>
    public double WindBearing { get; init; }

    /// <summary>How hard it rains or snows, 0 to 1.</summary>
    public double Fall { get; init; }
    public bool Snow { get; init; }
    public bool Fog { get; init; }
    public bool Dust { get; init; }

    /// <summary>weather.json kind: clear, hot, frost, cloud, fog, rain, snow, dust, thunder, gale, blizzard, sandstorm, tempest.</summary>
    public string Kind { get; init; }
}

public sealed class WeatherState
{
    /// <summary>The town's step at the last check, so the Ledger hears when a storm breaks and clears.</summary>
    public int Step { get; set; }

    /// <summary>The day the town last saw the signs of a storm coming.</summary>
    public int SignDay { get; set; } = -99;
}

/// <summary>
/// The weather (Conner, 2026-10-22: "a slow / lumbering ever-shifting presence, subtle until it's not"). Pressure systems
/// are born upwind, drift across the map on the season's prevailing wind for days, grow and fade: lows bring cloud, rain
/// and storms, highs clear still days. The land shapes them: the warm and wet sides of the map, mountains colder and
/// windier, deserts dry and dusty, the sea mild and foggy, summer afternoons thundery over warm land. Most days are fair
/// or grey; a storm is a few days a season, and a great storm rare. Everything is a pure function of the seed and the
/// hour, so the weather costs nothing to save and never touches the town's dice; only what the town has heard is saved.
/// </summary>
public static class Weather
{
    public const int Fair = 0, Grey = 1, Foul = 2, Storm = 3, Tempest = 4;

    public static WeatherRules Rules(Town town) => town.Content.Weather;

    private static double U(Town town, int k, int salt) => Voices.Hash("front", town.Seed, k, salt) / 4294967296.0;

    private static double Lerp(double[] r, double u) => r.Length == 0 ? 0 : r[0] + (r[^1] - r[0]) * u;

    /// <summary>Days since the campaign began, at an hour.</summary>
    public static double TimeOf(int tick) => tick / (double)SimTime.HoursPerDay;

    // ---- The year and the prevailing wind ----

    /// <summary>The bearing the prevailing wind blows toward at a time (days since the start), swinging over the year.</summary>
    public static double Prevailing(Town town, double t)
    {
        double year = Seasons.YearLength(town);
        double baseline = Voices.Hash("prevailing", town.Seed) % 360;
        return (baseline + Rules(town).Swing * Math.Sin(2 * Math.PI * Seasons.YearDay(town, t) / year) + 360) % 360;
    }

    /// <summary>The season's heat at a time, before the land and the sky change it.</summary>
    private static double SeasonHeat(Town town, double t)
    {
        var r = Rules(town);
        return r.Mean + r.Swing2 * Math.Cos(2 * Math.PI * (Seasons.YearDay(town, t) - r.Warmest) / Seasons.YearLength(town));
    }

    private static string SeasonId(Town town, double t)
    {
        var all = Seasons.Rules(town).Seasons;
        return all.Count == 0 ? "" : all[(int)(Seasons.YearDay(town, t) / Math.Max(1, Seasons.Rules(town).Days)) % all.Count].Id;
    }

    // ---- The pressure systems ----

    private readonly record struct Front(double X, double Y, double Dx, double Dy, double Speed, double Born, double Life, double Radius, double Power);

    private static Front Make(Town town, int k)
    {
        var r = Rules(town);
        double born = (k + (U(town, k, 1) - 0.5) * 0.8) * r.Every;
        double heading = (Prevailing(town, born) + (U(town, k, 2) - 0.5) * 2 * r.Steer) * Math.PI / 180;
        double dx = Math.Sin(heading), dy = -Math.Cos(heading);
        double life = Lerp(r.Life, U(town, k, 3));
        double speed = Lerp(r.Speed, U(town, k, 4));
        double radius = Lerp(r.Radius, U(town, k, 5));
        bool low = U(town, k, 6) < r.Lows;
        // Strong lows are few: most are a grey day or two of rain.
        double power = Lerp(r.Strength, Math.Pow(U(town, k, 7), 1.6));
        if (low && U(town, k, 8) < r.Great.GetValueOrDefault(SeasonId(town, born))) power = Lerp(r.GreatStrength, U(town, k, 9));
        if (!low) power = -power * r.HighShare;
        // Born upwind of where it will be at mid-life, anywhere across the map's breadth.
        double across = (U(town, k, 10) - 0.5) * 2 * r.Reach, along = (U(town, k, 11) - 0.5) * r.Reach * 0.6;
        double back = speed * life / 2;
        double x = -dy * across + dx * (along - back), y = dx * across + dy * (along - back);
        return new Front(x, y, dx, dy, speed, born, life, radius, power);
    }

    private sealed class Cache
    {
        public int From = int.MinValue;
        public Front[] Fronts = [];
        public readonly Dictionary<int, WeatherNow> Town = [];
        public readonly Dictionary<long, (double Snow, double Wet)> Ground = [];
        public List<(int Ring, double Days)>? Rings;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Town, Cache> Caches = new();

    /// <summary>The systems that may be alive at a time.</summary>
    private static ReadOnlySpan<Front> Alive(Town town, double t)
    {
        var r = Rules(town);
        var c = Caches.GetValue(town, _ => new Cache());
        double every = Math.Max(0.05, r.Every);
        int first = (int)Math.Floor((t - Lerp(r.Life, 1)) / every) - 1;
        int last = (int)Math.Ceiling(t / every) + 1;
        if (first < c.From || first > c.From + 8 || last >= c.From + c.Fronts.Length)
        {
            // A window of systems a little wider than needed, so a day's queries reuse it.
            int from = first - 2, n = last - first + 40;
            var fronts = new Front[n];
            for (int i = 0; i < n; i++) fronts[i] = Make(town, from + i);
            c.From = from;
            c.Fronts = fronts;
        }
        return c.Fronts.AsSpan(first - c.From, last - first + 1);
    }

    // ---- The land under the sky ----

    /// <summary>The region at a spot (day units from the town), or null in the town's own ring 0 gaps.</summary>
    public static Region? RegionAt(Town town, double x, double y)
    {
        double d = Math.Sqrt(x * x + y * y);
        if (town.Regions.Count == 0) return null;
        var c = Caches.GetValue(town, _ => new Cache());
        c.Rings ??= town.Regions.GroupBy(g => g.Ring).Select(g => (g.Key, g.Average(x => (double)x.Days))).OrderBy(p => p.Key).ToList();
        int ring = c.Rings[0].Ring;
        double best = double.MaxValue;
        foreach (var (rg, days) in c.Rings)
            if (Math.Abs(days - d) < best)
            {
                best = Math.Abs(days - d);
                ring = rg;
            }
        double b = (Math.Atan2(x, -y) * 180 / Math.PI + 360) % 360;
        Region? pick = null;
        double off = double.MaxValue;
        foreach (var g in town.Regions)
        {
            if (g.Ring != ring) continue;
            double o = Math.Abs(((b - g.Bearing) % 360 + 540) % 360 - 180);
            if (o < off)
            {
                off = o;
                pick = g;
            }
        }
        return pick;
    }

    /// <summary>What the country at a spot leans to: its habitats, then its theme's own.</summary>
    public static WeatherLean Lean(Town town, Region? region)
    {
        if (region == null) return new WeatherLean();
        var r = Rules(town);
        double wet = 0, heat = 0, wind = 0, fog = 0, mild = 0;
        bool dust = false, sea = false;
        int n = 0;
        foreach (var h in region.Theme.Habitats)
        {
            if (!r.Habitats.TryGetValue(h, out var l)) continue;
            n++;
            wet += l.Wet;
            heat += l.Heat;
            wind += l.Wind;
            fog += l.Fog;
            mild = Math.Max(mild, l.Mild);
            dust |= l.Dust;
            sea |= l.Sea;
        }
        if (n > 1)
        {
            wet /= n;
            heat /= n;
            wind /= n;
            fog /= n;
        }
        if (r.Themes.TryGetValue(region.Theme.Id, out var t))
        {
            wet += t.Wet;
            heat += t.Heat;
            wind += t.Wind;
            fog += t.Fog;
            mild = Math.Max(mild, t.Mild);
            dust |= t.Dust;
            sea |= t.Sea;
        }
        return new WeatherLean { Wet = wet, Heat = heat, Wind = wind, Fog = fog, Mild = mild, Dust = dust, Sea = sea };
    }

    /// <summary>The map's warm and wet sides at a spot, as <see cref="World.Climate"/> gives a region's, but smooth.</summary>
    private static (double Heat, double Wet) Climate(Town town, double x, double y)
    {
        double d = Math.Sqrt(x * x + y * y), b = (Math.Atan2(x, -y) * 180 / Math.PI + 360) % 360;
        var rings = town.Content.World.Rings;
        int ring = 0;
        while (ring < rings.Count - 1 && rings[ring].Days < d) ring++;
        return World.Climate(town, ring, b);
    }

    // ---- The weather ----

    /// <summary>The weather at a spot (day units from the town) and hour.</summary>
    public static WeatherNow At(Town town, double x, double y, int tick)
    {
        var r = Rules(town);
        double t = TimeOf(tick);
        int hour = SimTime.HourOf(tick);
        var lean = Lean(town, RegionAt(town, x, y));
        var (cHeat, cWet) = Climate(town, x, y);

        double storm = 0, gust = 0;
        double wx = 0, wy = 0;
        double pb = Prevailing(town, t) * Math.PI / 180;
        wx += Math.Sin(pb) * 0.6;
        wy += -Math.Cos(pb) * 0.6;
        foreach (var f in Alive(town, t))
        {
            double age = t - f.Born;
            if (age <= 0 || age >= f.Life) continue;
            double env = Math.Pow(Math.Sin(Math.PI * age / f.Life), 0.8);
            double cx = f.X + f.Dx * f.Speed * age, cy = f.Y + f.Dy * f.Speed * age;
            double ox = x - cx, oy = y - cy;
            // Along its path and across it: a front is long and narrow.
            double along = ox * f.Dx + oy * f.Dy, across = -ox * f.Dy + oy * f.Dx;
            double q = (along * along + across * across / (r.Stretch * r.Stretch)) / (f.Radius * f.Radius);
            if (q > 9) continue;
            double g = Math.Exp(-q);
            storm += f.Power * env * g;
            if (f.Power <= 0) continue;
            // Winds turn round a low and blow hardest on its flanks.
            double dist = Math.Sqrt(q), ring = dist * Math.Exp(0.5 - dist * dist / 2) * 1.0;
            gust += f.Power * env * (ring * 0.6 + g * 0.4);
            double len = Math.Sqrt(ox * ox + oy * oy);
            if (len > 1e-6)
            {
                wx += -oy / len * f.Power * env * ring;
                wy += ox / len * f.Power * env * ring;
            }
        }
        // The slow wet and dry spells that lie over the whole country for a fortnight.
        double spell = r.Spell * Noise(town, t / Math.Max(1, r.SpellDays), 17);
        storm += spell + cWet * r.ClimateWet + lean.Wet;

        double heat = SeasonHeat(town, t);
        heat = r.Mean + (heat - r.Mean) * (1 - lean.Mild) + cHeat * r.ClimateHeat + lean.Heat;
        double cloud = Math.Clamp(storm / Math.Max(0.01, r.Steps[Math.Min(1, r.Steps.Length - 1)]), 0, 1);
        heat += r.Daily * Math.Cos(2 * Math.PI * (hour - 15) / 24.0) * (1 - 0.6 * cloud);
        // Highs bring hard frost in winter and heat in summer: clear skies let it in.
        if (storm < 0) heat += -storm * (heat < r.Mean ? -3 : 3);

        // Summer thunder: warm land, the afternoon, and a cell here and not there.
        if (!lean.Sea && heat > r.ThunderAbove)
        {
            double afternoon = Math.Exp(-Math.Pow((hour - 16.5) / 2.5, 2));
            double cell = Cell(town, x, y, (int)Math.Floor(t));
            storm += Math.Min(r.Thunder, (heat - r.ThunderAbove) * r.ThunderPer) * afternoon * cell;
        }
        if (lean.Sea && storm > 0) storm *= 1.15;

        double wind = Math.Max(0, r.Breeze + lean.Wind + gust * r.Gusts + Math.Max(0, storm) * 0.15 + 0.06 * Noise(town, t * 3, 23));
        double bearing = (Math.Atan2(wx, -wy) * 180 / Math.PI + 360) % 360;

        int step = 0;
        while (step < r.Steps.Length && storm >= r.Steps[step]) step++;
        bool snow = heat < r.SnowBelow;
        bool dust = lean.Dust && wind >= r.Gale * 0.8;
        // A gale on a foul day is a storm; on dry ground, a sandstorm.
        if (step == Foul && wind >= r.Gale) step = Storm;
        if (dust && step < Storm && wind >= r.Gale) step = Storm;
        double fall = lean.Dust ? 0 : Math.Clamp((storm - r.Steps[Math.Min(1, r.Steps.Length - 1)] * 0.8) / 1.2, 0, 1);
        if (step < Grey) fall = 0;

        // Fog: still, damp air, thickest at dawn and in the cold months, on the sea and the marsh.
        double dawn = Math.Exp(-Math.Pow((hour - 6.5) / 2.2, 2));
        double cold = Math.Clamp((r.Mean + 4 - heat) / 12, 0, 1);
        double fogginess = lean.Fog + dawn * 0.5 + cold * 0.25 + (storm > 0.1 && storm < r.Steps[^1] ? 0.15 : -0.3) - wind * 1.4 + 0.2 * Noise(town, t * 2, 29);
        bool fog = step <= Foul && fogginess > 0.55;
        if (fog && step == Fair) step = Grey;

        string kind = step switch
        {
            >= Tempest => "tempest",
            Storm when dust => "sandstorm",
            Storm when snow => "blizzard",
            Storm when heat >= r.ThunderAbove - 4 && !lean.Sea && wind < r.Gale + 0.2 => "thunder",
            Storm => "gale",
            Foul when dust => "dust",
            _ when fog => "fog",
            Foul => snow ? "snow" : "rain",
            Grey => fall > 0.05 ? (snow ? "snow" : "rain") : "cloud",
            _ => heat >= 28 ? "hot" : heat <= -4 ? "frost" : "clear",
        };
        return new WeatherNow { Step = step, Storm = storm, Heat = heat, Wind = wind, WindBearing = bearing, Fall = fall, Snow = snow, Fog = fog, Dust = dust, Kind = kind };
    }

    /// <summary>Smooth noise in -1..1 over time, its own per salt: value noise between hashed knots.</summary>
    private static double Noise(Town town, double t, int salt)
    {
        int i = (int)Math.Floor(t);
        double f = t - i, s = f * f * (3 - 2 * f);
        double a = Voices.Hash("weather-noise", town.Seed, salt, i) / 4294967296.0, b = Voices.Hash("weather-noise", town.Seed, salt, i + 1) / 4294967296.0;
        return (a + (b - a) * s) * 2 - 1;
    }

    /// <summary>Whether a thunder cell builds over this patch today: 0 to 1, a few patches in each.</summary>
    private static double Cell(Town town, double x, double y, int day)
    {
        int cx = (int)Math.Floor(x / 3), cy = (int)Math.Floor(y / 3);
        double u = Voices.Hash("thunder", town.Seed, day, cx, cy) / 4294967296.0;
        return u < 0.25 ? 1 : u < 0.5 ? 0.4 : 0;
    }

    /// <summary>The weather over the town now (cached by the hour).</summary>
    public static WeatherNow Here(Town town) => Here(town, town.Tick);

    public static WeatherNow Here(Town town, int tick)
    {
        var c = Caches.GetValue(town, _ => new Cache());
        if (c.Town.TryGetValue(tick, out var w)) return w;
        if (c.Town.Count > 400) c.Town.Clear();
        w = At(town, 0, 0, tick);
        c.Town[tick] = w;
        return w;
    }

    /// <summary>The worst of a day over the town (or at a spot), sampled every three hours.</summary>
    public static WeatherNow Worst(Town town, double x, double y, int day)
    {
        WeatherNow worst = default;
        bool any = false;
        int start = (day - 1) * SimTime.HoursPerDay;
        for (int h = 1; h < SimTime.HoursPerDay; h += 3)
        {
            var w = x == 0 && y == 0 ? Here(town, start + h) : At(town, x, y, start + h);
            if (!any || w.Step > worst.Step || (w.Step == worst.Step && w.Storm > worst.Storm))
            {
                worst = w;
                any = true;
            }
        }
        return worst;
    }

    /// <summary>
    /// The ground at a spot on a day: snow lying (0 bare to 1 deep) and how wet it is (0 dry, about 1 after a week of rain),
    /// from the days before it.
    /// </summary>
    public static (double Snow, double Wet) Ground(Town town, double x, double y, int day)
    {
        var r = Rules(town);
        var c = Caches.GetValue(town, _ => new Cache());
        long key = ((long)day << 32) ^ ((long)Math.Round(x * 4) << 16) ^ (long)Math.Round(y * 4);
        if (c.Ground.TryGetValue(key, out var g)) return g;
        if (c.Ground.Count > 2000) c.Ground.Clear();
        double snow = 0, wet = 0, fade = 1;
        int back = Math.Max(r.WetDays, 20);
        for (int d = day - back; d <= day; d++)
        {
            if (d < 1) continue;
            int noon = (d - 1) * SimTime.HoursPerDay + 13, night = (d - 1) * SimTime.HoursPerDay + 3;
            var a = At(town, x, y, noon);
            var b = At(town, x, y, night);
            int step = Math.Max(a.Step, b.Step);
            bool falls = a.Fall > 0 || b.Fall > 0;
            if (falls && (a.Snow || b.Snow)) snow += r.Lays[Math.Min(step, r.Lays.Length - 1)];
            if (a.Heat > r.MeltAbove) snow *= 1 - r.Melt * Math.Min(3, (a.Heat - r.MeltAbove) / 3);
            snow = Math.Clamp(snow, 0, 1);
            if (day - d < r.WetDays)
            {
                fade = Math.Pow(r.WetFade, day - d);
                if (!a.Snow) wet += (a.Fall + b.Fall) * 0.5 * fade;
            }
        }
        g = (snow, Math.Min(1.5, wet * (1 - r.WetFade) * 2.2));
        c.Ground[key] = g;
        return g;
    }

    public static WeatherKind Kind(Town town, string id) =>
        Rules(town).Kinds.FirstOrDefault(k => k.Id == id) ?? new WeatherKind { Id = id, Name = id, Icon = "weather-" + id };

    /// <summary>How the step reads: "Fair", "Grey"...</summary>
    public static string StepName(int step) => step switch { Fair => "Fair", Grey => "Grey", Foul => "Foul", Storm => "Storm", _ => "Tempest" };

    /// <summary>Compass word for the way a wind blows from (the bearing it blows toward, turned round).</summary>
    public static string FromWord(double toward)
    {
        string[] words = ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];
        double from = (toward + 180) % 360;
        return words[(int)Math.Round(from / 45) % 8];
    }

    /// <summary>How much the wind helps a heading: 1 dead behind, -1 dead ahead, scaled by its strength (for ships and fliers).</summary>
    public static double Tailwind(WeatherNow w, double heading) =>
        Math.Cos((heading - w.WindBearing) * Math.PI / 180) * Math.Min(1.3, w.Wind);

    // ---- Forecasts and the town's Ledger ----

    /// <summary>Days ahead the town can read the sky: the sky's own signs, more with a druid or ranger at home.</summary>
    public static int Lead(Town town)
    {
        var r = Rules(town);
        bool reader = town.Heroes.Any(h => !h.Dead && !h.IsAway && r.Readers.Contains(h.Hero!.Cls.Id));
        return r.Lead + (reader ? r.LeadReader : 0);
    }

    /// <summary>The town's sky for the next days it can read: the worst of each day, today first.</summary>
    public static List<WeatherNow> Forecast(Town town)
    {
        var list = new List<WeatherNow>();
        for (int d = 0; d <= Lead(town); d++) list.Add(Worst(town, 0, 0, town.Day + d));
        return list;
    }

    /// <summary>Hours until the next storm reaches the town within the reading, or null.</summary>
    public static int? StormIn(Town town, int atLeast = Storm)
    {
        int hours = (Lead(town) + 1) * SimTime.HoursPerDay;
        for (int h = 0; h <= hours; h += 2)
            if (Here(town, town.Tick + h).Step >= atLeast) return h;
        return null;
    }

    /// <summary>Hours of a day a spot spends in a storm or worse, sampled every three hours.</summary>
    public static int StormHours(Town town, double x, double y, int day)
    {
        int start = (day - 1) * SimTime.HoursPerDay, hours = 0;
        for (int h = 1; h < SimTime.HoursPerDay; h += 3)
            if ((x == 0 && y == 0 ? Here(town, start + h) : At(town, x, y, start + h)).Step >= Storm) hours += 3;
        return hours;
    }

    /// <summary>Extra days a march toward the town takes: snow lying on the roads, and a long storm over them (not an afternoon's thunder).</summary>
    public static int MarchDays(Town town)
    {
        var r = Rules(town);
        var (snow, _) = Ground(town, 0, 0, town.Day);
        int days = r.MarchSnow.Count(mark => snow >= mark);
        if (StormHours(town, 0, 0, town.Day) >= r.MarchStormHours) days += r.MarchStorm;
        return Math.Min(r.MarchMost, days);
    }

    /// <summary>Daily: the town hears the signs of a storm coming, then of it breaking and clearing.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        var st = town.Weather;
        var now = Worst(town, 0, 0, town.Day);
        var dice = new Rng(unchecked((int)Voices.Hash("weather-line", town.Seed, town.Day)));
        string Fill(IReadOnlyList<string> lines, WeatherNow w, int? hours = null) => lines.Count == 0 ? "" : Saga.Cap(lines[dice.Range(0, lines.Count)]
            .Replace("{town}", town.Name)
            .Replace("{kind}", Kind(town, w.Kind).Name.ToLowerInvariant())
            .Replace("{wind}", FromWord(w.WindBearing))
            .Replace("{when}", hours is int h ? (h < 20 ? "before nightfall" : h < 40 ? "by tomorrow" : $"within {(h + 12) / 24} days") : ""));
        if (now.Step >= Storm && st.Step < Storm)
        {
            string line = Fill(now.Step >= Tempest ? r.Great2 : r.Breaks, now);
            if (line != "") town.Log(LedgerCategory.Town, now.Step >= Tempest ? Importance.Major : Importance.Notable, line);
            if (now.Step >= Tempest) Records.Pin(town, "tempest", Kind(town, now.Kind).Name);
        }
        else if (now.Step < Storm && st.Step >= Storm)
        {
            string line = Fill(r.Clears, now);
            if (line != "") town.Log(LedgerCategory.Town, Importance.Routine, line);
        }
        else if (now.Step < Storm && town.Day - st.SignDay > 3 && StormIn(town) is int hours && hours >= 12)
        {
            st.SignDay = town.Day;
            var coming = Here(town, town.Tick + hours);
            string line = Fill(r.Signs, coming, hours);
            if (line != "") town.Log(LedgerCategory.Town, Importance.Notable, line);
        }
        st.Step = now.Step;
    }
}
