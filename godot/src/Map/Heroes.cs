using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;
using static CrownAndCompass.Client.Map.HeroKit;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Every hero on the map, drawn Warcraft-style: a body for the armour they wear, a head for their class,
/// the weapon in their hand and what is on their arm, all from their sheet, so a hero who straps on a
/// flaming sword is seen carrying it, burning. Each piece is one instanced batch (a draw call per kind
/// of piece, whatever the head count), and the animation (walk, swing, cast, loose an arrow) runs in
/// the vertex shader (rig.gdshaderinc) from a number per instance.
/// </summary>
public partial class Heroes : Node3D
{
    /// <summary>What a hero is doing, as rig.gdshaderinc numbers it.</summary>
    public enum Act { Idle, Walk, Swing, Swing2, Cast, Shoot, Thrust, Cheer }

    /// <summary>How a hero looks, read off their sheet.</summary>
    public sealed record Look(Frame Frame, Head Head, Weapon Weapon, OffHand Off, bool Cape, Color Skin, Color Trim,
        Color? Glow, int GlowStyle, bool Aura, Color? Armour, int Sparkle, Act Attack, Color Spell);

    private sealed class Batch(MultiMeshInstance3D mmi)
    {
        public readonly MultiMeshInstance3D Mmi = mmi;
        public readonly List<float> Buf = [];
        public int Count;
    }

    private static ShaderMaterial Material => Mats.Hero;
    private static ShaderMaterial AuraMaterial => Mats.Aura;

    private readonly Batch[] _bodies = new Batch[Enum.GetValues<Frame>().Length];
    private readonly Batch[] _heads = new Batch[Enum.GetValues<Head>().Length];
    private readonly Batch[] _weapons = new Batch[Enum.GetValues<Weapon>().Length];
    private readonly Batch[] _auras = new Batch[Enum.GetValues<Weapon>().Length];
    private readonly Batch[] _offs = new Batch[Enum.GetValues<OffHand>().Length];
    private Batch _cape = null!;
    private readonly List<Batch> _all = [];

    // The palette the shader indexes: slot 0 is "none"; colours are added as heroes need them.
    private readonly Color[] _palette = new Color[32];
    private readonly Dictionary<Color, int> _slots = [];
    private bool _paletteDirty = true;

    private readonly Dictionary<int, (object?[] Key, Look Look)> _looks = [];

    public override void _Ready()
    {
        var kit = new MeshKit();
        Batch Make(Action<MeshKit> build, Material mat, bool shadow = true)
        {
            kit.Clear();
            build(kit);
            var mmi = new MultiMeshInstance3D
            {
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = kit.Build() },
                MaterialOverride = mat,
                CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mmi);
            var b = new Batch(mmi);
            _all.Add(b);
            return b;
        }
        foreach (var f in Enum.GetValues<Frame>()) _bodies[(int)f] = Make(k => Body(k, f), Material);
        foreach (var h in Enum.GetValues<Head>()) _heads[(int)h] = Make(k => HeadPiece(k, h), Material);
        foreach (var w in Enum.GetValues<Weapon>())
        {
            if (w == Weapon.None) continue;
            _weapons[(int)w] = Make(k => WeaponPiece(k, w), Material);
            _auras[(int)w] = Make(k => WeaponPiece(k, w), AuraMaterial, false);
        }
        foreach (var o in Enum.GetValues<OffHand>())
            if (o != OffHand.None) _offs[(int)o] = Make(k => OffHandPiece(k, o), Material);
        _cape = Make(Cape, Material);
        _palette[0] = new Color(0, 0, 0, 0);
    }

    private int Slot(Color c, int style = 1)
    {
        var key = c with { A = style };
        if (_slots.TryGetValue(key, out int s)) return s;
        // Full: reuse the nearest colour (never seen with the classes, races and enchantments there are).
        if (_slots.Count >= _palette.Length - 1)
            return _slots.OrderBy(kv => Mathf.Abs(kv.Key.R - c.R) + Mathf.Abs(kv.Key.G - c.G) + Mathf.Abs(kv.Key.B - c.B) + (kv.Key.A == style ? 0 : 9)).First().Value;
        s = _slots.Count + 1;
        _slots[key] = s;
        _palette[s] = key;
        _paletteDirty = true;
        return s;
    }

    // Reading the sheet ---------------------------------------------------------------------------------

    public Look LookOf(Agent a)
    {
        var s = a.Hero!;
        int tier = s.Level >= 11 ? 2 : s.Level >= 6 ? 1 : 0;
        object?[] key = [s.Weapon, s.Armor, s.Shield, s.Magic.Get("weapon", null!), s.Magic.Get("armor", null!), s.Magic.Get("shield", null!), s.Magic.Get("trinket", null!), tier, s.Cls.Id];
        if (_looks.TryGetValue(a.Id, out var had) && had.Key.SequenceEqual(key)) return had.Look;
        var look = Read(a, tier);
        _looks[a.Id] = (key, look);
        return look;
    }

    private static readonly string[] Casters = ["wizard", "sorcerer", "warlock", "druid", "hedge-witch"];

    private static Look Read(Agent a, int tier)
    {
        var s = a.Hero!;
        string cls = s.Cls.Id;
        var magicWeapon = s.Magic.Get("weapon", null!);
        var magicArmour = s.Magic.Get("armor", null!) ?? s.Magic.Get("shield", null!);
        string armour = s.Armor?.Id ?? "";
        var frame = armour switch
        {
            "plate-armor" or "splint-armor" or "half-plate-armor" => Frame.Plate,
            "chain-mail" or "ring-mail" or "scale-mail" or "breastplate" or "chain-shirt" => Frame.Mail,
            "" => cls is "barbarian" or "monk" ? Frame.Bare : Frame.Robe,
            _ => Frame.Leather,
        };
        var head = cls switch
        {
            "fighter" => Head.GreatHelm,
            "paladin" => Head.WingedHelm,
            "barbarian" => Head.HornedHelm,
            "rogue" or "ranger" or "cartographer" => Head.Hood,
            "wizard" => Head.WizardHat,
            "warlock" => Head.Cowl,
            "sorcerer" => Head.Circlet,
            "cleric" => Head.Mitre,
            "druid" => Head.Antlers,
            "bard" => Head.Feathered,
            "monk" => Head.Topknot,
            "warden" => Head.Sallet,
            "hedge-witch" => Head.WitchHat,
            "reliquarian" => Head.Coif,
            _ => Head.Hood,
        };
        var weapon = KindOf(s.Weapon?.Id ?? "", magicWeapon?.Name ?? "");
        var off = s.Shield != null ? cls is "fighter" or "paladin" ? OffHand.Kite : OffHand.Round
            : weapon == Weapon.Bow ? OffHand.Bow
            : cls switch
            {
                "sorcerer" or "warlock" or "hedge-witch" => OffHand.Orb,
                "bard" => OffHand.Lute,
                "reliquarian" => OffHand.Lantern,
                "wizard" or "cartographer" => OffHand.Book,
                "rogue" => OffHand.Dagger,
                _ => OffHand.None,
            };
        if (weapon == Weapon.Bow) weapon = Weapon.None;
        bool caster = Casters.Contains(cls);
        var spell = SpellColour(cls);
        // A magic weapon burns, freezes or shines in its own colour; a caster's focus glows with their magic.
        Color? glow = null;
        int style = 1;
        if (magicWeapon != null) (glow, style) = Enchantment(magicWeapon.Name);
        else if (caster || weapon == Weapon.Staff && cls != "monk") glow = spell;
        Color? armourGlow = magicArmour != null ? Enchantment(magicArmour.Name).Colour : null;
        int sparkle = Math.Min(3, tier + (frame == Frame.Plate ? 1 : 0) + (magicArmour != null ? 1 : 0));
        var attack = off == OffHand.Bow ? Act.Shoot
            : caster ? Act.Cast
            : weapon is Weapon.Greatsword or Weapon.Greataxe or Weapon.Maul or Weapon.Glaive ? Act.Swing2
            : weapon is Weapon.Spear or Weapon.Rapier or Weapon.Dagger ? Act.Thrust
            : Act.Swing;
        return new Look(frame, head, weapon, off, frame != Frame.Bare && cls != "monk", Palette.RaceColor(a.Race), Palette.ClassColor(cls),
            glow, style, magicWeapon != null && weapon != Weapon.None, armourGlow, sparkle, attack, spell);
    }

    /// <summary>The kind of weapon to draw: a magic weapon that names its kind (an axe, a hammer, a bow) shows as that, else the hero's own.</summary>
    public static Weapon KindOf(string id, string magic)
    {
        string m = magic.ToLowerInvariant();
        var own = id switch
        {
            "longsword" or "shortsword" or "scimitar" => Weapon.Sword,
            "greatsword" => Weapon.Greatsword,
            "rapier" => Weapon.Rapier,
            "dagger" or "dart" or "whip" or "net" or "sling" => Weapon.Dagger,
            "handaxe" or "battleaxe" => Weapon.Axe,
            "greataxe" => Weapon.Greataxe,
            "warhammer" or "light-hammer" or "war-pick" => Weapon.Hammer,
            "maul" => Weapon.Maul,
            "mace" or "morningstar" or "flail" or "club" or "greatclub" => Weapon.Mace,
            "spear" or "javelin" or "trident" or "pike" or "lance" => Weapon.Spear,
            "glaive" or "halberd" => Weapon.Glaive,
            "quarterstaff" or "staff" or "wooden-staff" => Weapon.Staff,
            "sickle" => Weapon.Sickle,
            "longbow" or "shortbow" or "light-crossbow" or "heavy-crossbow" or "hand-crossbow" => Weapon.Bow,
            "" => Weapon.None,
            _ => Weapon.Sword,
        };
        if (m.Contains("bow")) return Weapon.Bow;
        if (m.Contains("axe")) return own == Weapon.Greataxe ? own : Weapon.Axe;
        if (m.Contains("hammer of thunderbolts")) return Weapon.Maul;
        if (m.Contains("hammer") || m.Contains("thrower")) return Weapon.Hammer;
        if (m.Contains("mace")) return Weapon.Mace;
        if (m.Contains("dagger")) return Weapon.Dagger;
        if (m.Contains("trident") || m.Contains("javelin") || m.Contains("spear")) return Weapon.Spear;
        if (m.Contains("scimitar") || m.Contains("sword") || m.Contains("blade") || m.Contains("brand") || m.Contains("avenger") || m.Contains("defender"))
            return own is Weapon.Sword or Weapon.Greatsword or Weapon.Rapier ? own : Weapon.Sword;
        return own;
    }

    /// <summary>An enchantment's colour and style (1 steady, 2 fire, 3 lightning, 4 holy, 5 frost), from the item's name.</summary>
    public static (Color Colour, int Style) Enchantment(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("flame") || n.Contains("fire") || n.Contains("dragon") || n.Contains("demon")) return (new Color("ff7a1a"), 2);
        if (n.Contains("frost") || n.Contains("ice") || n.Contains("winter") || n.Contains("cold")) return (new Color("8ae0ff"), 5);
        if (n.Contains("thunder") || n.Contains("lightning") || n.Contains("storm") || n.Contains("speed")) return (new Color("7ab8ff"), 3);
        if (n.Contains("holy") || n.Contains("sun") || n.Contains("avenger") || n.Contains("disruption") || n.Contains("smiting") || n.Contains("oath")) return (new Color("ffe27a"), 4);
        if (n.Contains("venom") || n.Contains("poison") || n.Contains("elven")) return (new Color("8aff6a"), 1);
        if (n.Contains("life stealing") || n.Contains("nine lives") || n.Contains("terror") || n.Contains("wounding") || n.Contains("vicious") || n.Contains("berserker")) return (new Color("ff3a4a"), 1);
        if (n.Contains("vorpal") || n.Contains("sharpness") || n.Contains("mithral") || n.Contains("adamantine")) return (new Color("e8f4ff"), 1);
        return (new Color("6aa8ff"), 1);
    }

    /// <summary>The colour of a class's magic: bolts, the focus's glow, the burst where it lands.</summary>
    public static Color SpellColour(string cls) => cls switch
    {
        "wizard" => new Color("6ac8ff"),
        "sorcerer" => new Color("ff7a3a"),
        "warlock" => new Color("b65cff"),
        "druid" => new Color("8aff5a"),
        "hedge-witch" => new Color("c8ff6a"),
        "cleric" or "paladin" => new Color("ffe08a"),
        "bard" => new Color("ff8ad0"),
        "reliquarian" => new Color("ffcf70"),
        _ => new Color("ffffff"),
    };

    // Drawing -----------------------------------------------------------------------------------------

    /// <summary>The hero standing at p facing yaw, doing act at phase u (0..1) of its cycle; flash is a hit landing on them.</summary>
    public void Put(Agent a, Vector3 p, float yaw, Act act, float u, float flash = 0, float scale = 1) =>
        Put(a, new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(scale, scale, scale)), p), act, u, flash);

    public void Put(Agent a, Transform3D xf, Act act, float u, float flash = 0)
    {
        if (a.Hero == null) return;
        var l = LookOf(a);
        float r = Slot(l.Skin) + 32 * Slot(l.Trim);
        float g = l.Sparkle + Mathf.Clamp(flash, 0, 1) * 0.95f;
        float anim = (int)act + Mathf.Clamp(u - Mathf.Floor(u), 0, 0.998f);
        int wglow = l.Glow is Color wg ? Slot(wg, l.GlowStyle) : 0;
        int aglow = l.Armour is Color ag ? Slot(ag) : 0;
        Add(_bodies[(int)l.Frame], xf, r, g, 32 * aglow, anim);
        Add(_heads[(int)l.Head], xf, r, g, 0, anim);
        if (l.Cape) Add(_cape, xf, r, g, 0, anim);
        if (l.Weapon != Weapon.None)
        {
            Add(_weapons[(int)l.Weapon], xf, r, g, wglow, anim);
            if (l.Aura) Add(_auras[(int)l.Weapon], xf, r, g, wglow, anim);
        }
        if (l.Off != OffHand.None) Add(_offs[(int)l.Off], xf, r, g, (l.Off is OffHand.Orb ? wglow : 0) + 32 * aglow, anim);
    }

    private static void Add(Batch b, Transform3D xf, float r, float g, float bl, float a)
    {
        var x = xf.Basis;
        var p = xf.Origin;
        b.Buf.AddRange([x.X.X, x.Y.X, x.Z.X, p.X, x.X.Y, x.Y.Y, x.Z.Y, p.Y, x.X.Z, x.Y.Z, x.Z.Z, p.Z, 1, 1, 1, 1, r, g, bl, a]);
        b.Count++;
    }

    /// <summary>Sends this frame's heroes to the GPU and starts the next frame empty.</summary>
    public void Flush()
    {
        if (_paletteDirty)
        {
            Material.SetShaderParameter("palette", _palette);
            AuraMaterial.SetShaderParameter("palette", _palette);
            _paletteDirty = false;
        }
        foreach (var b in _all)
        {
            var mm = b.Mmi.Multimesh;
            if (mm.InstanceCount != b.Count) mm.InstanceCount = b.Count;
            if (b.Count > 0) mm.Buffer = b.Buf.ToArray();
            b.Mmi.Visible = b.Count > 0;
            b.Buf.Clear();
            b.Count = 0;
        }
    }

    /// <summary>A hero's act this frame: walking, or standing; u is the phase of its cycle at time now.</summary>
    public static (Act Act, float U) Moving(int id, bool walking, float now) =>
        walking ? (Act.Walk, now * 1.7f + id * 0.31f) : (Act.Idle, now * 0.3f + id * 0.17f);

    /// <summary>A hero fighting: their attack and its phase. Seconds per blow.</summary>
    public const float Blow = 1.15f;

    public (Act Act, float U) Fighting(Agent a, float now) => (LookOf(a).Attack, now / Blow + a.Id * 0.23f);

    /// <summary>How far from a foe a hero stands to fight: blade's length for the melee, a few strides for bows and spells.</summary>
    public float Reach(Agent a) => LookOf(a).Attack switch { Act.Cast or Act.Shoot => 1.5f, Act.Swing2 => 0.55f, _ => 0.45f };

    private readonly Dictionary<int, float> _lastPhase = [];

    /// <summary>True on the frame a hero's blow (or spell, or arrow) lands: the phase passed the moment of the strike.</summary>
    public bool Lands(int id, Act act, float u)
    {
        float strike = act switch { Act.Thrust => 0.45f, Act.Cast => 0.55f, Act.Shoot => 0.65f, _ => 0.5f };
        float f = u - Mathf.Floor(u);
        bool hit = _lastPhase.TryGetValue(id, out float was) && (was < strike ? f >= strike || f < was : f >= strike && f < was);
        _lastPhase[id] = f;
        return hit;
    }
}
