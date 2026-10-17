using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

public sealed record Selection(string Kind, int Id = 0, string SiteId = "")
{
    public static Selection Agent(int id) => new("agent", id);
    public static Selection Building(int id) => new("building", id);
    public static Selection Site(string id) => new("site", 0, id);
}

/// <summary>
/// The town map. World layers are Node2Ds under one camera transform, drawn in world tiles, and
/// each redraws only when its content changes; text is drawn on top in screen space so it stays
/// crisp at any zoom. Pure presentation: it reads the simulation and never changes it.
/// </summary>
public partial class MapView : Control
{
    public Simulation Sim { get; private set; } = null!;
    public Geo Geo { get; private set; } = null!;
    public Fog Fog { get; } = new();
    public Selection? Selection { get; set; }
    public event Action<Selection?>? Picked;

    /// <summary>Fraction of the current hour already elapsed, for smooth marching between steps.</summary>
    public float HourFraction { get; set; }

    private readonly Node2D _world = new();
    private TerrainLayer _terrain = null!;
    private TownLayer _town = null!;
    private AgentLayer _agents = null!;
    private NightLayer _night = null!;
    private FogLayer _fog = null!;
    private OverlayLayer _overlay = null!;
    private LabelLayer _labels = null!;

    // Camera in world tiles. It frames the town and its sites on its own until the player zooms or pans.
    private Vector2 _cam = Geo.Center;
    private float _span = 14;
    private bool _manual;
    private float _manualSpan = 14;
    private int _lastTick = -1;
    private bool _first = true;

    public float Zoom => Size.X / _span;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_world);
        _terrain = new TerrainLayer(this);
        _town = new TownLayer(this);
        _agents = new AgentLayer(this);
        _night = new NightLayer(this);
        _fog = new FogLayer(this);
        _overlay = new OverlayLayer(this);
        foreach (var l in new Node2D[] { _terrain, _town, _agents, _night, _fog, _overlay }) _world.AddChild(l);
        _labels = new LabelLayer(this) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_labels);
        _labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public void Bind(Simulation sim)
    {
        Sim = sim;
        Geo = new Geo(sim.Town);
        Selection = null;
        _first = true;
        _manual = false;
        _lastTick = -1;
        _agents.Reset();
        _terrain.QueueRedraw();
    }

    public void Recenter() => _manual = false;

    public override void _Process(double delta)
    {
        if (Sim == null) return;
        var town = Sim.Town;
        float dt = (float)delta;
        float ease = _first ? 1 : 1 - Mathf.Exp(-dt * 1.5f);
        if (Geo.PlaceSites(town, ease)) _terrain.QueueRedraw();
        if (Fog.Refresh(town, Geo)) _fog.QueueRedraw();
        UpdateCamera(ease);
        _first = false;
        if (town.Tick != _lastTick)
        {
            _lastTick = town.Tick;
            _town.QueueRedraw();
        }
        _agents.Advance(dt);
        _agents.QueueRedraw();
        _night.QueueRedraw();
        _overlay.QueueRedraw();
        _labels.QueueRedraw();
    }

    private void UpdateCamera(float ease)
    {
        if (Size.X < 1 || Size.Y < 1) return;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in Geo.Pins.Values)
        {
            x0 = Mathf.Min(x0, p.X - 2.4f);
            x1 = Mathf.Max(x1, p.X + 2.4f);
            y0 = Mathf.Min(y0, p.Y - 1.4f);
            y1 = Mathf.Max(y1, p.Y + 2.8f);
        }
        float aspect = Size.X / Size.Y;
        if (!_manual)
        {
            var target = new Vector2((x0 + x1) / 2, (y0 + y1) / 2);
            float span = Mathf.Max(x1 - x0, (y1 - y0) * aspect);
            _cam += (target - _cam) * ease;
            _span += (span - _span) * ease;
        }
        else _span += (_manualSpan - _span) * Mathf.Min(1, ease * 6);
        _world.Scale = new Vector2(Zoom, Zoom);
        _world.Position = Size / 2 - _cam * Zoom;
    }

    public Vector2 ToWorld(Vector2 screen) => (screen - _world.Position) / Zoom;
    public Vector2 ToScreen(Vector2 world) => _world.Position + world * Zoom;

    private Vector2 _pressAt;
    private bool _dragging;

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && mb.Pressed)
            {
                if (!_manual) _manualSpan = _span;
                _manual = true;
                var before = ToWorld(mb.Position);
                _manualSpan = Mathf.Clamp(_manualSpan * (mb.ButtonIndex == MouseButton.WheelUp ? 0.85f : 1.18f), 5, 60);
                _span = _manualSpan;
                // Keep the point under the cursor still.
                float s = Size.X / _span;
                _cam = before - (mb.Position - Size / 2) / s;
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _pressAt = mb.Position;
                    _dragging = false;
                }
                else if (!_dragging)
                {
                    Selection = Pick(ToWorld(mb.Position));
                    Picked?.Invoke(Selection);
                }
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed && mb.DoubleClick) Recenter();
        }
        else if (e is InputEventMouseMotion mm && (mm.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Right | MouseButtonMask.Middle)) != 0)
        {
            if (!_dragging && mm.Position.DistanceTo(_pressAt) < 5 && (mm.ButtonMask & MouseButtonMask.Left) != 0) return;
            _dragging = true;
            if (!_manual) _manualSpan = _span;
            _manual = true;
            _cam -= mm.Relative / Zoom;
            AcceptEvent();
        }
    }

    public Selection? Pick(Vector2 w)
    {
        var town = Sim.Town;
        int best = -1;
        float bestD = 0.3f;
        foreach (var (id, s) in _agents.Sprites)
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
        foreach (var (id, p) in Geo.Pins) if (p.DistanceTo(w) < 0.8f) return Selection.Site(id);
        int gx = Mathf.FloorToInt(w.X - Geo.Margin), gy = Mathf.FloorToInt(w.Y - Geo.Margin);
        if (gx >= 0 && gy >= 0 && gx < Geo.Grid && gy < Geo.Grid)
        {
            var lot = town.LotAt(gx, gy);
            if (lot.BuildingId is int b) return Selection.Building(b);
        }
        return null;
    }

    public AgentLayer Agents => _agents;
}

/// <summary>Shared drawing helpers for the world layers.</summary>
public abstract partial class Layer : Node2D
{
    protected readonly MapView Map;
    protected Town Town => Map.Sim.Town;

    protected Layer(MapView map) => Map = map;

    protected void Ellipse(Vector2 c, float rx, float ry, Color color, int points = 18)
    {
        var pts = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.Tau / points;
            pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        DrawColoredPolygon(pts, color);
    }

    protected void Circle(Vector2 c, float r, Color color) => Ellipse(c, r, r, color, r > 0.3f ? 28 : 14);

    protected void Ring(Vector2 c, float r, Color color, float width) => DrawArc(c, r, 0, Mathf.Tau, 40, color, width, true);

    protected void Poly(Color color, params Vector2[] pts) => DrawColoredPolygon(pts, color);

    protected void Rect(float x, float y, float w, float h, Color color) => DrawRect(new Rect2(x, y, w, h), color);
}
