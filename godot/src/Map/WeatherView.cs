using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The sky over the town, from the sim's weather (<see cref="Weather.Here"/>): cloud dims the sun and greys the haze,
/// rain and snow fall round the camera, fog thickens the distance, the wind drives the trees, banners and smoke harder,
/// wet ground darkens and snow lies as it fell. Thunderstorms and great storms flash with lightning. Everything eases
/// toward the next hour's sky so a front comes on over the hour; the inked campaign map keeps its own light. Client
/// randomness (when the lightning flashes) goes through <see cref="Geo.Hash"/>.
/// </summary>
public partial class MapView
{
    /// <summary>The sky as drawn now, each eased toward the hour's: cloud cover, rain and snow falling, fog, wind, wet ground, snow lying (0 to 1).</summary>
    public float Cloud, Rain, Snowing, Mistiness, Wind = 0.2f, Wet, SnowLying;

    /// <summary>The weather layer on the campaign map: off unless the player turns it on (the Weather button by the corner map).</summary>
    public bool WeatherLayer { get; set; }

    /// <summary>A lightning flash fading out, 0 to 1.</summary>
    public float Flash;

    /// <summary>The grade's saturation before the sky greys it (the season's, or the painted look's).</summary>
    private float _saturation = 1;

    private double _flashAt;
    private int _flashes;

    private static float CloudOf(WeatherNow w) => w.Fog ? 0.55f : w.Step switch
    {
        Weather.Fair => 0.05f,
        Weather.Grey => 0.45f,
        Weather.Foul => 0.75f,
        Weather.Storm => 0.92f,
        _ => 1f,
    };

    /// <summary>Every frame: ease the drawn sky toward this hour's and the next's, and set the shaders' wind, wet and snow.</summary>
    private void UpdateSky(Town town, float dt)
    {
        var now = Weather.Here(town);
        var next = Weather.Here(town, town.Tick + 1);
        float f = Mathf.Clamp(HourFraction, 0, 1);
        float Mix(float a, float b) => a + (b - a) * f;
        float k = _first ? 1 : 1 - Mathf.Exp(-dt * 1.2f);
        void Ease(ref float v, float goal) => v += (goal - v) * k;
        Ease(ref Cloud, Mix(CloudOf(now), CloudOf(next)));
        Ease(ref Rain, Mix(now.Snow ? 0 : (float)now.Fall, next.Snow ? 0 : (float)next.Fall));
        Ease(ref Snowing, Mix(now.Snow ? (float)now.Fall : 0, next.Snow ? (float)next.Fall : 0));
        Ease(ref Mistiness, Mix(now.Fog ? 1 : 0, next.Fog ? 1 : 0));
        Ease(ref Wind, Mathf.Clamp(Mix((float)now.Wind, (float)next.Wind), 0, 1.3f));
        var (snow, wet) = Weather.Ground(town, 0, 0, town.Day);
        Ease(ref Wet, Mathf.Clamp((float)wet + Rain * 0.6f, 0, 1));
        Ease(ref SnowLying, Mathf.Clamp((float)snow * 1.3f, 0, 1));
        Mats.SetAll("snow", SnowLying);
        Mats.SetAll("gale_wind", Wind);
        Mats.SetAll("wet_ground", Wet * (1 - _strategic));

        // Lightning: a flash every few seconds in a thunderstorm, more in a great storm.
        bool thunder = now.Kind is "thunder" or "tempest" || (now.Step >= Weather.Storm && !now.Snow && !now.Dust && now.Heat > 8);
        double clock = Godot.Time.GetTicksMsec() / 1000.0;
        if (thunder && !Strategic && clock >= _flashAt)
        {
            if (_flashAt > 0) Flash = 1;
            _flashes++;
            _flashAt = clock + (now.Step >= Weather.Tempest ? 1.5 : 3) + Geo.Hash(_flashes * 131 + town.Tick) * (now.Step >= Weather.Tempest ? 4 : 9);
        }
        else if (!thunder) _flashAt = 0;
        Flash = Mathf.Max(0, Flash - dt * 5);
    }

    /// <summary>A world point on screen wherever it falls, even off the edges (false only behind the camera).</summary>
    public bool Project(Vector3 w, out Vector2 screen)
    {
        screen = default;
        if (_camera.IsPositionBehind(w)) return false;
        screen = _camera.UnprojectPosition(w);
        return true;
    }

    /// <summary>After the day's light is set: cloud dims and greys it, fog closes in, lightning flashes.</summary>
    private void ApplySky()
    {
        float town = 1 - _strategic;
        float c = Cloud * town;
        _sun.LightEnergy *= 1 - 0.62f * c;
        _sun.LightColor = _sun.LightColor.Lerp(new Color("c8d0d8"), c * 0.6f);
        _env.AmbientLightColor = _env.AmbientLightColor.Lerp(new Color("9aa4b0"), c * 0.4f);
        _env.AmbientLightEnergy *= 1 - 0.2f * c;
        _grade.SetShaderParameter("saturation", _saturation * (1 - 0.28f * c));
        // Fog and driven snow whiten the distance.
        float white = Mathf.Clamp(Mistiness * 0.5f + Snowing * Wind * 1.2f, 0, 0.7f) * town;
        var haze = _env.BackgroundColor.Lerp(new Color("8e98a4"), c * 0.45f).Lerp(new Color("d6dade"), white);
        _env.BackgroundColor = haze;
        _env.FogLightColor = haze;
        _env.FogDensity *= 1 + town * (Mistiness * 3f + Rain * 0.8f + Snowing * (1.2f + 2.5f * Wind));
        if (Flash > 0)
        {
            _sun.LightEnergy += Flash * 2.2f * town;
            _env.AmbientLightColor = _env.AmbientLightColor.Lerp(new Color("e8eeff"), Flash * 0.6f * town);
        }
    }
}
