using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>How much the town knows about a place.</summary>
public enum Sight
{
    /// <summary>Never seen: the terrain's silhouette at most. Sites are names from tavern gossip.</summary>
    Rumoured,

    /// <summary>Described by a party that came home; the picture is as old as the report.</summary>
    Scouted,

    /// <summary>Seen right now: inside the town's lamplight and towers, or watched by a scrying, scout, mirror or the Spire.</summary>
    Live,
}

/// <summary>What one party's report taught the town.</summary>
public sealed record Report(string SiteId, int Day, bool ReachedSite, float Reach, ExpeditionOutcome? Outcome);

/// <summary>
/// The fog of war. Everything is derived from the simulation (parties that came home, scryings,
/// the town's size and towers), so it needs no saving and stays deterministic. The fog is a small
/// texture, one pixel per quarter tile, drawn with linear filtering for soft edges.
/// </summary>
public sealed class Fog
{
    public const float X0 = -12;
    public const float Span = Geo.World + 24;
    private const int PerTile = 4;
    public const int N = (int)Span * PerTile;

    /// <summary>A report older than this shows as a "last seen" ghost.</summary>
    public const int StaleDays = 30;

    private static readonly Color Unknown = new("1b1711");
    public const float UnknownAlpha = 0.84f;
    private static readonly Color Described = new("5d4a30");

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
        LightRadius = geo.TownRadius + 1.4f + town.Defenses.Towers * 0.9f;
        LiveAreas.Add((Geo.Center, LightRadius));
        // The Sleepless Spire lights every site within its reach.
        int reach = Watch.Reach(town);
        if (reach > 0) LiveAreas.Add((Geo.Center, geo.TownRadius + 1.3f + reach * 1.1f + 2.2f));
        foreach (var (site, source) in Watch.LiveSites(town))
            if (source != LiveSource.Spire && geo.Pins.TryGetValue(site.Def.Id, out var pin)) LiveAreas.Add((pin, 2.6f));

        string key = $"{town.Day}|{LightRadius:F2}|{Reports.Count}|{string.Join(',', LiveAreas.Skip(1).Select(a => $"{a.P}:{a.R:F1}"))}|{string.Join(',', geo.Pins.Values.Select(p => p.ToString("F1")))}";
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
            if (!p.Returned) continue;
            var chron = town.Chronicles.FirstOrDefault(c => c.PartyName == p.Name && c.DepartDay == SimTime.DayOf(p.DepartTick));
            // Nobody comes home from a wiped party, so nobody describes the road.
            if (chron?.Outcome == ExpeditionOutcome.Wiped) continue;
            var site = Quests.Site(town, p.SiteId)!;
            float reach = p.RecalledTick is int rt ? Mathf.Clamp((rt - p.DepartTick) / (float)(site.Def.Distance * SimTime.HoursPerDay), 0, 1) : 1;
            Reports.Add(new Report(p.SiteId, SimTime.DayOf(p.ReturnTick), p.RecalledTick == null, reach, chron?.Outcome));
        }
        // Scouts who come home describe the road and the site they watched.
        foreach (var s in town.Scouts)
        {
            if (!s.Home) continue;
            float reach = s.Arrived ? 1 : Mathf.Clamp((s.LeaveTick - s.DepartTick) / (float)Math.Max(1, s.ArriveTick - s.DepartTick), 0, 1);
            Reports.Add(new Report(s.SiteId, SimTime.DayOf(s.ReturnTick), s.Arrived, reach, null));
        }
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
                float k = Mathf.Clamp((radius - SegmentDistance(w, a, b)) / 0.7f, 0, 1);
                int i = y * N + x;
                if (k <= 0) continue;
                if (_day[i] < day) _day[i] = day;
                _known[i] = Mathf.Max(_known[i], k);
            }
    }

    private static float Y0Of(int y) => X0 + (y + 0.5f) / PerTile;

    private void Paint(Town town, Geo geo)
    {
        Array.Fill(_day, -1);
        Array.Fill(_known, 0f);
        foreach (var r in Reports)
        {
            if (!geo.Pins.TryGetValue(r.SiteId, out var pin)) continue;
            var end = Geo.Center + (pin - Geo.Center) * r.Reach;
            Stamp(Geo.Center, end, 1.0f, r.Day);
            if (r.ReachedSite) Stamp(pin, pin, 2.3f, r.Day);
        }
        int today = town.Day;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int i = y * N + x;
                var w = new Vector2(X0 + (x + 0.5f) / PerTile, Y0Of(y));
                float k = _known[i];
                float described = _day[i] >= 0 && today - _day[i] > StaleDays ? 0.58f : 0.34f;
                float a = Mathf.Lerp(UnknownAlpha, described, k);
                var c = Unknown.Lerp(Described, k);
                foreach (var (p, r) in LiveAreas)
                {
                    float soft = r > 4 ? 1.6f : 0.9f;
                    a *= Mathf.Clamp((w.DistanceTo(p) - r + soft) / soft, 0, 1);
                }
                _alpha[i] = a;
                _rgba[i * 4] = (byte)(c.R * 255);
                _rgba[i * 4 + 1] = (byte)(c.G * 255);
                _rgba[i * 4 + 2] = (byte)(c.B * 255);
                _rgba[i * 4 + 3] = (byte)(a * 255);
            }
        _image.SetData(N, N, false, Image.Format.Rgba8, _rgba);
        Texture.Update(_image);
    }

    /// <summary>Fog opacity at a world point: 0 is live, UnknownAlpha is unknown.</summary>
    public float AlphaAt(Vector2 w)
    {
        int x = Px(w.X), y = Px(w.Y);
        if (x < 0 || y < 0 || x >= N || y >= N) return UnknownAlpha;
        return _alpha[y * N + x];
    }

    public bool IsLive(Vector2 w) => AlphaAt(w) < 0.3f;

    public Sight SightOf(string siteId, Vector2 pin) =>
        IsLive(pin) ? Sight.Live : Reports.Any(r => r.SiteId == siteId && r.ReachedSite) ? Sight.Scouted : Sight.Rumoured;

    /// <summary>The newest report that reached a site.</summary>
    public Report? LastReport(string siteId) => Reports.Where(r => r.SiteId == siteId && r.ReachedSite).MaxBy(r => r.Day);
}
