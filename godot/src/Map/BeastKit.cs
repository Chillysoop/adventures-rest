using CrownAndCompass.Sim.Systems;
using Godot;
using static CrownAndCompass.Client.Map.HeroKit;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Every creature's model, made in code from its anatomy (<see cref="Bestiary"/>): a body for its plan (quadruped, bird,
/// dragon, serpent, spider, ooze, wraith, kraken, treant...), the head of its kind, its legs, arms, wings and tail where
/// its stat block puts them, its horns, tusks, shell and spikes, and its own colours baked in. Joints are the mesh's own
/// (<see cref="MeshKit.Pivot"/>) so beastrig.gdshaderinc can walk, slither, flap and lunge any of them; people and golems
/// are built from the heroes' kit and move on the heroes' rig. One mesh per creature: a draw call per kind on screen.
/// Proportions are the heroes' chunky ones: big heads, thick limbs, readable from the RTS camera.
/// </summary>
public sealed class BeastKit
{
    // Parts, as beastrig.gdshaderinc numbers them.
    public const float LegA = 10, LegB = 11, ClawR = 12, ClawL = 13, Neck = 14, Jaw = 15, TailPart = 16, WingR = 17, WingL = 18, Tentacle = 19, Frond = 20, Mote = 21;

    /// <summary>Gaits and strikes as the shader numbers them.</summary>
    public static int GaitCode(string g) => g switch
    {
        "walk" => 1, "lumber" => 2, "trot" => 3, "prowl" => 4, "skitter" => 5, "slither" => 6, "fly" => 7, "hover" => 8, "ooze" => 9, "swim" => 10, "root" => 11, "hop" => 12, "swarm" => 13, _ => 1,
    };

    public static int StrikeCode(string s) => s switch
    {
        "weapon" => 0, "shoot" => 1, "cast" => 2, "bite" => 3, "claw" => 4, "slam" => 5, "sting" => 6, "gore" => 7, "lash" => 8, "spit" => 9, "engulf" => 10, "talons" => 11, "touch" => 12, "trample" => 13, _ => 5,
    };

    /// <summary>Plans built on the heroes' rig (fixed joints): their scale goes on the instance, not into the mesh.</summary>
    public static bool OnHeroRig(Anatomy a) => a.Plan is "humanoid" or "golem" or "ape" || a.Plan == "elemental" && a.Head == "rock";

    private readonly MeshKit k;
    private readonly Anatomy a;
    private readonly uint seed;
    private Color hide, belly, dark, horn, claw, eye, teeth, mouth;

    private BeastKit(MeshKit kit, Anatomy anatomy)
    {
        k = kit;
        a = anatomy;
        seed = Bestiary.Hash(anatomy.Id);
        var h = new Color("#" + a.Hide);
        var b = new Color("#" + a.Belly);
        hide = Mat(h);
        belly = Mat(b);
        dark = Mat(h.Darkened(0.35f));
        horn = Plain(R(1) > 0.5f ? "e8dcc0" : "3a2a22");
        if (a.Material is "bone") horn = Plain("f0e8d0");
        claw = Plain("2a2018");
        eye = Glow(new Color("#" + a.Accent));
        teeth = Plain("f0e8d0");
        mouth = Plain("5a1a1e");
    }

    /// <summary>Builds the creature's model into the kit (cleared first).</summary>
    public static void Build(MeshKit kit, Anatomy anatomy)
    {
        kit.Clear();
        new BeastKit(kit, anatomy).Make();
        kit.Part = 0;
        kit.Pivot = null;
    }

    // Colours ---------------------------------------------------------------------------------------------------

    private static Color Plain(string hex) => new Color("#" + hex) { A = MeshKit.Flag.Plain };
    private static Color Glow(Color c) => c with { A = MeshKit.Flag.Magic };
    private static Color Ember(Color c) => c with { A = MeshKit.Flag.Ember };
    private static Color Mark(float v = 1) => new(v, v, v, MeshKit.Flag.Trim);

    /// <summary>A colour as the creature's stuff shows it: metal and ice glint, ghosts glow faintly, the rest is plain.</summary>
    private Color Mat(Color c) => a.Material switch
    {
        "metal" or "crystal" or "ice" => c with { A = MeshKit.Flag.Metal },
        "ghost" => (c * 0.5f) with { A = MeshKit.Flag.Magic },
        _ => c with { A = MeshKit.Flag.Plain },
    };

    /// <summary>A number in [0, 1) for this creature, the same every time: small variety within its plan.</summary>
    private float R(int i) => Geo.Hash((int)(seed % 100000) * 7 + i * 131);
    private float Vary(int i, float spread) => 1 + (R(i) - 0.5f) * spread;

    private bool Has(string mark) => a.Marks.Contains(mark);

    // Primitives ------------------------------------------------------------------------------------------------

    /// <summary>A tapered limb from p to q.</summary>
    private void Seg(Vector3 p, Vector3 q, float r0, float r1, Color c, int sides = 5)
    {
        var up = q - p;
        float len = up.Length();
        if (len < 1e-4f) return;
        var y = up / len;
        var x = Mathf.Abs(y.Y) > 0.9f ? Vector3.Right : y.Cross(Vector3.Up).Normalized();
        var z = x.Cross(y);
        k.Push(new Transform3D(new Basis(x, y, z), p));
        k.Frustum(Vector3.Zero, r0, r1, len, sides, c);
        k.Pop();
    }

    private void Ball(Vector3 c, Vector3 s, Color col, float jitter = 0, Color? shade = null) => k.Blob(c, s, col, jitter, (int)(seed % 997), shade);

    /// <summary>A pointed horn, spike or tooth from base, growing along dir.</summary>
    private void Spike(Vector3 at, Vector3 dir, float r, float len, Color c) => Seg(at, at + dir.Normalized() * len, r, 0, c, 4);

    private void Part(float part, Vector3? pivot)
    {
        k.Part = part;
        k.Pivot = pivot;
    }

    private void Body() => Part(0, null);

    /// <summary>Eyes in a row across a face at c, facing +Z.</summary>
    private void Eyes(Vector3 c, float spread, float size, int n)
    {
        if (n <= 0) return;
        for (int i = 0; i < n; i++)
        {
            float x = n == 1 ? 0 : (i / (float)(n - 1) - 0.5f) * spread * 2;
            float y = n > 2 ? (i % 2) * size * 1.4f : 0;
            k.Box(c + new Vector3(x, y, 0), new Vector3(size, size * 0.8f, size * 0.6f), eye);
        }
    }

    // Build -----------------------------------------------------------------------------------------------------

    private void Make()
    {
        bool hero = OnHeroRig(a);
        if (!hero) k.Push(new Transform3D(Basis.Identity.Scaled(Vector3.One * (float)a.Scale), Vector3.Zero));
        switch (a.Plan)
        {
            case "humanoid": People(); break;
            case "golem": Golem(); break;
            case "ape": Ape(); break;
            case "elemental": Elemental(); break;
            case "quadruped": Quadruped(); break;
            case "theropod": Theropod(); break;
            case "bird": Bird(); break;
            case "dragon": Dragon(); break;
            case "serpent": Serpent(); break;
            case "worm": Worm(); break;
            case "insect": Insect(); break;
            case "spider": Spider(); break;
            case "crab": Crab(); break;
            case "ooze": Ooze(); break;
            case "wraith": Wraith(); break;
            case "floater": Floater(); break;
            case "fish": Fish(); break;
            case "cephalopod": Cephalopod(); break;
            case "plant": Plant(); break;
            case "taur": Taur(); break;
            case "swarm": Swarm(); break;
            case "object": Thing(); break;
            default: Quadruped(); break;
        }
        if (!hero) k.Pop();
    }

    // Heads -----------------------------------------------------------------------------------------------------

    /// <summary>
    /// A beast's head at c facing +Z (size s against a wolf's), with its jaw on the jaw part; marks add horns,
    /// antlers, tusks, a crest or a mane. The caller sets the neck part first.
    /// </summary>
    private void Head(string kind, Vector3 c, float s, float neckPart, Vector3 neckPivot)
    {
        Part(neckPart, neckPivot);
        var jawPivot = c + new Vector3(0, -0.02f, 0) * s;
        void JawPart() => Part(Jaw, jawPivot);
        void NeckPart() => Part(neckPart, neckPivot);
        switch (kind)
        {
            case "wolf" or "dog" or "hyena" or "jackal":
                Ball(c, new Vector3(0.055f, 0.055f, 0.06f) * s, hide);
                Seg(c + new Vector3(0, -0.005f, 0.03f) * s, c + new Vector3(0, -0.015f, 0.11f) * s, 0.032f * s, 0.018f * s, hide);
                k.Box(c + new Vector3(0, -0.005f, 0.11f) * s, new Vector3(0.022f, 0.016f, 0.016f) * s, claw);
                foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.03f, 0.03f, -0.01f) * s, new Vector3(side * 0.3f, 1, -0.2f), 0.016f * s, 0.05f * s, dark);
                JawPart();
                Seg(c + new Vector3(0, -0.03f, 0.03f) * s, c + new Vector3(0, -0.035f, 0.1f) * s, 0.022f * s, 0.012f * s, belly);
                Fangs(c + new Vector3(0, -0.025f, 0.09f) * s, 0.018f * s, s);
                NeckPart();
                Eyes(c + new Vector3(0, 0.015f, 0.05f) * s, 0.022f * s, 0.009f * s, a.Eyes);
                break;
            case "cat" or "tiger" or "lion":
                Ball(c, new Vector3(0.06f, 0.055f, 0.058f) * s, hide);
                Ball(c + new Vector3(0, -0.012f, 0.05f) * s, new Vector3(0.035f, 0.025f, 0.03f) * s, belly);
                k.Box(c + new Vector3(0, 0, 0.075f) * s, new Vector3(0.016f, 0.012f, 0.01f) * s, claw);
                foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.035f, 0.035f, 0) * s, new Vector3(side * 0.4f, 1, 0), 0.016f * s, 0.03f * s, dark);
                JawPart();
                Ball(c + new Vector3(0, -0.04f, 0.04f) * s, new Vector3(0.025f, 0.012f, 0.025f) * s, belly);
                Fangs(c + new Vector3(0, -0.03f, 0.06f) * s, 0.016f * s, s * 1.2f);
                NeckPart();
                Eyes(c + new Vector3(0, 0.015f, 0.052f) * s, 0.022f * s, 0.009f * s, a.Eyes);
                break;
            case "bear" or "owlbear" or "yeti":
                Ball(c, new Vector3(0.07f, 0.065f, 0.065f) * s, hide, 0.15f);
                foreach (int side in new[] { -1, 1 }) Ball(c + new Vector3(side * 0.045f, 0.05f, -0.01f) * s, new Vector3(0.018f, 0.018f, 0.01f) * s, dark);
                if (kind == "owlbear")
                {
                    Ball(c + new Vector3(0, 0.005f, 0.05f) * s, new Vector3(0.055f, 0.05f, 0.025f) * s, belly);
                    Spike(c + new Vector3(0, -0.01f, 0.07f) * s, new Vector3(0, -0.6f, 1), 0.02f * s, 0.06f * s, Plain("d8a830"));
                    foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.04f, 0.05f, 0) * s, new Vector3(side * 0.5f, 1, -0.3f), 0.014f * s, 0.05f * s, dark);
                    Eyes(c + new Vector3(0, 0.02f, 0.075f) * s, 0.025f * s, 0.012f * s, a.Eyes);
                    break;
                }
                Seg(c + new Vector3(0, -0.01f, 0.04f) * s, c + new Vector3(0, -0.02f, 0.1f) * s, 0.035f * s, 0.025f * s, belly);
                k.Box(c + new Vector3(0, -0.008f, 0.1f) * s, new Vector3(0.024f, 0.018f, 0.014f) * s, claw);
                JawPart();
                Fangs(c + new Vector3(0, -0.04f, 0.08f) * s, 0.018f * s, s);
                NeckPart();
                Eyes(c + new Vector3(0, 0.02f, 0.055f) * s, 0.024f * s, 0.009f * s, a.Eyes);
                break;
            case "horse":
                Seg(c + new Vector3(0, 0.02f, -0.02f) * s, c + new Vector3(0, -0.04f, 0.12f) * s, 0.04f * s, 0.028f * s, hide);
                k.Box(c + new Vector3(0, -0.06f, 0.115f) * s, new Vector3(0.05f, 0.035f, 0.04f) * s, dark);
                foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.025f, 0.05f, -0.02f) * s, new Vector3(side * 0.2f, 1, -0.1f), 0.012f * s, 0.045f * s, hide);
                Eyes(c + new Vector3(0, 0.025f, 0.03f) * s, 0.035f * s, 0.009f * s, a.Eyes);
                break;
            case "deer":
                Seg(c + new Vector3(0, 0.015f, -0.02f) * s, c + new Vector3(0, -0.03f, 0.1f) * s, 0.035f * s, 0.02f * s, hide);
                k.Box(c + new Vector3(0, -0.04f, 0.1f) * s, new Vector3(0.026f, 0.02f, 0.02f) * s, claw);
                foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.03f, 0.035f, -0.02f) * s, new Vector3(side * 1, 0.6f, -0.2f), 0.012f * s, 0.05f * s, hide);
                Eyes(c + new Vector3(0, 0.02f, 0.03f) * s, 0.032f * s, 0.009f * s, a.Eyes);
                break;
            case "ox" or "bull":
                Ball(c, new Vector3(0.07f, 0.065f, 0.07f) * s, hide);
                k.Box(c + new Vector3(0, -0.045f, 0.06f) * s, new Vector3(0.07f, 0.045f, 0.05f) * s, belly);
                Eyes(c + new Vector3(0, 0.015f, 0.06f) * s, 0.04f * s, 0.01f * s, a.Eyes);
                break;
            case "boar":
                Ball(c, new Vector3(0.06f, 0.06f, 0.07f) * s, hide, 0.2f);
                Seg(c + new Vector3(0, -0.01f, 0.04f) * s, c + new Vector3(0, -0.02f, 0.1f) * s, 0.035f * s, 0.03f * s, dark);
                k.Box(c + new Vector3(0, -0.04f, 0.1f) * s, new Vector3(0.04f, 0.035f, 0.012f) * s, Plain("c88a8a"));
                foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.03f, -0.03f, 0.07f) * s, new Vector3(side * 0.3f, 1, 0.4f), 0.01f * s, 0.06f * s, teeth);
                Eyes(c + new Vector3(0, 0.02f, 0.055f) * s, 0.026f * s, 0.009f * s, a.Eyes);
                break;
            case "rat" or "baboon":
                Ball(c, new Vector3(0.045f, 0.045f, 0.055f) * s, hide);
                Seg(c + new Vector3(0, -0.005f, 0.03f) * s, c + new Vector3(0, -0.02f, 0.1f) * s, 0.03f * s, 0.008f * s, hide);
                k.Box(c + new Vector3(0, -0.02f, 0.1f) * s, new Vector3(0.014f, 0.012f, 0.01f) * s, Plain("d88a8a"));
                foreach (int side in new[] { -1, 1 }) Ball(c + new Vector3(side * 0.035f, 0.045f, -0.01f) * s, new Vector3(0.022f, 0.026f, 0.008f) * s, Plain("c88a8a"));
                Eyes(c + new Vector3(0, 0.02f, 0.045f) * s, 0.02f * s, 0.008f * s, a.Eyes);
                break;
            case "lizard" or "croc" or "dragon" or "wyvern" or "pseudo" or "rex" or "raptor" or "tarrasque" or "salamander":
                {
                    float snout = kind switch { "croc" => 0.2f, "rex" or "tarrasque" => 0.13f, "dragon" or "wyvern" => 0.12f, _ => 0.09f };
                    float wide = kind is "rex" or "tarrasque" ? 1.3f : 1;
                    Ball(c, new Vector3(0.055f * wide, 0.05f, 0.06f) * s, hide);
                    Seg(c + new Vector3(0, 0, 0.02f) * s, c + new Vector3(0, -0.01f, snout) * s, 0.04f * wide * s, 0.022f * s, hide, 6);
                    k.Box(c + new Vector3(0, 0.005f, snout * 0.7f) * s, new Vector3(0.05f * wide, 0.012f, snout * 0.5f) * s, dark);
                    JawPart();
                    Seg(c + new Vector3(0, -0.035f, 0.01f) * s, c + new Vector3(0, -0.04f, snout * 0.95f) * s, 0.03f * wide * s, 0.016f * s, belly, 6);
                    Fangs(c + new Vector3(0, -0.02f, snout * 0.8f) * s, 0.025f * wide * s, s);
                    NeckPart();
                    Eyes(c + new Vector3(0, 0.025f, 0.03f) * s, 0.032f * wide * s, 0.01f * s, a.Eyes);
                    if (kind is "dragon" or "wyvern" or "tarrasque" || Has("horns"))
                        foreach (int side in new[] { -1, 1 })
                        {
                            Spike(c + new Vector3(side * 0.03f, 0.04f, -0.02f) * s, new Vector3(side * 0.35f, 0.6f, -1), 0.016f * s, (Has("horns") ? 0.12f : 0.07f) * s, horn);
                            if (Has("frill")) k.Tri(c + new Vector3(side * 0.04f, 0.02f, -0.03f) * s, c + new Vector3(side * 0.11f, 0.05f, -0.07f) * s, c + new Vector3(side * 0.05f, -0.04f, -0.05f) * s, dark);
                            if (Has("whiskers")) Seg(c + new Vector3(side * 0.03f, -0.01f, snout * 0.9f) * s, c + new Vector3(side * 0.09f, -0.06f, snout * 0.8f) * s, 0.005f * s, 0.001f * s, belly, 3);
                        }
                    if (Has("horn")) Spike(c + new Vector3(0, 0.01f, snout * 0.8f) * s, new Vector3(0, 1, 0.5f), 0.016f * s, 0.07f * s, horn);
                    if (Has("plate")) k.Box(c + new Vector3(0, 0.04f, 0.01f) * s, new Vector3(0.08f, 0.012f, 0.07f) * s, horn);
                    if (Has("skull")) Ball(c + new Vector3(0, 0.01f, snout * 0.5f) * s, new Vector3(0.04f, 0.035f, 0.05f) * s, Plain("d8d0b8"));
                    break;
                }
            case "frog":
                Ball(c, new Vector3(0.08f, 0.04f, 0.06f) * s, hide);
                JawPart();
                Ball(c + new Vector3(0, -0.03f, 0.01f) * s, new Vector3(0.075f, 0.02f, 0.055f) * s, belly);
                NeckPart();
                foreach (int side in new[] { -1, 1 })
                {
                    Ball(c + new Vector3(side * 0.045f, 0.035f, 0.02f) * s, new Vector3(0.022f, 0.022f, 0.022f) * s, hide);
                    k.Box(c + new Vector3(side * 0.045f, 0.045f, 0.035f) * s, new Vector3(0.012f, 0.012f, 0.01f) * s, eye);
                }
                break;
            case "snail":
                Seg(c, c + new Vector3(0, 0.02f, 0.06f) * s, 0.035f * s, 0.03f * s, belly);
                foreach (int side in new[] { -1, 1 })
                {
                    Seg(c + new Vector3(side * 0.015f, 0.02f, 0.05f) * s, c + new Vector3(side * 0.035f, 0.1f, 0.07f) * s, 0.006f * s, 0.004f * s, belly, 3);
                    k.Box(c + new Vector3(side * 0.035f, 0.1f, 0.07f) * s, new Vector3(0.012f, 0.012f, 0.012f) * s, eye);
                }
                break;
            case "griffon" or "hawk" or "owl" or "crow" or "vulture" or "roc" or "phoenix" or "beak" or "couatl":
                {
                    bool owl = kind == "owl";
                    Ball(c, new Vector3(0.05f, owl ? 0.06f : 0.05f, 0.05f) * s, kind == "vulture" ? Plain("c89a8a") : owl ? belly : hide);
                    var beakCol = Plain(kind is "crow" or "vulture" ? "3a3430" : "e8b030");
                    Seg(c + new Vector3(0, 0, 0.035f) * s, c + new Vector3(0, -0.03f, owl ? 0.06f : 0.1f) * s, 0.02f * s, 0.002f * s, beakCol, 4);
                    JawPart();
                    Seg(c + new Vector3(0, -0.015f, 0.035f) * s, c + new Vector3(0, -0.03f, owl ? 0.05f : 0.08f) * s, 0.012f * s, 0.002f * s, beakCol, 4);
                    NeckPart();
                    Eyes(c + new Vector3(0, 0.012f, owl ? 0.045f : 0.035f) * s, (owl ? 0.025f : 0.032f) * s, (owl ? 0.016f : 0.009f) * s, a.Eyes);
                    if (owl || Has("crest")) foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.025f, 0.04f, 0) * s, new Vector3(side * 0.3f, 1, -0.5f), 0.012f * s, 0.05f * s, kind == "phoenix" ? Ember(new Color("ffb030")) : dark);
                    if (kind == "vulture") Ball(c + new Vector3(0, -0.05f, -0.03f) * s, new Vector3(0.06f, 0.03f, 0.05f) * s, Plain("f0ece0"), 0.4f);
                    break;
                }
            case "bat" or "pteranodon":
                Ball(c, new Vector3(0.05f, 0.045f, 0.05f) * s, hide);
                if (kind == "pteranodon")
                {
                    Seg(c + new Vector3(0, -0.01f, 0.03f) * s, c + new Vector3(0, -0.02f, 0.18f) * s, 0.02f * s, 0.002f * s, Plain("e8c070"), 4);
                    Seg(c + new Vector3(0, 0.02f, -0.02f) * s, c + new Vector3(0, 0.05f, -0.12f) * s, 0.018f * s, 0.002f * s, hide, 4);
                }
                else
                {
                    foreach (int side in new[] { -1, 1 }) k.Tri(c + new Vector3(side * 0.02f, 0.03f, 0) * s, c + new Vector3(side * 0.06f, 0.11f, -0.01f) * s, c + new Vector3(side * 0.05f, 0.02f, -0.01f) * s, dark);
                    Fangs(c + new Vector3(0, -0.03f, 0.04f) * s, 0.012f * s, s);
                }
                Eyes(c + new Vector3(0, 0.01f, 0.045f) * s, 0.02f * s, 0.009f * s, a.Eyes);
                break;
            case "snake":
                Ball(c, new Vector3(0.045f, 0.03f, 0.065f) * s, hide);
                JawPart();
                Ball(c + new Vector3(0, -0.02f, 0.01f) * s, new Vector3(0.035f, 0.012f, 0.05f) * s, belly);
                Spike(c + new Vector3(0, -0.025f, 0.06f) * s, new Vector3(0, 0, 1), 0.004f * s, 0.05f * s, Plain("c83a3a"));
                NeckPart();
                Eyes(c + new Vector3(0, 0.015f, 0.04f) * s, 0.024f * s, 0.008f * s, a.Eyes);
                if (Has("crest")) foreach (int side in new[] { -1, 1 }) k.Tri(c + new Vector3(side * 0.03f, 0, -0.02f) * s, c + new Vector3(side * 0.1f, 0.04f, -0.08f) * s, c + new Vector3(side * 0.04f, -0.03f, -0.07f) * s, dark);
                break;
            case "skull":
                Ball(c, new Vector3(0.05f, 0.05f, 0.06f) * s, Plain("e6dcc0"));
                JawPart();
                k.Box(c + new Vector3(0, -0.06f, 0.02f) * s, new Vector3(0.05f, 0.025f, 0.06f) * s, Plain("d8ceb0"));
                NeckPart();
                Eyes(c + new Vector3(0, 0.005f, 0.05f) * s, 0.022f * s, 0.016f * s, 2);
                break;
            case "goat":
                Seg(c + new Vector3(0, 0.02f, -0.01f) * s, c + new Vector3(0, -0.03f, 0.08f) * s, 0.035f * s, 0.02f * s, hide);
                Seg(c + new Vector3(0, -0.04f, 0.07f) * s, c + new Vector3(0, -0.09f, 0.06f) * s, 0.012f * s, 0.002f * s, dark, 4);
                Eyes(c + new Vector3(0, 0.015f, 0.03f) * s, 0.028f * s, 0.009f * s, a.Eyes);
                break;
            case "fish":
                Ball(c, new Vector3(0.05f, 0.055f, 0.07f) * s, hide);
                JawPart();
                k.Box(c + new Vector3(0, -0.05f, 0.04f) * s, new Vector3(0.07f, 0.02f, 0.04f) * s, belly);
                Fangs(c + new Vector3(0, -0.03f, 0.07f) * s, 0.03f * s, s);
                NeckPart();
                foreach (int side in new[] { -1, 1 }) k.Box(c + new Vector3(side * 0.05f, 0.01f, 0.03f) * s, new Vector3(0.012f, 0.016f, 0.016f) * s, eye);
                Spike(c + new Vector3(0, 0.04f, -0.02f) * s, new Vector3(0, 1, -0.6f), 0.02f * s, 0.07f * s, dark);
                break;
            case "spider":
                Ball(c, new Vector3(0.05f, 0.05f, 0.05f) * s, dark);
                Eyes(c + new Vector3(0, 0.01f, 0.045f) * s, 0.02f * s, 0.008f * s, 6);
                foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * 0.012f, -0.03f, 0.04f) * s, new Vector3(0, -1, 0.3f), 0.008f * s, 0.05f * s, claw);
                break;
            case "blind":
                Ball(c, new Vector3(0.058f, 0.062f, 0.058f) * s, hide);
                k.Box(c + new Vector3(0, 0.005f, 0.05f) * s, new Vector3(0.06f, 0.014f, 0.01f) * s, dark);
                Fangs(c + new Vector3(0, -0.03f, 0.05f) * s, 0.02f * s, s);
                break;
            case "child":
                Ball(c, new Vector3(0.07f, 0.075f, 0.07f) * s, hide);
                Eyes(c + new Vector3(0, 0.005f, 0.065f) * s, 0.025f * s, 0.016f * s, 2);
                break;
            case "cap":
                Seg(c + new Vector3(0, -0.05f, 0) * s, c + new Vector3(0, 0.03f, 0) * s, 0.035f * s, 0.03f * s, belly);
                k.Frustum(c + new Vector3(0, 0.02f, 0) * s, 0.11f * s, 0.02f * s, 0.07f * s, 8, hide, dark);
                Eyes(c + new Vector3(0, -0.02f, 0.03f) * s, 0.016f * s, 0.008f * s, 2);
                break;
            case "tusks":
            default:
                Ball(c, new Vector3(0.06f, 0.06f, 0.06f) * s, hide);
                Eyes(c + new Vector3(0, 0.01f, 0.055f) * s, 0.022f * s, 0.01f * s, a.Eyes);
                break;
        }
        NeckPart();
        HeadMarks(c, s, kind);
    }

    /// <summary>Two rows of teeth across a mouth at c.</summary>
    private void Fangs(Vector3 c, float half, float s)
    {
        foreach (int side in new[] { -1, 1 }) Spike(c + new Vector3(side * half, 0, 0), new Vector3(0, -1, 0.15f), 0.006f * s, 0.022f * s, teeth);
    }

    private void HeadMarks(Vector3 c, float s, string kind)
    {
        if (Has("horns") && kind is not ("dragon" or "wyvern" or "tarrasque" or "lizard" or "croc"))
            foreach (int side in new[] { -1, 1 })
            {
                var root = c + new Vector3(side * 0.04f, 0.04f, 0) * s;
                var mid = root + new Vector3(side * 0.05f, 0.03f, -0.01f) * s;
                Seg(root, mid, 0.018f * s, 0.012f * s, horn, 4);
                Seg(mid, mid + new Vector3(side * 0.01f, 0.05f, 0.03f) * s, 0.012f * s, 0, horn, 4);
            }
        if (Has("horn")) Spike(c + new Vector3(0, 0.04f, 0.05f) * s, new Vector3(0, 1, 0.8f), 0.014f * s, 0.11f * s, kind == "horse" ? Plain("f0e0a0") : horn);
        if (Has("antlers"))
            foreach (int side in new[] { -1, 1 })
            {
                var root = c + new Vector3(side * 0.03f, 0.05f, -0.01f) * s;
                var top = root + new Vector3(side * 0.08f, 0.12f, -0.03f) * s;
                Seg(root, top, 0.01f * s, 0.006f * s, Plain("c8b088"), 4);
                for (int i = 1; i <= 3; i++)
                {
                    var on = root.Lerp(top, i / 3.5f);
                    Seg(on, on + new Vector3(side * 0.02f, 0.04f, 0.02f) * s, 0.006f * s, 0.002f * s, Plain("c8b088"), 3);
                }
            }
        if (Has("crown")) k.Frustum(c + new Vector3(0, 0.05f, 0) * s, 0.04f * s, 0.045f * s, 0.025f * s, 6, Plain("f2c55a") with { A = MeshKit.Flag.Metal });
        if (Has("mane"))
            for (int i = 0; i < 7; i++)
            {
                float ang = (i / 6f - 0.5f) * 2.6f;
                Spike(c + new Vector3(Mathf.Sin(ang) * 0.05f, Mathf.Cos(ang) * 0.04f, -0.02f) * s, new Vector3(Mathf.Sin(ang), Mathf.Cos(ang), -0.8f), 0.025f * s, 0.07f * s, dark);
            }
    }

    // Legs, wings, tails ----------------------------------------------------------------------------------------

    /// <summary>A leg from hip down to the ground with a bend at the knee (forward or back), on one of the two gait sets.</summary>
    private void Leg(Vector3 hip, float set, float r, bool backKnee, Color c, Color foot, bool hoof = false, bool clawed = true)
    {
        Part(set, hip);
        var knee = new Vector3(hip.X * 1.05f, hip.Y * 0.5f, hip.Z + (backKnee ? -1 : 1) * hip.Y * 0.18f);
        var ground = new Vector3(hip.X * 1.08f, 0.0f, hip.Z);
        Seg(hip, knee, r, r * 0.75f, c);
        Seg(knee, ground + new Vector3(0, 0.02f, 0), r * 0.75f, r * 0.6f, c);
        if (hoof) k.Frustum(ground, r * 0.75f, r * 0.65f, 0.025f, 5, Plain("2a2420"));
        else
        {
            Ball(ground + new Vector3(0, 0.012f, 0.012f), new Vector3(r * 1.1f, r * 0.6f, r * 1.4f), foot);
            if (clawed) for (int i = -1; i <= 1; i++) Spike(ground + new Vector3(i * r * 0.6f, 0.008f, r * 1.2f), new Vector3(0, -0.3f, 1), r * 0.25f, r * 0.8f, claw);
        }
    }

    /// <summary>A wing on one side from its root on the back: bat (fingers and membrane), feather (layered vanes), insect (a pair of panes), flame or cloud.</summary>
    private void Wing(Vector3 root, int side, float span, string kind, Color c, Color edge)
    {
        Part(side < 0 ? WingR : WingL, root);
        float sx = side;
        switch (kind)
        {
            case "bat":
                {
                    var elbow = root + new Vector3(sx * span * 0.45f, span * 0.25f, -span * 0.1f);
                    var tip = root + new Vector3(sx * span, span * 0.35f, -span * 0.25f);
                    Seg(root, elbow, 0.018f * span, 0.012f * span, c, 4);
                    Seg(elbow, tip, 0.012f * span, 0.004f * span, c, 4);
                    var f1 = elbow + new Vector3(sx * span * 0.35f, -span * 0.2f, -span * 0.45f);
                    var f2 = elbow + new Vector3(sx * span * 0.1f, -span * 0.3f, -span * 0.5f);
                    Seg(elbow, f1, 0.008f * span, 0.003f * span, c, 3);
                    Seg(elbow, f2, 0.008f * span, 0.003f * span, c, 3);
                    var back = root + new Vector3(0, -span * 0.05f, -span * 0.35f);
                    foreach (var (p, q, r) in new[] { (root, elbow, back), (elbow, tip, f1), (elbow, f1, f2), (elbow, f2, back) })
                    {
                        k.Tri(p, q, r, edge);
                        k.Tri(p, r, q, edge);
                    }
                    break;
                }
            case "feather":
                for (int i = 0; i < 4; i++)
                {
                    float f = i / 3f;
                    var p = root + new Vector3(sx * span * (0.2f + 0.8f * f), span * (0.15f + 0.2f * f), -span * 0.1f * f);
                    var q = p + new Vector3(sx * span * 0.15f, -span * 0.05f, -span * (0.45f - 0.1f * f));
                    var r = root + new Vector3(sx * span * 0.15f * f, 0, -span * 0.25f);
                    var col = i % 2 == 0 ? c : edge;
                    k.Tri(r, p, q, col);
                    k.Tri(r, q, p, col);
                }
                Seg(root, root + new Vector3(sx * span, span * 0.35f, -span * 0.1f), 0.02f * span, 0.006f * span, c, 4);
                break;
            case "insect":
                foreach (float z in new[] { 0f, -0.18f })
                {
                    var b0 = root + new Vector3(0, 0, z * span);
                    var tip = b0 + new Vector3(sx * span, span * 0.12f, -span * 0.12f);
                    var pane = Glow(new Color(0.8f, 0.9f, 1f) * 0.35f);
                    k.Tri(b0, tip, b0 + new Vector3(sx * span * 0.6f, 0.02f, -span * 0.22f), pane);
                    k.Tri(b0, b0 + new Vector3(sx * span * 0.6f, 0.02f, -span * 0.22f), tip, pane);
                    k.Tri(b0, b0 + new Vector3(sx * span * 0.6f, 0.04f, span * 0.12f), tip, pane);
                    k.Tri(b0, tip, b0 + new Vector3(sx * span * 0.6f, 0.04f, span * 0.12f), pane);
                }
                break;
            case "flame":
                for (int i = 0; i < 5; i++)
                {
                    float f = i / 4f;
                    var p = root + new Vector3(sx * span * (0.2f + 0.8f * f), span * (0.3f - 0.1f * f), -span * 0.1f);
                    var fl = Ember(new Color(1f, 0.55f + 0.3f * f, 0.15f));
                    k.Tri(root, p, p + new Vector3(0, -span * 0.3f, -span * 0.2f), fl);
                    k.Tri(root, p + new Vector3(0, -span * 0.3f, -span * 0.2f), p, fl);
                }
                break;
        }
        Body();
    }

    /// <summary>A tail from root trailing back and down, tapering, with a club, a sting, a fin or a tuft at its end.</summary>
    private void Tail(Vector3 root, float len, float r, Color c, float droop = 0.35f, int segs = 4)
    {
        if (len <= 0) return;
        Part(TailPart, root);
        var p = root;
        for (int i = 1; i <= segs; i++)
        {
            float f = i / (float)segs;
            var q = root + new Vector3(0, -len * droop * f * f + (Has("stinger") ? len * 0.9f * f * f : 0), -len * f);
            Seg(p, q, r * (1 - 0.8f * (f - 1f / segs)), r * (1 - 0.8f * f), c);
            p = q;
        }
        if (Has("club")) Ball(p, new Vector3(r * 2, r * 1.5f, r * 2), horn, 0.3f);
        else if (Has("stinger")) Spike(p, new Vector3(0, 0.3f, 1), r * 0.8f, len * 0.3f, claw);
        else if (Has("fins")) foreach (int side in new[] { -1, 1 }) k.Tri(p, p + new Vector3(side * r * 3, r * 3, -r * 2), p + new Vector3(side * r, -r * 2, -r * 4), dark);
        else if (a.Plan == "quadruped" && a.Head is "ox" or "horse") Ball(p, new Vector3(r * 1.3f, r * 2.2f, r * 1.3f), dark, 0.3f);
        Body();
    }

    private void Spikes(Vector3 from, Vector3 to, int n, float size)
    {
        for (int i = 0; i < n; i++)
        {
            var at = from.Lerp(to, (i + 0.5f) / n);
            Spike(at, new Vector3(0, 1, -0.4f), size * 0.35f, size * (0.7f + 0.4f * R(40 + i)), horn);
        }
    }

    // Plans -----------------------------------------------------------------------------------------------------

    private void Quadruped()
    {
        string kind = a.Head;
        float bulk = (float)a.Bulk, leg = (float)a.Leg;
        float height = kind switch { "rat" or "lizard" or "croc" or "frog" or "snail" => 0.1f, "ox" => 0.24f, "horse" or "deer" => 0.27f, "bear" or "owlbear" => 0.2f, _ => 0.19f } * leg;
        float len = kind switch { "croc" or "lizard" => 0.24f, "horse" or "deer" => 0.19f, "ox" => 0.2f, "rat" => 0.13f, "frog" => 0.1f, _ => 0.17f } * Vary(1, 0.2f);
        float girth = kind switch { "ox" => 0.12f, "bear" or "owlbear" => 0.11f, "frog" => 0.1f, "rat" => 0.06f, _ => 0.08f } * bulk;
        float y = height + girth * 0.6f;
        if (kind == "snail")
        {
            Ball(new Vector3(0, 0.04f, 0.02f), new Vector3(0.08f, 0.04f, 0.2f), belly);
            Ball(new Vector3(0, 0.15f, -0.06f), new Vector3(0.12f, 0.12f, 0.12f), hide, 0.1f, dark);
            for (int i = 0; i < 3; i++) k.Frustum(new Vector3(0, 0.15f + i * 0.02f, -0.06f), 0.1f - i * 0.03f, 0.08f - i * 0.03f, 0.03f, 8, i % 2 == 0 ? dark : hide);
            Head("snail", new Vector3(0, 0.06f, 0.2f), 1, Neck, new Vector3(0, 0.05f, 0.15f));
            if (Has("tentacles")) for (int side = -1; side <= 1; side += 2) { Part(Tentacle, new Vector3(side * 0.05f, 0.08f, 0.15f)); Seg(new Vector3(side * 0.05f, 0.08f, 0.15f), new Vector3(side * 0.1f, 0.12f, 0.3f), 0.015f, 0.005f, belly); Body(); }
            return;
        }
        // The body: chest and haunches, the chest the bigger on hunters and the haunches on grazers.
        var chest = new Vector3(0, y + girth * 0.1f, len * 0.5f);
        var rump = new Vector3(0, y, -len * 0.5f);
        Ball(chest, new Vector3(girth * 1.05f, girth * 1.05f, girth * 1.2f), hide, 0.12f, belly);
        Ball(rump, new Vector3(girth * 0.95f, girth * 0.95f, girth * 1.1f), hide, 0.12f, belly);
        Ball((chest + rump) / 2 + new Vector3(0, -girth * 0.1f, 0), new Vector3(girth * 0.9f, girth * 0.85f, len * 0.6f), hide, 0.08f, belly);
        if (Has("shell"))
        {
            Ball((chest + rump) / 2 + new Vector3(0, girth * 0.35f, 0), new Vector3(girth * 1.6f, girth * 1.0f, len * 1.0f), dark, 0.05f, hide);
            for (int i = 0; i < 5; i++) k.Box((chest + rump) / 2 + new Vector3((i - 2) * girth * 0.4f, girth * 1.25f - Mathf.Abs(i - 2) * girth * 0.15f, 0), new Vector3(girth * 0.3f, 0.01f, len * 0.5f), horn);
        }
        if (Has("spikes")) Spikes(chest + new Vector3(0, girth * 0.9f, 0), rump + new Vector3(0, girth * 0.9f, 0), 5, girth * 0.7f);
        if (Has("barding")) k.Frustum((chest + rump) / 2 + new Vector3(0, -girth * 0.1f, 0), girth * 1.15f, girth * 1.1f, girth * 0.4f, 8, Mark());
        else if (a.Roam is "march" or "pack") k.Frustum(chest + new Vector3(0, girth * 0.3f, girth * 0.3f), girth * 0.75f, girth * 0.7f, girth * 0.25f, 8, Mark(0.9f));
        if (Has("flames")) Flames(chest + new Vector3(0, girth, 0), rump + new Vector3(0, girth, 0), girth);
        // Legs: diagonal pairs on the same set, so it trots.
        bool hoof = Has("hooves");
        float lr = girth * 0.32f * (kind == "ox" ? 1.4f : 1);
        foreach (int side in new[] { -1, 1 })
        {
            Leg(new Vector3(side * girth * 0.6f, y - girth * 0.2f, len * 0.55f), side < 0 ? LegA : LegB, lr, false, hide, dark, hoof, !hoof);
            Leg(new Vector3(side * girth * 0.6f, y - girth * 0.2f, -len * 0.55f), side < 0 ? LegB : LegA, lr * 1.1f, true, hide, dark, hoof, !hoof);
        }
        if (kind == "frog")
            foreach (int side in new[] { -1, 1 })
            {
                Part(side < 0 ? LegA : LegB, new Vector3(side * 0.08f, 0.1f, -0.08f));
                Seg(new Vector3(side * 0.08f, 0.1f, -0.08f), new Vector3(side * 0.16f, 0.12f, 0.0f), 0.03f, 0.025f, hide);
                Seg(new Vector3(side * 0.16f, 0.12f, 0.0f), new Vector3(side * 0.13f, 0.01f, -0.06f), 0.025f, 0.02f, hide);
                Body();
            }
        Tail(rump + new Vector3(0, girth * 0.3f, -girth * 0.8f), (float)a.Tail * len * (kind is "croc" or "lizard" ? 1.6f : 0.9f), girth * (kind is "croc" or "lizard" ? 0.6f : 0.22f), hide);
        // Neck and head(s).
        float neck = (float)a.Neck;
        float hs = Mathf.Clamp(girth / 0.08f, 0.8f, 1.6f) * (kind is "rat" ? 0.9f : 1);
        int heads = a.Heads;
        for (int i = 0; i < heads; i++)
        {
            float off = heads == 1 ? 0 : (i / (float)(heads - 1) - 0.5f) * girth * 1.6f;
            var root = chest + new Vector3(off * 0.5f, girth * 0.4f, girth * 0.8f);
            var top = root + new Vector3(off, neck * 0.2f + 0.02f + (kind == "hydra" ? 0.12f : 0), girth * 0.6f + neck * 0.12f);
            string hk = kind switch { "beast" => "wolf", "hydra" => "snake", "griffon" => "griffon", _ => kind };
            if (a.Id.Contains("chimera") && i == 0) hk = "goat";
            if (a.Id.Contains("chimera") && i == 2) hk = "dragon";
            Part(Neck, root);
            Seg(root, top, girth * 0.5f * (kind == "hydra" ? 0.5f : 1), girth * 0.4f * (kind == "hydra" ? 0.6f : 1), hide);
            Head(hk, top + new Vector3(0, 0.02f, 0.03f) * hs, hs, Neck, root);
            Body();
        }
        if (a.Wings != "") foreach (int side in new[] { -1, 1 }) Wing(chest + new Vector3(side * girth * 0.6f, girth * 0.7f, -0.02f), side, 0.3f * Mathf.Clamp(girth / 0.08f, 0.8f, 1.5f), a.Wings, hide, a.Wings == "bat" ? dark : belly);
    }

    private void Flames(Vector3 from, Vector3 to, float size)
    {
        for (int i = 0; i < 6; i++)
        {
            var at = from.Lerp(to, i / 5f);
            var fl = Ember(new Color(1f, 0.4f + 0.4f * R(60 + i), 0.12f));
            k.Frustum(at, size * 0.25f, 0, size * (0.6f + 0.6f * R(70 + i)), 4, fl);
        }
    }

    private void Theropod()
    {
        float bulk = (float)a.Bulk;
        bool beak = a.Head == "beak";
        float hip = (beak ? 0.2f : 0.22f) * (float)a.Leg;
        float girth = (beak ? 0.08f : 0.1f) * bulk;
        var body = new Vector3(0, hip + girth * 0.5f, 0);
        Ball(body, new Vector3(girth, girth * 0.95f, girth * 1.5f), hide, 0.1f, belly);
        if (Has("shell")) Ball(body + new Vector3(0, girth * 0.5f, -girth * 0.2f), new Vector3(girth * 1.2f, girth * 0.8f, girth * 1.6f), dark, 0.1f, hide);
        if (Has("spikes")) Spikes(body + new Vector3(0, girth, girth), body + new Vector3(0, girth * 0.9f, -girth * 1.4f), 6, girth * 0.6f);
        foreach (int side in new[] { -1, 1 })
        {
            Part(side < 0 ? LegA : LegB, new Vector3(side * girth * 0.6f, hip, 0));
            var knee = new Vector3(side * girth * 0.75f, hip * 0.55f, girth * 0.5f);
            var ankle = new Vector3(side * girth * 0.7f, hip * 0.2f, -girth * 0.4f);
            Seg(new Vector3(side * girth * 0.6f, hip, 0), knee, girth * 0.45f, girth * 0.28f, hide);
            Seg(knee, ankle, girth * 0.28f, girth * 0.18f, hide);
            Seg(ankle, new Vector3(side * girth * 0.7f, 0.01f, 0.02f), girth * 0.18f, girth * 0.12f, dark);
            for (int i = -1; i <= 1; i++) Spike(new Vector3(side * girth * 0.7f + i * girth * 0.12f, 0.01f, 0.02f), new Vector3(i * 0.3f, 0, 1), girth * 0.07f, girth * 0.5f, claw);
            // Little arms.
            Part(side < 0 ? ClawR : ClawL, body + new Vector3(side * girth * 0.7f, 0, girth * 1.1f));
            if (!beak) Seg(body + new Vector3(side * girth * 0.7f, 0, girth * 1.1f), body + new Vector3(side * girth * 0.8f, -girth * 0.6f, girth * 1.5f), girth * 0.15f, girth * 0.08f, hide, 4);
        }
        Body();
        Tail(body + new Vector3(0, 0, -girth * 1.2f), (float)a.Tail * girth * 5, girth * 0.55f, hide, 0.1f, 5);
        var neckRoot = body + new Vector3(0, girth * 0.4f, girth * 1.2f);
        var headAt = neckRoot + new Vector3(0, girth * (beak ? 1.6f : 0.9f), girth * 0.7f);
        Part(Neck, neckRoot);
        Seg(neckRoot, headAt, girth * 0.55f, girth * 0.4f, hide);
        Head(beak ? "griffon" : a.Head, headAt, girth / 0.055f * (beak ? 0.9f : 0.8f), Neck, neckRoot);
        Body();
        if (a.Wings != "") foreach (int side in new[] { -1, 1 }) Wing(body + new Vector3(side * girth * 0.6f, girth * 0.6f, 0), side, girth * 3, a.Wings, hide, dark);
    }

    private void Bird()
    {
        float g = 0.07f * (float)a.Bulk;
        bool bat = a.Head is "bat";
        float hip = bat ? 0.06f : 0.12f;
        var body = new Vector3(0, hip + g, 0);
        Ball(body, new Vector3(g, g * 1.05f, g * 1.4f), hide, 0.08f, belly);
        Ball(body + new Vector3(0, -g * 0.2f, g * 0.6f), new Vector3(g * 0.8f, g * 0.8f, g * 0.7f), belly);
        foreach (int side in new[] { -1, 1 })
        {
            Part(side < 0 ? LegA : LegB, new Vector3(side * g * 0.4f, hip + g * 0.2f, 0));
            Seg(new Vector3(side * g * 0.4f, hip + g * 0.2f, 0), new Vector3(side * g * 0.45f, 0.01f, g * 0.1f), g * 0.12f, g * 0.08f, Plain(bat ? "3a2a2a" : "d8a830"), 4);
            for (int i = -1; i <= 1; i++) Spike(new Vector3(side * g * 0.45f, 0.01f, g * 0.1f), new Vector3(i * 0.5f, 0, 1), g * 0.04f, g * 0.4f, claw);
        }
        Body();
        // A fan of tail feathers.
        Part(TailPart, body + new Vector3(0, 0, -g * 1.2f));
        if (!bat)
            for (int i = -2; i <= 2; i++)
            {
                var r = body + new Vector3(0, 0, -g * 1.2f);
                k.Tri(r, r + new Vector3(i * g * 0.25f - g * 0.12f, -g * 0.1f, -g * 1.4f), r + new Vector3(i * g * 0.25f + g * 0.12f, -g * 0.1f, -g * 1.4f), i % 2 == 0 ? dark : hide);
                k.Tri(r, r + new Vector3(i * g * 0.25f + g * 0.12f, -g * 0.1f, -g * 1.4f), r + new Vector3(i * g * 0.25f - g * 0.12f, -g * 0.1f, -g * 1.4f), i % 2 == 0 ? dark : hide);
            }
        Body();
        var neckRoot = body + new Vector3(0, g * 0.6f, g * 0.9f);
        var headAt = neckRoot + new Vector3(0, g * (0.4f + (float)a.Neck), g * 0.3f);
        Part(Neck, neckRoot);
        if (a.Neck > 0.3) Seg(neckRoot, headAt, g * 0.4f, g * 0.3f, a.Head == "vulture" ? Plain("c89a8a") : hide);
        Head(a.Head, headAt, g / 0.05f * 0.8f, Neck, neckRoot);
        Body();
        if (a.Head == "phoenix") Flames(body + new Vector3(0, g, g), body + new Vector3(0, g * 0.5f, -g * 2), g);
        foreach (int side in new[] { -1, 1 }) Wing(body + new Vector3(side * g * 0.7f, g * 0.6f, g * 0.3f), side, g * 5f, a.Wings == "" ? "feather" : a.Wings, hide, a.Wings == "bat" ? dark : belly);
    }

    private void Dragon()
    {
        float g = 0.1f * (float)a.Bulk;
        bool wyvern = a.Legs == 2;
        float hip = 0.17f * (float)a.Leg;
        var chest = new Vector3(0, hip + g * 0.6f, g * 0.9f);
        var rump = new Vector3(0, hip + g * 0.5f, -g * 0.9f);
        Ball(chest, new Vector3(g * 1.1f, g * 1.05f, g * 1.3f), hide, 0.1f, belly);
        Ball(rump, new Vector3(g, g, g * 1.2f), hide, 0.1f, belly);
        Ball((chest + rump) / 2 + new Vector3(0, -g * 0.25f, 0), new Vector3(g * 0.75f, g * 0.6f, g * 1.6f), belly);
        Spikes(chest + new Vector3(0, g * 0.95f, g * 0.3f), rump + new Vector3(0, g * 0.9f, -g * 0.6f), Has("spikes") ? 8 : 5, g * (Has("spikes") ? 0.8f : 0.5f));
        foreach (int side in new[] { -1, 1 })
        {
            if (!wyvern) Leg(new Vector3(side * g * 0.75f, hip, chest.Z + g * 0.2f), side < 0 ? LegA : LegB, g * 0.35f, false, hide, dark);
            Leg(new Vector3(side * g * 0.8f, hip, rump.Z), side < 0 ? LegB : LegA, g * 0.42f, true, hide, dark);
        }
        Tail(rump + new Vector3(0, 0, -g * 1.0f), (float)a.Tail * g * 6, g * 0.55f, hide, 0.25f, 6);
        for (int i = 0; i < a.Heads; i++)
        {
            float off = a.Heads == 1 ? 0 : (i / (float)(a.Heads - 1) - 0.5f) * g * 1.8f;
            var root = chest + new Vector3(off * 0.4f, g * 0.6f, g * 1.0f);
            var mid = root + new Vector3(off * 0.6f, g * (1.0f + (float)a.Neck), g * 0.6f);
            var top = mid + new Vector3(off * 0.2f, g * 0.6f, g * 0.6f);
            Part(Neck, root);
            Seg(root, mid, g * 0.55f, g * 0.42f, hide, 6);
            Seg(mid, top, g * 0.42f, g * 0.35f, hide, 6);
            Head(a.Head is "pseudo" ? "dragon" : a.Head, top + new Vector3(0, 0, g * 0.3f), g / 0.06f, Neck, root);
            Body();
        }
        foreach (int side in new[] { -1, 1 }) Wing(chest + new Vector3(side * g * 0.7f, g * 0.9f, -g * 0.1f), side, g * (wyvern ? 5f : 4.5f), a.Wings == "" ? "bat" : a.Wings, hide, dark);
        if (a.Material == "fire") Flames(chest + new Vector3(0, g, 0), rump + new Vector3(0, g, 0), g * 0.8f);
    }

    private void Serpent()
    {
        float g = 0.05f * (float)a.Bulk * (a.Head == "dragon" ? 1.4f : 1);
        int n = 9;
        // A body that lies in an S on the ground and rises to the head.
        var pts = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1);
            pts[i] = new Vector3(Mathf.Sin(f * 5.5f + R(2) * 2) * g * 2.2f * (1 - f * 0.5f), g * 0.8f, -0.45f + f * 0.55f);
        }
        for (int i = 0; i < n - 1; i++)
        {
            float f = i / (float)(n - 1);
            float r0 = g * (0.5f + 0.6f * Mathf.Sin(Mathf.Pi * Mathf.Min(f + 0.15f, 1)));
            float r1 = g * (0.5f + 0.6f * Mathf.Sin(Mathf.Pi * Mathf.Min(f + 0.15f + 1f / n, 1)));
            Seg(pts[i], pts[i + 1], r0 + g * 0.05f, r1 + g * 0.05f, i % 2 == 0 ? hide : dark, 6);
            Ball(pts[i + 1], new Vector3(r1, r1 * 0.9f, r1), hide);
        }
        if (Has("spikes") || a.Head == "dragon") Spikes(pts[1] + new Vector3(0, g, 0), pts[n - 1] + new Vector3(0, g, 0), 7, g * 0.9f);
        if (Has("bones") || a.Material == "bone") for (int i = 1; i < n; i += 2) k.Frustum(pts[i] - new Vector3(0, g * 0.9f, 0), g * 1.2f, g * 1.2f, g * 0.2f, 6, Plain("e6dcc0"));
        // Legs along the body.
        int legs = a.Legs;
        for (int i = 0; i < legs / 2; i++)
        {
            var at = pts[Mathf.Clamp(n - 2 - i * Mathf.Max(1, (n - 2) / Math.Max(1, legs / 2)), 1, n - 1)];
            foreach (int side in new[] { -1, 1 }) Leg(at + new Vector3(side * g * 0.8f, -g * 0.1f, 0), (i + (side < 0 ? 0 : 1)) % 2 == 0 ? LegA : LegB, g * 0.35f, i % 2 == 1, hide, dark);
        }
        Body();
        // The tail tip.
        Part(TailPart, pts[0]);
        Seg(pts[0], pts[0] + new Vector3(g, -g * 0.3f, -g * 3), g * 0.5f, 0.002f, hide);
        if (Has("stinger")) Spike(pts[0] + new Vector3(g, -g * 0.3f, -g * 3), new Vector3(0, 1, -0.3f), g * 0.4f, g * 1.5f, claw);
        Body();
        // The raised neck and head; a naga's is a person's.
        var root = pts[n - 1];
        var top = root + new Vector3(0, g * 4.5f, g * 1.5f);
        Part(Neck, root);
        Seg(root, top, g * 0.85f, g * 0.6f, hide, 6);
        if (a.Head == "naga")
        {
            Ball(top + new Vector3(0, g * 0.8f, 0), new Vector3(g * 1.1f, g * 1.2f, g * 1.1f), belly);
            Eyes(top + new Vector3(0, g * 0.9f, g * 1.0f), g * 0.4f, g * 0.18f, 2);
            Ball(top + new Vector3(0, g * 1.4f, -g * 0.3f), new Vector3(g * 1.2f, g * 0.7f, g * 1.2f), dark);
            if (Has("crown")) k.Frustum(top + new Vector3(0, g * 1.8f, 0), g * 0.8f, g * 0.9f, g * 0.5f, 6, Plain("f2c55a") with { A = MeshKit.Flag.Metal });
        }
        else Head(a.Head is "couatl" ? "couatl" : a.Head, top + new Vector3(0, g * 0.3f, g * 0.6f), g / 0.035f, Neck, root);
        Body();
        if (a.Wings != "") foreach (int side in new[] { -1, 1 }) Wing(top - new Vector3(0, g * 1.5f, g * 0.5f) + new Vector3(side * g, 0, 0), side, g * 6, a.Wings, hide, belly);
    }

    private void Worm()
    {
        float g = 0.07f * (float)a.Bulk;
        // Coils out of the ground rising to a great round maw.
        int n = 7;
        Vector3 prev = new(0, -g, -g * 4);
        for (int i = 0; i <= n; i++)
        {
            float f = i / (float)n;
            var p = new Vector3(Mathf.Sin(f * 3) * g * 0.8f, g * (0.3f + 3.2f * f * f), -g * 4 + f * g * 5);
            Seg(prev, p, g * (1.1f - 0.1f * f), g * (1.1f - 0.1f * f), i % 2 == 0 ? hide : belly, 8);
            Ball(p, new Vector3(g * 1.12f, g * 1.12f, g * 1.12f), i % 2 == 0 ? hide : belly);
            prev = p;
        }
        Part(Neck, prev);
        var mouthAt = prev + new Vector3(0, g * 0.6f, g * 0.5f);
        k.Frustum(mouthAt - new Vector3(0, g * 0.4f, 0), g * 1.1f, g * 1.3f, g * 0.4f, 10, dark, mouth);
        string h = a.Head;
        int teethN = h == "maw" ? 14 : 10;
        for (int i = 0; i < teethN; i++)
        {
            float ang = i * Mathf.Tau / teethN;
            Spike(mouthAt + new Vector3(Mathf.Cos(ang) * g * 1.15f, 0, Mathf.Sin(ang) * g * 1.15f), new Vector3(-Mathf.Cos(ang), 0.6f, -Mathf.Sin(ang)), g * 0.12f, g * 0.45f, teeth);
        }
        if (h == "grick")
            for (int i = 0; i < 4; i++)
            {
                float ang = i * Mathf.Tau / 4 + 0.4f;
                var r = mouthAt + new Vector3(Mathf.Cos(ang) * g, 0, Mathf.Sin(ang) * g);
                Part(Tentacle, r);
                Seg(r, r + new Vector3(Mathf.Cos(ang) * g * 2, g * 2, Mathf.Sin(ang) * g * 2), g * 0.25f, g * 0.05f, hide);
            }
        if (h == "leech") Eyes(mouthAt + new Vector3(0, g * 0.2f, g), g * 0.5f, g * 0.15f, 2);
        if (h == "grub") for (int i = 0; i < 8; i++) Spike(new Vector3(0, g * (0.5f + i * 0.3f), -g * 3.5f + i * 0.6f * g), new Vector3(R(80 + i) - 0.5f, 1, 0), g * 0.1f, g * 0.6f, claw);
        Body();
        if (Has("stinger")) { Part(TailPart, new Vector3(0, 0, -g * 4)); Spike(new Vector3(0, g * 0.3f, -g * 4.5f), new Vector3(0, 1.2f, -0.8f), g * 0.4f, g * 2, claw); Body(); }
    }

    private void Insect()
    {
        float g = 0.06f * (float)a.Bulk;
        string h = a.Head;
        bool centipede = h == "centipede";
        bool flyer = a.Wings != "";
        float hip = flyer ? 0.14f : 0.08f;
        int segs = centipede ? 7 : 3;
        float span = centipede ? g * 1.3f : g * 1.6f;
        var head = new Vector3(0, hip + g * 0.4f, span * (segs - 1) * 0.5f + g);
        for (int i = 0; i < segs; i++)
        {
            float z = (i - (segs - 1) / 2f) * span;
            float r = centipede ? g * 0.75f : i == 0 ? g * 1.2f : i == 1 ? g * 0.8f : g * 0.7f;
            if (h == "wasp" && i == 0) r = g * 0.9f;
            var at = new Vector3(0, hip + g * (i == 0 && !centipede ? 0.6f : 0.4f), z - (i == 0 && !centipede ? g * 0.4f : 0));
            Ball(at, new Vector3(r, r * (centipede ? 0.6f : 0.85f), r * 1.2f), i % 2 == 0 ? hide : dark, 0.05f, belly);
            if (h == "wasp" && i == 0 || Has("stripes")) for (int s = 0; s < 3; s++) k.Frustum(at + new Vector3(0, -r * 0.5f, (s - 1) * r * 0.5f), r * 0.95f, r * 0.95f, r * 0.15f, 8, Plain("1a1410"));
        }
        if (Has("shell") || h == "beetle") Ball(new Vector3(0, hip + g * 1.0f, -span * 0.3f), new Vector3(g * 1.3f, g * 0.7f, span * 1.5f), dark, 0.02f, hide);
        // Legs: alternating tripods.
        int pairs = centipede ? 12 / 2 * 1 + 3 : 3;
        for (int i = 0; i < pairs; i++)
        {
            float z = ((i + 0.5f) / pairs - 0.5f) * span * (segs - 1) * 1.1f;
            foreach (int side in new[] { -1, 1 })
            {
                var hipAt = new Vector3(side * g * 0.6f, hip + g * 0.3f, z);
                Part((i + (side < 0 ? 0 : 1)) % 2 == 0 ? LegA : LegB, hipAt);
                var knee = hipAt + new Vector3(side * g * 1.5f, g * 1.2f, z * 0.15f);
                var foot = new Vector3(side * g * 2.6f, 0, z * 1.25f);
                Seg(hipAt, knee, g * 0.14f, g * 0.1f, dark, 4);
                Seg(knee, foot, g * 0.1f, g * 0.03f, dark, 4);
            }
        }
        // The head: mandibles on the jaw, antennae that wave.
        Part(Neck, head - new Vector3(0, 0, g * 0.6f));
        Ball(head, new Vector3(g * 0.7f, g * 0.6f, g * 0.6f), hide);
        Eyes(head + new Vector3(0, g * 0.2f, g * 0.45f), g * 0.4f, g * 0.15f, Math.Max(2, a.Eyes));
        if (h == "stirge" || h == "rust") Spike(head + new Vector3(0, 0, g * 0.5f), new Vector3(0, -0.4f, 1), g * 0.12f, g * 1.4f, dark);
        Part(Jaw, head + new Vector3(0, -g * 0.2f, g * 0.4f));
        if (h is "ankheg" or "beetle" or "centipede" or "engine" or "scorpion")
            foreach (int side in new[] { -1, 1 }) Spike(head + new Vector3(side * g * 0.35f, -g * 0.2f, g * 0.5f), new Vector3(-side * 0.5f, 0, 1), g * 0.16f, g * 0.9f, claw);
        foreach (int side in new[] { -1, 1 })
        {
            Part(Tentacle, head + new Vector3(side * g * 0.2f, g * 0.4f, g * 0.4f));
            Seg(head + new Vector3(side * g * 0.2f, g * 0.4f, g * 0.4f), head + new Vector3(side * g * (h == "moth" ? 1.0f : 0.7f), g * 1.8f, g * 1.4f), g * 0.05f, g * 0.02f, dark, 3);
            if (h is "moth") k.Tri(head + new Vector3(side * g * 0.4f, g * 1.0f, g * 0.8f), head + new Vector3(side * g * 1.0f, g * 1.8f, g * 1.4f), head + new Vector3(side * g * 0.4f, g * 1.6f, g * 1.0f), dark);
        }
        if (Has("pincers") || h == "scorpion")
            foreach (int side in new[] { -1, 1 })
            {
                var sh = head + new Vector3(side * g * 0.6f, -g * 0.1f, -g * 0.2f);
                Part(side < 0 ? ClawR : ClawL, sh);
                var el = sh + new Vector3(side * g * 1.2f, g * 0.2f, g * 0.9f);
                Seg(sh, el, g * 0.2f, g * 0.16f, hide, 4);
                Seg(el, el + new Vector3(-side * g * 0.2f, 0, g * 1.0f), g * 0.22f, g * 0.15f, hide, 5);
                Spike(el + new Vector3(-side * g * 0.2f, 0, g * 1.0f), new Vector3(side * 0.3f, 0, 1), g * 0.12f, g * 0.6f, claw);
                Spike(el + new Vector3(-side * g * 0.1f, 0, g * 1.0f), new Vector3(-side * 0.5f, 0, 1), g * 0.1f, g * 0.5f, claw);
            }
        Body();
        if (h == "scorpion" || Has("stinger"))
        {
            var root = new Vector3(0, hip + g * 0.5f, -span * (segs - 1) * 0.5f - g * 0.6f);
            Part(TailPart, root);
            var p = root;
            for (int i = 1; i <= 5; i++)
            {
                float f = i / 5f;
                var q = root + new Vector3(0, g * 3.2f * Mathf.Sin(f * 2.2f), -g * 1.8f * Mathf.Cos(f * 2.6f) * (1 - f) - g * 0.3f * f);
                Seg(p, q, g * 0.32f * (1.1f - f * 0.5f), g * 0.3f * (1.1f - f * 0.5f), i % 2 == 0 ? hide : dark, 5);
                p = q;
            }
            Spike(p, new Vector3(0, 0.2f, 1), g * 0.2f, g * 0.9f, Plain("c83a3a"));
            Body();
        }
        if (flyer) foreach (int side in new[] { -1, 1 }) Wing(new Vector3(side * g * 0.4f, hip + g * 1.0f, g * 0.3f), side, g * (h == "moth" ? 4.0f : 2.8f), h == "moth" ? "feather" : "insect", h == "moth" ? hide : dark, belly);
    }

    private void Spider()
    {
        float g = 0.07f * (float)a.Bulk;
        string h = a.Head;
        float hip = 0.13f;
        var abdomen = new Vector3(0, hip + g * 0.9f, -g * 1.6f);
        var thorax = new Vector3(0, hip + g * 0.3f, g * 0.2f);
        Ball(abdomen, new Vector3(g * 1.5f, g * 1.3f, g * 1.7f), hide, 0.12f, dark);
        Ball(thorax, new Vector3(g * 0.9f, g * 0.7f, g * 0.9f), dark);
        // A marking on the back: an hourglass, a skull, a band of the host's colour.
        k.Blob(abdomen + new Vector3(0, g * 1.25f, 0), new Vector3(g * 0.55f, g * 0.15f, g * 0.7f), h == "phase" ? Glow(new Color("8a6aff")) : Mark(1.1f));
        if (h == "hunter") for (int i = 0; i < 3; i++) k.Box(abdomen + new Vector3(0, g * 1.1f, (i - 1) * g * 0.6f), new Vector3(g * 1.6f, g * 0.2f, g * 0.15f), belly);
        if (h == "weaver") for (int i = 0; i < 5; i++) Seg(abdomen + new Vector3(0, -g * 0.5f, -g * 1.5f), abdomen + new Vector3((R(90 + i) - 0.5f) * g * 6, -abdomen.Y, -g * (3 + 2 * R(95 + i))), g * 0.03f, g * 0.02f, Plain("e8e8e8"), 3);
        Part(Neck, thorax);
        Ball(thorax + new Vector3(0, 0, g * 0.9f), new Vector3(g * 0.55f, g * 0.5f, g * 0.5f), dark);
        Eyes(thorax + new Vector3(0, g * 0.25f, g * 1.3f), g * 0.35f, g * 0.12f, 8);
        Part(Jaw, thorax + new Vector3(0, -g * 0.2f, g * 1.2f));
        foreach (int side in new[] { -1, 1 }) Spike(thorax + new Vector3(side * g * 0.18f, -g * 0.2f, g * 1.3f), new Vector3(0, -1, 0.4f), g * 0.12f, g * 0.6f, claw);
        if (h == "woman")
        {
            Part(Neck, thorax);
            Seg(thorax + new Vector3(0, g * 0.5f, g * 0.5f), thorax + new Vector3(0, g * 2.6f, g * 0.9f), g * 0.5f, g * 0.45f, belly);
            Ball(thorax + new Vector3(0, g * 3.1f, g * 1.0f), new Vector3(g * 0.5f, g * 0.55f, g * 0.5f), belly);
            Ball(thorax + new Vector3(0, g * 3.2f, g * 0.8f), new Vector3(g * 0.6f, g * 0.65f, g * 0.55f), Plain("1a1418"));
            Eyes(thorax + new Vector3(0, g * 3.1f, g * 1.45f), g * 0.2f, g * 0.1f, 2);
        }
        int legs = 8;
        for (int i = 0; i < legs / 2; i++)
            foreach (int side in new[] { -1, 1 })
            {
                float z = (0.45f - i * 0.3f) * g * 2;
                var hipAt = thorax + new Vector3(side * g * 0.6f, 0, z * 0.4f);
                Part((i + (side < 0 ? 0 : 1)) % 2 == 0 ? LegA : LegB, hipAt);
                var knee = hipAt + new Vector3(side * g * 2.2f, g * 2.0f, z * 1.2f);
                var foot = new Vector3(side * g * 3.8f, 0, z * 2.0f + g * 0.2f);
                Seg(hipAt, knee, g * 0.2f, g * 0.16f, dark, 5);
                Seg(knee, foot, g * 0.16f, g * 0.05f, dark, 5);
                if (h == "dancer") Ball(knee, new Vector3(g * 0.25f, g * 0.25f, g * 0.25f), Mark());
            }
        Body();
    }

    private void Crab()
    {
        float g = 0.07f * (float)a.Bulk;
        float hip = 0.1f;
        var shell = new Vector3(0, hip + g * 0.6f, 0);
        Ball(shell, new Vector3(g * 2.0f, g * 0.8f, g * 1.5f), hide, 0.08f, belly);
        foreach (int side in new[] { -1, 1 })
        {
            for (int i = 0; i < 3; i++)
            {
                var hipAt = shell + new Vector3(side * g * 1.6f, -g * 0.2f, (0.6f - i * 0.6f) * g);
                Part((i + (side < 0 ? 0 : 1)) % 2 == 0 ? LegA : LegB, hipAt);
                var knee = hipAt + new Vector3(side * g * 1.0f, g * 0.6f, 0);
                Seg(hipAt, knee, g * 0.18f, g * 0.15f, hide, 4);
                Seg(knee, new Vector3(side * g * 3.2f, 0, (0.8f - i * 0.8f) * g), g * 0.15f, g * 0.04f, hide, 4);
            }
            var sh = shell + new Vector3(side * g * 1.4f, 0, g * 1.0f);
            Part(side < 0 ? ClawR : ClawL, sh);
            var el = sh + new Vector3(side * g * 0.8f, g * 0.4f, g * 1.0f);
            Seg(sh, el, g * 0.3f, g * 0.25f, hide, 5);
            Ball(el + new Vector3(0, 0, g * 0.7f), new Vector3(g * 0.55f, g * 0.45f, g * 0.8f), dark);
            Spike(el + new Vector3(side * g * 0.2f, 0, g * 1.3f), new Vector3(-side * 0.3f, 0, 1), g * 0.25f, g * 0.9f, dark);
            Spike(el + new Vector3(-side * g * 0.2f, 0, g * 1.2f), new Vector3(side * 0.3f, 0, 1), g * 0.18f, g * 0.6f, claw);
            Part(Tentacle, shell + new Vector3(side * g * 0.4f, g * 0.5f, g * 1.2f));
            Seg(shell + new Vector3(side * g * 0.4f, g * 0.5f, g * 1.2f), shell + new Vector3(side * g * 0.5f, g * 1.3f, g * 1.5f), g * 0.06f, g * 0.05f, hide, 3);
            k.Box(shell + new Vector3(side * g * 0.5f, g * 1.3f, g * 1.5f), new Vector3(g * 0.2f, g * 0.2f, g * 0.2f), eye);
        }
        if (a.Head == "tentacles")
            for (int i = 0; i < 4; i++)
            {
                var r = shell + new Vector3((i - 1.5f) * g * 0.3f, -g * 0.1f, g * 1.4f);
                Part(Tentacle, r);
                Seg(r, r + new Vector3((i - 1.5f) * g * 0.4f, -g * 0.6f, g * 0.9f), g * 0.1f, g * 0.03f, Plain("c87a8a"), 4);
            }
        Body();
        if (a.Material == "crystal" || a.Id.Contains("salt")) for (int i = 0; i < 5; i++) Spike(shell + new Vector3((R(100 + i) - 0.5f) * g * 3, g * 0.6f, (R(110 + i) - 0.5f) * g * 2), new Vector3(R(120 + i) - 0.5f, 1, 0), g * 0.2f, g * 0.8f, Plain("f0f0e8") with { A = MeshKit.Flag.Metal });
    }

    private void Ooze()
    {
        float g = 0.12f * (float)a.Bulk;
        string h = a.Head;
        var slime = a.Material == "slime" ? hide with { A = MeshKit.Flag.Magic } : hide;
        slime = h == "pudding" || a.Id.Contains("black") ? Plain(a.Hide) : (new Color("#" + a.Hide) * 0.55f) with { A = MeshKit.Flag.Magic };
        switch (h)
        {
            case "cube":
                k.Box(new Vector3(0, 0, 0), new Vector3(g * 2.4f, g * 2.4f, g * 2.4f), slime);
                // What it has swallowed: a skull, a sword, bones.
                Ball(new Vector3(-g * 0.4f, g * 1.0f, g * 0.2f), new Vector3(g * 0.2f, g * 0.22f, g * 0.2f), Plain("e6dcc0"));
                Seg(new Vector3(g * 0.2f, g * 0.4f, -g * 0.3f), new Vector3(g * 0.6f, g * 1.6f, g * 0.4f), g * 0.03f, g * 0.02f, Plain("c9d1da") with { A = MeshKit.Flag.Metal }, 4);
                Seg(new Vector3(-g * 0.6f, g * 0.5f, -g * 0.5f), new Vector3(-g * 0.1f, g * 0.6f, -g * 0.7f), g * 0.04f, g * 0.04f, Plain("e6dcc0"), 4);
                Part(ClawR, new Vector3(0, g * 1.2f, g * 1.2f));
                Ball(new Vector3(0, g * 1.2f, g * 1.3f), new Vector3(g * 0.5f, g * 0.4f, g * 0.3f), slime);
                Body();
                return;
            case "mouths":
                Ball(new Vector3(0, g * 0.6f, 0), new Vector3(g * 1.4f, g * 0.8f, g * 1.4f), hide, 0.35f, dark);
                for (int i = 0; i < 9; i++)
                {
                    float ang = i * 2.4f, up = 0.3f + 0.6f * R(130 + i);
                    var at = new Vector3(Mathf.Cos(ang) * g * 1.2f * (1 - up * 0.5f), g * (0.4f + up * 0.9f), Mathf.Sin(ang) * g * 1.2f * (1 - up * 0.5f));
                    if (i % 2 == 0) { k.Box(at, new Vector3(g * 0.2f, g * 0.2f, g * 0.2f), eye); }
                    else { Ball(at, new Vector3(g * 0.25f, g * 0.12f, g * 0.25f), mouth); Fangs(at + new Vector3(0, g * 0.05f, 0), g * 0.12f, g * 4); }
                }
                break;
            case "lemure":
                Ball(new Vector3(0, g * 0.5f, 0), new Vector3(g * 1.1f, g * 0.6f, g * 1.2f), hide, 0.3f, dark);
                Ball(new Vector3(0, g * 1.2f, g * 0.2f), new Vector3(g * 0.6f, g * 0.6f, g * 0.6f), hide, 0.2f);
                Eyes(new Vector3(0, g * 1.3f, g * 0.75f), g * 0.25f, g * 0.12f, 2);
                Part(Jaw, new Vector3(0, g * 1.0f, g * 0.6f));
                Ball(new Vector3(0, g * 0.95f, g * 0.7f), new Vector3(g * 0.3f, g * 0.12f, g * 0.1f), mouth);
                Body();
                break;
            case "blot":
                for (int i = 0; i < 6; i++) Ball(new Vector3((R(140 + i) - 0.5f) * g * 2.4f, g * 0.3f, (R(150 + i) - 0.5f) * g * 2.4f), new Vector3(g * 0.7f, g * 0.25f, g * 0.7f), slime, 0.4f);
                Ball(new Vector3(0, g * 0.9f, 0), new Vector3(g * 0.9f, g * 1.0f, g * 0.9f), slime, 0.4f);
                Eyes(new Vector3(0, g * 1.2f, g * 0.75f), g * 0.4f, g * 0.15f, a.Eyes + 1);
                break;
            default:
                // A heaped slick of slime with a few lumps, pseudopods raised.
                Ball(new Vector3(0, g * 0.3f, 0), new Vector3(g * 1.8f, g * 0.4f, g * 1.8f), slime, 0.4f);
                Ball(new Vector3(0, g * 0.7f, -g * 0.2f), new Vector3(g * 1.1f, g * 0.6f, g * 1.1f), slime, 0.35f);
                for (int i = 0; i < 4; i++) Ball(new Vector3((R(160 + i) - 0.5f) * g * 2, g * 0.4f, (R(170 + i) - 0.5f) * g * 2), new Vector3(g * 0.4f, g * 0.4f, g * 0.4f), slime, 0.3f);
                if (h == "pudding" || a.Id.Contains("black")) for (int i = 0; i < 5; i++) Ball(new Vector3((R(180 + i) - 0.5f) * g, g * (0.9f + 0.3f * R(185 + i)), (R(190 + i) - 0.5f) * g), new Vector3(g * 0.08f, g * 0.08f, g * 0.08f), Glow(new Color(1, 1, 1) * 0.6f));
                break;
        }
        foreach (int side in new[] { -1, 1 })
        {
            Part(side < 0 ? ClawR : ClawL, new Vector3(side * g * 0.6f, g * 0.6f, g * 0.6f));
            Seg(new Vector3(side * g * 0.6f, g * 0.6f, g * 0.6f), new Vector3(side * g * 1.0f, g * 1.4f, g * 1.3f), g * 0.3f, g * 0.15f, slime, 6);
        }
        Body();
    }

    private void Wraith()
    {
        string h = a.Head;
        float g = 1;
        var robe = a.Material == "shadow" ? Plain("1e1a26") : hide;
        // Robes that taper to nothing above the ground.
        k.Frustum(new Vector3(0, 0.08f, 0), 0.015f, 0.1f * (float)a.Bulk, 0.24f, 7, h == "ash" ? Plain("4a4440") : (robe.Darkened(0.2f)));
        k.Frustum(new Vector3(0, 0.32f, 0), 0.1f * (float)a.Bulk, 0.07f, 0.1f, 7, robe);
        // Rags trailing back.
        for (int i = 0; i < 4; i++)
        {
            var r = new Vector3((i - 1.5f) * 0.03f, 0.2f, -0.06f);
            Part(Frond, r);
            k.Tri(r, r + new Vector3(0.02f, -0.18f - 0.05f * R(200 + i), -0.06f), r + new Vector3(-0.02f, -0.15f, -0.08f), robe.Darkened(0.3f));
            k.Tri(r, r + new Vector3(-0.02f, -0.15f, -0.08f), r + new Vector3(0.02f, -0.18f - 0.05f * R(200 + i), -0.06f), robe.Darkened(0.3f));
        }
        Body();
        var hc = new Vector3(0, 0.46f, 0);
        Part(Neck, new Vector3(0, 0.4f, 0));
        if (h is "husk" or "maw")
        {
            Ball(hc, new Vector3(0.06f, 0.07f, 0.06f), Plain("c8bca0"));
            Ball(hc + new Vector3(0, -0.02f, 0.05f), new Vector3(0.035f, 0.03f, 0.02f), mouth);
        }
        else
        {
            Ball(hc + new Vector3(0, 0, -0.005f), new Vector3(0.07f, 0.078f, 0.07f), robe);
            Ball(hc + new Vector3(0, -0.01f, 0.03f), new Vector3(0.05f, 0.055f, 0.04f), Plain("0a0808"));
        }
        Eyes(hc + new Vector3(0, 0, 0.068f), 0.02f, 0.012f, Math.Max(a.Eyes, 2));
        switch (h)
        {
            case "crown": k.Frustum(hc + new Vector3(0, 0.05f, 0), 0.05f, 0.06f, 0.04f, 6, Glow(new Color("#" + a.Accent))); break;
            case "halo": k.Frustum(hc + new Vector3(0, 0.1f, 0), 0.06f, 0.06f, 0.01f, 10, Glow(new Color("c8ff6a"))); break;
            case "veil": k.Frustum(hc + new Vector3(0, -0.04f, 0.01f), 0.08f, 0.075f, 0.12f, 8, Plain("1a1418")); break;
        }
        foreach (int side in new[] { -1, 1 })
        {
            var sh = new Vector3(side * 0.09f, 0.37f, 0);
            Part(side < 0 ? ClawR : ClawL, sh);
            Seg(sh, new Vector3(side * 0.13f, 0.2f, 0.04f), 0.03f, 0.04f, robe, 5);
            Ball(new Vector3(side * 0.13f, 0.19f, 0.04f), new Vector3(0.018f, 0.02f, 0.018f), a.Material == "shadow" ? Plain("0a0a10") : Plain("d8e8f0"));
            for (int f = -1; f <= 1; f++) Spike(new Vector3(side * 0.13f + f * 0.01f, 0.18f, 0.05f), new Vector3(f * 0.2f, -1, 0.5f), 0.004f, 0.035f, Plain("d8e8f0"));
            if (side < 0)
                switch (h)
                {
                    case "scythe":
                        Seg(new Vector3(-0.13f, 0.05f, 0.04f), new Vector3(-0.13f, 0.5f, 0.06f), 0.008f, 0.008f, Plain("3a2a1a"), 4);
                        k.Tri(new Vector3(-0.13f, 0.5f, 0.06f), new Vector3(-0.13f, 0.47f, 0.2f), new Vector3(-0.13f, 0.44f, 0.06f), Plain("c9d1da") with { A = MeshKit.Flag.Metal });
                        k.Tri(new Vector3(-0.13f, 0.5f, 0.06f), new Vector3(-0.13f, 0.44f, 0.06f), new Vector3(-0.13f, 0.47f, 0.2f), Plain("c9d1da") with { A = MeshKit.Flag.Metal });
                        break;
                    case "crook": Seg(new Vector3(-0.13f, 0.02f, 0.04f), new Vector3(-0.13f, 0.48f, 0.06f), 0.008f, 0.008f, Plain("6a5a4a"), 4); k.Frustum(new Vector3(-0.13f, 0.48f, 0.08f), 0.03f, 0.03f, 0.012f, 6, Plain("6a5a4a")); break;
                    case "bell": k.Frustum(new Vector3(-0.13f, 0.08f, 0.06f), 0.05f, 0.025f, 0.08f, 8, Plain("a8743a") with { A = MeshKit.Flag.Metal }); break;
                    case "banner": Seg(new Vector3(-0.13f, 0.05f, 0.04f), new Vector3(-0.13f, 0.6f, 0.04f), 0.006f, 0.006f, Plain("3a2a1a"), 4); k.Quad(new Vector3(-0.13f, 0.58f, 0.04f), new Vector3(-0.13f, 0.58f, -0.12f), new Vector3(-0.13f, 0.42f, -0.12f), new Vector3(-0.13f, 0.42f, 0.04f), Mark()); k.Quad(new Vector3(-0.13f, 0.58f, -0.12f), new Vector3(-0.13f, 0.58f, 0.04f), new Vector3(-0.13f, 0.42f, 0.04f), new Vector3(-0.13f, 0.42f, -0.12f), Mark(0.8f)); break;
                }
        }
        Body();
        if (h == "ash") Flames(new Vector3(0, 0.1f, 0), new Vector3(0, 0.38f, 0), 0.06f);
        _ = g;
    }

    private void Floater()
    {
        string h = a.Head;
        float g = 0.1f * (float)a.Bulk;
        float y = 0.32f;
        var c = new Vector3(0, y, 0);
        switch (h)
        {
            case "eye":
                Ball(c, new Vector3(g * 1.3f, g * 1.3f, g * 1.3f), hide, 0.05f, dark);
                Ball(c + new Vector3(0, 0, g * 1.0f), new Vector3(g * 0.7f, g * 0.7f, g * 0.4f), Plain("f0ece0"));
                k.Box(c + new Vector3(0, -g * 0.12f, g * 1.35f), new Vector3(g * 0.3f, g * 0.3f, g * 0.1f), eye);
                Part(Jaw, c + new Vector3(0, -g * 0.6f, g * 0.6f));
                Ball(c + new Vector3(0, -g * 0.8f, g * 0.8f), new Vector3(g * 0.6f, g * 0.2f, g * 0.3f), mouth);
                Body();
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * Mathf.Tau / 6;
                    var r = c + new Vector3(Mathf.Cos(ang) * g * 0.6f, g * 1.0f, Mathf.Sin(ang) * g * 0.6f - g * 0.2f);
                    Part(Tentacle, r);
                    var tip = r + new Vector3(Mathf.Cos(ang) * g * 0.9f, g * 1.0f, Mathf.Sin(ang) * g * 0.6f);
                    Seg(r, tip, g * 0.1f, g * 0.06f, hide, 4);
                    k.Box(tip, new Vector3(g * 0.18f, g * 0.18f, g * 0.18f), eye);
                }
                break;
            case "wisp" or "star" or "notes":
                for (int i = 0; i < 3; i++) Ball(c + new Vector3(0, i * g * 0.2f, 0), new Vector3(g * (0.6f - i * 0.15f), g * (0.6f - i * 0.15f), g * (0.6f - i * 0.15f)), Glow(new Color("#" + a.Accent) * (0.8f + i * 0.1f)));
                if (h == "star") for (int i = 0; i < 5; i++) { float ang = i * Mathf.Tau / 5; Spike(c, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0.2f), g * 0.15f, g * 1.4f, Glow(new Color("fff0a0"))); }
                if (h == "notes") for (int i = 0; i < 4; i++) { var at = c + new Vector3(Mathf.Cos(i * 1.7f) * g * 1.4f, g * (0.5f + i * 0.3f), Mathf.Sin(i * 1.7f) * g * 1.4f); Part(Mote, at); Ball(at, new Vector3(g * 0.15f, g * 0.12f, g * 0.1f), Glow(new Color("ffe08a"))); Seg(at + new Vector3(g * 0.12f, 0, 0), at + new Vector3(g * 0.12f, g * 0.5f, 0), g * 0.03f, g * 0.03f, Glow(new Color("ffe08a")), 3); Body(); }
                foreach (int side in new[] { -1, 1 }) { Part(side < 0 ? ClawR : ClawL, c); Seg(c, c + new Vector3(side * g * 1.0f, -g * 0.6f, 0), g * 0.08f, 0.002f, Glow(new Color("#" + a.Accent) * 0.6f), 3); }
                break;
            case "eclipse":
                k.Frustum(c - new Vector3(0, g * 1.4f, 0), g * 1.4f, g * 1.4f, g * 0.15f, 14, Plain("0a0810"));
                for (int i = 0; i < 12; i++) { float ang = i * Mathf.Tau / 12; Spike(c + new Vector3(Mathf.Cos(ang) * g * 1.4f, Mathf.Sin(ang) * g * 1.4f, 0), new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0), g * 0.12f, g * 0.6f, Glow(new Color("ffb040"))); }
                break;
            case "manta":
                Ball(c, new Vector3(g * 0.6f, g * 0.35f, g * 0.9f), hide);
                foreach (int side in new[] { -1, 1 }) Wing(c + new Vector3(side * g * 0.4f, 0, 0), side, g * 3.0f, "bat", hide, dark);
                Part(TailPart, c - new Vector3(0, 0, g * 0.8f));
                Seg(c - new Vector3(0, 0, g * 0.8f), c - new Vector3(0, g * 0.4f, g * 3.5f), g * 0.12f, 0.002f, dark, 4);
                Body();
                Eyes(c + new Vector3(0, g * 0.2f, g * 0.8f), g * 0.3f, g * 0.12f, a.Eyes);
                break;
            case "mirror" or "rune" or "fractal":
                for (int i = 0; i < (h == "fractal" ? 7 : 3); i++)
                {
                    var at = c + new Vector3((R(210 + i) - 0.5f) * g * 2, (R(220 + i) - 0.3f) * g * 2, (R(230 + i) - 0.5f) * g);
                    Part(Mote, at);
                    k.Box(at, new Vector3(g * (h == "mirror" ? 0.9f : 0.4f), g * (h == "mirror" ? 1.4f : 0.6f), g * 0.12f), h == "rune" ? Glow(new Color("#" + a.Accent)) : Plain("c8e8f0") with { A = MeshKit.Flag.Metal });
                    Body();
                }
                Eyes(c + new Vector3(0, g * 0.3f, g * 0.2f), g * 0.4f, g * 0.12f, 2);
                break;
            case "jellyfish":
                k.Frustum(c, g * 1.0f, g * 0.2f, g * 0.8f, 10, hide with { A = MeshKit.Flag.Magic });
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.Tau / 8;
                    var r = c + new Vector3(Mathf.Cos(ang) * g * 0.7f, 0, Mathf.Sin(ang) * g * 0.7f);
                    Part(Tentacle, r);
                    Seg(r, r + new Vector3(0, -y + 0.02f, 0), g * 0.06f, g * 0.02f, belly with { A = MeshKit.Flag.Magic }, 3);
                }
                break;
            case "whirl":
                for (int i = 0; i < 6; i++) k.Frustum(new Vector3(0, i * 0.07f, 0), 0.03f + i * 0.02f, 0.04f + i * 0.02f, 0.06f, 9, Plain(i % 2 == 0 ? "c8dce8" : "a8bcc8") with { A = MeshKit.Flag.Metal });
                Eyes(new Vector3(0, 0.38f, 0.12f), 0.03f, 0.012f, 2);
                foreach (int side in new[] { -1, 1 }) { Part(side < 0 ? ClawR : ClawL, new Vector3(side * 0.1f, 0.35f, 0)); Seg(new Vector3(side * 0.1f, 0.35f, 0), new Vector3(side * 0.2f, 0.25f, 0.08f), 0.03f, 0.02f, Plain("c8dce8")); }
                break;
            default:
                // Constructs of the clockwork planes: a solid of so many faces with an eye, arms where it fights.
                {
                    int sides = h switch { "tetra" => 3, "octa" => 4, "di" => 2, "cube" => 4, "icosa" => 10, "dodeca" => 6, "trapezo" => 5, _ => 6 };
                    float r = g * (h == "dodeca" ? 1.5f : 1.0f);
                    k.Frustum(c, r, 0.001f, r * 1.2f, Math.Max(3, sides), hide with { A = MeshKit.Flag.Metal });
                    k.Push(new Transform3D(Basis.FromEuler(new Vector3(Mathf.Pi, 0, 0)), c));
                    k.Frustum(Vector3.Zero, r, 0.001f, r * (h == "cube" ? 1.2f : 0.9f), Math.Max(3, sides), dark with { A = MeshKit.Flag.Metal });
                    k.Pop();
                    k.Box(c + new Vector3(0, r * 0.1f, r * 0.7f), new Vector3(r * 0.35f, r * 0.35f, r * 0.2f), eye);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Part(side < 0 ? ClawR : ClawL, c + new Vector3(side * r * 0.8f, 0, 0));
                        Seg(c + new Vector3(side * r * 0.8f, 0, 0), c + new Vector3(side * r * 1.4f, -r * 0.5f, r * 0.4f), r * 0.08f, r * 0.06f, Plain("6a6e76") with { A = MeshKit.Flag.Metal }, 4);
                        if (side < 0 && a.Weapon == "" && h is "tetra" or "octa" or "trapezo") Spike(c + new Vector3(-r * 1.4f, -r * 0.5f, r * 0.4f), new Vector3(0, 0.3f, 1), r * 0.08f, r * 0.8f, Plain("c9d1da") with { A = MeshKit.Flag.Metal });
                    }
                    break;
                }
        }
        Body();
    }

    private void Fish()
    {
        string h = a.Head;
        float g = 0.06f * (float)a.Bulk;
        float y = 0.12f;
        float len = h switch { "eel" => 2.2f, "shark" => 1.6f, "whale" => 1.5f, "piranha" => 0.8f, "seahorse" => 0.6f, _ => 1.1f };
        if (h == "seahorse")
        {
            for (int i = 0; i < 6; i++) Ball(new Vector3(0, g * (0.8f + i * 0.6f), Mathf.Sin(i * 0.8f) * g * 0.5f), new Vector3(g * 0.6f, g * 0.55f, g * 0.6f), i % 2 == 0 ? hide : dark);
            Part(Neck, new Vector3(0, g * 3.8f, 0));
            Seg(new Vector3(0, g * 4.2f, 0), new Vector3(0, g * 4.0f, g * 1.4f), g * 0.4f, g * 0.15f, hide);
            Eyes(new Vector3(0, g * 4.4f, g * 0.4f), g * 0.4f, g * 0.15f, 2);
            Body();
            Part(TailPart, new Vector3(0, g * 0.8f, 0));
            Seg(new Vector3(0, g * 0.8f, 0), new Vector3(0, 0.01f, -g * 0.8f), g * 0.4f, g * 0.1f, hide);
            Body();
            return;
        }
        if (h == "turtle" || h == "plesio")
        {
            Ball(new Vector3(0, y + g, 0), new Vector3(g * 2.2f, g * 1.0f, g * 2.6f), dark, 0.05f, belly);
            foreach (int side in new[] { -1, 1 }) foreach (float z in new[] { 1f, -1f })
                {
                    Part(z > 0 ? (side < 0 ? LegA : LegB) : (side < 0 ? LegB : LegA), new Vector3(side * g * 1.5f, y + g * 0.6f, z * g * 1.4f));
                    k.Tri(new Vector3(side * g * 1.5f, y + g * 0.6f, z * g * 1.4f), new Vector3(side * g * 3.4f, y + g * 0.3f, z * g * 1.6f - g * 0.8f), new Vector3(side * g * 1.5f, y + g * 0.5f, z * g * 1.4f - g * 0.6f), hide);
                    k.Tri(new Vector3(side * g * 1.5f, y + g * 0.6f, z * g * 1.4f), new Vector3(side * g * 1.5f, y + g * 0.5f, z * g * 1.4f - g * 0.6f), new Vector3(side * g * 3.4f, y + g * 0.3f, z * g * 1.6f - g * 0.8f), hide);
                }
            var root = new Vector3(0, y + g, g * 2.5f);
            var top = root + new Vector3(0, g * (h == "plesio" ? 4.5f : 0.6f), g * (h == "plesio" ? 2.0f : 1.0f));
            Part(Neck, root);
            Seg(root, top, g * 0.6f, g * 0.45f, hide);
            Head(h == "plesio" ? "lizard" : "snake", top, g / 0.045f * 0.7f, Neck, root);
            Body();
            return;
        }
        // A fish body along z: a fat front tapering to the tail, fins above and at the sides.
        var front = new Vector3(0, y + g, g * len * 0.6f);
        Ball(new Vector3(0, y + g, 0), new Vector3(g * 1.0f, g * 1.1f, g * len * 1.4f), hide, 0.05f, belly);
        Ball(front, new Vector3(g * 0.9f, g * 1.0f, g * 0.9f), hide, 0.05f, belly);
        if (h == "whale") k.Box(new Vector3(0, y + g * 0.1f, g * len * 0.2f), new Vector3(g * 1.6f, g * 0.6f, g * len * 1.6f), belly);
        Spike(new Vector3(0, y + g * 1.9f, -g * 0.2f), new Vector3(0, 1, -0.6f), g * 0.4f, g * (h == "shark" ? 1.6f : 0.8f), dark);
        foreach (int side in new[] { -1, 1 })
        {
            Part(side < 0 ? ClawR : ClawL, new Vector3(side * g * 0.9f, y + g * 0.6f, g * 0.6f));
            k.Tri(new Vector3(side * g * 0.9f, y + g * 0.6f, g * 0.6f), new Vector3(side * g * 2.0f, y + g * 0.1f, -g * 0.3f), new Vector3(side * g * 0.9f, y + g * 0.5f, 0), dark);
            k.Tri(new Vector3(side * g * 0.9f, y + g * 0.6f, g * 0.6f), new Vector3(side * g * 0.9f, y + g * 0.5f, 0), new Vector3(side * g * 2.0f, y + g * 0.1f, -g * 0.3f), dark);
        }
        Part(TailPart, new Vector3(0, y + g, -g * len * 1.2f));
        var tail = new Vector3(0, y + g, -g * len * 1.4f);
        Seg(new Vector3(0, y + g, -g * len * 1.0f), tail, g * 0.5f, g * 0.25f, hide);
        bool flat = h == "whale";
        foreach (int side in new[] { -1, 1 })
        {
            var tip = tail + (flat ? new Vector3(side * g * 1.4f, 0, -g * 0.8f) : new Vector3(0, side * g * 1.4f, -g * 0.8f));
            k.Tri(tail, tip, tail + new Vector3(0, 0, -g * 0.4f), dark);
            k.Tri(tail, tail + new Vector3(0, 0, -g * 0.4f), tip, dark);
        }
        Body();
        string head = h switch { "shark" or "piranha" or "angler" => "fish", "eel" => "snake", _ => "fish" };
        Part(Neck, front);
        if (h == "angler") { Seg(front + new Vector3(0, g, 0), front + new Vector3(0, g * 2.2f, g * 1.4f), g * 0.06f, g * 0.04f, dark, 3); Ball(front + new Vector3(0, g * 2.2f, g * 1.4f), new Vector3(g * 0.25f, g * 0.25f, g * 0.25f), Glow(new Color("8ae0ff"))); }
        if (h == "drifter") for (int i = 0; i < 6; i++) k.Box(new Vector3((R(240 + i) - 0.5f) * g * 3, y + g * (0.5f + R(250 + i) * 2), (R(260 + i) - 0.5f) * g * 3), new Vector3(g * 0.12f, g * 0.12f, g * 0.12f), Glow(new Color("e8f0ff") * 0.7f));
        Head(head, front + new Vector3(0, 0, g * 0.7f), g / 0.05f * 0.9f, Neck, front);
        Body();
    }

    private void Cephalopod()
    {
        string h = a.Head;
        float g = 0.08f * (float)a.Bulk;
        float y = h is "roper" ? 0.02f : 0.12f;
        int arms = h switch { "otyugh" => 3, "aboleth" => 4, "roper" => 6, "imp" => 6, "envoy" => 7, _ => 8 };
        Vector3 c;
        switch (h)
        {
            case "roper":
                c = new Vector3(0, g * 2.2f, 0);
                k.Frustum(new Vector3(0, 0, 0), g * 1.1f, g * 0.5f, g * 3.6f, 7, hide, dark);
                Eyes(c + new Vector3(0, g * 0.4f, g * 0.6f), 0, g * 0.3f, 1);
                Part(Jaw, c);
                Ball(c + new Vector3(0, -g * 0.3f, g * 0.7f), new Vector3(g * 0.5f, g * 0.2f, g * 0.2f), mouth);
                Fangs(c + new Vector3(0, -g * 0.2f, g * 0.8f), g * 0.3f, g * 4);
                Body();
                break;
            case "otyugh":
                c = new Vector3(0, g * 1.4f, 0);
                Ball(c, new Vector3(g * 1.5f, g * 1.2f, g * 1.5f), hide, 0.2f, dark);
                Part(Jaw, c + new Vector3(0, 0, g * 1.2f));
                Ball(c + new Vector3(0, -g * 0.2f, g * 1.3f), new Vector3(g * 0.9f, g * 0.4f, g * 0.3f), mouth);
                Fangs(c + new Vector3(0, 0, g * 1.5f), g * 0.6f, g * 4);
                Body();
                for (int i = 0; i < 3; i++) Leg(new Vector3(Mathf.Cos(i * 2.1f + 0.5f) * g, g * 0.5f, Mathf.Sin(i * 2.1f + 0.5f) * g), i % 2 == 0 ? LegA : LegB, g * 0.35f, false, hide, dark);
                Part(Neck, c + new Vector3(0, g, 0));
                Seg(c + new Vector3(0, g, 0), c + new Vector3(0, g * 2.2f, g * 0.4f), g * 0.15f, g * 0.12f, hide);
                Eyes(c + new Vector3(0, g * 2.3f, g * 0.5f), g * 0.15f, g * 0.15f, 3);
                Body();
                break;
            case "monk":
                c = new Vector3(0, g * 2.2f, 0);
                Ball(c, new Vector3(g * 2.0f, g * 2.4f, g * 2.0f), Plain("1a1c24"), 0.05f, Plain("0e1018"));
                Eyes(c + new Vector3(0, g * 0.5f, g * 1.8f), g * 0.6f, g * 0.3f, 2);
                k.Frustum(new Vector3(0, 0, 0), g * 2.8f, g * 2.0f, g * 0.6f, 12, Plain("3a5a7a") with { A = MeshKit.Flag.Metal });
                break;
            case "mist":
                c = new Vector3(0, g * 1.4f, 0);
                for (int i = 0; i < 5; i++) Ball(c + new Vector3((R(270 + i) - 0.5f) * g * 2.5f, (R(280 + i) - 0.5f) * g, (R(290 + i) - 0.5f) * g * 2.5f), new Vector3(g, g * 0.7f, g), Plain("c0c8cc"), 0.3f);
                Eyes(c + new Vector3(0, g * 0.3f, g * 1.2f), g * 0.3f, g * 0.12f, 2);
                break;
            case "aboleth":
                c = new Vector3(0, g * 1.2f, 0);
                Ball(c, new Vector3(g * 1.1f, g * 0.9f, g * 2.4f), hide, 0.05f, belly);
                Part(TailPart, c - new Vector3(0, 0, g * 2.2f));
                Seg(c - new Vector3(0, 0, g * 2.0f), c - new Vector3(0, g * 0.3f, g * 4.2f), g * 0.7f, g * 0.2f, hide);
                k.Tri(c - new Vector3(0, g * 0.3f, g * 4.2f), c - new Vector3(0, -g * 0.8f, g * 5.0f), c - new Vector3(0, g * 1.2f, g * 5.0f), dark);
                k.Tri(c - new Vector3(0, g * 0.3f, g * 4.2f), c - new Vector3(0, g * 1.2f, g * 5.0f), c - new Vector3(0, -g * 0.8f, g * 5.0f), dark);
                Body();
                Eyes(c + new Vector3(0, g * 0.6f, g * 1.4f), g * 0.4f, g * 0.2f, 3);
                break;
            case "imp":
                c = new Vector3(0, 0.25f, 0);
                Ball(c, new Vector3(0.07f, 0.08f, 0.07f), hide);
                Eyes(c + new Vector3(0, 0.02f, 0.065f), 0.025f, 0.012f, 2);
                Fangs(c + new Vector3(0, -0.03f, 0.06f), 0.02f, 1);
                g = 0.05f;
                break;
            case "envoy":
                c = new Vector3(0, 0.45f, 0);
                Ball(c, new Vector3(g * 1.2f, g * 1.6f, g * 1.2f), hide, 0.1f, dark);
                Eyes(c + new Vector3(0, g * 0.3f, g * 1.1f), g * 0.5f, g * 0.15f, 5);
                for (int i = 0; i < 4; i++) k.Box(c + new Vector3((R(300 + i) - 0.5f) * g * 3, g * (1.5f + R(310 + i)), (R(320 + i) - 0.5f) * g * 2), new Vector3(g * 0.1f, g * 0.1f, g * 0.1f), Glow(new Color("e8e0ff")));
                break;
            default:
                // An octopus or a kraken: a great mantle above a ring of arms, two big eyes.
                c = new Vector3(0, y + g * 1.6f, -g * 0.3f);
                Ball(c + new Vector3(0, g * 0.6f, -g * 0.4f), new Vector3(g * 1.2f, g * 1.7f, g * 1.3f), hide, 0.08f, dark);
                Ball(c, new Vector3(g * 1.1f, g * 0.9f, g * 1.1f), hide);
                foreach (int side in new[] { -1, 1 }) { Ball(c + new Vector3(side * g * 0.75f, g * 0.2f, g * 0.6f), new Vector3(g * 0.32f, g * 0.32f, g * 0.25f), Plain("f0e0a0")); k.Box(c + new Vector3(side * g * 0.8f, g * 0.18f, g * 0.85f), new Vector3(g * 0.25f, g * 0.12f, g * 0.05f), Plain("1a1410")); }
                break;
        }
        if (h is not ("monk" or "mist" or "aboleth" or "otyugh" or "roper"))
        {
            float ry = h == "envoy" ? c.Y - g : y + g * 0.8f;
            for (int i = 0; i < arms; i++)
            {
                float ang = i * Mathf.Tau / arms + 0.2f;
                var r = new Vector3(Mathf.Cos(ang) * g * 0.7f, ry, Mathf.Sin(ang) * g * 0.7f);
                Part(Tentacle, r);
                var p = r;
                for (int s = 1; s <= 4; s++)
                {
                    float f = s / 4f;
                    var q = r + new Vector3(Mathf.Cos(ang) * g * 3.4f * f, -ry * Mathf.Min(1, f * 1.6f) + (h == "envoy" ? 0 : g * 0.5f * f * f), Mathf.Sin(ang) * g * 3.4f * f);
                    Seg(p, q, g * 0.35f * (1.05f - f * 0.8f), g * 0.35f * (1 - f * 0.85f), s % 2 == 0 ? hide : belly, 5);
                    p = q;
                }
            }
        }
        else if (h is "aboleth" or "otyugh" or "roper")
        {
            int n = h == "roper" ? 6 : h == "aboleth" ? 4 : 2;
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)n - 0.5f) * (h == "roper" ? Mathf.Tau : 2.4f);
                var r = c + new Vector3(Mathf.Sin(ang) * g * 0.8f, h == "roper" ? g * 0.3f : -g * 0.3f, Mathf.Cos(ang) * g * 0.8f);
                Part(Tentacle, r);
                Seg(r, r + new Vector3(Mathf.Sin(ang) * g * 3, -g * 0.5f, Mathf.Cos(ang) * g * 3), g * 0.22f, g * 0.05f, hide, 5);
            }
        }
        Body();
    }

    private void Plant()
    {
        string h = a.Head;
        float g = 0.1f * (float)a.Bulk;
        var wood = Plain(a.Hide);
        var leaf = Plain(R(5) > 0.5f ? "4a7a32" : "5a8a3a");
        if (a.Material == "fire") leaf = Ember(new Color("ff8a30"));
        switch (h)
        {
            case "tree":
                {
                    // A walking tree: a trunk with a face, two root legs, two branch arms, a crown of leaves.
                    float th = 0.5f;
                    k.Frustum(new Vector3(0, 0.12f, 0), g * 1.0f, g * 0.75f, th, 7, wood, null, R(6));
                    Ball(new Vector3(0, 0.12f + th * 0.6f, g * 0.6f), new Vector3(g * 0.6f, g * 0.15f, g * 0.15f), Plain("1a120c"));
                    Eyes(new Vector3(0, 0.12f + th * 0.75f, g * 0.72f), g * 0.35f, g * 0.12f, 2);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Part(side < 0 ? LegA : LegB, new Vector3(side * g * 0.5f, 0.14f, 0));
                        Seg(new Vector3(side * g * 0.5f, 0.14f, 0), new Vector3(side * g * 0.9f, 0, g * 0.4f), g * 0.4f, g * 0.25f, wood);
                        Seg(new Vector3(side * g * 0.5f, 0.12f, 0), new Vector3(side * g * 0.6f, 0, -g * 0.7f), g * 0.3f, g * 0.15f, wood);
                        var sh = new Vector3(side * g * 0.8f, 0.12f + th * 0.85f, 0);
                        Part(side < 0 ? ClawR : ClawL, sh);
                        var el = sh + new Vector3(side * g * 1.4f, -g * 0.4f, g * 0.6f);
                        Seg(sh, el, g * 0.3f, g * 0.2f, wood);
                        Seg(el, el + new Vector3(side * g * 0.2f, -g * 1.3f, g * 0.6f), g * 0.2f, g * 0.08f, wood);
                        Ball(el + new Vector3(0, g * 0.3f, 0), new Vector3(g * 0.6f, g * 0.4f, g * 0.6f), leaf, 0.3f);
                    }
                    Part(Frond, new Vector3(0, 0.12f + th, 0));
                    for (int i = 0; i < 5; i++) Ball(new Vector3((R(330 + i) - 0.5f) * g * 2.4f, 0.12f + th + g * (0.4f + R(340 + i) * 0.8f), (R(350 + i) - 0.5f) * g * 2), new Vector3(g * 1.0f, g * 0.8f, g * 1.0f), leaf, 0.35f, leaf.Darkened(0.25f));
                    if (Has("antlers")) foreach (int side in new[] { -1, 1 }) Seg(new Vector3(side * g * 0.4f, 0.12f + th, 0), new Vector3(side * g * 1.4f, 0.12f + th + g * 2, -g * 0.3f), g * 0.12f, g * 0.04f, wood, 4);
                    break;
                }
            case "shrub":
                Part(Frond, new Vector3(0, 0, 0));
                for (int i = 0; i < 6; i++) Ball(new Vector3((R(360 + i) - 0.5f) * g * 2, g * (0.6f + R(370 + i) * 0.8f), (R(380 + i) - 0.5f) * g * 2), new Vector3(g * 0.8f, g * 0.7f, g * 0.8f), leaf, 0.35f, leaf.Darkened(0.3f));
                Eyes(new Vector3(0, g * 1.1f, g * 0.95f), g * 0.3f, g * 0.12f, 2);
                break;
            case "mound":
                Ball(new Vector3(0, g * 1.2f, 0), new Vector3(g * 1.6f, g * 1.4f, g * 1.4f), Plain("3a4a22"), 0.4f, Plain("2a3418"));
                for (int i = 0; i < 8; i++) { var r = new Vector3((R(390 + i) - 0.5f) * g * 2.6f, g * (0.4f + R(400 + i) * 1.8f), (R(410 + i) - 0.5f) * g * 2.4f); Part(Frond, r); Seg(r, r + new Vector3((R(420 + i) - 0.5f) * g, -g * 1.0f, 0), g * 0.1f, g * 0.04f, leaf, 4); }
                foreach (int side in new[] { -1, 1 }) { var sh = new Vector3(side * g * 1.2f, g * 1.6f, g * 0.3f); Part(side < 0 ? ClawR : ClawL, sh); Seg(sh, sh + new Vector3(side * g * 0.8f, -g * 1.2f, g * 0.6f), g * 0.4f, g * 0.25f, Plain("3a4a22")); }
                Eyes(new Vector3(0, g * 1.6f, g * 1.35f), g * 0.4f, g * 0.15f, 2);
                break;
            case "mushroom":
                {
                    float cap = g * 1.4f * Vary(7, 0.4f);
                    k.Frustum(new Vector3(0, 0, 0), g * 0.45f, g * 0.35f, g * 2.4f, 7, belly);
                    Part(Frond, new Vector3(0, g * 2.2f, 0));
                    k.Frustum(new Vector3(0, g * 2.2f, 0), cap, g * 0.1f, g * 1.0f, 9, hide, dark);
                    for (int i = 0; i < 6; i++) Ball(new Vector3(Mathf.Cos(i * 1.1f) * cap * 0.6f, g * (2.6f + 0.2f * R(430 + i)), Mathf.Sin(i * 1.1f) * cap * 0.6f), new Vector3(g * 0.15f, g * 0.06f, g * 0.15f), a.Id.Contains("violet") ? Plain("c8a0ff") : Plain("f0ece0"));
                    Body();
                    Eyes(new Vector3(0, g * 1.6f, g * 0.4f), g * 0.2f, g * 0.1f, a.Id.Contains("shrieker") ? 0 : 2);
                    if (a.Id.Contains("shrieker")) Ball(new Vector3(0, g * 1.4f, g * 0.42f), new Vector3(g * 0.2f, g * 0.25f, g * 0.05f), mouth);
                    if (a.Id.Contains("violet")) for (int i = 0; i < 4; i++) { var r = new Vector3(Mathf.Cos(i * 1.6f) * g * 0.4f, g * 1.2f, Mathf.Sin(i * 1.6f) * g * 0.4f); Part(Tentacle, r); Seg(r, r + new Vector3(Mathf.Cos(i * 1.6f) * g * 1.4f, -g * 0.8f, Mathf.Sin(i * 1.6f) * g * 1.4f), g * 0.1f, g * 0.03f, Plain("8a5ab8"), 4); }
                    if (a.Id.Contains("sporeling") || a.Id.Contains("ghostcap")) for (int i = 0; i < 5; i++) k.Box(new Vector3((R(440 + i) - 0.5f) * g * 3, g * (2.8f + R(450 + i) * 1.5f), (R(460 + i) - 0.5f) * g * 3), new Vector3(g * 0.1f, g * 0.1f, g * 0.1f), Glow(new Color("c8ffd8") * 0.6f));
                    break;
                }
            case "roots":
                for (int i = 0; i < 6; i++) { float ang = i * Mathf.Tau / 6; var r = new Vector3(0, g * 0.5f, 0); Part(Tentacle, r); Seg(r, new Vector3(Mathf.Cos(ang) * g * 2.4f, 0, Mathf.Sin(ang) * g * 2.4f), g * 0.35f, g * 0.1f, wood); }
                Body();
                k.Frustum(new Vector3(0, 0, 0), g * 0.8f, g * 0.4f, g * 2.8f, 7, wood);
                Flames(new Vector3(0, g * 1.5f, 0), new Vector3(0, g * 3.0f, 0), g * 1.6f);
                Eyes(new Vector3(0, g * 2.2f, g * 0.55f), g * 0.25f, g * 0.12f, 2);
                break;
            default:
                {
                    // A carnivorous bloom: a stem, leaves, and a head of petals round a toothed mouth.
                    var petal = Plain(a.Hide);
                    float stem = g * 2.6f;
                    Seg(new Vector3(0, 0, 0), new Vector3(0, stem, g * 0.2f), g * 0.25f, g * 0.2f, leaf);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Part(side < 0 ? ClawR : ClawL, new Vector3(0, g * 0.8f, 0));
                        k.Tri(new Vector3(0, g * 0.8f, 0), new Vector3(side * g * 1.8f, g * 1.4f, g * 0.4f), new Vector3(side * g * 0.8f, g * 0.6f, -g * 0.4f), leaf);
                        k.Tri(new Vector3(0, g * 0.8f, 0), new Vector3(side * g * 0.8f, g * 0.6f, -g * 0.4f), new Vector3(side * g * 1.8f, g * 1.4f, g * 0.4f), leaf);
                    }
                    var hc = new Vector3(0, stem + g * 0.2f, g * 0.3f);
                    Part(Neck, new Vector3(0, stem * 0.5f, 0));
                    Ball(hc, new Vector3(g * 0.55f, g * 0.55f, g * 0.45f), mouth);
                    int petals = h == "blossom" ? 5 : h == "rose" ? 9 : 7;
                    for (int i = 0; i < petals; i++)
                    {
                        float ang = i * Mathf.Tau / petals;
                        var tip = hc + new Vector3(Mathf.Cos(ang) * g * 1.4f, Mathf.Sin(ang) * g * 1.4f, g * 0.6f);
                        k.Tri(hc + new Vector3(Mathf.Cos(ang - 0.4f) * g * 0.4f, Mathf.Sin(ang - 0.4f) * g * 0.4f, 0), tip, hc + new Vector3(Mathf.Cos(ang + 0.4f) * g * 0.4f, Mathf.Sin(ang + 0.4f) * g * 0.4f, 0), petal);
                        k.Tri(hc + new Vector3(Mathf.Cos(ang - 0.4f) * g * 0.4f, Mathf.Sin(ang - 0.4f) * g * 0.4f, 0), hc + new Vector3(Mathf.Cos(ang + 0.4f) * g * 0.4f, Mathf.Sin(ang + 0.4f) * g * 0.4f, 0), tip, petal.Darkened(0.2f));
                    }
                    Part(Jaw, hc);
                    Fangs(hc + new Vector3(0, 0, g * 0.4f), g * 0.25f, g * 8);
                    if (a.Id.Contains("skull")) Ball(hc + new Vector3(0, 0, g * 0.3f), new Vector3(g * 0.35f, g * 0.38f, g * 0.3f), Plain("e6dcc0"));
                    if (a.Id.Contains("bloat")) Ball(hc + new Vector3(0, -g * 1.2f, 0), new Vector3(g * 0.8f, g * 0.8f, g * 0.8f), Plain("9a7aa8"), 0.2f);
                    if (Has("spikes") || a.Id.Contains("thorn")) for (int i = 0; i < 6; i++) Spike(new Vector3(0, stem * (0.2f + i * 0.12f), 0), new Vector3((i % 2) * 2 - 1, 0.3f, 0.2f), g * 0.08f, g * 0.4f, Plain("c8b088"));
                    Body();
                    break;
                }
        }
        Body();
    }

    // People and golems: the heroes' kit, on the heroes' rig ------------------------------------------------

    private void People()
    {
        var frame = Enum.TryParse<Frame>(a.Frame, out var f) ? f : Frame.Bare;
        var skin = Plain(a.Hide);
        int mark = k.Corners;
        HeroKit.Body(k, frame);
        if (a.Helm != "" && Enum.TryParse<Head>(a.Helm, out var helm)) HeadPiece(k, helm);
        else if (a.Head != "")
        {
            // A beast's head on a person's shoulders: on the heroes' head part, so it nods with them.
            k.Part = HeadPart;
            Head(a.Head, new Vector3(0, 0.43f, 0.0f), 1.0f, HeadPart, Vector3.Zero);
            k.Pivot = null;
        }
        else
        {
            k.Part = HeadPart;
            Ball(new Vector3(0, 0.43f, 0), new Vector3(0.062f, 0.066f, 0.06f), skin);
            Eyes(new Vector3(0, 0.432f, 0.054f), 0.022f, 0.011f, Math.Max(a.Eyes, 1));
            HeadMarks(new Vector3(0, 0.43f, 0), 1, "");
            if (Has("beard")) Ball(new Vector3(0, 0.38f, 0.04f), new Vector3(0.045f, 0.05f, 0.03f), a.Material == "fire" ? Ember(new Color("ff8a30")) : Plain("6a4a2a"));
            if (Has("snakehair")) for (int i = 0; i < 7; i++) { float ang = (i / 6f - 0.5f) * 3f; Spike(new Vector3(Mathf.Sin(ang) * 0.05f, 0.47f, Mathf.Cos(ang) * 0.02f - 0.02f), new Vector3(Mathf.Sin(ang), 0.6f, -0.5f), 0.01f, 0.08f, Plain("4a7a3a")); }
        }
        k.Part = 0;
        k.Pivot = null;
        if (Enum.TryParse<Weapon>(a.Weapon, out var w) && w != Weapon.None) WeaponPiece(k, w);
        if (Enum.TryParse<OffHand>(a.Off, out var o) && o != OffHand.None) OffHandPiece(k, o);
        if (Has("cape") || Has("crown") && a.Frame is "Plate" or "Mail") Cape(k);
        k.Part = 0;
        // The heroes' palette slots baked into this person's own colours: skin as theirs, cloth in the host's colour.
        k.Recolour(mark, c => Mathf.Abs(c.A - MeshKit.Flag.Skin) < 0.03f ? Tint(skin, c) : c);
        PeopleExtras();
    }

    /// <summary>A skin-flagged colour's shade (the kit lightens or darkens skin by its value) applied to a creature's own skin.</summary>
    private Color Tint(Color skin, Color kitSkin) => Mat((skin * kitSkin.V) with { A = 1 }) with { A = Mat(skin).A };

    /// <summary>Wings, tails and flames on people (fiends, celestials, gargoyles): on the creature parts, which the shader still moves for them.</summary>
    private void PeopleExtras()
    {
        if (a.Wings != "") foreach (int side in new[] { -1, 1 }) Wing(new Vector3(side * 0.05f, 0.33f, -0.07f), side, 0.32f, a.Wings, Plain(a.Hide).Darkened(0.2f), a.Wings == "feather" ? Plain("f8f4ea") : Plain(a.Hide).Darkened(0.45f));
        if (Has("stinger") || a.Id.Contains("devil") || a.Id.Contains("tiefling") || a.Id.Contains("imp") || a.Id.Contains("quasit"))
        {
            Part(TailPart, new Vector3(0, 0.15f, -0.07f));
            Seg(new Vector3(0, 0.15f, -0.07f), new Vector3(0, 0.08f, -0.2f), 0.015f, 0.01f, Plain(a.Hide), 4);
            Seg(new Vector3(0, 0.08f, -0.2f), new Vector3(0, 0.14f, -0.3f), 0.01f, 0.006f, Plain(a.Hide), 4);
            Spike(new Vector3(0, 0.14f, -0.3f), new Vector3(0, 0.4f, -1), 0.015f, 0.04f, Plain(a.Hide).Darkened(0.4f));
            Body();
        }
        if (a.Material == "fire") Flames(new Vector3(0, 0.3f, 0), new Vector3(0, 0.5f, 0), 0.08f);
        if (a.Material == "stone" && a.Id.Contains("gargoyle")) Spikes(new Vector3(0, 0.35f, -0.06f), new Vector3(0, 0.18f, -0.08f), 3, 0.05f);
    }

    private void Golem()
    {
        // A blocky body on the heroes' joints: bricks of its stuff, a slab of a head with a glowing eye-slit.
        float b = (float)a.Bulk;
        var stuff = hide;
        var seam = dark;
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? LegR : LegL;
            k.Box(new Vector3(side * 0.05f, 0, 0.01f), new Vector3(0.06f * b, 0.17f, 0.07f * b), stuff, seam);
            k.Box(new Vector3(side * 0.05f, 0, 0.02f), new Vector3(0.07f * b, 0.035f, 0.09f * b), seam);
        }
        k.Part = 0;
        k.Box(new Vector3(0, 0.15f, 0), new Vector3(0.17f * b, 0.07f, 0.12f * b), seam);
        k.Box(new Vector3(0, 0.2f, 0), new Vector3(0.22f * b, 0.17f, 0.15f * b), stuff, seam);
        if (a.Material == "crystal") for (int i = 0; i < 4; i++) Spike(new Vector3((R(470 + i) - 0.5f) * 0.2f, 0.36f, (R(480 + i) - 0.5f) * 0.1f), new Vector3(R(490 + i) - 0.5f, 1, -0.3f), 0.02f, 0.09f, Glow(new Color("#" + a.Accent) * 0.7f));
        if (a.Id.Contains("flesh")) for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.08f + i * 0.08f, 0.22f + i * 0.04f, 0.076f * b), new Vector3(0.05f, 0.006f, 0.005f), Plain("3a1a1a"));
        if (a.Id.Contains("bell")) k.Frustum(new Vector3(0, 0.36f, 0), 0.06f, 0.1f, 0.1f, 8, Plain("a8743a") with { A = MeshKit.Flag.Metal });
        k.Box(new Vector3(0, 0.25f, 0.078f * b), new Vector3(0.05f, 0.05f, 0.01f), eye);
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? ArmR : ArmL;
            k.Box(new Vector3(side * 0.14f * b, 0.3f, 0), new Vector3(0.08f * b, 0.07f, 0.08f * b), stuff, seam);
            k.Box(new Vector3(side * 0.13f * b, 0.16f, 0.015f), new Vector3(0.065f * b, 0.15f, 0.07f * b), stuff, seam);
            k.Box(new Vector3(side * 0.13f * b, 0.13f, 0.02f), new Vector3(0.08f * b, 0.05f, 0.085f * b), seam);
        }
        k.Part = HeadPart;
        if (a.Helm != "" && Enum.TryParse<Head>(a.Helm, out var helm)) { k.Part = 0; HeadPiece(k, helm); }
        else
        {
            k.Box(new Vector3(0, 0.37f, 0.01f), new Vector3(0.08f, 0.09f, 0.08f), stuff, seam);
            k.Box(new Vector3(0, 0.42f, 0.052f), new Vector3(0.06f, 0.012f, 0.01f), eye);
        }
        HeadMarks(new Vector3(0, 0.42f, 0), 1, "");
        k.Part = 0;
        if (Enum.TryParse<Weapon>(a.Weapon, out var w) && w != Weapon.None) WeaponPiece(k, w);
        if (Enum.TryParse<OffHand>(a.Off, out var o) && o != OffHand.None) OffHandPiece(k, o);
        k.Part = 0;
    }

    private void Ape()
    {
        // Hunched on the heroes' joints: a great barrel chest, long arms to the ground, a low head.
        float b = (float)a.Bulk;
        var fur = hide;
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? LegR : LegL;
            Seg(new Vector3(side * 0.05f, 0.17f, 0), new Vector3(side * 0.06f, 0.02f, 0.02f), 0.04f * b, 0.035f * b, fur);
            Ball(new Vector3(side * 0.06f, 0.02f, 0.03f), new Vector3(0.035f, 0.02f, 0.045f), dark);
        }
        k.Part = 0;
        Ball(new Vector3(0, 0.22f, 0), new Vector3(0.1f * b, 0.08f, 0.08f * b), fur, 0.15f, belly);
        Ball(new Vector3(0, 0.31f, 0.02f), new Vector3(0.13f * b, 0.08f, 0.1f * b), fur, 0.15f);
        Ball(new Vector3(0, 0.28f, 0.07f * b), new Vector3(0.07f, 0.06f, 0.03f), belly);
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? ArmR : ArmL;
            Seg(new Vector3(side * 0.12f * b, 0.34f, 0), new Vector3(side * 0.15f * b, 0.06f, 0.04f), 0.045f * b, 0.035f * b, fur);
            Ball(new Vector3(side * 0.15f * b, 0.05f, 0.05f), new Vector3(0.04f, 0.03f, 0.04f), dark);
        }
        k.Part = HeadPart;
        Head(a.Head is "yeti" ? "bear" : a.Head is "bat" ? "bat" : "tusks", new Vector3(0, 0.39f, 0.06f), 1.0f, HeadPart, Vector3.Zero);
        k.Pivot = null;
        if (a.Head is "ape" or "baboon" or "")
        {
            Ball(new Vector3(0, 0.37f, 0.11f), new Vector3(0.04f, 0.03f, 0.025f), belly);
            if (a.Head == "baboon") Ball(new Vector3(0, 0.38f, 0.13f), new Vector3(0.025f, 0.02f, 0.03f), Plain("c83a3a"));
        }
        k.Part = 0;
        PeopleExtras();
    }

    private void Elemental()
    {
        if (a.Head == "rock")
        {
            Golem();
            for (int i = 0; i < 4; i++) Ball(new Vector3((R(500 + i) - 0.5f) * 0.25f, 0.2f + R(510 + i) * 0.2f, (R(520 + i) - 0.5f) * 0.12f), new Vector3(0.05f, 0.04f, 0.05f), hide, 0.4f);
            return;
        }
        float g = 0.1f * (float)a.Bulk;
        Color stuff = a.Head switch
        {
            "flame" => Ember(new Color("#" + a.Hide)),
            "wave" => (new Color("#" + a.Hide) * 0.8f) with { A = MeshKit.Flag.Metal },
            _ => Plain(a.Hide) with { A = MeshKit.Flag.Metal },
        };
        // A pillar of its element rising to shoulders and a head, arms of it reaching out.
        for (int i = 0; i < 6; i++)
        {
            float f = i / 5f;
            float r = g * (0.4f + 1.1f * f * (a.Head == "whirl" ? 1.2f : 1));
            var at = new Vector3(Mathf.Sin(i * 1.3f) * g * 0.1f, f * 0.4f, 0);
            Part(Frond, new Vector3(0, at.Y, 0));
            k.Frustum(at, r * 0.8f, r, 0.08f, 8, i % 2 == 0 ? stuff : stuff.Darkened(0.15f), null, i * 0.4f);
        }
        Body();
        Part(Neck, new Vector3(0, 0.45f, 0));
        Ball(new Vector3(0, 0.52f, 0.02f), new Vector3(g * 0.8f, g * 0.9f, g * 0.8f), stuff, 0.3f);
        Eyes(new Vector3(0, 0.53f, g * 0.75f), g * 0.3f, g * 0.12f, 2);
        if (a.Head == "flame") Flames(new Vector3(0, 0.55f, 0), new Vector3(0, 0.6f, -0.02f), g * 1.2f);
        foreach (int side in new[] { -1, 1 })
        {
            var sh = new Vector3(side * g * 1.2f, 0.42f, 0);
            Part(side < 0 ? ClawR : ClawL, sh);
            Seg(sh, sh + new Vector3(side * g * 1.0f, -g * 1.2f, g * 0.8f), g * 0.4f, g * 0.25f, stuff);
            Ball(sh + new Vector3(side * g * 1.0f, -g * 1.3f, g * 0.9f), new Vector3(g * 0.4f, g * 0.4f, g * 0.4f), stuff, 0.4f);
        }
        Body();
    }

    // Mixed bodies ----------------------------------------------------------------------------------------------

    private void Taur()
    {
        string low = a.Head;
        float g = 0.08f * (float)a.Bulk;
        Vector3 waist;
        switch (low)
        {
            case "serpent" or "fish" or "wisp":
                {
                    // A coiled lower body rising into a torso.
                    var col = low == "wisp" ? (a.Material == "fire" ? Ember(new Color("ff8a30")) : (new Color("#" + a.Hide) * 0.7f) with { A = MeshKit.Flag.Magic }) : hide;
                    if (low == "wisp")
                        for (int i = 0; i < 5; i++) k.Frustum(new Vector3(Mathf.Sin(i * 1.2f) * 0.02f, 0.05f + i * 0.05f, 0), 0.01f + i * 0.012f, 0.02f + i * 0.012f, 0.05f, 7, col);
                    else
                    {
                        var p = new Vector3(0, g * 0.5f, -g * 4);
                        for (int i = 1; i <= 6; i++)
                        {
                            float f = i / 6f;
                            var q = new Vector3(Mathf.Sin(f * 4) * g * 1.5f * (1 - f), g * 0.5f + f * f * g * 2.5f, -g * 4 + f * g * 4);
                            Seg(p, q, g * (0.4f + 0.5f * f), g * (0.45f + 0.5f * f), i % 2 == 0 ? hide : dark, 6);
                            p = q;
                        }
                        Part(TailPart, new Vector3(0, g * 0.5f, -g * 4));
                        if (low == "fish") foreach (int side in new[] { -1, 1 }) k.Tri(new Vector3(0, g * 0.5f, -g * 4), new Vector3(side * g * 1.6f, g * 0.3f, -g * 5), new Vector3(side * g * 0.4f, g * 0.6f, -g * 5.2f), dark);
                        Body();
                    }
                    waist = new Vector3(0, low == "wisp" ? 0.28f : g * 3.0f, low == "wisp" ? 0 : 0);
                    break;
                }
            default:
                {
                    // Four legs (or six, or eight) and a beast's body, the person rising from where its neck would be.
                    int pairs = a.Legs / 2;
                    float hip = low is "spider" or "scorpion" ? 0.14f : 0.24f * (float)a.Leg;
                    float len = low is "spider" ? 0.12f : 0.18f;
                    var chest = new Vector3(0, hip + g * 0.5f, len * 0.5f);
                    var rump = new Vector3(0, hip + g * 0.4f, -len * 0.5f);
                    if (low == "spider") Ball(rump + new Vector3(0, g * 0.5f, -g * 0.8f), new Vector3(g * 1.4f, g * 1.2f, g * 1.6f), dark, 0.1f, hide);
                    else
                    {
                        Ball(chest, new Vector3(g, g, g * 1.1f), hide, 0.1f, belly);
                        Ball(rump, new Vector3(g * 0.95f, g * 0.95f, g * 1.1f), hide, 0.1f, belly);
                    }
                    for (int i = 0; i < pairs; i++)
                    {
                        float z = pairs == 1 ? 0 : len * (0.55f - i * 1.1f / (pairs - 1));
                        foreach (int side in new[] { -1, 1 })
                        {
                            if (low is "spider" or "scorpion")
                            {
                                var hipAt = new Vector3(side * g * 0.6f, hip + g * 0.2f, z);
                                Part((i + (side < 0 ? 0 : 1)) % 2 == 0 ? LegA : LegB, hipAt);
                                var knee = hipAt + new Vector3(side * g * 2.0f, g * 1.6f, z * 0.5f);
                                Seg(hipAt, knee, g * 0.18f, g * 0.14f, dark, 4);
                                Seg(knee, new Vector3(side * g * 3.4f, 0, z * 1.6f), g * 0.14f, g * 0.04f, dark, 4);
                            }
                            else Leg(new Vector3(side * g * 0.6f, hip, z), (i + (side < 0 ? 0 : 1)) % 2 == 0 ? LegA : LegB, g * 0.3f, i > 0, hide, dark, low is "horse" or "boar", low is not ("horse" or "boar"));
                        }
                    }
                    Body();
                    if (low == "scorpion")
                    {
                        Part(TailPart, rump);
                        Seg(rump, rump + new Vector3(0, g * 2.5f, -g * 1.5f), g * 0.3f, g * 0.2f, dark);
                        Spike(rump + new Vector3(0, g * 2.5f, -g * 1.5f), new Vector3(0, 0, 1), g * 0.2f, g * 0.8f, Plain("c83a3a"));
                        Body();
                    }
                    else if (low != "spider") Tail(rump + new Vector3(0, g * 0.3f, -g * 0.8f), len * 0.8f, g * 0.25f, low == "lion" ? hide : dark);
                    if (low == "boar" || a.Id.Contains("rider") || a.Id.Contains("saltstrider"))
                    {
                        // A beast under a rider: its own head before the saddle.
                        var root = chest + new Vector3(0, g * 0.2f, g * 0.8f);
                        Part(Neck, root);
                        Head(low == "boar" ? "boar" : "lizard", root + new Vector3(0, g * 0.3f, g * 0.6f), g / 0.06f, Neck, root);
                        Body();
                        k.Box(chest + new Vector3(0, g * 0.9f, -g * 0.3f), new Vector3(g * 1.4f, g * 0.2f, g * 1.4f), Mark());
                        waist = chest + new Vector3(0, g * 1.4f, -g * 0.4f);
                    }
                    else waist = chest + new Vector3(0, g * 0.9f, g * 0.3f);
                    break;
                }
        }
        // The upper body: a torso, arms (six for a marilith), a head under its helm.
        bool headless = a.Id.Contains("dullahan");
        var skin = Plain(a.Hide);
        var torso = a.Frame is "Plate" or "Mail" ? Plain("8e969f") with { A = MeshKit.Flag.Metal } : a.Frame == "Leather" ? Plain("74492b") : skin;
        k.Frustum(waist, 0.06f, 0.09f, 0.14f, 7, torso);
        k.Frustum(waist + new Vector3(0, 0.14f, 0), 0.09f, 0.04f, 0.03f, 7, torso);
        k.Box(waist + new Vector3(0, 0.02f, 0.06f), new Vector3(0.06f, 0.08f, 0.01f), Mark());
        int arms = Math.Max(2, a.Arms);
        for (int i = 0; i < arms; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            int tier = i / 2;
            var sh = waist + new Vector3(side * 0.1f, 0.13f - tier * 0.04f, 0);
            Part(side < 0 ? ClawR : ClawL, sh);
            var hand = sh + new Vector3(side * (0.03f + tier * 0.04f), -0.12f + tier * 0.03f, 0.05f + tier * 0.02f);
            Seg(sh, hand, 0.025f, 0.02f, skin, 5);
            Ball(hand, new Vector3(0.024f, 0.024f, 0.024f), skin);
            if (side < 0 && (tier == 0 && a.Weapon != "" || tier > 0))
            {
                bool bow = a.Weapon == "Bow";
                var dir = new Vector3(0, 1, 0.6f).Normalized();
                if (bow) Seg(hand + new Vector3(0, -0.1f, 0.02f), hand + new Vector3(0, 0.1f, 0.02f), 0.006f, 0.006f, Plain("7a5232"), 4);
                else if (a.Weapon is "Spear" or "Glaive" || a.Id.Contains("rider") || a.Id.Contains("saltstrider")) { Seg(hand - dir * 0.1f, hand + dir * 0.32f, 0.006f, 0.006f, Plain("7a5232"), 4); Spike(hand + dir * 0.32f, dir, 0.012f, 0.05f, Plain("c9d1da") with { A = MeshKit.Flag.Rune }); }
                else Spike(hand, dir, 0.012f, 0.18f, Plain("c9d1da") with { A = MeshKit.Flag.Rune });
            }
            if (side > 0 && tier > 0) Spike(hand, new Vector3(0, 1, 0.6f), 0.012f, 0.16f, Plain("c9d1da") with { A = MeshKit.Flag.Rune });
        }
        Body();
        var hc = waist + new Vector3(0, 0.21f, 0);
        Part(Neck, waist + new Vector3(0, 0.16f, 0));
        if (headless) { Ball(hc + new Vector3(0.12f, -0.12f, 0.05f), new Vector3(0.04f, 0.045f, 0.04f), skin); Eyes(hc + new Vector3(0.12f, -0.11f, 0.09f), 0.015f, 0.008f, 2); }
        else
        {
            Ball(hc, new Vector3(0.055f, 0.06f, 0.055f), skin);
            Eyes(hc + new Vector3(0, 0, 0.05f), 0.02f, 0.01f, Math.Max(1, a.Eyes));
            switch (a.Helm)
            {
                case "GreatHelm" or "Sallet": k.Frustum(hc + new Vector3(0, -0.01f, 0), 0.062f, 0.055f, 0.07f, 7, Plain("c9d1da") with { A = MeshKit.Flag.Metal }); break;
                case "Hood" or "Cowl": k.Frustum(hc + new Vector3(0, -0.02f, -0.01f), 0.07f, 0.02f, 0.11f, 7, Mark(0.8f)); break;
                case "Circlet" or "Feathered": k.Frustum(hc + new Vector3(0, 0.03f, 0), 0.058f, 0.058f, 0.012f, 7, Plain("f2c55a") with { A = MeshKit.Flag.Metal }); break;
                default: Ball(hc + new Vector3(0, 0.03f, -0.02f), new Vector3(0.06f, 0.04f, 0.055f), dark); break;
            }
            HeadMarks(hc, 1, "");
            if (a.Material == "fire") Flames(hc + new Vector3(0, 0.02f, 0), hc + new Vector3(0, 0.04f, -0.03f), 0.08f);
        }
        Body();
    }

    private void Swarm()
    {
        string h = a.Head;
        int n = h is "snake" or "fish" or "hand" ? 9 : 14;
        float g = 0.03f;
        for (int i = 0; i < n; i++)
        {
            float ang = i * 2.4f, r = 0.05f + 0.22f * Mathf.Sqrt((i + 0.5f) / n);
            bool flies = h is "bat" or "bird" or "insect";
            var at = new Vector3(Mathf.Cos(ang) * r, flies ? 0.12f + 0.25f * R(530 + i) : 0.02f, Mathf.Sin(ang) * r);
            Part(Mote, at);
            float yaw = R(540 + i) * Mathf.Tau;
            var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            var side = new Vector3(fwd.Z, 0, -fwd.X);
            switch (h)
            {
                case "rat":
                    Ball(at + new Vector3(0, g * 0.6f, 0), new Vector3(g * 0.8f, g * 0.7f, g * 0.8f), hide);
                    Ball(at + new Vector3(0, g * 0.6f, 0) + fwd * g, new Vector3(g * 0.4f, g * 0.4f, g * 0.4f), hide);
                    Seg(at + new Vector3(0, g * 0.4f, 0) - fwd * g * 0.8f, at - fwd * g * 2.4f, g * 0.12f, 0.001f, Plain("c88a8a"), 3);
                    if (Has("plague")) k.Box(at + new Vector3(0, g * 1.4f, 0), new Vector3(g * 0.3f, g * 0.3f, g * 0.3f), Glow(new Color("c8ff6a")));
                    break;
                case "bat" or "bird":
                    Ball(at, new Vector3(g * 0.5f, g * 0.5f, g * 0.6f), hide);
                    k.Tri(at, at + side * g * 2.2f + fwd * g * 0.2f, at - fwd * g * 0.6f + side * g * 0.8f, h == "bat" ? dark : hide);
                    k.Tri(at, at - side * g * 2.2f + fwd * g * 0.2f, at - fwd * g * 0.6f - side * g * 0.8f, h == "bat" ? dark : hide);
                    k.Tri(at, at - fwd * g * 0.6f + side * g * 0.8f, at + side * g * 2.2f + fwd * g * 0.2f, h == "bat" ? dark : hide);
                    k.Tri(at, at - fwd * g * 0.6f - side * g * 0.8f, at - side * g * 2.2f + fwd * g * 0.2f, h == "bat" ? dark : hide);
                    break;
                case "snake":
                    for (int s = 0; s < 4; s++) Ball(at + fwd * (s - 1.5f) * g * 0.9f + side * Mathf.Sin(s * 1.6f) * g * 0.6f + new Vector3(0, g * 0.3f, 0), new Vector3(g * 0.4f, g * 0.35f, g * 0.4f), s % 2 == 0 ? hide : dark);
                    break;
                case "fish":
                    Ball(at + new Vector3(0, g, 0), new Vector3(g * 0.5f, g * 0.6f, g * 0.9f), hide);
                    k.Tri(at + new Vector3(0, g, 0) - fwd * g * 0.8f, at + new Vector3(0, g * 1.6f, 0) - fwd * g * 1.5f, at + new Vector3(0, g * 0.4f, 0) - fwd * g * 1.5f, dark);
                    k.Tri(at + new Vector3(0, g, 0) - fwd * g * 0.8f, at + new Vector3(0, g * 0.4f, 0) - fwd * g * 1.5f, at + new Vector3(0, g * 1.6f, 0) - fwd * g * 1.5f, dark);
                    k.Box(at + new Vector3(0, g * 0.9f, 0) + fwd * g * 0.6f, new Vector3(g * 0.6f, g * 0.15f, g * 0.2f), teeth);
                    break;
                case "hand":
                    Ball(at + new Vector3(0, g * 0.4f, 0), new Vector3(g * 0.6f, g * 0.3f, g * 0.7f), Plain("c8b0a0"));
                    for (int f = -2; f <= 2; f++) Seg(at + new Vector3(0, g * 0.3f, 0) + side * f * g * 0.25f + fwd * g * 0.4f, at + side * f * g * 0.4f + fwd * g * 1.5f, g * 0.1f, g * 0.06f, Plain("c8b0a0"), 3);
                    break;
                default:
                    Ball(at, new Vector3(g * 0.35f, g * 0.3f, g * 0.5f), dark);
                    k.Tri(at, at + side * g * 1.2f, at - fwd * g + side * g * 0.4f, Glow(new Color(0.8f, 0.9f, 1f) * 0.3f));
                    k.Tri(at, at - side * g * 1.2f, at - fwd * g - side * g * 0.4f, Glow(new Color(0.8f, 0.9f, 1f) * 0.3f));
                    break;
            }
        }
        Body();
    }

    private void Thing()
    {
        string h = a.Head;
        var wood = Plain("7a5232");
        switch (h)
        {
            case "chest":
                k.Box(new Vector3(0, 0, 0), new Vector3(0.28f, 0.14f, 0.18f), wood, Plain("5a3a22"));
                foreach (int side in new[] { -1, 1 }) k.Box(new Vector3(side * 0.12f, 0, 0), new Vector3(0.025f, 0.145f, 0.185f), Plain("8a8f98") with { A = MeshKit.Flag.Metal });
                Part(Jaw, new Vector3(0, 0.14f, -0.09f));
                k.Box(new Vector3(0, 0.14f, 0), new Vector3(0.28f, 0.07f, 0.18f), wood, Plain("8a5a32"));
                for (int i = 0; i < 6; i++) Spike(new Vector3(-0.11f + i * 0.044f, 0.14f, 0.085f), new Vector3(0, -1, 0), 0.01f, 0.04f, teeth);
                Body();
                for (int i = 0; i < 6; i++) Spike(new Vector3(-0.11f + i * 0.044f, 0.14f, 0.085f), new Vector3(0, 1, 0), 0.01f, 0.03f, teeth);
                Part(Tentacle, new Vector3(0, 0.13f, 0.05f));
                Seg(new Vector3(0, 0.13f, 0.05f), new Vector3(0, 0.05f, 0.25f), 0.03f, 0.015f, Plain("c84a5a"), 5);
                Part(ClawR, new Vector3(-0.1f, 0.05f, 0.08f));
                Seg(new Vector3(-0.1f, 0.05f, 0.08f), new Vector3(-0.2f, 0.0f, 0.18f), 0.03f, 0.02f, hide, 5);
                Body();
                break;
            case "door":
                k.Box(new Vector3(0, 0, 0), new Vector3(0.36f, 0.62f, 0.06f), Plain("4a4440"));
                k.Box(new Vector3(0, 0.02f, 0.01f), new Vector3(0.24f, 0.5f, 0.05f), wood);
                Part(Jaw, new Vector3(0, 0.25f, 0.04f));
                for (int i = 0; i < 5; i++) { Spike(new Vector3(-0.09f + i * 0.045f, 0.3f, 0.04f), new Vector3(0, -1, 0.2f), 0.012f, 0.05f, teeth); Spike(new Vector3(-0.09f + i * 0.045f, 0.2f, 0.04f), new Vector3(0, 1, 0.2f), 0.012f, 0.05f, teeth); }
                Body();
                Eyes(new Vector3(0, 0.42f, 0.045f), 0.06f, 0.02f, 2);
                break;
            case "rug":
                for (int i = 0; i < 5; i++)
                {
                    var r = new Vector3(0, 0.05f, -0.15f + i * 0.08f);
                    Part(Tentacle, new Vector3(0, 0.05f, -0.2f));
                    k.Box(r, new Vector3(0.4f, 0.015f, 0.082f), i % 2 == 0 ? Mark() : hide);
                }
                Body();
                break;
            case "sword":
                Spike(new Vector3(0, 0.12f, 0), new Vector3(0, 1, 0), 0.02f, 0.4f, Plain("c9d1da") with { A = MeshKit.Flag.Rune });
                k.Box(new Vector3(0, 0.1f, 0), new Vector3(0.12f, 0.02f, 0.025f), Plain("f2c55a") with { A = MeshKit.Flag.Metal });
                Seg(new Vector3(0, 0.0f, 0), new Vector3(0, 0.1f, 0), 0.012f, 0.012f, Plain("45291a"), 4);
                Ball(new Vector3(0, 0, 0), new Vector3(0.02f, 0.02f, 0.02f), eye);
                break;
            case "catapult":
                foreach (int side in new[] { -1, 1 }) for (int i = 0; i < 2; i++) k.Frustum(new Vector3(side * 0.15f, 0.06f, (i - 0.5f) * 0.3f), 0.06f, 0.06f, 0.025f, 10, Plain("e6dcc0"));
                k.Box(new Vector3(0, 0.07f, 0), new Vector3(0.28f, 0.06f, 0.4f), Plain("d8ceb0"));
                Part(ClawR, new Vector3(0, 0.15f, -0.1f));
                Seg(new Vector3(0, 0.15f, -0.1f), new Vector3(0, 0.45f, 0.1f), 0.02f, 0.015f, Plain("e6dcc0"), 4);
                Ball(new Vector3(0, 0.47f, 0.12f), new Vector3(0.05f, 0.05f, 0.05f), Plain("e6dcc0"));
                Body();
                for (int i = 0; i < 4; i++) k.Frustum(new Vector3(0, 0.13f, -0.15f + i * 0.1f), 0.12f, 0.12f, 0.015f, 6, Plain("e6dcc0"));
                break;
            case "cart":
                foreach (int side in new[] { -1, 1 }) foreach (float z in new[] { -0.12f, 0.12f })
                    {
                        k.Push(new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2), new Vector3(side * 0.13f, 0.07f, z)));
                        k.Frustum(Vector3.Zero, 0.07f, 0.07f, 0.02f, 8, Plain("3a2a1a"));
                        k.Pop();
                    }
                k.Box(new Vector3(0, 0.08f, 0), new Vector3(0.24f, 0.08f, 0.38f), Plain("4a3a2a"));
                for (int i = 0; i < 5; i++) Ball(new Vector3((R(550 + i) - 0.5f) * 0.18f, 0.18f + R(560 + i) * 0.06f, (R(570 + i) - 0.5f) * 0.3f), new Vector3(0.05f, 0.04f, 0.06f), Plain(i % 2 == 0 ? "8a9a7a" : "e6dcc0"), 0.3f);
                foreach (int side in new[] { -1, 1 }) { var r = new Vector3(side * 0.08f, 0.2f, 0.1f); Part(side < 0 ? ClawR : ClawL, r); Seg(r, r + new Vector3(side * 0.08f, 0.12f, 0.1f), 0.015f, 0.012f, Plain("8a9a7a"), 4); }
                Body();
                break;
            default:
                Ball(new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.065f, 0.07f), h == "skull" && a.Id.Contains("gilded") ? Plain("f2c55a") with { A = MeshKit.Flag.Metal } : Plain("e6dcc0"));
                Part(Jaw, new Vector3(0, 0.27f, 0.02f));
                k.Box(new Vector3(0, 0.22f, 0.02f), new Vector3(0.05f, 0.025f, 0.05f), Plain("d8ceb0"));
                Body();
                Eyes(new Vector3(0, 0.305f, 0.06f), 0.022f, 0.016f, 2);
                if (a.Id.Contains("candle")) { k.Frustum(new Vector3(0, 0.36f, 0), 0.015f, 0.015f, 0.05f, 6, Plain("eee0b0")); Ball(new Vector3(0, 0.425f, 0), new Vector3(0.012f, 0.02f, 0.012f), Ember(new Color("ffb030"))); }
                break;
        }
        Body();
    }
}
