using CrownAndCompass.Client.Map;
using Godot;
using static CrownAndCompass.Client.Map.HeroKit;

namespace CrownAndCompass.Client;

/// <summary>
/// The council chamber at the head of the Council window: a stone hall with a horseshoe table, a high chair for each
/// seat and each councillor standing at their place, drawn with the heroes' kit
/// (<see cref="Heroes"/>) in their seat's colours. At a glance: a name plate with their loyalty, a raised hand or a turned shoulder on the motion before them, and badges over their heads for what the mayor holds
/// over them. Empty seats stand empty, seats not yet open are sheeted. Click a councillor to pick them.
/// The hall is its own small 3D world; it only renders while the window shows it.
/// </summary>
public partial class Chamber : Control
{
    /// <summary>One seat as the chamber shows it, filled in by the Council window each refresh.</summary>
    public sealed record SeatView(string Id, string Title, string Ability, int? Holder, string Name, string Race,
        double Loyalty, bool Locked, int Opens, bool? Vote, bool Mover, bool Secret, bool Suspect, string Tip);

    /// <summary>A seat was clicked (its index).</summary>
    public Action<int>? Picked;

    public int Selected = -1;

    private const float FigureScale = 3.2f;
    private static readonly Vector3 Centre = new(0, 0, -0.6f);
    private const float SeatRadius = 1.95f, TableRadius = 1.42f, ChairRadius = 2.3f, WallRadius = 3.9f;

    private readonly SubViewport _vp;
    private readonly Camera3D _cam;
    private readonly Heroes _heroes = new();
    private readonly MeshInstance3D _room = new();
    private readonly ShaderMaterial _props;
    private readonly OmniLight3D _spot = new() { LightColor = new Color("ffd890"), LightEnergy = 1.4f, OmniRange = 1.6f, Visible = false };
    private readonly Control _marks;
    private IReadOnlyList<SeatView> _seats = [];
    private string _roomSig = "";
    private float _now;
    private int _hover = -1;
    private readonly List<(Rect2 Box, int Seat)> _hits = [];

    public Chamber()
    {
        CustomMinimumSize = new Vector2(0, 300);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetMeta("keep", true);
        TooltipText = "seat";

        var box = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(box);
        _vp = new SubViewport { OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa2X, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
        box.AddChild(_vp);

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("140f0b"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("b89a80"),
            AmbientLightEnergy = 0.7f,
            GlowEnabled = true,
            GlowIntensity = 0.5f,
            GlowBloom = 0.06f,
            GlowHdrThreshold = 0.9f,
            FogEnabled = true,
            FogLightColor = new Color("1d1610"),
            FogDensity = 0.035f,
        };
        _vp.AddChild(new WorldEnvironment { Environment = env });
        // Light through the high windows, warm, and the braziers' glow either side of the hall.
        _vp.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-52, 24, 0),
            LightColor = new Color("ffe0b8"),
            LightEnergy = 1.05f,
            ShadowEnabled = true,
        });
        // A soft fill from the mayor's side of the table, so faces read.
        _vp.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 180, 0), LightColor = new Color("c8d0ff"), LightEnergy = 0.45f });
        foreach (int side in new[] { -1, 1 })
            _vp.AddChild(new OmniLight3D { Position = new Vector3(side * 2.75f, 1.2f, 0.5f), LightColor = new Color("ff9a48"), LightEnergy = 1.1f, OmniRange = 3.6f });

        _cam = new Camera3D { Fov = 50, Current = true };
        _vp.AddChild(_cam);
        _cam.Position = new Vector3(0, 2.05f, 1.55f);
        _cam.LookAtFromPosition(_cam.Position, new Vector3(0, 0.9f, -2.2f), Vector3.Up);

        var noise = Mats.NoiseTexture();
        var clear = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
        _props = new ShaderMaterial { Shader = Mats.Props.Shader };
        _props.SetShaderParameter("fog_tex", clear);
        _props.SetShaderParameter("noise_tex", noise);
        _props.SetShaderParameter("toon", 1.0f);
        var ink = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink.gdshader") };
        ink.SetShaderParameter("fog_tex", clear);
        ink.SetShaderParameter("noise_tex", noise);
        ink.SetShaderParameter("width", 0.0022f);
        _props.NextPass = ink;
        _room.MaterialOverride = _props;
        _vp.AddChild(_room);
        _heroes.OwnMaterials(noise);
        _vp.AddChild(_heroes);
        _vp.AddChild(_spot);

        _marks = new Marks(this) { MouseFilter = MouseFilterEnum.Ignore };
        _marks.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_marks);
    }

    /// <summary>The seats to show; rebuilds the hall only when a seat opens, empties or fills.</summary>
    public void Show(IReadOnlyList<SeatView> seats)
    {
        _seats = seats;
        string sig = string.Join(",", seats.Select(s => $"{s.Id}:{(s.Locked ? "L" : s.Holder == null ? "E" : "H")}"));
        if (sig != _roomSig)
        {
            _roomSig = sig;
            BuildRoom();
        }
        // A warm light over the councillor picked.
        _spot.Visible = Selected >= 0 && Selected < seats.Count;
        if (_spot.Visible) _spot.Position = SeatAt(Selected, seats.Count, SeatRadius) + new Vector3(0, 1.9f, 0.5f);
        _marks.QueueRedraw();
    }

    // The hall -------------------------------------------------------------------------------------------

    /// <summary>Where seat i of n stands: round the far side of the table, facing the hall.</summary>
    private static Vector3 SeatAt(int i, int n, float radius)
    {
        float spread = n <= 1 ? 0 : Mathf.DegToRad(Mathf.Min(140, 55 * (n - 1)));
        float a = n <= 1 ? 0 : -spread / 2 + spread * i / (n - 1);
        return Round(a, radius);
    }

    /// <summary>A point round the table at angle a: a flattened ring, so the seat at the head isn't sunk at the back of the hall.</summary>
    private static Vector3 Round(float a, float radius) => Centre + new Vector3(Mathf.Sin(a) * radius, 0, -Mathf.Cos(a) * radius * Depth);

    private const float Depth = 0.62f;

    private static float Facing(Vector3 from) => Mathf.Atan2(-from.X, 1.6f - from.Z);

    /// <summary>The colour of a seat's banner and robes, from what it minds.</summary>
    public static Color SeatColour(string id, string ability) => id switch
    {
        "steward" => new Color("d9a441"),
        "speaker" => new Color("4f8fd6"),
        "captain" => new Color("c0463b"),
        _ => ability switch { "str" => new Color("c0463b"), "int" => new Color("d9a441"), "cha" => new Color("4f8fd6"), "wis" => new Color("6fae5a"), _ => new Color("9a6fc4") },
    };

    private static Color Shade(string hex, float jitter, int seed) =>
        new Color(hex).Darkened((Geo.Hash(seed * 7 + 3) - 0.5f) * jitter) with { A = MeshKit.Flag.Plain };

    private void BuildRoom()
    {
        var k = new MeshKit();
        // Flagstones on dark grout, in a circle.
        k.Frustum(new Vector3(0, -0.06f, -0.6f), 4.7f, 4.7f, 0.06f, 40, new Color("2a231d"));
        for (int x = -9; x <= 9; x++)
            for (int z = -9; z <= 7; z++)
            {
                var c = new Vector3(x * 0.5f + (z % 2 == 0 ? 0 : 0.25f), 0, z * 0.5f);
                if ((c - Centre).Length() > 4.5f) continue;
                k.Box(c, new Vector3(0.46f, 0.012f, 0.46f), Shade("6e655a", 0.22f, x * 31 + z * 7));
            }
        // A red runner from the hall's mouth to the dais, and the town's seal in the middle of the floor.
        k.Box(new Vector3(0, 0.012f, 0.4f), new Vector3(0.96f, 0.012f, 4.6f), new Color("7c2420"));
        k.Box(new Vector3(0, 0.014f, 0.4f), new Vector3(0.76f, 0.012f, 4.6f), new Color("8f2d26"));
        k.Frustum(Centre + new Vector3(0, 0.02f, 0), 0.62f, 0.62f, 0.012f, 28, new Color("b8913a"));
        k.Frustum(Centre + new Vector3(0, 0.03f, 0), 0.5f, 0.5f, 0.012f, 28, new Color("6e2420"));
        k.Frustum(Centre + new Vector3(0, 0.04f, 0), 0.18f, 0.18f, 0.012f, 12, new Color("d8b45a"));

        // The back wall: a half-round of stone with pillars, tall windows and a cornice.
        const int Segs = 22;
        float from = Mathf.DegToRad(-104), to = Mathf.DegToRad(104), step = (to - from) / Segs;
        for (int i = 0; i < Segs; i++)
        {
            float a = from + step * (i + 0.5f);
            var p = Centre + new Vector3(Mathf.Sin(a) * WallRadius, 0, -Mathf.Cos(a) * WallRadius);
            float w = 2 * WallRadius * Mathf.Sin(step / 2) + 0.06f;
            k.Push(MeshKit.At(p.X, 0, p.Z, -a));
            k.Box(Vector3.Zero, new Vector3(w, 3.2f, 0.3f), Shade("67615a", 0.12f, i));
            k.Box(new Vector3(0, 0, 0.12f), new Vector3(w, 0.35f, 0.14f), new Color("4a4239"));
            k.Box(new Vector3(0, 3.0f, 0.12f), new Vector3(w, 0.2f, 0.2f), new Color("7a7064"));
            k.Pop();
        }
        for (int i = -3; i <= 3; i++)
        {
            float a = Mathf.DegToRad(i * 30);
            var p = Centre + new Vector3(Mathf.Sin(a) * (WallRadius - 0.24f), 0, -Mathf.Cos(a) * (WallRadius - 0.24f));
            k.Box(p, new Vector3(0.42f, 0.22f, 0.42f), new Color("6f665a"));
            k.Frustum(p + new Vector3(0, 0.22f, 0), 0.15f, 0.13f, 2.6f, 10, new Color("8a8072"));
            k.Box(p + new Vector3(0, 2.8f, 0), new Vector3(0.4f, 0.2f, 0.4f), new Color("6f665a"));
            if (i == 0 || i == 3 || i == -3) continue;
            // A tall leaded window between this pillar and the next one in.
            float wa = Mathf.DegToRad(i * 30 - Mathf.Sign(i) * 15);
            var wp = Centre + new Vector3(Mathf.Sin(wa) * (WallRadius - 0.17f), 0, -Mathf.Cos(wa) * (WallRadius - 0.17f));
            k.Push(MeshKit.At(wp.X, 0, wp.Z, -wa));
            k.Box(new Vector3(0, 1.15f, 0), new Vector3(0.62f, 1.55f, 0.04f), new Color("3b332b"));
            k.Box(new Vector3(0, 1.22f, 0.01f), new Vector3(0.48f, 1.38f, 0.04f), new Color("a8946a"));
            k.Box(new Vector3(0, 1.22f, 0.03f), new Vector3(0.04f, 1.38f, 0.03f), new Color("3b332b"));
            k.Box(new Vector3(0, 1.85f, 0.03f), new Vector3(0.48f, 0.04f, 0.03f), new Color("3b332b"));
            k.Pop();
        }

        // The town's great banner behind the head of the table.
        var banner = Centre + new Vector3(0, 0, -WallRadius + 0.16f);
        k.Box(banner + new Vector3(0, 1.95f, 0), new Vector3(1.05f, 1.0f, 0.03f), new Color("8f2d26"));
        k.Box(banner + new Vector3(0, 1.85f, 0.02f), new Vector3(0.3f, 0.3f, 0.02f), new Color("e2b24a"));
        k.Box(banner + new Vector3(0, 2.92f, 0.02f), new Vector3(1.25f, 0.05f, 0.05f), new Color("c9a24a") with { A = MeshKit.Flag.Metal });

        // Braziers either side of the hall's mouth.
        foreach (int side in new[] { -1, 1 })
        {
            var b = new Vector3(side * 2.75f, 0, 0.5f);
            k.Frustum(b, 0.16f, 0.06f, 0.55f, 8, new Color("2e2925"));
            k.Frustum(b + new Vector3(0, 0.55f, 0), 0.12f, 0.24f, 0.16f, 10, new Color("3b342e"), new Color("2a1810"));
            k.Blob(b + new Vector3(0, 0.76f, 0), new Vector3(0.15f, 0.2f, 0.15f), new Color("ff9a3a") with { A = MeshKit.Flag.Ember }, 0.2f, side);
        }

        // The horseshoe table, open towards the hall.
        const int Table = 16;
        float tf = Mathf.DegToRad(-82), tt = Mathf.DegToRad(82), ts = (tt - tf) / Table;
        for (int i = 0; i < Table; i++)
        {
            float a = tf + ts * (i + 0.5f);
            Vector3 a0 = Round(a - ts / 2, TableRadius), a1 = Round(a + ts / 2, TableRadius), p = (a0 + a1) / 2, along = a1 - a0;
            float w = along.Length() + 0.04f;
            k.Push(MeshKit.At(p.X, 0, p.Z, Mathf.Atan2(along.Z, -along.X) + Mathf.Pi));
            k.Box(new Vector3(0, 0.48f, 0), new Vector3(w, 0.07f, 0.44f), Shade("7a5232", 0.1f, i), new Color("8a5e3a"));
            k.Box(new Vector3(0, 0.08f, 0.18f), new Vector3(w, 0.4f, 0.04f), new Color("5a3a22"));
            if (i % 3 == 0) k.Box(new Vector3(0, 0, 0), new Vector3(0.08f, 0.48f, 0.08f), new Color("4a2c1a"));
            k.Pop();
        }

        // A high chair at each seat; a scroll and a candle at each place laid. Unopened seats are sheeted.
        int n = _seats.Count;
        for (int i = 0; i < n; i++)
        {
            var s = _seats[i];
            var colour = SeatColour(s.Id, s.Ability);
            var c = SeatAt(i, n, ChairRadius);
            float yaw = Facing(c);
            k.Push(MeshKit.At(c.X, 0, c.Z, yaw));
            k.Box(new Vector3(0, 0, 0), new Vector3(0.52f, 0.42f, 0.46f), new Color("4a2c1a"), colour.Darkened(0.2f));
            k.Box(new Vector3(0, 0, -0.22f), new Vector3(0.56f, 1.5f, 0.09f), new Color("4a2c1a"));
            k.Box(new Vector3(0, 0.55f, -0.17f), new Vector3(0.4f, 0.8f, 0.03f), colour.Darkened(0.15f));
            k.Blob(new Vector3(0, 1.55f, -0.22f), new Vector3(0.07f, 0.08f, 0.07f), new Color("e2b24a") with { A = MeshKit.Flag.Metal });
            if (s.Locked)
            {
                // A dust sheet over the chair: nobody sits here yet.
                k.Box(new Vector3(0, 0, 0.02f), new Vector3(0.6f, 0.47f, 0.54f), new Color("9a9284"));
                k.Box(new Vector3(0, 0.3f, -0.2f), new Vector3(0.64f, 1.24f, 0.16f), new Color("a39b8c"));
            }
            k.Pop();
            // The seat's banner on the wall behind.
            var wb = SeatAt(i, n, WallRadius - 0.17f);
            k.Push(MeshKit.At(wb.X, 0, wb.Z, Facing(wb)));
            k.Box(new Vector3(0, 1.62f, 0), new Vector3(0.46f, 1.05f, 0.03f), s.Locked ? new Color("4a443c") : colour.Darkened(0.1f));
            k.Box(new Vector3(0, 1.5f, 0.02f), new Vector3(0.46f, 0.08f, 0.02f), s.Locked ? new Color("5a544c") : new Color("e2c06a"));
            k.Box(new Vector3(0, 2.66f, 0.02f), new Vector3(0.58f, 0.04f, 0.05f), new Color("c9a24a") with { A = MeshKit.Flag.Metal });
            k.Pop();
            if (s.Locked) continue;
            var t = SeatAt(i, n, TableRadius - 0.06f);
            k.Push(MeshKit.At(t.X, 0, t.Z, Facing(t)));
            k.Log(new Vector3(0.12f, 0.55f, 0.02f), 0.22f, 0.025f, new Color("e8dcc0"), new Color("c9b896"));
            k.Frustum(new Vector3(-0.14f, 0.52f, 0.02f), 0.025f, 0.025f, 0.1f, 6, new Color("efe6d2"));
            k.Blob(new Vector3(-0.14f, 0.65f, 0.02f), new Vector3(0.02f, 0.035f, 0.02f), new Color("ffc060") with { A = MeshKit.Flag.Ember });
            k.Pop();
        }
        _room.Mesh = k.Build();
    }

    /// <summary>Loyalty as a colour: green when loyal, amber when wary, red when disloyal (the bands the Council window uses).</summary>
    public static Color Loyal(double loyalty) => loyalty >= 60 ? Ui.Good : loyalty >= 25 ? Ui.Gold : Ui.Bad;

    public static string LoyalIcon(double loyalty) => loyalty >= 60 ? "loyal" : loyalty >= 25 ? "wary" : "disloyal";

    // The people ----------------------------------------------------------------------------------------

    /// <summary>A councillor's look: robes in the seat's colour, a hat or helm for what the seat minds.</summary>
    private static Heroes.Look LookOf(SeatView s)
    {
        var trim = Heroes.Heraldic(SeatColour(s.Id, s.Ability));
        var (frame, head, weapon, off) = (s.Id, s.Ability) switch
        {
            ("captain", _) or (_, "str") => (Frame.Mail, Head.Sallet, Weapon.Sword, OffHand.Kite),
            ("steward", _) or (_, "int") => (Frame.Robe, Head.Feathered, Weapon.None, OffHand.Book),
            ("speaker", _) or (_, "cha") => (Frame.Leather, Head.Coif, Weapon.None, OffHand.Lantern),
            (_, "wis") => (Frame.Robe, Head.Mitre, Weapon.Staff, OffHand.None),
            _ => (Frame.Robe, Head.Hood, Weapon.None, OffHand.Book),
        };
        return new Heroes.Look(frame, head, weapon, off, frame != Frame.Leather, Palette.RaceColor(s.Race), trim, null, 1, false, null,
            frame == Frame.Mail ? 1 : 0, Heroes.Act.Idle, Colors.White, Trinket.None, null, null, null);
    }

    private readonly Dictionary<string, (string Key, Heroes.Look Look)> _looks = [];

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _now += (float)delta;
        int n = _seats.Count;
        for (int i = 0; i < n; i++)
        {
            var s = _seats[i];
            if (s.Holder == null) continue;
            string key = $"{s.Holder}|{s.Race}";
            if (!_looks.TryGetValue(s.Id, out var had) || had.Key != key) _looks[s.Id] = had = (key, LookOf(s));
            var p = SeatAt(i, n, SeatRadius);
            float yaw = Facing(p);
            // Aye: a hand raised. Nay: arms down and a shoulder turned to the mayor.
            var act = s.Vote == true ? Heroes.Act.Cheer : Heroes.Act.Idle;
            if (s.Vote == false) yaw += (p.X >= 0 ? 1 : -1) * 0.6f;
            float u = act == Heroes.Act.Cheer ? _now * 0.25f + i * 0.21f : _now * 0.3f + i * 0.17f;
            _heroes.PutLook(had.Look, new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(Vector3.One * FigureScale), p), act, u);
        }
        _heroes.Flush(_now);
        _marks.QueueRedraw();
    }

    // Picking and the marks over heads ------------------------------------------------------------------

    /// <summary>Where a point in the hall shows on this control.</summary>
    private Vector2 OnScreen(Vector3 p)
    {
        var vs = (Vector2)_vp.Size;
        if (vs.X < 1 || vs.Y < 1) return Vector2.Zero;
        return _cam.UnprojectPosition(p) * (Size / vs);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion m)
        {
            int was = _hover;
            _hover = HitAt(m.Position);
            MouseDefaultCursorShape = _hover >= 0 ? CursorShape.PointingHand : CursorShape.Arrow;
            if (was != _hover) _marks.QueueRedraw();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b && HitAt(b.Position) is var seat and >= 0)
        {
            Picked?.Invoke(seat);
            AcceptEvent();
        }
    }

    private int HitAt(Vector2 at)
    {
        foreach (var (box, seat) in _hits)
            if (box.HasPoint(at)) return seat;
        return -1;
    }

    public override string _GetTooltip(Vector2 at) => HitAt(at) is var i and >= 0 && i < _seats.Count ? _seats[i].Tip : "";

    private sealed partial class Marks(Chamber c) : Control
    {
        public override void _Draw()
        {
            c._hits.Clear();
            var font = Ui.Bold;
            int n = c._seats.Count;
            for (int i = 0; i < n; i++)
            {
                var s = c._seats[i];
                var feet = c.OnScreen(SeatAt(i, n, SeatRadius));
                var head = c.OnScreen(SeatAt(i, n, SeatRadius) + new Vector3(0, FigureScale * 0.52f, 0));
                float h = Mathf.Max(feet.Y - head.Y, 30);
                var box = new Rect2(head.X - h * 0.38f, head.Y - 6, h * 0.76f, h + 20);
                c._hits.Add((box, i));
                bool hot = i == c._hover || i == c.Selected;

                // Badges over their head: their vote, then what the mayor holds over them or suspects.
                var badges = new List<(string Icon, Color Ring)>();
                if (s.Vote is bool v) badges.Add((v ? "vote-aye" : "vote-nay", v ? Ui.Good : Ui.Bad));
                if (s.Mover) badges.Add(("tab-council", Ui.Gold));
                if (s.Secret) badges.Add(("secret", Ui.Gold));
                if (s.Suspect) badges.Add(("traitor", Ui.Bad));
                if (s.Locked) badges.Add(("seat-locked", Ui.Ink2));
                float r = 15, gap = 4, x0 = head.X - (badges.Count * (2 * r) + (badges.Count - 1) * gap) / 2 + r;
                float y = (s.Holder == null ? c.OnScreen(SeatAt(i, n, ChairRadius) + new Vector3(0, 1.75f, 0)).Y : head.Y) - r - 4;
                for (int j = 0; j < badges.Count; j++)
                {
                    var at = new Vector2(x0 + j * (2 * r + gap), y);
                    DrawCircle(at, r, new Color("1d1712"));
                    DrawArc(at, r - 1, 0, Mathf.Tau, 24, badges[j].Ring, 2, true);
                    float g = r * 1.3f;
                    DrawTextureRect(Ui.Icon(badges[j].Icon), new Rect2(at - new Vector2(g / 2, g / 2), new Vector2(g, g)), false);
                }

                // A name plate at the table's edge: the seat's colour, their name and their loyalty.
                var plateAt = c.OnScreen(SeatAt(i, n, TableRadius - 0.3f) + new Vector3(0, 0.5f, 0));
                string name = s.Holder == null ? (s.Locked ? $"{s.Opens}" : "—") : s.Name;
                int size = 13;
                float tw = font.GetStringSize(name, HorizontalAlignment.Left, -1, size).X;
                float icon = s.Holder != null || s.Locked ? 16 : 0;
                var plate = new Rect2(plateAt.X - (tw + icon + 18) / 2, plateAt.Y - 11, tw + icon + 18, 22);
                var colour = SeatColour(s.Id, s.Ability);
                DrawRect(plate, new Color("1d1712", 0.88f));
                DrawRect(plate, hot ? Ui.Gold : colour.Darkened(0.1f), false, hot ? 2 : 1.5f);
                DrawRect(new Rect2(plate.Position, new Vector2(4, plate.Size.Y)), colour);
                float tx = plate.Position.X + 9;
                if (s.Holder != null)
                {
                    DrawTextureRect(Ui.Icon(LoyalIcon(s.Loyalty)), new Rect2(tx, plateAt.Y - 8, 16, 16), false);
                    tx += 18;
                }
                else if (s.Locked)
                {
                    DrawTextureRect(Ui.Icon("population"), new Rect2(tx, plateAt.Y - 8, 16, 16), false);
                    tx += 18;
                }
                DrawString(font, new Vector2(tx, plateAt.Y + 5), name, HorizontalAlignment.Left, -1, size, s.Holder == null ? Ui.Ink2 : Ui.Ink);
            }
        }
    }
}
