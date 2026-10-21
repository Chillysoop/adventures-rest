using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The heroes' pieces, made in code like every other model: five bodies by the armour worn, a head
/// for each class, every kind of weapon and what goes in the off hand, and a cape. All share one rig
/// (rig.gdshaderinc), so any head, body and weapon fit together and swing as one; each piece is its
/// own instanced batch, and a hero is drawn as the handful of pieces they wear (see <see cref="Heroes"/>).
/// Proportions are heroic: big heads and hands, broad pauldrons, weapons a size too large, so a hero
/// reads at a glance from the RTS camera.
/// </summary>
public static class HeroKit
{
    public enum Frame { Plate, Mail, Leather, Robe, Bare, Bones, Brute, Beast, Spider, Wyrm, Wraith, Crawler, Rat }

    public enum Head { GreatHelm, WingedHelm, HornedHelm, Hood, WizardHat, Cowl, Circlet, Mitre, Antlers, Feathered, Topknot, Sallet, WitchHat, Coif, Skull, Tusks, Lizard, None }

    public enum Weapon { None, Sword, Greatsword, Axe, Greataxe, Hammer, Maul, Mace, Spear, Glaive, Dagger, Rapier, Staff, Sickle, Bow }

    public enum OffHand { None, Kite, Round, Orb, Lute, Lantern, Dagger, Book, Bow }

    /// <summary>Where a worn trinket shows: a ring on the sword hand, a pendant, bracers, a belt gem, a circlet's stone, anklets. A cloak shows on the cape instead.</summary>
    public enum Trinket { None, Ring, Pendant, Bracers, Belt, Circlet, Anklets }

    // Rig joints, matching rig.gdshaderinc.
    public static readonly Vector3 HandR = new(-0.128f, 0.19f, 0.02f), HandL = new(0.128f, 0.19f, 0.02f);
    public const float ArmR = 1, ArmL = 2, LegR = 3, LegL = 4, HeadPart = 5, CapePart = 6, ItemR = 7, ItemL = 8;

    private static Color Hex(string hex, float flag = MeshKit.Flag.Plain) => new Color(hex) { A = flag };
    private static Color Trim(float v = 1) => new(v, v, v, MeshKit.Flag.Trim);
    private static Color Metal(string hex) => Hex(hex, MeshKit.Flag.Metal);
    private static Color Rune(string hex) => Hex(hex, MeshKit.Flag.Rune);
    private static Color Glow(string hex) => Hex(hex, MeshKit.Flag.Magic);
    private static readonly Color Skin = new(1, 1, 1, MeshKit.Flag.Skin);

    private static readonly Color Steel = Metal("c9d1da"), DarkSteel = Metal("7a838e"), GoldM = Metal("f2c55a"), Chain = Metal("8e969f");
    private static readonly Color Edge = Rune("e4ecf4"), Leather = Hex("74492b"), DarkLeather = Hex("45291a"), Wood = Hex("7a5232"), Fur = Hex("d9c7a2"), Ink = Hex("1c1410");

    // Bodies ------------------------------------------------------------------------------------------

    public static void Body(MeshKit k, Frame f)
    {
        if (f == Frame.Beast)
        {
            Beast(k);
            return;
        }
        if (f is Frame.Spider or Frame.Wyrm or Frame.Wraith or Frame.Crawler or Frame.Rat)
        {
            Creature(k, f);
            return;
        }
        if (f == Frame.Bones)
        {
            Bones(k);
            return;
        }
        var (legs, boots) = f switch
        {
            Frame.Brute => (Skin, DarkLeather),
            Frame.Plate => (Steel, DarkSteel),
            Frame.Mail => (Chain, DarkLeather),
            Frame.Bare => (Skin, DarkLeather),
            _ => (Hex("4a3a2c"), DarkLeather),
        };
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? LegR : LegL;
            float x = side * 0.045f;
            k.Frustum(new Vector3(x, 0.06f, 0), 0.031f, 0.036f, 0.115f, 6, legs);
            k.Frustum(new Vector3(x, 0, 0.004f), 0.037f, 0.033f, 0.075f, 6, boots);
            k.Box(new Vector3(x, 0, 0.032f), new Vector3(0.052f, 0.034f, 0.05f), boots);
            if (f == Frame.Plate) k.Blob(new Vector3(x, 0.095f, 0.028f), new Vector3(0.022f, 0.02f, 0.016f), GoldM);
            if (f == Frame.Bare) k.Frustum(new Vector3(x, 0.05f, 0.004f), 0.039f, 0.039f, 0.025f, 6, Fur);
        }

        k.Part = 0;
        var chest = f switch
        {
            Frame.Plate => Steel,
            Frame.Mail => Chain,
            Frame.Leather => Leather,
            Frame.Robe => Trim(),
            _ => Skin,
        };
        if (f == Frame.Brute)
        {
            // Hunched and broad: a bare chest, a loincloth in the host's colour, one great spiked pauldron.
            k.Frustum(new Vector3(0, 0.145f, 0), 0.08f, 0.085f, 0.045f, 8, DarkLeather, null, Mathf.Pi / 8);
            k.Frustum(new Vector3(0, 0.19f, 0.01f), 0.085f, 0.118f, 0.15f, 8, Skin, null, Mathf.Pi / 8);
            k.Frustum(new Vector3(0, 0.34f, 0.01f), 0.118f, 0.05f, 0.04f, 8, Skin, null, Mathf.Pi / 8);
            k.Quad(new(-0.05f, 0.15f, 0.086f), new(0.05f, 0.15f, 0.086f), new(0.055f, 0.05f, 0.08f), new(-0.055f, 0.05f, 0.08f), Trim());
            k.Quad(new(0.05f, 0.15f, -0.086f), new(-0.05f, 0.15f, -0.086f), new(-0.055f, 0.05f, -0.08f), new(0.055f, 0.05f, -0.08f), Trim(0.8f));
            k.Blob(new Vector3(0.128f, 0.35f, 0), new Vector3(0.075f, 0.055f, 0.07f), DarkSteel);
            k.Frustum(new Vector3(0.14f, 0.38f, 0), 0.02f, 0, 0.07f, 4, Hex("d8d0c0"));
            k.Frustum(new Vector3(0.1f, 0.38f, 0.03f), 0.016f, 0, 0.05f, 4, Hex("d8d0c0"));
            k.Push(new Transform3D(new Basis(Vector3.Back, -0.6f), new Vector3(0, 0.26f, 0.1f)));
            k.Box(Vector3.Zero, new Vector3(0.026f, 0.22f, 0.012f), DarkLeather);
            k.Pop();
            Arms(k, Skin, Skin, DarkLeather, 0.034f);
            return;
        }
        // Hips and a broad belt with a gold buckle.
        k.Frustum(new Vector3(0, 0.145f, 0), 0.07f, 0.075f, 0.045f, 8, DarkLeather, null, Mathf.Pi / 8);
        k.Box(new Vector3(0, 0.15f, 0.074f), new Vector3(0.034f, 0.032f, 0.012f), GoldM);
        // A chest that widens to the shoulders, and the shoulders' slope to the neck.
        k.Frustum(new Vector3(0, 0.19f, 0), 0.074f, 0.1f, 0.15f, 8, chest, null, Mathf.Pi / 8);
        k.Frustum(new Vector3(0, 0.34f, 0), 0.1f, 0.045f, 0.035f, 8, chest, null, Mathf.Pi / 8);
        k.Frustum(new Vector3(0, 0.36f, 0), 0.032f, 0.03f, 0.03f, 6, f == Frame.Plate ? GoldM : Skin);
        switch (f)
        {
            case Frame.Plate:
                Tabard(k, 0.32f, 0.07f, 0.042f);
                // Tassets over the hips and great round pauldrons with gold rims.
                k.Frustum(new Vector3(0, 0.1f, 0), 0.082f, 0.076f, 0.06f, 8, Steel, null, Mathf.Pi / 8);
                foreach (int side in new[] { -1, 1 })
                {
                    k.Blob(new Vector3(side * 0.118f, 0.345f, 0), new Vector3(0.07f, 0.05f, 0.066f), Steel);
                    k.Frustum(new Vector3(side * 0.118f, 0.318f, 0), 0.074f, 0.068f, 0.014f, 8, GoldM);
                }
                // A gold sunburst on the breastplate.
                k.Blob(new Vector3(0, 0.29f, 0.094f), new Vector3(0.024f, 0.024f, 0.01f), GoldM);
                break;
            case Frame.Mail:
                Tabard(k, 0.32f, 0.08f, 0.04f);
                foreach (int side in new[] { -1, 1 })
                    k.Blob(new Vector3(side * 0.115f, 0.34f, 0), new Vector3(0.056f, 0.04f, 0.052f), DarkSteel);
                break;
            case Frame.Leather:
                // A bandolier across the chest and a short skirt, a scarf in the class colour.
                k.Push(new Transform3D(new Basis(Vector3.Back, 0.7f), new Vector3(0, 0.26f, 0.088f)));
                k.Box(Vector3.Zero, new Vector3(0.026f, 0.2f, 0.014f), DarkLeather);
                k.Pop();
                k.Frustum(new Vector3(0, 0.1f, 0), 0.08f, 0.074f, 0.055f, 8, Leather.Darkened(0.15f), null, Mathf.Pi / 8);
                k.Frustum(new Vector3(0, 0.335f, 0), 0.09f, 0.05f, 0.04f, 8, Trim(0.85f), null, Mathf.Pi / 8);
                foreach (int side in new[] { -1, 1 })
                    k.Blob(new Vector3(side * 0.112f, 0.34f, 0), new Vector3(0.046f, 0.032f, 0.044f), Leather.Darkened(0.2f));
                break;
            case Frame.Robe:
                // A robe to the ankles with a gold hem and collar, and a sash.
                k.Frustum(new Vector3(0, 0.005f, 0), 0.105f, 0.075f, 0.16f, 8, Trim(0.82f), null, Mathf.Pi / 8);
                k.Frustum(new Vector3(0, 0.005f, 0), 0.108f, 0.104f, 0.018f, 8, GoldM, null, Mathf.Pi / 8);
                k.Frustum(new Vector3(0, 0.355f, 0), 0.056f, 0.04f, 0.03f, 8, GoldM, null, Mathf.Pi / 8);
                k.Quad(new(-0.02f, 0.3f, 0.1f), new(0.02f, 0.3f, 0.1f), new(0.03f, 0.03f, 0.106f), new(-0.03f, 0.03f, 0.106f), Hex("e8d8b0"));
                foreach (int side in new[] { -1, 1 })
                    k.Blob(new Vector3(side * 0.11f, 0.338f, 0), new Vector3(0.044f, 0.032f, 0.042f), Trim(0.7f));
                break;
            case Frame.Bare:
                // A fur kilt, a strap, a great fur mantle over the shoulders.
                k.Frustum(new Vector3(0, 0.09f, 0), 0.085f, 0.075f, 0.07f, 8, Fur, null, Mathf.Pi / 8);
                k.Push(new Transform3D(new Basis(Vector3.Back, -0.6f), new Vector3(0, 0.26f, 0.09f)));
                k.Box(Vector3.Zero, new Vector3(0.024f, 0.2f, 0.012f), DarkLeather);
                k.Pop();
                k.Blob(new Vector3(0, 0.345f, -0.01f), new Vector3(0.15f, 0.045f, 0.09f), Fur, 0.25f, 3, Fur.Darkened(0.2f));
                break;
        }

        var (sleeve, hand) = f switch
        {
            Frame.Plate => (Steel, Steel),
            Frame.Mail => (Chain, DarkLeather),
            Frame.Leather => (Trim(0.55f), DarkLeather),
            Frame.Robe => (Trim(0.82f), Skin),
            _ => (Skin, Skin),
        };
        var cuff = f switch { Frame.Plate => GoldM, Frame.Robe => GoldM, Frame.Mail => DarkSteel, _ => Leather };
        Arms(k, sleeve, hand, cuff, f == Frame.Robe ? 0.042f : 0.027f);
    }

    private static void Arms(MeshKit k, Color sleeve, Color hand, Color cuff, float upper)
    {
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? ArmR : ArmL;
            float x = side * 0.122f;
            k.Frustum(new Vector3(x, 0.205f, 0.012f), upper, 0.033f, 0.13f, 6, sleeve);
            k.Blob(new Vector3(side * 0.128f, 0.19f, 0.02f), new Vector3(0.035f, 0.033f, 0.035f), hand);
            k.Frustum(new Vector3(x, 0.208f, 0.014f), 0.034f, 0.034f, 0.03f, 6, cuff);
        }
        k.Part = 0;
    }

    private static readonly Color Bone = Hex("e6dcc0"), Rust = Hex("8a5a3a");

    /// <summary>The risen dead: bare bones, a rag in the host's colour, a rusted cap.</summary>
    private static void Bones(MeshKit k)
    {
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? LegR : LegL;
            float x = side * 0.045f;
            k.Frustum(new Vector3(x, 0.02f, 0), 0.012f, 0.014f, 0.15f, 4, Bone);
            k.Blob(new Vector3(x, 0.09f, 0.01f), new Vector3(0.018f, 0.016f, 0.016f), Bone);
            k.Box(new Vector3(x, 0, 0.015f), new Vector3(0.03f, 0.02f, 0.05f), Bone);
        }
        k.Part = 0;
        k.Frustum(new Vector3(0, 0.15f, 0), 0.05f, 0.04f, 0.035f, 6, Bone);
        k.Frustum(new Vector3(0, 0.17f, -0.02f), 0.012f, 0.012f, 0.19f, 4, Bone);
        for (int i = 0; i < 4; i++)
            k.Frustum(new Vector3(0, 0.215f + i * 0.032f, 0), 0.068f - Mathf.Abs(i - 1.5f) * 0.006f, 0.068f - Mathf.Abs(i - 1.5f) * 0.006f, 0.014f, 8, Bone, null, Mathf.Pi / 8);
        k.Quad(new(-0.05f, 0.18f, 0.06f), new(0.05f, 0.18f, 0.06f), new(0.06f, 0.06f, 0.055f), new(-0.04f, 0.08f, 0.055f), Trim(0.7f));
        foreach (int side in new[] { -1, 1 }) k.Blob(new Vector3(side * 0.1f, 0.33f, 0), new Vector3(0.03f, 0.022f, 0.03f), Rust);
        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? ArmR : ArmL;
            k.Frustum(new Vector3(side * 0.122f, 0.205f, 0.012f), 0.011f, 0.013f, 0.13f, 4, Bone);
            k.Blob(new Vector3(side * 0.128f, 0.19f, 0.02f), new Vector3(0.022f, 0.02f, 0.022f), Bone);
        }
        k.Part = 0;
    }

    /// <summary>A great wolf of the wilds, low and long, the host's colour in its pelt.</summary>
    private static void Beast(MeshKit k)
    {
        var pelt = Trim(0.9f);
        k.Blob(new Vector3(0, 0.2f, 0), new Vector3(0.09f, 0.08f, 0.17f), pelt, 0.15f, 4, Trim(0.6f));
        k.Blob(new Vector3(0, 0.25f, 0.08f), new Vector3(0.1f, 0.08f, 0.09f), pelt, 0.25f, 6, Trim(0.6f));
        k.Blob(new Vector3(0, 0.27f, 0.2f), new Vector3(0.055f, 0.055f, 0.06f), pelt);
        k.Frustum(new Vector3(0, 0.255f, 0.24f), 0.03f, 0.015f, 0.07f, 5, Trim(0.7f), Hex("1a1410"));
        k.Push(new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), new Vector3(0, 0.255f, 0.24f)));
        k.Frustum(Vector3.Zero, 0.03f, 0.014f, 0.07f, 5, Trim(0.7f), Hex("1a1410"));
        k.Pop();
        foreach (int side in new[] { -1, 1 })
        {
            k.Frustum(new Vector3(side * 0.03f, 0.31f, 0.19f), 0.014f, 0, 0.04f, 3, Trim(0.6f));
            k.Box(new Vector3(side * 0.02f, 0.28f, 0.255f), new Vector3(0.012f, 0.01f, 0.01f), Glow("ffcc40"));
        }
        k.Push(new Transform3D(new Basis(Vector3.Right, -2.3f), new Vector3(0, 0.22f, -0.15f)));
        k.Frustum(Vector3.Zero, 0.03f, 0.008f, 0.14f, 5, pelt);
        k.Pop();
        foreach (int side in new[] { -1, 1 })
            foreach (float z in new[] { 0.1f, -0.1f })
            {
                k.Part = (side < 0) == (z > 0) ? LegR : LegL;
                k.Frustum(new Vector3(side * 0.05f, 0, z), 0.018f, 0.024f, 0.17f, 4, Trim(0.6f));
            }
        k.Part = 0;
    }

    /// <summary>A limb from a to b: a tapered post turned to run between them.</summary>
    private static void Limb(MeshKit k, Vector3 a, Vector3 b, float r0, float r1, Color c, int sides = 4)
    {
        var up = b - a;
        float len = up.Length();
        var y = up / len;
        var x = Mathf.Abs(y.Y) > 0.9f ? Vector3.Right : y.Cross(Vector3.Up).Normalized();
        var z = x.Cross(y);
        k.Push(new Transform3D(new Basis(x, y, z), a));
        k.Frustum(Vector3.Zero, r0, r1, len, sides, c);
        k.Pop();
    }

    /// <summary>
    /// The creatures that aren't men or wolves, in the host's colour: a great spider, a wyrm on four legs with
    /// wings, a hooded wraith that floats, a many-legged crawler of the hives, a giant rat.
    /// </summary>
    private static void Creature(MeshKit k, Frame f)
    {
        var hide = Trim(0.9f);
        var dark = Trim(0.55f);
        switch (f)
        {
            case Frame.Spider:
                k.Blob(new Vector3(0, 0.2f, -0.13f), new Vector3(0.12f, 0.1f, 0.14f), hide, 0.12f, 3, dark);
                k.Blob(new Vector3(0, 0.15f, 0.04f), new Vector3(0.07f, 0.055f, 0.07f), dark);
                k.Blob(new Vector3(0, 0.15f, 0.11f), new Vector3(0.045f, 0.04f, 0.04f), dark);
                // A ring of eyes and the fangs under them.
                foreach (int side in new[] { -1, 1 })
                {
                    k.Box(new Vector3(side * 0.018f, 0.165f, 0.145f), new Vector3(0.012f, 0.012f, 0.01f), Glow("ff4a3a"));
                    k.Box(new Vector3(side * 0.034f, 0.175f, 0.135f), new Vector3(0.008f, 0.008f, 0.008f), Glow("ff4a3a"));
                    Limb(k, new Vector3(side * 0.015f, 0.13f, 0.14f), new Vector3(side * 0.01f, 0.09f, 0.16f), 0.008f, 0.002f, Hex("1a1410"));
                }
                // A band of the host's mark across the abdomen.
                k.Blob(new Vector3(0, 0.29f, -0.14f), new Vector3(0.05f, 0.012f, 0.06f), Trim(1.1f));
                foreach (int side in new[] { -1, 1 })
                    for (int i = 0; i < 4; i++)
                    {
                        k.Part = (i % 2 == 0) == (side < 0) ? LegR : LegL;
                        float z = 0.1f - i * 0.065f;
                        var hip = new Vector3(side * 0.05f, 0.16f, z * 0.5f + 0.03f);
                        var knee = new Vector3(side * 0.17f, 0.3f, z * 1.4f + 0.03f);
                        var foot = new Vector3(side * 0.27f, 0, z * 2.1f + 0.03f);
                        Limb(k, hip, knee, 0.014f, 0.011f, dark);
                        Limb(k, knee, foot, 0.011f, 0.004f, dark);
                    }
                break;
            case Frame.Wyrm:
                k.Blob(new Vector3(0, 0.24f, -0.02f), new Vector3(0.1f, 0.1f, 0.18f), hide, 0.1f, 5, dark);
                k.Blob(new Vector3(0, 0.22f, 0.02f), new Vector3(0.07f, 0.07f, 0.12f), Trim(1.15f));
                // The neck rising to a horned head, the jaw a little open.
                Limb(k, new Vector3(0, 0.27f, 0.12f), new Vector3(0, 0.42f, 0.2f), 0.05f, 0.035f, hide, 6);
                k.Blob(new Vector3(0, 0.44f, 0.24f), new Vector3(0.045f, 0.04f, 0.07f), hide);
                k.Blob(new Vector3(0, 0.415f, 0.28f), new Vector3(0.03f, 0.018f, 0.05f), dark);
                foreach (int side in new[] { -1, 1 })
                {
                    Limb(k, new Vector3(side * 0.025f, 0.47f, 0.22f), new Vector3(side * 0.05f, 0.53f, 0.15f), 0.012f, 0.002f, Hex("e8dcc0"));
                    k.Box(new Vector3(side * 0.025f, 0.455f, 0.29f), new Vector3(0.012f, 0.008f, 0.01f), Glow("ffc030"));
                    // A wing folded half open over the back.
                    var root = new Vector3(side * 0.07f, 0.31f, 0.04f);
                    var tip = new Vector3(side * 0.3f, 0.5f, -0.12f);
                    Limb(k, root, tip, 0.012f, 0.005f, dark);
                    var low = new Vector3(side * 0.22f, 0.24f, -0.2f);
                    k.Tri(root, tip, low, dark);
                    k.Tri(root, low, tip, dark);
                }
                k.Push(new Transform3D(new Basis(Vector3.Right, -2.0f), new Vector3(0, 0.22f, -0.18f)));
                k.Frustum(Vector3.Zero, 0.05f, 0.006f, 0.3f, 5, hide);
                k.Pop();
                foreach (int side in new[] { -1, 1 })
                    foreach (float z in new[] { 0.1f, -0.12f })
                    {
                        k.Part = (side < 0) == (z > 0) ? LegR : LegL;
                        Limb(k, new Vector3(side * 0.07f, 0.2f, z), new Vector3(side * 0.09f, 0, z + 0.02f), 0.03f, 0.022f, hide);
                        k.Box(new Vector3(side * 0.09f, 0, z + 0.04f), new Vector3(0.05f, 0.02f, 0.05f), dark);
                    }
                break;
            case Frame.Wraith:
                // Robes that taper to nothing above the ground, a hood with no face but two eyes, long sleeves.
                k.Frustum(new Vector3(0, 0.06f, 0), 0.02f, 0.1f, 0.24f, 7, Trim(0.5f));
                k.Frustum(new Vector3(0, 0.3f, 0), 0.1f, 0.07f, 0.1f, 7, hide);
                k.Blob(new Vector3(0, 0.44f, -0.005f), new Vector3(0.07f, 0.075f, 0.07f), hide);
                k.Blob(new Vector3(0, 0.43f, 0.03f), new Vector3(0.05f, 0.055f, 0.04f), Hex("0a0808"));
                foreach (int side in new[] { -1, 1 })
                {
                    k.Box(new Vector3(side * 0.02f, 0.44f, 0.065f), new Vector3(0.014f, 0.01f, 0.01f), Glow("8ae0ff"));
                    k.Part = side < 0 ? ArmR : ArmL;
                    Limb(k, new Vector3(side * 0.09f, 0.37f, 0), new Vector3(side * 0.13f, 0.2f, 0.03f), 0.03f, 0.04f, hide, 5);
                    k.Blob(new Vector3(side * 0.13f, 0.19f, 0.03f), new Vector3(0.018f, 0.02f, 0.018f), Hex("d8e8f0"));
                }
                break;
            case Frame.Crawler:
                // A beetle-backed thing of the hives: segments, mandibles and six legs.
                for (int i = 0; i < 3; i++)
                    k.Blob(new Vector3(0, 0.15f + i * 0.01f, -0.12f + i * 0.1f), new Vector3(0.08f - i * 0.012f, 0.07f - i * 0.01f, 0.07f), i == 0 ? hide : dark);
                k.Blob(new Vector3(0, 0.2f, -0.12f), new Vector3(0.06f, 0.02f, 0.06f), Trim(1.15f));
                foreach (int side in new[] { -1, 1 })
                {
                    Limb(k, new Vector3(side * 0.02f, 0.16f, 0.12f), new Vector3(side * 0.005f, 0.14f, 0.2f), 0.012f, 0.003f, Hex("e8dcc0"));
                    Limb(k, new Vector3(side * 0.02f, 0.21f, 0.1f), new Vector3(side * 0.06f, 0.33f, 0.16f), 0.005f, 0.002f, dark);
                    k.Box(new Vector3(side * 0.03f, 0.19f, 0.135f), new Vector3(0.012f, 0.012f, 0.01f), Glow("c8ff6a"));
                    for (int i = 0; i < 3; i++)
                    {
                        k.Part = (i % 2 == 0) == (side < 0) ? LegR : LegL;
                        float z = 0.07f - i * 0.08f;
                        var hip = new Vector3(side * 0.05f, 0.13f, z);
                        var knee = new Vector3(side * 0.13f, 0.2f, z * 1.2f);
                        Limb(k, hip, knee, 0.012f, 0.009f, dark);
                        Limb(k, knee, new Vector3(side * 0.18f, 0, z * 1.5f), 0.009f, 0.003f, dark);
                    }
                }
                break;
            case Frame.Rat:
                k.Blob(new Vector3(0, 0.15f, -0.02f), new Vector3(0.08f, 0.075f, 0.14f), hide, 0.2f, 7, dark);
                k.Blob(new Vector3(0, 0.16f, 0.12f), new Vector3(0.045f, 0.045f, 0.06f), hide);
                k.Push(new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), new Vector3(0, 0.155f, 0.15f)));
                k.Frustum(Vector3.Zero, 0.03f, 0.008f, 0.07f, 5, hide, Hex("d88a8a"));
                k.Pop();
                foreach (int side in new[] { -1, 1 })
                {
                    k.Blob(new Vector3(side * 0.035f, 0.21f, 0.1f), new Vector3(0.022f, 0.026f, 0.008f), Hex("c88a8a"));
                    k.Box(new Vector3(side * 0.02f, 0.18f, 0.17f), new Vector3(0.01f, 0.01f, 0.01f), Glow("ff4a3a"));
                }
                Limb(k, new Vector3(0, 0.13f, -0.15f), new Vector3(0, 0.03f, -0.4f), 0.012f, 0.003f, Hex("c88a8a"));
                foreach (int side in new[] { -1, 1 })
                    foreach (float z in new[] { 0.07f, -0.09f })
                    {
                        k.Part = (side < 0) == (z > 0) ? LegR : LegL;
                        Limb(k, new Vector3(side * 0.045f, 0.1f, z), new Vector3(side * 0.05f, 0, z + 0.02f), 0.016f, 0.01f, dark);
                    }
                break;
        }
        k.Part = 0;
    }

    /// <summary>A tabard in the class colour, front and back, from the chest to below the belt.</summary>
    private static void Tabard(MeshKit k, float top, float bottom, float half)
    {
        k.Quad(new(-half, top, 0.099f), new(half, top, 0.099f), new(half * 1.15f, bottom, 0.086f), new(-half * 1.15f, bottom, 0.086f), Trim());
        k.Quad(new(half, top, -0.099f), new(-half, top, -0.099f), new(-half * 1.15f, bottom, -0.086f), new(half * 1.15f, bottom, -0.086f), Trim(0.8f));
        k.Quad(new(-half * 1.15f, bottom + 0.012f, 0.0875f), new(half * 1.15f, bottom + 0.012f, 0.0875f), new(half * 1.15f, bottom, 0.087f), new(-half * 1.15f, bottom, 0.087f), GoldM);
    }

    // Heads -------------------------------------------------------------------------------------------

    private static readonly Vector3 HeadAt = new(0, 0.43f, 0);

    public static void HeadPiece(MeshKit k, Head h)
    {
        k.Part = HeadPart;
        var c = HeadAt;
        if (h == Head.None) return;
        if (h is Head.Skull or Head.Tusks or Head.Lizard)
        {
            FoeHead(k, h, c);
            k.Part = 0;
            return;
        }
        bool face = h is not (Head.GreatHelm);
        k.Blob(c, new Vector3(0.062f, 0.066f, 0.06f), Skin);
        if (face)
        {
            k.Box(new Vector3(-0.022f, c.Y + 0.002f, 0.054f), new Vector3(0.012f, 0.014f, 0.008f), Ink);
            k.Box(new Vector3(0.022f, c.Y + 0.002f, 0.054f), new Vector3(0.012f, 0.014f, 0.008f), Ink);
        }
        switch (h)
        {
            case Head.GreatHelm:
                k.Frustum(c + new Vector3(0, -0.06f, 0), 0.066f, 0.068f, 0.1f, 8, Steel, Steel, Mathf.Pi / 8);
                k.Frustum(c + new Vector3(0, 0.04f, 0), 0.068f, 0.03f, 0.04f, 8, Steel, null, Mathf.Pi / 8);
                k.Box(c + new Vector3(0, 0.005f, 0.062f), new Vector3(0.09f, 0.012f, 0.01f), Ink);
                k.Box(c + new Vector3(0, -0.04f, 0.064f), new Vector3(0.012f, 0.04f, 0.008f), GoldM);
                // A plume in the class colour.
                k.Blob(c + new Vector3(0, 0.1f, -0.02f), new Vector3(0.018f, 0.04f, 0.05f), Trim(), 0.2f, 2);
                break;
            case Head.WingedHelm:
                k.Frustum(c + new Vector3(0, 0.0f, 0), 0.068f, 0.05f, 0.06f, 8, Steel, Steel, Mathf.Pi / 8);
                k.Frustum(c + new Vector3(0, 0.0f, 0), 0.07f, 0.07f, 0.014f, 8, GoldM, null, Mathf.Pi / 8);
                foreach (int side in new[] { -1, 1 })
                {
                    k.Push(new Transform3D(new Basis(Vector3.Back, side * -0.5f), c + new Vector3(side * 0.07f, 0.03f, -0.01f)));
                    k.Quad(new(0, 0, 0.02f), new(0, 0.1f, -0.02f), new(0, 0.06f, -0.06f), new(0, -0.01f, -0.03f), GoldM);
                    k.Quad(new(0, -0.01f, -0.03f), new(0, 0.06f, -0.06f), new(0, 0.1f, -0.02f), new(0, 0, 0.02f), GoldM);
                    k.Pop();
                }
                break;
            case Head.HornedHelm:
                k.Frustum(c + new Vector3(0, 0.005f, 0), 0.068f, 0.045f, 0.05f, 8, DarkSteel, DarkSteel, Mathf.Pi / 8);
                foreach (int side in new[] { -1, 1 })
                {
                    k.Push(new Transform3D(new Basis(Vector3.Back, side * -1.0f), c + new Vector3(side * 0.06f, 0.03f, 0)));
                    k.Frustum(Vector3.Zero, 0.02f, 0, 0.09f, 5, Hex("efe6cf"));
                    k.Pop();
                }
                // A wild beard.
                k.Blob(c + new Vector3(0, -0.05f, 0.04f), new Vector3(0.05f, 0.05f, 0.03f), Hex("9a4a22"), 0.3f, 5);
                break;
            case Head.Hood:
                k.Blob(c + new Vector3(0, 0.012f, -0.006f), new Vector3(0.07f, 0.074f, 0.068f), Trim(0.6f), 0.08f, 7);
                k.Frustum(c + new Vector3(0, 0.05f, -0.03f), 0.04f, 0, 0.06f, 5, Trim(0.6f));
                k.Box(c + new Vector3(0, -0.035f, 0.052f), new Vector3(0.07f, 0.03f, 0.012f), Hex("3a2e28"));
                break;
            case Head.WizardHat:
                k.Frustum(c + new Vector3(0, 0.035f, 0), 0.12f, 0.11f, 0.012f, 10, Trim(0.75f));
                k.Push(new Transform3D(new Basis(Vector3.Right, -0.25f), c + new Vector3(0, 0.045f, 0)));
                k.Frustum(Vector3.Zero, 0.06f, 0, 0.17f, 8, Trim(0.75f));
                k.Pop();
                k.Frustum(c + new Vector3(0, 0.045f, 0), 0.062f, 0.06f, 0.016f, 8, GoldM);
                k.Blob(c + new Vector3(0, -0.07f, 0.04f), new Vector3(0.045f, 0.07f, 0.03f), Hex("ece8e0"), 0.2f, 9);
                break;
            case Head.Cowl:
                k.Blob(c + new Vector3(0, 0.012f, -0.006f), new Vector3(0.07f, 0.074f, 0.068f), Trim(0.45f), 0.1f, 4);
                foreach (int side in new[] { -1, 1 })
                {
                    k.Push(new Transform3D(new Basis(Vector3.Back, side * -0.45f), c + new Vector3(side * 0.04f, 0.06f, 0.01f)));
                    k.Frustum(Vector3.Zero, 0.014f, 0, 0.06f, 5, Hex("2a2024"));
                    k.Pop();
                }
                // Eyes that burn with the patron's fire.
                k.Box(new Vector3(-0.022f, c.Y + 0.002f, 0.056f), new Vector3(0.014f, 0.012f, 0.008f), Glow("c070ff"));
                k.Box(new Vector3(0.022f, c.Y + 0.002f, 0.056f), new Vector3(0.014f, 0.012f, 0.008f), Glow("c070ff"));
                break;
            case Head.Circlet:
                k.Blob(c + new Vector3(0, 0.02f, -0.012f), new Vector3(0.066f, 0.062f, 0.064f), Hex("e8e0f0"), 0.15f, 6);
                k.Frustum(c + new Vector3(0, 0.025f, 0), 0.066f, 0.066f, 0.012f, 8, GoldM);
                k.Blob(c + new Vector3(0, 0.035f, 0.064f), new Vector3(0.014f, 0.016f, 0.01f), Glow("ff5aa0"));
                break;
            case Head.Mitre:
                k.Frustum(c + new Vector3(0, 0.03f, 0), 0.062f, 0.05f, 0.11f, 4, Trim(), null, Mathf.Pi / 4);
                k.Frustum(c + new Vector3(0, 0.03f, 0), 0.064f, 0.064f, 0.014f, 4, GoldM, null, Mathf.Pi / 4);
                k.Box(c + new Vector3(0, 0.06f, 0.044f), new Vector3(0.012f, 0.06f, 0.006f), GoldM);
                k.Box(c + new Vector3(0, 0.08f, 0.044f), new Vector3(0.04f, 0.012f, 0.006f), GoldM);
                break;
            case Head.Antlers:
                k.Blob(c + new Vector3(0, 0.02f, -0.012f), new Vector3(0.066f, 0.06f, 0.064f), Hex("5a3a22"), 0.2f, 8);
                k.Frustum(c + new Vector3(0, 0.03f, 0), 0.068f, 0.066f, 0.016f, 8, Hex("4f8a3a"));
                foreach (int side in new[] { -1, 1 })
                {
                    k.Push(new Transform3D(new Basis(Vector3.Back, side * -0.6f), c + new Vector3(side * 0.05f, 0.05f, 0)));
                    k.Frustum(Vector3.Zero, 0.01f, 0.006f, 0.09f, 4, Hex("d8c8a0"));
                    k.Push(MeshKit.At(0, 0.05f, 0));
                    k.Push(new Transform3D(new Basis(Vector3.Back, side * 0.9f), Vector3.Zero));
                    k.Frustum(Vector3.Zero, 0.007f, 0.003f, 0.045f, 4, Hex("d8c8a0"));
                    k.Pop();
                    k.Pop();
                    k.Pop();
                }
                break;
            case Head.Feathered:
                k.Blob(c + new Vector3(0, 0.0f, -0.02f), new Vector3(0.064f, 0.06f, 0.06f), Hex("c08a4a"), 0.2f, 5);
                k.Frustum(c + new Vector3(0, 0.04f, 0), 0.11f, 0.1f, 0.01f, 10, Trim(0.8f));
                k.Frustum(c + new Vector3(0, 0.045f, 0), 0.06f, 0.05f, 0.04f, 8, Trim(0.8f));
                k.Push(new Transform3D(new Basis(Vector3.Back, -0.5f), c + new Vector3(0.05f, 0.07f, -0.02f)));
                k.Quad(new(0, 0, 0), new(0.012f, 0.12f, 0), new(-0.012f, 0.12f, 0), new(-0.004f, 0, 0), Hex("f4f0e0"));
                k.Quad(new(-0.004f, 0, 0), new(-0.012f, 0.12f, 0), new(0.012f, 0.12f, 0), new(0, 0, 0), Hex("f4f0e0"));
                k.Pop();
                break;
            case Head.Topknot:
                k.Blob(c + new Vector3(0, 0.08f, -0.02f), new Vector3(0.022f, 0.022f, 0.022f), Hex("2a2018"));
                k.Frustum(c + new Vector3(0, 0.0f, 0), 0.064f, 0.064f, 0.014f, 8, Trim());
                break;
            case Head.Sallet:
                k.Frustum(c + new Vector3(0, 0.0f, 0), 0.07f, 0.05f, 0.06f, 8, Steel, Steel, Mathf.Pi / 8);
                k.Frustum(c + new Vector3(0, -0.005f, -0.01f), 0.085f, 0.07f, 0.012f, 8, Steel, null, Mathf.Pi / 8);
                k.Box(c + new Vector3(0, -0.02f, 0.058f), new Vector3(0.012f, 0.05f, 0.012f), Steel);
                k.Frustum(c + new Vector3(0, 0.06f, 0), 0.012f, 0.005f, 0.03f, 4, GoldM);
                break;
            case Head.WitchHat:
                k.Blob(c + new Vector3(0, -0.01f, -0.03f), new Vector3(0.07f, 0.07f, 0.05f), Hex("8a8a7a"), 0.3f, 4);
                k.Frustum(c + new Vector3(0, 0.035f, 0), 0.13f, 0.12f, 0.01f, 10, Hex("2e3428"));
                k.Push(new Transform3D(new Basis(Vector3.Right, -0.35f) * new Basis(Vector3.Back, 0.3f), c + new Vector3(0, 0.04f, 0)));
                k.Frustum(Vector3.Zero, 0.06f, 0.025f, 0.11f, 7, Hex("2e3428"));
                k.Push(new Transform3D(new Basis(Vector3.Right, -0.8f), new Vector3(0, 0.11f, 0)));
                k.Frustum(Vector3.Zero, 0.025f, 0, 0.06f, 6, Hex("2e3428"));
                k.Pop();
                k.Pop();
                k.Frustum(c + new Vector3(0, 0.04f, 0), 0.063f, 0.062f, 0.014f, 8, Trim());
                break;
            case Head.Coif:
                k.Blob(c + new Vector3(0, 0.006f, -0.004f), new Vector3(0.069f, 0.072f, 0.066f), Chain, 0.05f, 2);
                k.Frustum(c + new Vector3(0, 0.03f, 0), 0.07f, 0.07f, 0.014f, 8, GoldM);
                k.Box(new Vector3(-0.022f, c.Y + 0.002f, 0.06f), new Vector3(0.012f, 0.014f, 0.006f), Ink);
                k.Box(new Vector3(0.022f, c.Y + 0.002f, 0.06f), new Vector3(0.012f, 0.014f, 0.006f), Ink);
                k.Box(c + new Vector3(0, -0.04f, 0.06f), new Vector3(0.06f, 0.035f, 0.008f), Skin);
                break;
        }
        k.Part = 0;
    }

    private static void FoeHead(MeshKit k, Head h, Vector3 c)
    {
        switch (h)
        {
            case Head.Skull:
                k.Blob(c, new Vector3(0.056f, 0.06f, 0.056f), Bone);
                k.Box(c + new Vector3(0, -0.07f, 0.01f), new Vector3(0.06f, 0.03f, 0.05f), Bone);
                k.Box(new Vector3(-0.022f, c.Y, 0.05f), new Vector3(0.018f, 0.018f, 0.01f), Glow("5ac8ff"));
                k.Box(new Vector3(0.022f, c.Y, 0.05f), new Vector3(0.018f, 0.018f, 0.01f), Glow("5ac8ff"));
                k.Frustum(c + new Vector3(0, 0.02f, 0), 0.064f, 0.04f, 0.045f, 7, Rust, Rust);
                break;
            case Head.Tusks:
                k.Blob(c + new Vector3(0, -0.01f, 0.01f), new Vector3(0.066f, 0.062f, 0.064f), Skin);
                k.Box(c + new Vector3(0, -0.06f, 0.03f), new Vector3(0.08f, 0.04f, 0.05f), Skin);
                k.Frustum(c + new Vector3(-0.025f, -0.05f, 0.06f), 0.009f, 0, 0.035f, 4, Hex("f0e8d0"));
                k.Frustum(c + new Vector3(0.025f, -0.05f, 0.06f), 0.009f, 0, 0.035f, 4, Hex("f0e8d0"));
                k.Box(c + new Vector3(0, 0.015f, 0.05f), new Vector3(0.08f, 0.016f, 0.02f), Hex("2a2420"));
                k.Box(new Vector3(-0.022f, c.Y - 0.005f, 0.058f), new Vector3(0.014f, 0.01f, 0.008f), Glow("ff4a20"));
                k.Box(new Vector3(0.022f, c.Y - 0.005f, 0.058f), new Vector3(0.014f, 0.01f, 0.008f), Glow("ff4a20"));
                k.Frustum(c + new Vector3(0, 0.05f, -0.01f), 0.02f, 0.008f, 0.07f, 4, Hex("1e1a18"));
                break;
            case Head.Lizard:
                k.Blob(c, new Vector3(0.058f, 0.058f, 0.062f), Skin);
                k.Push(new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2 - 0.15f), c + new Vector3(0, -0.015f, 0.03f)));
                k.Frustum(Vector3.Zero, 0.04f, 0.022f, 0.09f, 6, Skin);
                k.Pop();
                foreach (int side in new[] { -1, 1 })
                {
                    k.Push(new Transform3D(new Basis(Vector3.Right, -0.9f), c + new Vector3(side * 0.035f, 0.035f, -0.02f)));
                    k.Frustum(Vector3.Zero, 0.014f, 0, 0.08f, 4, Hex("3a2a20"));
                    k.Pop();
                    k.Box(new Vector3(side * 0.03f, c.Y + 0.01f, 0.05f), new Vector3(0.012f, 0.01f, 0.01f), Glow("ffd040"));
                }
                break;
        }
    }

    // Weapons -----------------------------------------------------------------------------------------

    /// <summary>A weapon in the right hand, its grip at the hand and its business end along local +Y, pointed forward and a little up.</summary>
    public static void WeaponPiece(MeshKit k, Weapon w)
    {
        k.Part = ItemR;
        float tilt = w switch { Weapon.Staff => 0.3f, Weapon.Spear or Weapon.Glaive => 0.9f, _ => 1.3f };
        k.Push(new Transform3D(new Basis(Vector3.Right, tilt), HandR));
        switch (w)
        {
            case Weapon.Sword: Blade(k, 0.22f, 0.032f, 0.1f); break;
            case Weapon.Greatsword: Blade(k, 0.36f, 0.046f, 0.15f, 0.09f); break;
            case Weapon.Rapier:
                Blade(k, 0.28f, 0.014f, 0.05f);
                k.Blob(new Vector3(0, 0.03f, 0), new Vector3(0.034f, 0.02f, 0.034f), GoldM);
                break;
            case Weapon.Dagger: Blade(k, 0.1f, 0.026f, 0.06f); break;
            case Weapon.Axe:
                Haft(k, -0.05f, 0.26f, 0.011f);
                AxeHead(k, 0.2f, 0.08f, 1);
                break;
            case Weapon.Greataxe:
                Haft(k, -0.1f, 0.4f, 0.013f);
                AxeHead(k, 0.3f, 0.12f, 1);
                AxeHead(k, 0.3f, 0.1f, -1);
                k.Frustum(new Vector3(0, 0.37f, 0), 0.016f, 0, 0.05f, 4, Steel);
                break;
            case Weapon.Hammer:
                Haft(k, -0.05f, 0.25f, 0.011f);
                k.Box(new Vector3(0, 0.2f, 0), new Vector3(0.11f, 0.06f, 0.06f), Edge);
                k.Box(new Vector3(0, 0.198f, 0), new Vector3(0.03f, 0.064f, 0.064f), GoldM);
                break;
            case Weapon.Maul:
                Haft(k, -0.1f, 0.38f, 0.013f);
                k.Box(new Vector3(0, 0.28f, 0), new Vector3(0.15f, 0.09f, 0.09f), Edge);
                k.Box(new Vector3(0, 0.278f, 0), new Vector3(0.04f, 0.094f, 0.094f), GoldM);
                break;
            case Weapon.Mace:
                Haft(k, -0.05f, 0.2f, 0.011f);
                k.Blob(new Vector3(0, 0.2f, 0), new Vector3(0.042f, 0.05f, 0.042f), Edge, 0.5f, 11);
                k.Frustum(new Vector3(0, 0.24f, 0), 0.014f, 0, 0.03f, 4, GoldM);
                break;
            case Weapon.Spear:
                Haft(k, -0.18f, 0.5f, 0.01f);
                k.Frustum(new Vector3(0, 0.32f, 0), 0.024f, 0, 0.09f, 4, Edge);
                k.Frustum(new Vector3(0, 0.3f, 0), 0.016f, 0.016f, 0.02f, 6, GoldM);
                break;
            case Weapon.Glaive:
                Haft(k, -0.18f, 0.5f, 0.011f);
                k.Push(MeshKit.At(0, 0.3f, 0));
                k.Quad(new(0, 0, 0.008f), new(0.02f, 0.16f, 0.008f), new(0.05f, 0.1f, 0.008f), new(0.04f, 0f, 0.008f), Edge);
                k.Quad(new(0.04f, 0f, -0.008f), new(0.05f, 0.1f, -0.008f), new(0.02f, 0.16f, -0.008f), new(0, 0, -0.008f), Edge);
                k.Pop();
                k.Frustum(new Vector3(0, 0.29f, 0), 0.018f, 0.018f, 0.02f, 6, GoldM);
                break;
            case Weapon.Staff:
                Haft(k, -0.17f, 0.52f, 0.012f);
                // Claws holding an orb at the head (the orb takes the class's glow from the instance's weapon colour when enchanted).
                foreach (float a in new[] { 0f, 2.1f, 4.2f })
                {
                    k.Push(new Transform3D(new Basis(Vector3.Up, a) * new Basis(Vector3.Back, -0.5f), new Vector3(0, 0.33f, 0)));
                    k.Frustum(Vector3.Zero, 0.008f, 0.004f, 0.05f, 4, GoldM);
                    k.Pop();
                }
                k.Blob(new Vector3(0, 0.37f, 0), new Vector3(0.032f, 0.032f, 0.032f), Hex("ffffff", MeshKit.Flag.Rune));
                break;
            case Weapon.Sickle:
                Haft(k, -0.04f, 0.12f, 0.01f);
                for (int i = 0; i < 4; i++)
                {
                    float a0 = i * 0.5f, a1 = (i + 1) * 0.5f;
                    var p0 = new Vector3(Mathf.Sin(a0) * 0.07f, 0.08f + Mathf.Cos(a0) * 0.06f, 0);
                    var p1 = new Vector3(Mathf.Sin(a1) * 0.07f, 0.08f + Mathf.Cos(a1) * 0.06f, 0);
                    var q0 = p0 * 0.75f + new Vector3(0, 0.02f, 0);
                    var q1 = p1 * 0.75f + new Vector3(0, 0.02f, 0);
                    k.Quad(p0, p1, q1, q0, Edge);
                    k.Quad(q0, q1, p1, p0, Edge);
                }
                break;
        }
        k.Pop();
        k.Part = 0;
    }

    private static void Blade(MeshKit k, float len, float width, float guard, float grip = 0.05f)
    {
        k.Frustum(new Vector3(0, -grip, 0), 0.01f, 0.01f, grip, 5, DarkLeather);
        k.Blob(new Vector3(0, -grip - 0.01f, 0), new Vector3(0.016f, 0.016f, 0.016f), GoldM);
        k.Box(new Vector3(0, 0, 0), new Vector3(guard, 0.02f, 0.026f), GoldM);
        k.Box(new Vector3(0, 0.02f, 0), new Vector3(width, len, 0.012f), Edge);
        // The point.
        float y = 0.02f + len, h = width * 1.2f, x = width / 2;
        k.Tri(new(-x, y, 0.006f), new(0, y + h, 0), new(x, y, 0.006f), Edge);
        k.Tri(new(x, y, -0.006f), new(0, y + h, 0), new(-x, y, -0.006f), Edge);
        k.Tri(new(-x, y, -0.006f), new(0, y + h, 0), new(-x, y, 0.006f), Edge);
        k.Tri(new(x, y, 0.006f), new(0, y + h, 0), new(x, y, -0.006f), Edge);
        // A fuller down the middle, gold, so the blade reads at a distance.
        k.Box(new Vector3(0, 0.03f, 0.0065f), new Vector3(width * 0.25f, len * 0.7f, 0.001f), GoldM);
    }

    private static void Haft(MeshKit k, float from, float len, float r) => k.Frustum(new Vector3(0, from, 0), r, r, len, 5, Wood);

    private static void AxeHead(MeshKit k, float y, float size, int side)
    {
        float s = side;
        var a = new Vector3(0, y - size * 0.3f, 0);
        var b = new Vector3(s * size, y - size * 0.55f, 0);
        var c = new Vector3(s * size * 1.05f, y + size * 0.5f, 0);
        var d = new Vector3(0, y + size * 0.3f, 0);
        var t = new Vector3(0, 0, 0.009f);
        if (side > 0)
        {
            k.Quad(a + t, d + t, c + t, b + t, Edge);
            k.Quad(b - t, c - t, d - t, a - t, Edge);
            k.Quad(b + t, c + t, c - t, b - t, Edge);
        }
        else
        {
            k.Quad(b + t, c + t, d + t, a + t, Edge);
            k.Quad(a - t, d - t, c - t, b - t, Edge);
            k.Quad(c + t, b + t, b - t, c - t, Edge);
        }
        k.Box(new Vector3(0, y - size * 0.32f, 0), new Vector3(0.03f, size * 0.64f, 0.03f), DarkSteel);
    }

    // The off hand ------------------------------------------------------------------------------------

    public static void OffHandPiece(MeshKit k, OffHand o)
    {
        k.Part = ItemL;
        switch (o)
        {
            case OffHand.Kite or OffHand.Round:
                // Held before the forearm, its face turned forward and out.
                k.Push(new Transform3D(new Basis(Vector3.Up, 0.95f), HandL + new Vector3(0.03f, 0.035f, 0.02f)));
                Vector2[] outline = o == OffHand.Kite
                    ? [new(0, -0.13f), new(0.075f, -0.02f), new(0.08f, 0.07f), new(0.04f, 0.1f), new(-0.04f, 0.1f), new(-0.08f, 0.07f), new(-0.075f, -0.02f)]
                    : Enumerable.Range(0, 10).Select(i => new Vector2(Mathf.Cos(i * Mathf.Tau / 10) * 0.085f, Mathf.Sin(i * Mathf.Tau / 10) * 0.085f)).ToArray();
                Slab(k, outline, 0.016f, Trim(), Wood, Steel);
                // A rim, a boss and a gold device.
                var rim = outline;
                for (int i = 0; i < rim.Length; i++)
                {
                    var p0 = rim[i];
                    var p1 = rim[(i + 1) % rim.Length];
                    var i0 = p0 * 0.82f;
                    var i1 = p1 * 0.82f;
                    k.Quad(new(p0.X, p0.Y, 0.0095f), new(i0.X, i0.Y, 0.0095f), new(i1.X, i1.Y, 0.0095f), new(p1.X, p1.Y, 0.0095f), Steel);
                }
                k.Blob(new Vector3(0, 0.01f, 0.012f), new Vector3(0.026f, 0.026f, 0.016f), GoldM);
                k.Pop();
                break;
            case OffHand.Orb:
                k.Blob(HandL + new Vector3(0, 0.05f, 0.03f), new Vector3(0.03f, 0.03f, 0.03f), Hex("ffffff", MeshKit.Flag.Rune));
                break;
            case OffHand.Lantern:
                k.Box(HandL + new Vector3(0, -0.07f, 0.02f), new Vector3(0.04f, 0.05f, 0.04f), GoldM);
                k.Box(HandL + new Vector3(0, -0.065f, 0.02f), new Vector3(0.044f, 0.04f, 0.03f), Glow("ffcf70"));
                k.Frustum(HandL + new Vector3(0, -0.02f, 0.02f), 0.024f, 0, 0.02f, 4, GoldM);
                break;
            case OffHand.Book:
                k.Box(HandL + new Vector3(0.01f, -0.02f, 0.03f), new Vector3(0.025f, 0.08f, 0.065f), Hex("6a2a2a"));
                k.Box(HandL + new Vector3(0.024f, -0.012f, 0.03f), new Vector3(0.004f, 0.064f, 0.055f), Hex("f0e6c8"));
                k.Box(HandL + new Vector3(-0.004f, 0.02f, 0.03f), new Vector3(0.006f, 0.02f, 0.02f), GoldM);
                break;
            case OffHand.Dagger:
                k.Push(new Transform3D(new Basis(Vector3.Right, 1.3f), HandL));
                Blade(k, 0.09f, 0.024f, 0.05f, 0.04f);
                k.Pop();
                break;
            case OffHand.Lute:
                k.Push(new Transform3D(new Basis(Vector3.Back, 0.5f) * new Basis(Vector3.Up, 1.2f), HandL + new Vector3(-0.03f, 0.04f, 0.06f)));
                k.Blob(new Vector3(0, -0.03f, 0), new Vector3(0.055f, 0.065f, 0.025f), Hex("c08040"));
                k.Box(new Vector3(0, 0.02f, 0), new Vector3(0.018f, 0.13f, 0.012f), Hex("5a3820"));
                k.Box(new Vector3(0, 0.15f, 0), new Vector3(0.026f, 0.03f, 0.014f), Hex("5a3820"));
                k.Pop();
                break;
            case OffHand.Bow:
                // A recurve bow standing in the left hand; the draw (rig.gdshaderinc) brings it up level.
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 3; i++)
                    {
                        float a0 = i * 0.32f, a1 = (i + 1) * 0.32f;
                        var p0 = new Vector3(0, side * Mathf.Sin(a0) * 0.2f, 0.035f * Mathf.Cos(a0) + 0.0f);
                        var p1 = new Vector3(0, side * Mathf.Sin(a1) * 0.2f, 0.035f * Mathf.Cos(a1) + 0.0f);
                        var (lo, hi) = side > 0 ? (p0, p1) : (p1, p0);
                        k.Push(new Transform3D(new Basis(Vector3.Right, Mathf.Atan2(hi.Z - lo.Z, hi.Y - lo.Y)), HandL + lo));
                        k.Box(Vector3.Zero, new Vector3(0.016f, p0.DistanceTo(p1) + 0.004f, 0.014f), i == 0 ? DarkLeather : Wood);
                        k.Pop();
                    }
                k.Box(HandL + new Vector3(0, -0.19f, 0.0f), new Vector3(0.003f, 0.38f, 0.003f), Hex("e8e0d0"));
                k.Frustum(HandL + new Vector3(0, 0.19f, 0.0f), 0.01f, 0, 0.03f, 4, GoldM);
                break;
        }
        k.Part = 0;
    }

    /// <summary>A flat convex shape (outline counter-clockwise in x, y) with thickness, its face toward +Z.</summary>
    private static void Slab(MeshKit k, Vector2[] outline, float thick, Color front, Color back, Color edge)
    {
        float t = thick / 2;
        var c = Vector2.Zero;
        foreach (var p in outline) c += p;
        c /= outline.Length;
        for (int i = 0; i < outline.Length; i++)
        {
            var p0 = outline[i];
            var p1 = outline[(i + 1) % outline.Length];
            k.Tri(new(c.X, c.Y, t), new(p1.X, p1.Y, t), new(p0.X, p0.Y, t), front);
            k.Tri(new(c.X, c.Y, -t), new(p0.X, p0.Y, -t), new(p1.X, p1.Y, -t), back);
            k.Quad(new(p0.X, p0.Y, t), new(p1.X, p1.Y, t), new(p1.X, p1.Y, -t), new(p0.X, p0.Y, -t), edge);
        }
    }

    /// <summary>A trinket's glowing stones and bands (Rune: they take the trinket's enchantment colour).</summary>
    public static void TrinketPiece(MeshKit k, Trinket t)
    {
        var gem = Rune("ffffff");
        switch (t)
        {
            case Trinket.Ring:
                k.Part = ArmR;
                k.Frustum(HandR + new Vector3(0, 0.012f, 0.0f), 0.038f, 0.038f, 0.01f, 8, GoldM);
                k.Blob(HandR + new Vector3(-0.02f, 0.02f, 0.03f), new Vector3(0.012f, 0.012f, 0.012f), gem);
                break;
            case Trinket.Pendant:
                k.Quad(new(-0.03f, 0.355f, 0.075f), new(0.03f, 0.355f, 0.075f), new(0.004f, 0.3f, 0.1f), new(-0.004f, 0.3f, 0.1f), GoldM);
                k.Blob(new Vector3(0, 0.29f, 0.104f), new Vector3(0.018f, 0.022f, 0.01f), gem);
                break;
            case Trinket.Bracers:
                foreach (int side in new[] { -1, 1 })
                {
                    k.Part = side < 0 ? ArmR : ArmL;
                    k.Frustum(new Vector3(side * 0.122f, 0.212f, 0.014f), 0.037f, 0.037f, 0.022f, 6, gem);
                }
                break;
            case Trinket.Belt:
                k.Blob(new Vector3(0, 0.166f, 0.084f), new Vector3(0.016f, 0.016f, 0.01f), gem);
                k.Frustum(new Vector3(0, 0.142f, 0), 0.078f, 0.078f, 0.008f, 8, gem, null, Mathf.Pi / 8);
                break;
            case Trinket.Circlet:
                k.Part = HeadPart;
                k.Blob(HeadAt + new Vector3(0, 0.035f, 0.066f), new Vector3(0.014f, 0.016f, 0.01f), gem);
                break;
            case Trinket.Anklets:
                foreach (int side in new[] { -1, 1 })
                {
                    k.Part = side < 0 ? LegR : LegL;
                    k.Frustum(new Vector3(side * 0.045f, 0.06f, 0.004f), 0.04f, 0.04f, 0.016f, 6, gem);
                }
                break;
        }
        k.Part = 0;
    }

    /// <summary>A legend's mark: a ring of light on the ground at their feet (drawn additive, aura.gdshader).</summary>
    public static void Halo(MeshKit k)
    {
        const int N = 24;
        var c = Hex("ffffff");
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.Tau / N, a1 = (i + 1) * Mathf.Tau / N;
            Vector3 P(float a, float r) => new(Mathf.Cos(a) * r, 0.012f, Mathf.Sin(a) * r);
            k.Quad(P(a0, 0.2f), P(a0, 0.25f), P(a1, 0.25f), P(a1, 0.2f), c);
            // Rays rising off the ring, short and faint.
            if (i % 3 == 0) k.Quad(P(a0, 0.23f), P(a0, 0.23f) + new Vector3(0, 0.12f, 0), P(a0 + 0.05f, 0.23f) + new Vector3(0, 0.12f, 0), P(a0 + 0.05f, 0.23f), c);
        }
    }

    /// <summary>A cape in the class colour hanging from the shoulders, lined darker.</summary>
    public static void Cape(MeshKit k)
    {
        k.Part = CapePart;
        Vector3 a = new(-0.075f, 0.355f, -0.075f), b = new(0.075f, 0.355f, -0.075f), c = new(0.095f, 0.07f, -0.1f), d = new(-0.095f, 0.07f, -0.1f);
        k.Quad(b, a, (a + d) / 2 + new Vector3(0, 0, -0.03f), (b + c) / 2 + new Vector3(0, 0, -0.03f), Trim(0.85f));
        k.Quad((b + c) / 2 + new Vector3(0, 0, -0.03f), (a + d) / 2 + new Vector3(0, 0, -0.03f), d, c, Trim(0.85f));
        k.Quad(a, b, (b + c) / 2 + new Vector3(0, 0, -0.025f), (a + d) / 2 + new Vector3(0, 0, -0.025f), Trim(0.45f));
        k.Quad((a + d) / 2 + new Vector3(0, 0, -0.025f), (b + c) / 2 + new Vector3(0, 0, -0.025f), c, d, Trim(0.45f));
        k.Part = 0;
    }
}
