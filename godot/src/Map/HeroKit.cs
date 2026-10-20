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
    public enum Frame { Plate, Mail, Leather, Robe, Bare }

    public enum Head { GreatHelm, WingedHelm, HornedHelm, Hood, WizardHat, Cowl, Circlet, Mitre, Antlers, Feathered, Topknot, Sallet, WitchHat, Coif }

    public enum Weapon { None, Sword, Greatsword, Axe, Greataxe, Hammer, Maul, Mace, Spear, Glaive, Dagger, Rapier, Staff, Sickle, Bow }

    public enum OffHand { None, Kite, Round, Orb, Lute, Lantern, Dagger, Book, Bow }

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
        var (legs, boots) = f switch
        {
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

        foreach (int side in new[] { -1, 1 })
        {
            k.Part = side < 0 ? ArmR : ArmL;
            float x = side * 0.122f;
            var (sleeve, hand) = f switch
            {
                Frame.Plate => (Steel, Steel),
                Frame.Mail => (Chain, DarkLeather),
                Frame.Leather => (Trim(0.55f), DarkLeather),
                Frame.Robe => (Trim(0.82f), Skin),
                _ => (Skin, Skin),
            };
            k.Frustum(new Vector3(x, 0.205f, 0.012f), f == Frame.Robe ? 0.042f : 0.027f, 0.033f, 0.13f, 6, sleeve);
            k.Blob(new Vector3(side * 0.128f, 0.19f, 0.02f), new Vector3(0.035f, 0.033f, 0.035f), hand);
            // Bracers or a gold cuff.
            var cuff = f switch { Frame.Plate => GoldM, Frame.Robe => GoldM, Frame.Mail => DarkSteel, _ => Leather };
            k.Frustum(new Vector3(x, 0.208f, 0.014f), 0.034f, 0.034f, 0.03f, 6, cuff);
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
