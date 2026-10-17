using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The corner map: the known world from above, as the fog of war has it (green where the town sees
/// now, sepia where reports describe, dark where nobody knows), with the sites, markers, parties and
/// the camera's view. Click or drag to look somewhere. It frames what the town knows, widening as
/// that grows, so a small early world isn't a speck in a sea of dark.
/// </summary>
public partial class Minimap(MapView map) : Control
{
    private const int Px = 160;
    private readonly Image _image = Image.CreateEmpty(Px, Px, false, Image.Format.Rgba8);
    private ImageTexture? _texture;
    private int _version = -1;
    private float _radius = 160, _builtRadius;
    private Vector2[] _frame = [];
    private int _frameTick;

    private static readonly Color Unknown = new("26221c"), Sepia = new("b59f74"), Live = new("6f8a4a"), Water = new("4f7a8a");

    private Vector2 ToMini(Vector2 w) => (w - Geo.Center) / (_radius * 2) * Size + Size / 2;

    private Vector2 ToWorld(Vector2 p) => Geo.Center + (p - Size / 2) / Size * (_radius * 2);

    public override void _Process(double delta)
    {
        if (map.Sim == null || !IsVisibleInTree()) return;
        var town = map.Sim.Town;
        // Frame everything the town has heard of, with a margin, in steps so the view doesn't creep.
        float extent = 120;
        foreach (var site in town.Sites)
            if (site.Known != SiteKnowledge.Unheard && map.Geo.Pins.TryGetValue(site.Def.Id, out var pin)) extent = Mathf.Max(extent, pin.DistanceTo(Geo.Center) + 24);
        _radius = Mathf.Min(Geo.World / 2f, Mathf.Ceil(extent / 40) * 40);
        if (map.Fog.Version != _version || _radius != _builtRadius) Rebuild();
        // The camera's footprint is a ray-march per corner; a few times a second is plenty.
        if (++_frameTick % 6 == 0)
        {
            var s = map.Size;
            _frame = [map.ToWorld(Vector2.Zero), map.ToWorld(new Vector2(s.X, 0)), map.ToWorld(s), map.ToWorld(new Vector2(0, s.Y))];
        }
        QueueRedraw();
    }

    private void Rebuild()
    {
        _version = map.Fog.Version;
        _builtRadius = _radius;
        var fog = map.Fog;
        for (int y = 0; y < Px; y++)
            for (int x = 0; x < Px; x++)
            {
                var w = Geo.Center + new Vector2((x + 0.5f) / Px - 0.5f, (y + 0.5f) / Px - 0.5f) * (_radius * 2);
                float a = fog.AlphaAt(w);
                Color c;
                if (a >= Fog.UnknownAlpha - 0.01f) c = Unknown;
                else
                {
                    bool wet = map.Gen.HeightAt(w.X, w.Y) < 0.2f;
                    var land = wet ? Water : Live.Lerp(Sepia, Mathf.Clamp(a / 0.6f, 0, 1));
                    c = land.Lerp(Unknown, Mathf.Clamp((a - 0.6f) / (Fog.UnknownAlpha - 0.6f), 0, 1));
                }
                _image.SetPixel(x, y, c);
            }
        if (_texture == null) _texture = ImageTexture.CreateFromImage(_image);
        else _texture.Update(_image);
    }

    public override void _Draw()
    {
        if (map.Sim == null || _texture == null) return;
        var town = map.Sim.Town;
        DrawTextureRect(_texture, new Rect2(Vector2.Zero, Size), false);
        foreach (var site in town.Sites)
        {
            if (site.Known == SiteKnowledge.Unheard || !map.Geo.Pins.TryGetValue(site.Def.Id, out var pin)) continue;
            var p = ToMini(pin);
            bool bounty = town.Bounties.Any(b => b.SiteId == site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
            bool seen = map.Fog.SightOf(site.Def.Id, pin) != Sight.Rumoured;
            DrawCircle(p, 3.2f, new Color(0, 0, 0, 0.6f));
            DrawCircle(p, 2.4f, bounty ? Ui.Gold : site.Cleared && seen ? Ui.Good : seen ? Ui.Ink : Ui.Ink2);
        }
        foreach (var m in map.Markers)
        {
            var p = ToMini(m.P);
            DrawColoredPolygon([p, p + new Vector2(-4, -7), p + new Vector2(4, -7)], Overlay.MarkerColor(m.Kind));
        }
        var c = ToMini(Geo.Center);
        DrawRect(new Rect2(c - new Vector2(3, 3), new Vector2(6, 6)), Ui.Accent);
        if (_frame.Length == 4)
        {
            var pts = _frame.Select(ToMini).Append(ToMini(_frame[0])).ToArray();
            DrawPolyline(pts, new Color(1, 0.97f, 0.88f, 0.85f), 1.5f, true);
        }
        DrawRect(new Rect2(Vector2.Zero, Size), Ui.Line, false, 2);
    }

    public override void _GuiInput(InputEvent e)
    {
        bool press = e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true };
        bool drag = e is InputEventMouseMotion mm && (mm.ButtonMask & MouseButtonMask.Left) != 0;
        if (!press && !drag) return;
        var at = ((InputEventMouse)e).Position;
        map.LookAt(ToWorld(at));
        AcceptEvent();
    }
}
