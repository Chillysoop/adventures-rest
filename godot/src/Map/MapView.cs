using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

public sealed record Selection(string Kind, int Id = 0, string SiteId = "")
{
    public static Selection Agent(int id) => new("agent", id);
    public static Selection Building(int id) => new("building", id);
    public static Selection Site(string id) => new("site", 0, id);

    /// <summary>A section of the outer wall (0 is the gate), one of its towers, or a guard flag (by its order's id).</summary>
    public static Selection Wall(int section) => new("wall", section);
    public static Selection Tower(int index) => new("tower", index);
    public static Selection Flag(int postId) => new("flag", postId);

    /// <summary>An enemy's agent loitering in town (<see cref="Sim.Systems.Lurkers"/>).</summary>
    public static Selection Lurker(int id) => new("lurker", id);
}

/// <summary>
/// The map: one live 3D world under a Northgard-style camera that zooms continuously from street
/// level to the whole 1024-tile map, with icons and names drawn on top in screen space so they
/// stay readable at any zoom. M toggles the strategic view, the same live map drawn as an inked
/// campaign map where markers can be placed. Pure presentation: it reads the simulation and never
/// changes it.
/// </summary>
public partial class MapView : Control
{
    public Simulation Sim { get; private set; } = null!;
    public Geo Geo { get; private set; } = null!;
    public WorldGen Gen { get; private set; } = null!;
    public Fog Fog { get; } = new();
    public Selection? Selection { get; set; }
    public event Action<Selection?>? Picked;

    /// <summary>Fraction of the current hour already elapsed, for smooth marching between steps.</summary>
    public float HourFraction { get; set; }

    /// <summary>Torches and burning roofs while a siege plays out (0 to 1), which lift the dark a little (<see cref="Crowd"/>).</summary>
    public float Firelight { get; set; }
    private float _fire;
    /// <summary>Game hours per real second (0 while paused); sets how briskly figures walk.</summary>
    public float TimeScale { get; set; } = 1;

    /// <summary>Extra trees per forest tile, for stress-testing a denser late-game map (--trees).</summary>
    public float TreeScale { get; set; } = 1;

    public readonly List<Marker> Markers = [];
    public MarkerKind? Placing { get; set; }

    /// <summary>The marker under the cursor in the strategic view, drawn raised.</summary>
    public Marker? HoverMarker { get; private set; }

    /// <summary>A marker was clicked or just placed, so the player can name or change it.</summary>
    public event Action<Marker>? MarkerOpened;

    /// <summary>The left button went down on the map; the marker editor closes on it.</summary>
    public event Action? Pressed;

    /// <summary>The town plan; while it has a tool, left clicks and drags zone or place instead of panning.</summary>
    public Planner Planner { get; private set; } = null!;

    /// <summary>Decorations in hand; while it has a piece, left clicks and drags place it instead of panning.</summary>
    public Decorator Decorator { get; private set; } = null!;
    public DecorView Decor => _decor;
    public TownView TownView => _town;

    /// <summary>The guard flag is in hand: a left click plants it there.</summary>
    public bool Flagging { get; set; }

    /// <summary>The player clicked the map with the guard flag in hand.</summary>
    public event Action<Vector2>? FlagDropped;

    /// <summary>The scout's mark is in hand: a left click, in either view, marks that spot for a company to look at.</summary>
    public bool Scouting { get; set; }

    /// <summary>The player clicked the map with the scout's mark in hand.</summary>
    public event Action<Vector2>? SpotMarked;
    public bool Strategic { get; private set; }
    private float _strategic;

    private SubViewportContainer _box = null!;
    private SubViewport _vp = null!;
    private Camera3D _camera = null!;
    private DirectionalLight3D _sun = null!;
    private Godot.Environment _env = null!;
    private WorldView _world = null!;
    private TownView _town = null!;
    private Crowd _crowd = null!;
    private LurkerView _lurkers = null!;
    private DecorView _decor = null!;
    private Overlay _overlay = null!;
    private ShaderMaterial _grade = null!;

    // Camera: a target on the ground, a distance along the view ray, and a heading in 90-degree steps.
    private Vector2 _target = Geo.Center;
    private float _ground = WorldGen.TownHeight;
    private float _dist = 60, _goalDist = 60;
    private float _yaw, _goalYaw;
    private bool _manual;
    private int _lastTick = -1;
    private bool _first = true;
    private string _stateKey = "";

    public const float MinDist = 7, MaxDist = 1500;

    public Camera3D Camera => _camera;
    public float Distance => _dist;
    public AgentPositions Agents => _crowd.People;
    public Crowd Crowd => _crowd;

    /// <summary>The little d20s over people's heads when they make a check.</summary>
    public TownRolls Rolls { get; private set; } = null!;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        _grade = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/grade.gdshader") };
        _box = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore, Material = _grade };
        AddChild(_box);
        _box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _vp = new SubViewport
        {
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa2X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            PositionalShadowAtlasSize = 0,
        };
        _box.AddChild(_vp);

        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("c4cfd2"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("aab8c6"),
            AmbientLightEnergy = 0.45f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 1.05f,
            FogEnabled = true,
            FogLightColor = new Color("c4cfd2"),
            FogDensity = 0.002f,
        };
        _vp.AddChild(new WorldEnvironment { Environment = _env });
        _sun = new DirectionalLight3D
        {
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            ShadowBlur = 1.5f,
            LightColor = new Color("fff0d8"),
            LightEnergy = 1.2f,
        };
        _vp.AddChild(_sun);
        _camera = new Camera3D { Fov = 38, Near = 0.5f, Far = 5000 };
        _vp.AddChild(_camera);
        _world = new WorldView();
        _vp.AddChild(_world);
        _town = new TownView(this);
        _vp.AddChild(_town);
        _decor = new DecorView(this);
        _vp.AddChild(_decor);
        _crowd = new Crowd(this);
        _vp.AddChild(_crowd);
        _lurkers = new LurkerView(this);
        _vp.AddChild(_lurkers);
        _overlay = new Overlay(this) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Rolls = new TownRolls(this) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(Rolls);
        Rolls.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Planner = new Planner(this) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(Planner);
        Planner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Paint and placements show at once, even while the game is paused.
        Planner.Changed += () =>
        {
            _town.Planning(Planner.Active);
            if (Sim != null) _town.Refresh();
        };
        Decorator = new Decorator(this) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(Decorator);
        Decorator.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Lens = new Lens(this) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(Lens);
        Lens.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>The info lens over the town (L): greys the map and colours one thing about it.</summary>
    public Lens Lens { get; private set; } = null!;

    private float _lens;

    public void Bind(Simulation sim)
    {
        Sim = sim;
        Geo = new Geo(sim.Town);
        Gen = new WorldGen(sim.Options.Seed, Geo, sim.Town, TreeScale);
        _world.Build(Gen, TreeScale);
        _town.Reset();
        _decor.Reset();
        _crowd.Reset();
        Markers.Clear();
        HoverMarker = null;
        _grab = null;
        Selection = null;
        _first = true;
        _manual = false;
        _lastTick = -1;
        _stateKey = "";
        _target = Geo.Center;
        _yaw = _goalYaw = 0;
        Mats.SetAll("fog_tex", Fog.Texture);
    }

    public void Recenter() => _manual = false;

    public void ToggleStrategic() => Strategic = !Strategic;

    public void Rotate(int dir) => _goalYaw += dir * Mathf.Pi / 2;

    /// <summary>Turn the camera by quarter turns until it stands nearest the given side of what it looks at.</summary>
    public void Face(Vector2 side)
    {
        float want = Mathf.Round(Mathf.Atan2(side.X, side.Y) / (Mathf.Pi / 2)) * (Mathf.Pi / 2);
        _goalYaw += Mathf.Wrap(want - _goalYaw, -Mathf.Pi, Mathf.Pi);
    }

    /// <summary>Fly the camera to a world point, as the Ledger's links and the minimap do.</summary>
    /// <summary>Pull the camera back (factor above 1) or in, and turn it by yaw radians, keeping its target.</summary>
    public void Drift(float factor, float yaw)
    {
        _goalDist = Mathf.Clamp(_goalDist * factor, MinDist, MaxDist);
        _yaw += yaw;
        _goalYaw += yaw;
    }

    public void LookAt(Vector2 p, float? dist = null)
    {
        _manual = true;
        _target = p;
        if (dist is float d) _goalDist = d;
    }

    /// <summary>Client CPU spent on the map this frame (process and overlay drawing), in milliseconds.</summary>
    public static double CpuMs;

    public static void Time(Action work)
    {
        long t = System.Diagnostics.Stopwatch.GetTimestamp();
        work();
        CpuMs += System.Diagnostics.Stopwatch.GetElapsedTime(t).TotalMilliseconds;
    }

    public override void _Process(double delta) => Time(() => Advance(delta));

    private void Advance(double delta)
    {
        if (Sim == null) return;
        var town = Sim.Town;
        float dt = (float)delta;
        float ease = _first ? 1 : 1 - Mathf.Exp(-dt * 1.5f);
        Geo.PlaceSites(town, ease);
        Fog.Refresh(town, Geo);
        UpdateStates(town);
        UpdateCamera(dt);
        _first = false;
        if (town.Tick != _lastTick)
        {
            _lastTick = town.Tick;
            _town.Refresh();
        }
        _decor.Refresh();
        _crowd.Advance(dt);
        _lurkers.Advance();
        UpdateLight(town);
        _overlay.QueueRedraw();
        if (Planner.Active) Planner.QueueRedraw();
        if (Decorator.Active) Decorator.QueueRedraw();
        _lens += ((Lens.On && !Strategic ? 1 : 0) - _lens) * (1 - Mathf.Exp(-dt * 8));
        _grade.SetShaderParameter("lens", _lens);
        if (Lens.On) Lens.QueueRedraw();
    }

    /// <summary>The town holds the territory it stands in; the enemy's camp corrupts its own and seeps into its neighbours.</summary>
    private void UpdateStates(Town town)
    {
        var states = new Dictionary<int, (float, float)> { [Gen.TownCell] = (1, 0) };
        var en = town.Enemy;
        var camp = town.Sites.FirstOrDefault(s => s.Def.Id == town.Content.Enemy.Outpost.Id);
        if (camp != null && !camp.Cleared && Geo.Pins.TryGetValue(camp.Def.Id, out var pin))
        {
            int cell = Gen.TerritoryAt(pin);
            float creep = Mathf.Clamp((float)en.Strength / 180, 0.15f, 0.7f);
            int cx = cell % WorldGen.Cells, cy = cell / WorldGen.Cells;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = cx + ox, y = cy + oy;
                    if (x < 0 || y < 0 || x >= WorldGen.Cells || y >= WorldGen.Cells) continue;
                    int c = y * WorldGen.Cells + x;
                    if (c == Gen.TownCell) continue;
                    states[c] = (0, c == cell ? 1 : creep * 0.5f);
                }
        }
        string key = string.Join(';', states.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value.Item1:F2}:{k.Value.Item2:F2}"));
        if (key == _stateKey) return;
        _stateKey = key;
        _world.SetStates(states);
    }

    private void UpdateCamera(float dt)
    {
        if (Size.X < 1 || Size.Y < 1) return;
        if (!_manual)
        {
            // Frame the town and the lamplit ground around it.
            _target += (Geo.Center - _target) * (_first ? 1 : 1 - Mathf.Exp(-dt * 2));
            _goalDist = Mathf.Clamp(Fog.LightRadius * 1.1f, 16, 70);
        }
        float k = _first ? 1 : 1 - Mathf.Exp(-dt * 6);
        _dist += (_goalDist - _dist) * k;
        _yaw += (_goalYaw - _yaw) * k;
        _target = _target.Clamp(Vector2.Zero, new Vector2(Geo.World, Geo.World));
        _ground += (Gen.SurfaceAt(_target.X, _target.Y) - _ground) * (_first ? 1 : k);
        // Close in, the camera leans in like Northgard's; pulled out, it flattens toward a map.
        float s = Mathf.InverseLerp(Mathf.Log(MinDist), Mathf.Log(MaxDist), Mathf.Log(_dist));
        float pitch = Mathf.DegToRad(Mathf.Lerp(50, 72, s));
        var dir = new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(_yaw) * Mathf.Cos(pitch));
        var focus = new Vector3(_target.X, _ground, _target.Y);
        _camera.Position = focus + dir * _dist;
        _camera.LookAt(focus, Vector3.Up);
        _camera.Far = _dist * 4 + 600;
        _sun.DirectionalShadowMaxDistance = Mathf.Clamp(_dist * 2.2f, 40, 260);
        _sun.ShadowEnabled = _dist < 400;
        _env.FogDensity = 0.35f / (_dist + 120);
        float far = Mathf.SmoothStep(220, 650, _dist);
        _strategic += ((Strategic ? 1 : 0) - _strategic) * (1 - Mathf.Exp(-dt * 5));
        if (Mathf.Abs(_strategic - (Strategic ? 1 : 0)) < 0.002f) _strategic = Strategic ? 1 : 0;
        Mats.Terrain.SetShaderParameter("far", far);
        Mats.Terrain.SetShaderParameter("border_width", Mathf.Clamp(_dist * 0.006f, 0.3f, 6f));
        Mats.SetAll("strategic", _strategic);
        _grade.SetShaderParameter("strategic", _strategic);
    }

    private void UpdateLight(Town town)
    {
        _fire += (Firelight - _fire) * 0.05f;
        float night = Geo.Nightness(town, HourFraction) * (1 - 0.6f * _fire);
        float dusk = Mathf.Clamp(1 - Mathf.Abs(night - 0.45f) * 2.5f, 0, 1);
        Mats.SetAll("night", night);
        float hour = (town.Tick % SimTime.HoursPerDay) + HourFraction;
        // The sun swings from east to west across the day; the moon is a cool, dim light from above.
        float arc = Mathf.Clamp((hour - 6) / 14f, 0, 1);
        var dir = new Vector3(Mathf.Lerp(0.9f, -0.9f, arc) * 0.6f + 0.35f, -1.25f + night * 0.3f, 0.75f).Normalized();
        _sun.Basis = Basis.LookingAt(dir, Vector3.Up);
        var day = new Color("fff0d8");
        var evening = new Color("ffb878");
        var moon = new Color("8fa8e8");
        _sun.LightColor = day.Lerp(evening, dusk).Lerp(moon, night);
        _sun.LightEnergy = Mathf.Lerp(1.45f, 0.32f, night);
        _env.AmbientLightColor = new Color("a4b4c6").Lerp(new Color("4a5a88"), night).Lerp(new Color("c08060"), _fire * night * 0.5f);
        _env.AmbientLightEnergy = Mathf.Lerp(0.48f, 0.4f, night);
        var haze = new Color("c4cfd2").Lerp(new Color("e2b896"), dusk * 0.6f).Lerp(new Color("1c2236"), night);
        _env.BackgroundColor = haze;
        _env.FogLightColor = haze;
        UpdateSeason(town);
        if (town.Outcome == Outcome.Defeat) _env.AmbientLightColor = _env.AmbientLightColor.Lerp(new Color("a03020"), 0.4f);
    }

    private Vector3 _season = new(-1, -1, -1);

    /// <summary>The land shows the season: snow in winter, turned leaves in autumn, fresh green in spring; winter light is colder and greyer.</summary>
    private void UpdateSeason(Town town)
    {
        var look = Seasons.Look(town);
        var s = new Vector3((float)look.Winter, (float)look.Autumn, (float)look.Spring);
        if (!s.IsEqualApprox(_season))
        {
            _season = s;
            Mats.SetAll("snow", s.X);
            Mats.SetAll("autumn", s.Y);
            Mats.SetAll("spring", s.Z);
            _grade.SetShaderParameter("saturation", 0.9f - s.X * 0.12f + s.Z * 0.04f);
            _grade.SetShaderParameter("cold", s.X);
        }
        // The inked campaign map keeps its own light.
        s *= 1 - _strategic;
        _sun.LightColor = _sun.LightColor.Lerp(new Color("e4ecf6"), s.X * 0.6f).Lerp(new Color("ffe2b8"), s.Y * 0.35f);
        _sun.LightEnergy *= 1 - s.X * 0.12f;
        _env.AmbientLightColor = _env.AmbientLightColor.Lerp(new Color("b8c4d4"), s.X * 0.5f);
        var haze = _env.BackgroundColor.Lerp(new Color("cdd3da"), s.X * 0.3f * (1 - Geo.Nightness(town, HourFraction)));
        _env.BackgroundColor = haze;
        _env.FogLightColor = haze;
    }

    // ---------- Projection and picking ----------

    public bool OnScreen(Vector3 w, out Vector2 screen)
    {
        screen = default;
        if (_camera.IsPositionBehind(w)) return false;
        screen = _camera.UnprojectPosition(w);
        return screen.X > -80 && screen.Y > -80 && screen.X < Size.X + 80 && screen.Y < Size.Y + 80;
    }

    public Vector2 ToScreen(Vector2 tile, float lift = 0) => _camera.UnprojectPosition(Gen.At(tile, lift));

    /// <summary>The ground point under a screen position: march the view ray until it dips below the land.</summary>
    public Vector2 ToWorld(Vector2 screen)
    {
        var o = _camera.ProjectRayOrigin(screen);
        var d = _camera.ProjectRayNormal(screen);
        if (d.Y > -0.01f) return _target;
        float t = (o.Y - _ground) / -d.Y;
        for (int i = 0; i < 6; i++)
        {
            var p = o + d * t;
            float h = Gen.SurfaceAt(p.X, p.Z);
            t += (p.Y - h) / -d.Y * 0.8f;
        }
        var hit = o + d * t;
        return new Vector2(hit.X, hit.Z);
    }

    /// <summary>Tiles per screen pixel at the camera's focus, for sizing things that live in the world.</summary>
    public float TilesPerPixel => 2 * _dist * Mathf.Tan(Mathf.DegToRad(_camera.Fov / 2)) / Mathf.Max(1, Size.Y);

    private Vector2 _pressAt;
    private bool _dragging;

    // A marker picked up in the strategic view, and where on it the cursor took hold.
    private Marker? _grab;
    private Vector2 _grabOffset;

    public override void _GuiInput(InputEvent e)
    {
        if (Planner.Active && PlanInput(e)) return;
        if (Decorator.Active && DecorInput(e)) return;
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && mb.Pressed)
            {
                _manual = true;
                var before = ToWorld(mb.Position);
                _goalDist = Mathf.Clamp(_goalDist * (mb.ButtonIndex == MouseButton.WheelUp ? 0.82f : 1.22f), MinDist, MaxDist);
                // Zoom toward the point under the cursor.
                if (mb.ButtonIndex == MouseButton.WheelUp) _target += (before - _target) * 0.18f;
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _pressAt = mb.Position;
                    _dragging = false;
                    Pressed?.Invoke();
                    if (Strategic && Placing == null && !Scouting && MarkerAt(mb.Position) is Marker m)
                    {
                        _grab = m;
                        _grabOffset = ToScreen(m.P) - mb.Position;
                    }
                }
                else if (_grab != null)
                {
                    if (!_dragging) MarkerOpened?.Invoke(_grab);
                    _grab = null;
                }
                else if (!_dragging)
                {
                    var w = ToWorld(mb.Position);
                    if (Scouting) SpotMarked?.Invoke(w);
                    else if (Flagging && !Strategic) FlagDropped?.Invoke(w);
                    else if (Placing is MarkerKind kind)
                    {
                        var m = new Marker(kind, w, Sim.Town.Day);
                        Markers.Add(m);
                        // Shift keeps the tool for a row of markers; otherwise name the one just placed.
                        if (!mb.ShiftPressed)
                        {
                            Placing = null;
                            MarkerOpened?.Invoke(m);
                        }
                    }
                    else
                    {
                        Selection = Pick(w, mb.Position);
                        Picked?.Invoke(Selection);
                    }
                }
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                if (mb.DoubleClick) Recenter();
                else if (Strategic && MarkerAt(mb.Position) is Marker m) Markers.Remove(m);
            }
        }
        else if (e is InputEventMouseMotion hover && hover.ButtonMask == 0)
        {
            HoverMarker = Strategic && Placing == null ? MarkerAt(hover.Position) : null;
            MouseDefaultCursorShape = Placing != null || Flagging || Scouting ? CursorShape.Cross : HoverMarker != null ? CursorShape.Move : CursorShape.Arrow;
        }
        else if (e is InputEventMouseMotion carry && _grab != null && (carry.ButtonMask & MouseButtonMask.Left) != 0)
        {
            if (!_dragging && carry.Position.DistanceTo(_pressAt) < 5) return;
            _dragging = true;
            _grab.P = ToWorld(carry.Position + _grabOffset).Clamp(Vector2.Zero, new Vector2(Geo.World, Geo.World));
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && (mm.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Right | MouseButtonMask.Middle)) != 0)
        {
            if (!_dragging && mm.Position.DistanceTo(_pressAt) < 5 && (mm.ButtonMask & MouseButtonMask.Left) != 0) return;
            _dragging = true;
            _manual = true;
            var a = ToWorld(mm.Position - mm.Relative);
            var b = ToWorld(mm.Position);
            _target -= b - a;
            AcceptEvent();
        }
    }

    /// <summary>Left button plans while a tool is picked (a drag paints, the map no longer pans), right-click puts it down.</summary>
    private bool PlanInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } b:
                if (b.Pressed) Planner.Press(ToWorld(b.Position));
                else Planner.Release(ToWorld(b.Position));
                AcceptEvent();
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true, ShiftPressed: true } w:
                if (!Planner.Wheel(w.ButtonIndex == MouseButton.WheelUp)) return false;
                AcceptEvent();
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true, DoubleClick: false }:
                Planner.Pick(null);
                AcceptEvent();
                return true;
            case InputEventMouseMotion mm when (mm.ButtonMask & (MouseButtonMask.Right | MouseButtonMask.Middle)) == 0:
                Planner.Hover(ToWorld(mm.Position));
                return true;
        }
        return false;
    }

    /// <summary>Left button places the piece in hand (a drag aims, paints or runs a row), Shift+wheel turns it, right-click puts it down.</summary>
    private bool DecorInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } b:
                if (b.Pressed) Decorator.Press(ToWorld(b.Position));
                else Decorator.Release(ToWorld(b.Position));
                AcceptEvent();
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true, ShiftPressed: true } w:
                Decorator.Turn(w.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                AcceptEvent();
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true, DoubleClick: false }:
                Decorator.Pick(null);
                AcceptEvent();
                return true;
            case InputEventMouseMotion mm when (mm.ButtonMask & MouseButtonMask.Left) != 0:
                Decorator.Drag(ToWorld(mm.Position));
                AcceptEvent();
                return true;
            case InputEventMouseMotion mm when (mm.ButtonMask & (MouseButtonMask.Right | MouseButtonMask.Middle)) == 0:
                Decorator.Hover(ToWorld(mm.Position));
                return true;
        }
        return false;
    }

    /// <summary>The marker whose pin is under a screen point, if any; the nearest when they overlap.</summary>
    public Marker? MarkerAt(Vector2 screen)
    {
        Marker? best = null;
        float bestD = 16;
        foreach (var m in Markers)
        {
            if (!OnScreen(Gen.At(m.P), out var at)) continue;
            float d = Mathf.Min(screen.DistanceTo(at - new Vector2(0, 22)), screen.DistanceTo(at - new Vector2(0, 8)));
            if (d <= bestD)
            {
                bestD = d;
                best = m;
            }
        }
        return best;
    }

    /// <summary>Leave the strategic view's marker tools: drop the tool and any marker in hand.</summary>
    public void ClearMarkerTools()
    {
        Placing = null;
        HoverMarker = null;
        _grab = null;
        MouseDefaultCursorShape = CursorShape.Arrow;
    }

    public Selection? Pick(Vector2 w, Vector2 screen)
    {
        var town = Sim.Town;
        // Icons first, in screen space, since they keep their size at any zoom.
        foreach (var site in town.Sites)
        {
            // Only what the town has heard of is on the map, and hearsay only roughly where it is.
            if (site.Known == SiteKnowledge.Unheard || !Geo.Pins.TryGetValue(site.Def.Id, out var p)) continue;
            var spot = Fog.SightOf(site.Def.Id, p) == Sight.Rumoured ? Geo.Rough(site) : p;
            if (ToScreen(spot, 1).DistanceTo(screen) < 26) return Selection.Site(site.Def.Id);
        }
        // An enemy's agent in the crowd comes before the townsfolk round them: finding them is the point.
        foreach (var (id, p) in _lurkers.Spots)
            if (p.DistanceTo(w) < 0.4f) return Selection.Lurker(id);
        int best = -1;
        float bestD = 0.35f;
        foreach (var (id, s) in _crowd.People.Sprites)
        {
            if (!s.Visible) continue;
            float d = s.P.DistanceTo(w);
            if (d < bestD)
            {
                bestD = d;
                best = id;
            }
        }
        if (best >= 0) return Selection.Agent(best);
        // The watch: its flags, a guard (who stands for their post), then the wall itself.
        foreach (var p in town.Garrison.Posts)
            if (p.Kind == PostKind.Flag && new Vector2(Geo.Margin + (float)p.X, Geo.Margin + (float)p.Y).DistanceTo(w) < 0.45f) return Selection.Flag(p.Id);
        foreach (var (id, p) in _crowd.GuardSpots)
        {
            if (p.DistanceTo(w) > 0.25f || town.Garrison.Guards.FirstOrDefault(g => g.Id == id) is not { } guard) continue;
            var post = Garrison.Post(town, guard.PostId);
            return post?.Kind switch
            {
                PostKind.Flag => Selection.Flag(post.Id),
                PostKind.Tower => Selection.Tower(post.Index),
                PostKind.Wall or PostKind.Gate => Selection.Wall(post.Index),
                _ => Garrison.Walled(town) ? Selection.Wall(guard.Beat) : null,
            };
        }
        if (_town.WallAt(town, w) is var (kind, idx)) return kind == "tower" ? Selection.Tower(idx) : Selection.Wall(idx);
        var at = Planner.ToTown(w);
        return Land.BuildingAt(town, at.X, at.Y) is int b ? Selection.Building(b) : null;
    }

    /// <summary>Rendering counters for the frame-time check: draw calls, primitives, objects.</summary>
    public (int Draws, int Primitives, int Objects) Stats()
    {
        var rid = _vp.GetViewportRid();
        return ((int)RenderingServer.ViewportGetRenderInfo(rid, RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.DrawCallsInFrame),
            (int)RenderingServer.ViewportGetRenderInfo(rid, RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.PrimitivesInFrame),
            (int)RenderingServer.ViewportGetRenderInfo(rid, RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.ObjectsInFrame));
    }

    public int TreeInstances => _world.TreeInstances;
}

/// <summary>What a marker says. Saves keep the name, so new kinds go at the end.</summary>
public enum MarkerKind
{
    Attack,
    Defend,
    Scout,
    Danger,
    Note,
    Rally,
    Claim,
}

/// <summary>
/// A mark on the map placed by the player (and later by allies), shown in both views. It belongs to
/// the client, not the simulation, and is kept in the client part of a save.
/// </summary>
public sealed class Marker(MarkerKind kind, Vector2 p, int day, string label = "")
{
    public MarkerKind Kind { get; set; } = kind;
    public Vector2 P { get; set; } = p;
    public int Day { get; } = day;
    public string Label { get; set; } = label;
}
