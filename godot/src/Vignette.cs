using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim.Systems;
using Godot;
using static CrownAndCompass.Client.Map.HeroKit;

namespace CrownAndCompass.Client;

/// <summary>
/// A faction's picture on its first-meeting card (<see cref="Main"/>'s intro, intros.json): its host standing in its
/// own setting under its own sky, drawn live with the heroes' kit and the baked models in the painted look, the chief
/// in front, the camera easing in. Nothing is painted by hand, so a new faction is one line of intros.json. Like the
/// council chamber it is its own small 3D world and only renders while it shows.
/// </summary>
public partial class Vignette : Control
{
    private const float FigureScale = 3.0f;

    /// <summary>A figure's height in the picture, the unit props are placed and sized in.</summary>
    private const float Tall = FigureScale * 0.6f;

    private readonly SubViewport _vp;
    private readonly Camera3D _cam;
    private readonly Heroes _heroes = new();
    private readonly MeshInstance3D _set = new();
    private readonly Backdrop _back;
    private readonly ShaderMaterial _props;
    private readonly Godot.Environment _env;
    private readonly DirectionalLight3D _sun, _rim;
    private readonly Air _air;
    private readonly List<(Heroes.Look Look, Vector3 At, float Yaw, float Scale, Heroes.Act Act, float Phase)> _figures = [];
    private float _now;

    public Vignette(float height = 300)
    {
        CustomMinimumSize = new Vector2(0, height);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;

        // The sky and far hills are painted flat behind the 3D: the viewport is see-through where nothing stands.
        _back = new Backdrop(this) { MouseFilter = MouseFilterEnum.Ignore };
        _back.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_back);
        var box = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(box);
        _vp = new SubViewport { OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa2X, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
        box.AddChild(_vp);

        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightEnergy = 0.55f,
            GlowEnabled = true,
            GlowIntensity = 0.6f,
            GlowBloom = 0.08f,
            GlowHdrThreshold = 0.85f,
            FogEnabled = true,
            FogDensity = 0.016f,
        };
        _vp.AddChild(new WorldEnvironment { Environment = _env });
        // The sun low and to one side, and a strong light from behind that rims the host against the sky.
        _sun = new DirectionalLight3D { RotationDegrees = new Vector3(-28, 38, 0), LightEnergy = 1.1f, ShadowEnabled = true };
        _rim = new DirectionalLight3D { RotationDegrees = new Vector3(-12, 170, 0), LightEnergy = 1.3f };
        _vp.AddChild(_sun);
        _vp.AddChild(_rim);

        _cam = new Camera3D { Fov = 38, Current = true };
        _vp.AddChild(_cam);

        var noise = Mats.NoiseTexture();
        var clear = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
        _props = new ShaderMaterial { Shader = Mats.Props.Shader };
        _props.SetShaderParameter("fog_tex", clear);
        _props.SetShaderParameter("noise_tex", noise);
        _props.SetShaderParameter("toon", 1.0f);
        var ink = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink.gdshader") };
        ink.SetShaderParameter("fog_tex", clear);
        ink.SetShaderParameter("noise_tex", noise);
        ink.SetShaderParameter("width", 0.0026f);
        _props.NextPass = ink;
        _set.MaterialOverride = _props;
        _vp.AddChild(_set);
        _heroes.OwnMaterials(noise);
        _vp.AddChild(_heroes);

        // Dark edges like an old plate, and whatever drifts in the air.
        _air = new Air(this) { MouseFilter = MouseFilterEnum.Ignore };
        _air.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_air);
    }

    private IntroScene _scene = new();
    private int _seed;

    /// <summary>Stage a faction's picture: its scene, its host in its colours, the chief in front.</summary>
    public void Show(IntroDef d, IntroScene? scene)
    {
        _scene = scene ?? new IntroScene();
        _seed = Seed(d.Lineage);
        _now = 0;
        Color horizon = new(_scene.Horizon);
        _env.AmbientLightColor = horizon.Lerp(new Color(_scene.Light), 0.5f);
        _env.FogLightColor = horizon;
        _sun.LightColor = new Color(_scene.Light);
        _rim.LightColor = new Color(_scene.Rim);
        BuildSet(d);
        _back.QueueRedraw();
        PlaceHost(d);
    }

    /// <summary>A name's stable number (string hashes differ between runs in .NET).</summary>
    private static int Seed(string s)
    {
        int h = 17;
        foreach (char ch in s) h = unchecked(h * 31 + ch);
        return h & 0x7fffff;
    }

    internal float Rand(int i) => Geo.Hash(_seed * 131 + i * 7919);

    private void BuildSet(IntroDef d)
    {
        var k = new MeshKit();
        Color g1 = new(_scene.Ground), g2 = new(_scene.Ground2);
        // The ground: rings of patches from the host out to the haze, in the scene's two earths.
        for (int r = 0; r < 14; r++)
            for (int i = 0; i < 18 + r * 4; i++)
            {
                float a = Mathf.Tau * (i + Rand(r * 97 + i) * 0.5f) / (18 + r * 4), dist = 0.8f + r * 1.4f;
                var at = new Vector3(Mathf.Cos(a) * dist, -0.04f, -Mathf.Sin(a) * dist);
                var c = g1.Lerp(g2, Rand(r * 31 + i * 3)) with { A = MeshKit.Flag.Plain };
                k.Box(at, new Vector3(1.9f + r * 0.3f, 0.04f + Rand(i + r) * 0.03f, 1.9f + r * 0.3f), c);
            }
        // Props in their bands round the host, never in front of it (the camera's side).
        int n = 0;
        foreach (var p in _scene.Props)
        {
            for (int i = 0; i < p.Count; i++, n++)
            {
                float side = (n % 2 == 0 ? 1 : -1);
                float a = Mathf.Pi / 2 + side * (0.25f + Rand(n * 13) * 1.35f);
                float dist = (float)(p.Near + (p.Far - p.Near) * Rand(n * 17 + 5)) * Tall;
                var at = new Vector3(Mathf.Cos(a) * dist, 0, -Mathf.Sin(a) * dist);
                float s = (float)p.Scale * Tall * (0.8f + 0.4f * Rand(n * 23));
                Func<Color, Color>? tint = p.Tint == "" ? null : c => c.Lerp(new Color(p.Tint), 0.6f) with { A = c.A };
                k.Add(p.Model, MeshKit.At(at.X, 0, at.Z, Rand(n * 29) * Mathf.Tau, s), tint);
            }
        }
        // War banners in the faction's colours either side behind the chief: a field with a band of the charge's
        // colour and a swallow-tailed foot.
        Color field = new(d.Field), charge = new(d.Charge);
        foreach (int side in new[] { -1, 1 })
        {
            var b = new Vector3(side * 1.75f * Tall, 0, -1.5f * Tall);
            float bw = 0.3f * Tall, top = 2.15f * Tall, low = 1.05f * Tall, z = 0.05f;
            k.Push(MeshKit.At(b.X, 0, b.Z, -side * 0.25f));
            k.Frustum(Vector3.Zero, 0.035f, 0.03f, 2.3f * Tall, 6, new Color("3a2a1c"));
            k.Blob(new Vector3(0, 2.32f * Tall, 0), new Vector3(0.06f, 0.08f, 0.06f), new Color("c9a24a") with { A = MeshKit.Flag.Metal });
            k.Box(new Vector3(0, top, z), new Vector3(2 * bw + 0.12f, 0.035f, 0.05f), new Color("c9a24a") with { A = MeshKit.Flag.Metal });
            var cloth = field.Lightened(0.12f);
            k.Box(new Vector3(0, low, z), new Vector3(2 * bw, top - low, 0.03f), cloth);
            k.Box(new Vector3(0, top - 0.42f * Tall, z + 0.01f), new Vector3(2 * bw + 0.01f, 0.12f * Tall, 0.035f), charge);
            // Points hanging from its foot, like a war-banner's tails.
            foreach (float x in new[] { -bw * 0.66f, 0, bw * 0.66f })
                k.Frustum(new Vector3(x, low - 0.2f * Tall, z), 0.002f, bw * 0.34f, 0.2f * Tall, 4, cloth);
            k.Pop();
        }
        _set.Mesh = k.Build();
    }

    private void PlaceHost(IntroDef d)
    {
        _figures.Clear();
        var skin = d.Skin != "" ? new Color(d.Skin) : new Color("7a8a5a");
        var cloth = d.Cloth != "" ? new Color(d.Cloth) : new Color(d.Field);
        // No ring at the chief's feet: that marks a foe on the map.
        var chief = LookOf(d.Host, true, cloth, skin) with { Legend = null };
        var rank = LookOf(d.Host, false, cloth, skin) with { Legend = null };
        // The chief at the front, the rest in a loose arc behind, turned a little towards the middle.
        _figures.Add((chief, Vector3.Zero, 0, FigureScale * (float)d.Chief, Heroes.Act.Cheer, 0));
        int n = Math.Max(0, d.Count - 1);
        for (int i = 0; i < n; i++)
        {
            float t = n == 1 ? 0 : (float)i / (n - 1) * 2 - 1;
            float row = i % 2 == 0 ? 1 : 1.6f;
            var at = new Vector3(t * 1.25f * Tall * (row > 1 ? 1.15f : 1), 0, -row * 0.62f * Tall + Rand(i * 3) * 0.2f);
            _figures.Add((rank, at, -t * 0.35f + (Rand(i * 7) - 0.5f) * 0.3f, FigureScale * (0.92f + Rand(i * 11) * 0.12f), Heroes.Act.Idle, Rand(i * 5)));
        }
    }

    /// <summary>A figure of the host: the foes' looks for menaces, and plainer folk for peoples and powers.</summary>
    private Heroes.Look LookOf(string host, bool chief, Color cloth, Color skin)
    {
        var trim = Heroes.Heraldic(cloth);
        Heroes.Look Folk(Frame f, Head h, Weapon w, OffHand o, Color? glow = null) =>
            new(f, h, w, o, chief, skin, trim, glow, 1, chief && glow != null, null, chief ? 1 : 0, Heroes.Act.Idle, glow ?? Colors.White, Trinket.None, null, null, null);
        return host switch
        {
            "folk" => chief ? Folk(Frame.Mail, Head.Circlet, Weapon.Sword, OffHand.Kite) : Folk(Frame.Leather, Head.Coif, Weapon.Spear, OffHand.None),
            "knights" => chief ? Folk(Frame.Plate, Head.WingedHelm, Weapon.Greatsword, OffHand.None, new Color("ffe08a")) : Folk(Frame.Plate, Head.GreatHelm, Weapon.Sword, OffHand.Kite),
            "mages" => chief ? Folk(Frame.Robe, Head.WizardHat, Weapon.Staff, OffHand.Orb, new Color("ff5a5a")) : Folk(Frame.Robe, Head.Hood, Weapon.Staff, OffHand.Book),
            "witches" => chief ? Folk(Frame.Robe, Head.WitchHat, Weapon.Staff, OffHand.Lantern, new Color("8aff7a")) : Folk(Frame.Robe, Head.WitchHat, Weapon.Sickle, OffHand.None),
            "merchants" => chief ? Folk(Frame.Leather, Head.Feathered, Weapon.Rapier, OffHand.Book) : Folk(Frame.Leather, Head.Hood, Weapon.Dagger, OffHand.Lantern),
            "druids" => chief ? Folk(Frame.Robe, Head.Antlers, Weapon.Staff, OffHand.None, new Color("8aff5a")) : Folk(Frame.Leather, Head.Hood, Weapon.Bow, OffHand.None),
            "priests" => chief ? Folk(Frame.Robe, Head.Mitre, Weapon.Mace, OffHand.Book, new Color("ffe08a")) : Folk(Frame.Mail, Head.Coif, Weapon.Mace, OffHand.Round),
            "dwarves" => chief ? Folk(Frame.Plate, Head.HornedHelm, Weapon.Hammer, OffHand.Round) : Folk(Frame.Mail, Head.HornedHelm, Weapon.Axe, OffHand.Round),
            _ => _heroes.FoeLook(host, chief, cloth, skin),
        };
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _now += (float)delta;
        // The camera eases in and drifts round a little, low, looking up at the chief.
        float ease = 1 - Mathf.Exp(-_now * 0.35f);
        float drift = Mathf.Sin(_now * 0.12f) * 0.12f;
        float dist = Mathf.Lerp(3.5f, 2.9f, ease) * Tall;
        var eye = new Vector3(Mathf.Sin(drift) * dist, 0.38f * Tall, Mathf.Cos(drift) * dist);
        _cam.LookAtFromPosition(eye, new Vector3(0, 0.95f * Tall, -0.5f * Tall), Vector3.Up);
        foreach (var (look, at, yaw, scale, act, phase) in _figures)
        {
            float u = act == Heroes.Act.Cheer ? _now * 0.22f + phase : _now * 0.3f + phase;
            _heroes.PutLook(look, new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(Vector3.One * scale), at), act, u);
        }
        _heroes.Flush(_now);
        _air.QueueRedraw();
    }

    /// <summary>The painted sky: overhead fading to the horizon's glow, a low sun or moon haloed in the rim light, and two bands of hills in the haze.</summary>
    private sealed partial class Backdrop(Vignette v) : Control
    {
        public override void _Draw()
        {
            var size = Size;
            var sc = v._scene;
            Color sky = new(sc.Sky), horizon = new(sc.Horizon), rim = new(sc.Rim), light = new(sc.Light);
            const int Bands = 40;
            float line = size.Y * 0.62f;
            for (int i = 0; i < Bands; i++)
            {
                float f = (float)i / Bands;
                DrawRect(new Rect2(0, f * line, size.X, line / Bands + 1), sky.Lerp(horizon, f * f));
            }
            DrawRect(new Rect2(0, line, size.X, size.Y - line), horizon);
            // The sun (or moon) low on one side, by lot from the faction.
            var sun = new Vector2(size.X * (v.Rand(1) < 0.5f ? 0.2f + v.Rand(2) * 0.12f : 0.68f + v.Rand(2) * 0.12f), line * (0.45f + v.Rand(3) * 0.2f));
            for (int i = 8; i >= 1; i--) DrawCircle(sun, 10 + i * 9, new Color(rim.Lerp(light, 0.3f), 0.05f));
            DrawCircle(sun, 18, light.Lerp(Colors.White, 0.35f));
            // Two ranges of hills, the far one paler in the haze.
            var ground = new Color(sc.Ground2);
            for (int layer = 0; layer < 2; layer++)
            {
                var c = horizon.Lerp(ground, layer == 0 ? 0.35f : 0.6f).Darkened(layer * 0.1f);
                float baseY = line + (layer == 0 ? -size.Y * 0.04f : size.Y * 0.03f), amp = size.Y * (layer == 0 ? 0.12f : 0.07f);
                var pts = new List<Vector2> { new(0, size.Y) };
                for (int x = 0; x <= 32; x++)
                {
                    float fx = x / 32f, ph = v._seed % 97 + layer * 13;
                    float h = Mathf.Sin(fx * 7 + ph) * 0.5f + Mathf.Sin(fx * 17 + ph * 2) * 0.25f + Mathf.Sin(fx * 3 + ph) * 0.4f;
                    pts.Add(new Vector2(fx * size.X, baseY - amp * (0.6f + h * 0.5f)));
                }
                pts.Add(new Vector2(size.X, size.Y));
                DrawColoredPolygon(pts.ToArray(), c);
            }
        }
    }

    /// <summary>The plate's dark edges and what drifts in the air: embers rising, snow and ash falling, spores and mist.</summary>
    private sealed partial class Air(Vignette v) : Control
    {
        public override void _Draw()
        {
            var size = Size;
            float t = v._now;
            string air = v._scene.Air;
            var rim = new Color(v._scene.Rim);
            if (air != "")
                for (int i = 0; i < 60; i++)
                {
                    float x0 = Geo.Hash(i * 17 + 3), y0 = Geo.Hash(i * 31 + 7), sp = 0.4f + Geo.Hash(i * 13 + 1);
                    float x, y, r;
                    Color c;
                    switch (air)
                    {
                        case "embers":
                            y = 1 - Mathf.PosMod(y0 + t * 0.05f * sp, 1);
                            x = x0 + Mathf.Sin(t * 0.8f + i) * 0.01f;
                            r = 1.2f + sp;
                            c = new Color(rim, 0.5f + 0.5f * Mathf.Sin(t * 3 + i));
                            break;
                        case "snow" or "ash":
                            y = Mathf.PosMod(y0 + t * 0.04f * sp, 1);
                            x = x0 + Mathf.Sin(t * 0.5f + i) * 0.02f;
                            r = 1 + sp;
                            c = air == "snow" ? new Color(1, 1, 1, 0.7f) : new Color(0.55f, 0.5f, 0.48f, 0.6f);
                            break;
                        default:
                            // Spores and mist hang and wander.
                            x = Mathf.PosMod(x0 + t * 0.01f * sp, 1);
                            y = 0.35f + y0 * 0.6f + Mathf.Sin(t * 0.4f + i) * 0.02f;
                            r = air == "mist" ? 30 + 40 * sp : 1.3f + sp;
                            c = air == "mist" ? new Color(new Color(v._scene.Horizon), 0.05f) : new Color(rim, 0.55f);
                            break;
                    }
                    DrawCircle(new Vector2(x * size.X, y * size.Y), r, c);
                }
            // The plate's edges darken, like a picture in an old book.
            var dark = new Color("120d09");
            for (int i = 0; i < 18; i++)
            {
                float f = i / 18f, a = 0.5f * (1 - f) * (1 - f);
                DrawRect(new Rect2(0, f * 26, size.X, 2), new Color(dark, a));
                DrawRect(new Rect2(0, size.Y - f * 44 - 3, size.X, 3), new Color(dark, a * 1.4f));
                DrawRect(new Rect2(f * 40, 0, 3, size.Y), new Color(dark, a));
                DrawRect(new Rect2(size.X - f * 40 - 3, 0, 3, size.Y), new Color(dark, a));
            }
        }
    }
}
