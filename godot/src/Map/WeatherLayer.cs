using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The weather layer (Conner, 2026-10-23: "not intrusive to the main map, a toggle able layer"): off unless the player
/// turns it on, and drawn only on the campaign map or the map pulled far out. Cloud, rain, snow, fog and storms lie over
/// the land as soft washes (a small texture of the sky, smoothly filtered), storms named at their hearts, the wind's way
/// in small arrows. Fog of war holds: the weather shows over ground in sight and the sky the town can see from its walls;
/// over ground only known, just the storms on the horizon, faintly; nothing over unknown land. Worked out once a game hour.
/// </summary>
public partial class Overlay
{
    /// <summary>Days a cell of the layer spans, and how far round the town it reaches.</summary>
    private const double SkyCell = 1.25, SkyReach = 22, Horizon = 2.5;

    /// <summary>Patches across the layer it is drawn in, so the map's perspective bends it little.</summary>
    private const int SkyPatches = 12;

    private int _skyTick = -1;
    private readonly List<(double X, double Y, WeatherNow W, float Alpha)> _skyCells = [];
    private Image? _skyImage;
    private ImageTexture? _skyTexture;

    private static int SkyN => (int)Math.Ceiling(SkyReach / SkyCell);

    /// <summary>The sky over what the town can see, once a game hour: the cells and the wash's texture.</summary>
    private void SkyCells(Town town)
    {
        if (_skyTick == town.Tick && _skyTexture != null) return;
        _skyTick = town.Tick;
        _skyCells.Clear();
        int n = SkyN;
        _skyImage ??= Image.CreateEmpty(2 * n, 2 * n, false, Image.Format.Rgba8);
        _skyImage.Fill(Colors.Transparent);
        for (int i = -n; i < n; i++)
            for (int j = -n; j < n; j++)
            {
                double x = (i + 0.5) * SkyCell, y = (j + 0.5) * SkyCell;
                if (x * x + y * y > SkyReach * SkyReach) continue;
                float fogA = map.Fog.AlphaAt(Geo.FromDays(x, y));
                if (fogA >= Fog.UnknownAlpha - 0.02f) continue;
                var w = Weather.At(town, x, y, town.Tick);
                bool seen = fogA <= Fog.SightAlpha + 0.05f || x * x + y * y <= Horizon * Horizon;
                // Known but out of sight: only a storm shows, as thunderheads on the horizon.
                if (!seen && w.Step < Weather.Storm) continue;
                float alpha = seen ? 1 : 0.45f;
                _skyCells.Add((x, y, w, alpha));
                var col = SkyColor(w);
                _skyImage.SetPixel(i + n, j + n, col with { A = col.A * alpha });
            }
        if (_skyTexture == null) _skyTexture = ImageTexture.CreateFromImage(_skyImage);
        else _skyTexture.Update(_skyImage);
    }

    private static Color SkyColor(WeatherNow w)
    {
        if (w.Fog) return new Color(0.9f, 0.89f, 0.84f, 0.3f);
        if (w.Dust && w.Step >= Weather.Foul) return new Color(0.85f, 0.66f, 0.4f, 0.34f);
        bool snow = w.Snow && w.Fall > 0;
        return w.Step switch
        {
            Weather.Grey => snow ? new Color(0.94f, 0.96f, 1f, 0.12f) : new Color(0.7f, 0.74f, 0.8f, 0.1f),
            Weather.Foul => snow ? new Color(0.94f, 0.96f, 1f, 0.3f) : new Color(0.4f, 0.5f, 0.68f, 0.24f),
            Weather.Storm => snow ? new Color(0.88f, 0.92f, 1f, 0.46f) : new Color(0.24f, 0.26f, 0.46f, 0.4f),
            Weather.Tempest => new Color(0.42f, 0.18f, 0.44f, 0.52f),
            _ => Colors.Transparent,
        };
    }

    private void DrawWeather(Town town)
    {
        if (!map.WeatherLayer || (!map.Strategic && map.Distance < 150)) return;
        SkyCells(town);
        var view = GetViewportRect().Grow(40);
        bool At(double x, double y, out Vector2 s) => map.Project(map.Gen.At(Geo.FromDays(x, y), 1.5f), out s);
        // The wash: the sky's texture laid over the land in patches, each corner where the map puts it.
        double reach = SkyN * SkyCell, step = 2 * reach / SkyPatches;
        for (int i = 0; i < SkyPatches; i++)
            for (int j = 0; j < SkyPatches; j++)
            {
                double x0 = -reach + i * step, y0 = -reach + j * step, x1 = x0 + step, y1 = y0 + step;
                if (!At(x0, y0, out var a) || !At(x1, y0, out var b) || !At(x1, y1, out var c) || !At(x0, y1, out var d)) continue;
                if (!new Rect2(a, Vector2.Zero).Expand(b).Expand(c).Expand(d).Intersects(view)) continue;
                float u0 = (float)(i / (double)SkyPatches), u1 = (float)((i + 1) / (double)SkyPatches);
                float v0 = (float)(j / (double)SkyPatches), v1 = (float)((j + 1) / (double)SkyPatches);
                DrawPolygon([a, b, c, d], [Colors.White, Colors.White, Colors.White, Colors.White], [new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1)], _skyTexture);
            }
        // The storms' names at their hearts.
        var marked = new List<(double X, double Y)>();
        foreach (var (x, y, w, alpha) in _skyCells.Where(c => c.W.Step >= Weather.Storm).OrderByDescending(c => c.W.Storm))
        {
            if (marked.Any(m => (m.X - x) * (m.X - x) + (m.Y - y) * (m.Y - y) < 25)) continue;
            marked.Add((x, y));
            if (!map.OnScreen(map.Gen.At(Geo.FromDays(x, y), 2), out var at)) continue;
            var kind = Weather.Kind(town, w.Kind);
            Disc(at, 12, new Color(0.12f, 0.12f, 0.2f, 0.75f * alpha), w.Step >= Weather.Tempest ? Ui.Bad : Ui.Gold, 2);
            DrawTextureRect(Ui.Icon(kind.Icon), new Rect2(at - new Vector2(9, 9), new Vector2(18, 18)), false, new Color(1, 1, 1, alpha));
            Text(at + new Vector2(0, 26), kind.Name, Ui.Bold, 12, new Color(0.95f, 0.92f, 0.82f, alpha), 3);
            // A storm a lord works or called: who is behind it, once the town knows the lair.
            if (w.By != "") Text(at + new Vector2(0, 40), Ui.Cap(w.By), Ui.Bold, 11, new Color(1f, 0.62f, 0.5f, alpha), 3);
        }
        // The wind's way, every third cell of what is in sight.
        foreach (var (x, y, w, alpha) in _skyCells)
        {
            if (alpha < 1 || w.Wind < 0.15) continue;
            int i = (int)Math.Floor(x / SkyCell), j = (int)Math.Floor(y / SkyCell);
            if (((i % 3) + 3) % 3 != 0 || ((j % 3) + 3) % 3 != 0) continue;
            if (!map.OnScreen(map.Gen.At(Geo.FromDays(x, y), 2), out var at)) continue;
            double b = w.WindBearing * Math.PI / 180;
            if (!map.Project(map.Gen.At(Geo.FromDays(x + Math.Sin(b) * 0.6, y - Math.Cos(b) * 0.6), 2), out var ahead) || ahead == at) continue;
            float len = 12 + 20 * Mathf.Clamp((float)w.Wind, 0, 1.3f);
            var dir = (ahead - at).Normalized();
            var tip = at + dir * len * 0.5f;
            var tail = at - dir * len * 0.5f;
            bool gale = w.Wind >= Weather.Rules(town).Gale;
            var ink = gale ? new Color(1f, 0.86f, 0.5f, 0.95f) : new Color(0.93f, 0.96f, 1f, 0.75f);
            DrawLine(tail + new Vector2(1, 1), tip + new Vector2(1, 1), new Color(0, 0, 0, 0.35f), gale ? 3 : 2);
            DrawLine(tail, tip, ink, gale ? 3 : 2);
            var side = new Vector2(-dir.Y, dir.X) * 4.5f;
            DrawColoredPolygon([tip + dir * 2, tip - dir * 7 + side, tip - dir * 7 - side], ink);
        }
    }
}
