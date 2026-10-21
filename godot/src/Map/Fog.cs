using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>How much the town knows about a place.</summary>
public enum Sight
{
    /// <summary>Never seen: the terrain's silhouette at most. Sites are names from tavern gossip.</summary>
    Rumoured,

    /// <summary>Described by a party that came home on a council bounty, or whose report the town bought; the picture is as old as the trip.</summary>
    Scouted,

    /// <summary>Seen right now: inside the town's lamplight and towers, or watched by a scrying, scout, mirror or the Spire.</summary>
    Live,
}

/// <summary>What one party's report taught the town.</summary>
/// <summary>A party's or scout's account; <see cref="Roads"/> are the roads it walked (world points), or null for the straight road out.</summary>
public sealed record Report(string SiteId, int Day, bool ReachedSite, float Reach, ExpeditionOutcome? Outcome, IReadOnlyList<Vector2[]>? Roads = null);

/// <summary>
/// The fog of war. Everything is derived from the simulation (parties that came home, scryings,
/// the town's size and towers), so it needs no saving and stays deterministic. The fog is one
/// texture over the whole map, one texel per tile, sampled with linear filtering for soft edges by
/// every world shader. Only the area that reports and live sight touch is repainted.
/// </summary>
public sealed class Fog
{
    public const float X0 = 0;
    public const float Span = Geo.World;
    // Half a pixel a tile: the fog is soft-edged, and a 2048-tile world at full size would cost four times the repaint.
    private const float PerTile = 0.5f;
    public const int N = (int)(Span * PerTile);

    /// <summary>A report older than this shows as a "last seen" ghost.</summary>
    public const int StaleDays = 30;

    public const float UnknownAlpha = 0.84f;

    private readonly byte[] _rgba = new byte[N * N * 4];
    private readonly float[] _alpha = new float[N * N];
    private readonly int[] _day = new int[N * N];
    private readonly float[] _known = new float[N * N];
    private readonly Image _image;
    public ImageTexture Texture { get; }

    public readonly List<Report> Reports = [];
    public readonly List<(Vector2 P, float R)> LiveAreas = [];
    public float LightRadius { get; private set; }

    private string _key = "";

    /// <summary>How far a live view of a site reaches around it, in tiles.</summary>
    public const float SiteSight = 13;

    /// <summary>Set by --reveal: the whole map is in sight.</summary>
    public static bool RevealAll;

    public static float SpireRadius(Geo geo, int reach) => Geo.DayRadius(reach) + SiteSight;

    public Fog()
    {
        _image = Image.CreateEmpty(N, N, false, Image.Format.Rgba8);
        Texture = ImageTexture.CreateFromImage(_image);
    }

    /// <summary>Recompute when anything that shapes the fog changed; cheap to call every frame.</summary>
    public bool Refresh(Town town, Geo geo)
    {
        CollectReports(town);
        LiveAreas.Clear();
        // The town's lamplight reaches a little past its edge; each watchtower sees farther.
        LightRadius = geo.TownRadius + 9 + town.Defenses.Towers * 5;
        LiveAreas.Add((Geo.Center, LightRadius));
        if (RevealAll) LiveAreas.Add((Geo.Center, Geo.World));
        // The Sleepless Spire lights every site within its reach.
        int reach = Watch.Reach(town);
        if (reach > 0) LiveAreas.Add((Geo.Center, SpireRadius(geo, reach)));
        // Each watchtower sees its own circle of the wilds, wider as the town grows.
        float tower = (float)Watch.TowerSight(town);
        foreach (var b in Watch.Watchtowers(town)) LiveAreas.Add((Geo.LotPos(town.Lot(b.LotId)), tower));
        foreach (var (site, source) in Watch.LiveSites(town))
            if (source is not (LiveSource.Spire or LiveSource.Tower) && geo.Pins.TryGetValue(site.Def.Id, out var pin)) LiveAreas.Add((pin, SiteSight));

        string key = $"{town.Day}|{LightRadius:F0}|{Reports.Count}|{string.Join(',', LiveAreas.Skip(1).Select(a => $"{a.P.Round()}:{a.R:F0}"))}|{string.Join(',', geo.Pins.Values.Select(p => p.Round().ToString("F0")))}";
        if (key == _key) return false;
        _key = key;
        Paint(town, geo);
        return true;
    }

    private void CollectReports(Town town)
    {
        Reports.Clear();
        foreach (var p in town.Parties)
        {
            // A company back from a trip the council didn't pay for keeps what it saw until the town buys it.
            if (!p.Returned || p.Withheld) continue;
            var chron = town.Chronicles.FirstOrDefault(c => c.PartyName == p.Name && c.DepartDay == SimTime.DayOf(p.DepartTick));
            // Nobody comes home from a wiped party, so nobody describes the road.
            if (chron?.Outcome == ExpeditionOutcome.Wiped) continue;
            // They describe the roads they actually walked, round or through the hard country.
            var roads = Roads.Walked(town, p).Select(line => line.Select(Geo.FromDays).ToArray()).ToList();
            Reports.Add(new Report(p.SiteId, SimTime.DayOf(p.ReturnTick), p.RecalledTick == null, 1, chron?.Outcome, roads));
        }
        // Explorers describe every lair they sent word of, as they found it; the country they walked, once home.
        foreach (var f in town.Finds)
        {
            if (!f.Known || f.SiteId == null || town.Tick < f.ReportTick) continue;
            Reports.Add(new Report(f.SiteId, SimTime.DayOf(f.ReportTick), true, 1, null, []));
        }
        foreach (var s in town.Surveys)
        {
            if (s.Phase != SurveyPhase.Home) continue;
            var roads = Explore.Walked(town, s).Select(line => line.Select(Geo.FromDays).ToArray()).ToList();
            Reports.Add(new Report("", SimTime.DayOf(s.ReturnTick), false, 1, null, roads));
        }
        // Scouts who come home describe the road and the site they watched.
        foreach (var s in town.Scouts)
        {
            if (!s.Home) continue;
            float reach = s.Arrived ? 1 : Mathf.Clamp((s.LeaveTick - s.DepartTick) / (float)Math.Max(1, s.ArriveTick - s.DepartTick), 0, 1);
            Reports.Add(new Report(s.SiteId, SimTime.DayOf(s.ReturnTick), s.Arrived, reach, null));
        }
    }

    /// <summary>The stretches of road a report describes.</summary>
    private static IEnumerable<(Vector2 A, Vector2 B)> Segments(Report r, Vector2 pin)
    {
        if (r.Roads == null)
        {
            yield return (Geo.Center, Geo.Center + (pin - Geo.Center) * r.Reach);
            yield break;
        }
        foreach (var line in r.Roads)
            for (int i = 1; i < line.Length; i++) yield return (line[i - 1], line[i]);
    }

    /// <summary>The site a report is about; a report of roads alone (explorers) has none and needs its roads.</summary>
    private static bool PinOf(Geo geo, Report r, out Vector2 pin)
    {
        if (geo.Pins.TryGetValue(r.SiteId, out pin)) return true;
        pin = Geo.Center;
        return r.Roads != null && !r.ReachedSite;
    }

    private static int Px(float world) => (int)((world - X0) * PerTile);

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.LengthSquared() < 1e-6f ? 0 : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0, 1);
        return p.DistanceTo(a + ab * t);
    }

    private void Stamp(Vector2 a, Vector2 b, float radius, int day)
    {
        int x0 = Math.Max(0, Px(Mathf.Min(a.X, b.X) - radius)), x1 = Math.Min(N - 1, Px(Mathf.Max(a.X, b.X) + radius));
        int y0 = Math.Max(0, Px(Mathf.Min(a.Y, b.Y) - radius)), y1 = Math.Min(N - 1, Px(Mathf.Max(a.Y, b.Y) + radius));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var w = new Vector2(X0 + (x + 0.5f) / PerTile, Y0Of(y));
                // Soft edges: a report describes the middle of the road better than its verges.
                float k = Mathf.Clamp((radius - SegmentDistance(w, a, b)) / 3f, 0, 1);
                int i = y * N + x;
                if (k <= 0) continue;
                if (_day[i] < day) _day[i] = day;
                _known[i] = Mathf.Max(_known[i], k);
            }
    }

    private static float Y0Of(int y) => X0 + (y + 0.5f) / PerTile;

    private void Paint(Town town, Geo geo)
    {
        // Everything starts unknown; only the box that reports and live sight touch is worked out.
        Array.Fill(_alpha, UnknownAlpha);
        for (int i = 0; i < N * N; i++)
        {
            _rgba[i * 4] = 255;
            _rgba[i * 4 + 1] = 0;
            _rgba[i * 4 + 2] = 0;
            _rgba[i * 4 + 3] = 255;
        }
        float bx0 = float.MaxValue, by0 = float.MaxValue, bx1 = float.MinValue, by1 = float.MinValue;
        void Grow(Vector2 p, float r)
        {
            bx0 = Mathf.Min(bx0, p.X - r);
            by0 = Mathf.Min(by0, p.Y - r);
            bx1 = Mathf.Max(bx1, p.X + r);
            by1 = Mathf.Max(by1, p.Y + r);
        }
        foreach (var (p, r) in LiveAreas) Grow(p, r + 2);
        foreach (var r in Reports)
        {
            if (!PinOf(geo, r, out var pin)) continue;
            foreach (var (a, b) in Segments(r, pin))
            {
                Grow(a, ReportRoad);
                Grow(b, ReportRoad);
            }
            if (r.ReachedSite) Grow(pin, ReportSite);
        }
        int x0 = Math.Max(0, Px(bx0)), y0 = Math.Max(0, Px(by0)), x1 = Math.Min(N - 1, Px(bx1)), y1 = Math.Min(N - 1, Px(by1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * N + x;
                _day[i] = -1;
                _known[i] = 0;
            }
        foreach (var r in Reports)
        {
            if (!PinOf(geo, r, out var pin)) continue;
            foreach (var (a, b) in Segments(r, pin)) Stamp(a, b, ReportRoad, r.Day);
            if (r.ReachedSite) Stamp(pin, pin, ReportSite, r.Day);
        }
        int today = town.Day;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * N + x;
                var w = new Vector2(X0 + (x + 0.5f) / PerTile, Y0Of(y));
                float k = _known[i];
                float described = _day[i] >= 0 && today - _day[i] > StaleDays ? 0.58f : 0.34f;
                float a = Mathf.Lerp(UnknownAlpha, described, k);
                foreach (var (p, r) in LiveAreas)
                {
                    float soft = r > 20 ? 6f : 3f;
                    a *= Mathf.Clamp((w.DistanceTo(p) - r + soft) / soft, 0, 1);
                }
                _alpha[i] = a;
                _rgba[i * 4] = (byte)(a / UnknownAlpha * 255);
                _rgba[i * 4 + 1] = (byte)(k * 255);
            }
        _image.SetData(N, N, false, Image.Format.Rgba8, _rgba);
        Texture.Update(_image);
        Version++;
    }

    /// <summary>Fog opacity at a world point: 0 is live, UnknownAlpha is unknown.</summary>
    public float AlphaAt(Vector2 w)
    {
        int x = Px(w.X), y = Px(w.Y);
        if (x < 0 || y < 0 || x >= N || y >= N) return UnknownAlpha;
        return _alpha[y * N + x];
    }

    /// <summary>Fog thinner than this is seen live; anything moving through thicker fog, or its tracks, is never drawn.</summary>
    public const float SightAlpha = 0.3f;

    public bool IsLive(Vector2 w) => AlphaAt(w) < SightAlpha;

    /// <summary>How well reports describe a world point, 0 (never described) to 1.</summary>
    public float KnownAt(Vector2 w)
    {
        int x = Px(w.X), y = Px(w.Y);
        if (x < 0 || y < 0 || x >= N || y >= N) return 0;
        return _known[y * N + x];
    }

    /// <summary>Bumped every time the fog is repainted, so views built from it know to rebuild.</summary>
    public int Version { get; private set; }

    /// <summary>Half-width of the strip of road a report describes, and the radius it describes around a site.</summary>
    public const float ReportRoad = 5, ReportSite = 16;

    public Sight SightOf(string siteId, Vector2 pin) =>
        IsLive(pin) ? Sight.Live : Reports.Any(r => r.SiteId == siteId && r.ReachedSite) ? Sight.Scouted : Sight.Rumoured;

    /// <summary>The newest report that reached a site.</summary>
    public Report? LastReport(string siteId) => Reports.Where(r => r.SiteId == siteId && r.ReachedSite).MaxBy(r => r.Day);
}
