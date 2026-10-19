using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>The buildings of the city's institutions: the adventurers' hall, the druid grove and the grand bazaar.</summary>
public partial class TownView
{
    /// <summary>The adventurers' hall: a long two-storey hall under a red roof, company banners at the eaves, a contract board and a weapon rack out front.</summary>
    private static void AdventurersHall(MeshKit k, int v)
    {
        k.Box(new Vector3(0, 0.004f, 0.02f), new Vector3(0.98f, 0.012f, 0.78f), new Color("9a8a6a"));
        k.Push(MeshKit.At(0, 0, -0.12f, 0, Fit(4, 2, 0.86f)));
        Walls(k, 4, 2, 0, true, v + 301, true);
        Walls(k, 4, 2, 1, false, v + 303, false);
        k.Box(new Vector3(0, 0.96f, 0), new Vector3(4.1f, 0.08f, 2.1f), Palette.DarkTimber);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 4, 2, 1.25f, new Color("8e3b2e"), Palette.Plaster, 0.14f);
        k.Pop();
        Chimney(k, -1.4f, 2, -0.4f, true);
        // Company banners in their own colours along the front.
        for (int i = 0; i < 4; i++)
            k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(-1.5f + i, 0.95f, 1.02f)), Palette.Kenney(Dyes[(v + i) % Dyes.Length]));
        // The trophy over the door: a great horned skull.
        k.Box(new Vector3(0, 1.55f, 1.04f), new Vector3(0.36f, 0.26f, 0.08f), new Color("d8d0bc"));
        k.Frustum(new Vector3(-0.28f, 1.72f, 1.05f), 0.05f, 0, 0.3f, 5, new Color("cfc4a8"));
        k.Frustum(new Vector3(0.28f, 1.72f, 1.05f), 0.05f, 0, 0.3f, 5, new Color("cfc4a8"));
        k.Pop();
        // The contract board on two posts, papered with notices.
        k.Push(MeshKit.At(-0.26f, 0, 0.27f, 0.25f));
        foreach (float x in new[] { -0.07f, 0.07f }) k.Box(new Vector3(x, 0, 0), new Vector3(0.014f, 0.15f, 0.014f), Palette.DarkTimber);
        k.Box(new Vector3(0, 0.07f, 0), new Vector3(0.18f, 0.08f, 0.012f), Palette.Timber);
        for (int i = 0; i < 5; i++)
            k.Box(new Vector3(-0.065f + i * 0.032f, 0.08f + (i % 2) * 0.03f, 0.007f), new Vector3(0.024f, 0.028f, 0.002f), new Color("efe6cc"));
        k.Box(new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.012f, 0.04f), new Color("6e5238"));
        k.Pop();
        // A weapon rack and a training dummy in the yard.
        k.Box(new Vector3(0.28f, 0, 0.27f), new Vector3(0.14f, 0.012f, 0.02f), Palette.DarkTimber);
        for (int i = 0; i < 4; i++)
            k.Box(new Vector3(0.23f + i * 0.033f, 0.012f, 0.275f), new Vector3(0.006f, 0.11f, 0.006f), new Color("9aa0a8"));
        k.Box(new Vector3(0.4f, 0, 0.2f), new Vector3(0.016f, 0.1f, 0.016f), Palette.Timber);
        k.Blob(new Vector3(0.4f, 0.12f, 0.2f), new Vector3(0.035f, 0.045f, 0.03f), new Color("c8a86a"), 0.2f, v, new Color("a08050"));
        k.Box(new Vector3(0.4f, 0.09f, 0.2f), new Vector3(0.09f, 0.012f, 0.012f), Palette.Timber);
        k.Add("nature/campfire_logs", MeshKit.At(0.05f, 0.004f, 0.31f, 0, 0.1f));
    }

    /// <summary>The druid grove: mossy ground round a vast old tree, a sacred pool with lilies, a ring of standing stones and wild thickets.</summary>
    private static void DruidGrove(MeshKit k, int v)
    {
        var moss = new Color("5f7f3a");
        k.Box(Vector3.Zero, new Vector3(0.98f, 0.016f, 0.98f), moss, moss.Lightened(0.06f));
        // The sacred pool, rimmed with stones.
        var pool = new Vector3(0.2f, 0.016f, 0.2f);
        k.Frustum(pool, 0.17f, 0.17f, 0.006f, 12, new Color("3a6a8a", MeshKit.Flag.Window), new Color("4a86a8"));
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.Tau;
            k.Blob(pool + new Vector3(Mathf.Cos(a) * 0.18f, 0.01f, Mathf.Sin(a) * 0.18f), new Vector3(0.025f, 0.018f, 0.025f), new Color("8a8a7a"), 0.3f, i + v, new Color("6a6a5a"));
        }
        k.Add("nature/lily_large", MeshKit.At(pool.X - 0.05f, 0.024f, pool.Z + 0.03f, 0.6f, 0.12f));
        k.Add("nature/lily_large", MeshKit.At(pool.X + 0.06f, 0.024f, pool.Z - 0.05f, 2.1f, 0.1f));
        // A ring of standing stones about the great tree.
        var heart = new Vector3(-0.12f, 0.016f, -0.12f);
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.Tau + 0.3f;
            var at = heart + new Vector3(Mathf.Cos(a) * 0.3f, 0, Mathf.Sin(a) * 0.3f);
            k.Push(MeshKit.At(at.X, at.Y, at.Z, a));
            k.Box(Vector3.Zero, new Vector3(0.04f, 0.1f + (i % 3) * 0.02f, 0.025f), new Color("9a968a"), new Color("6f8a4a"));
            k.Pop();
        }
        // The elder tree: a broad trunk, roots and a vast crown.
        k.Frustum(heart, 0.07f, 0.045f, 0.32f, 8, new Color("5a4030"));
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.Tau + 0.5f;
            k.Push(MeshKit.At(heart.X + Mathf.Cos(a) * 0.06f, heart.Y, heart.Z + Mathf.Sin(a) * 0.06f, -a));
            k.Box(Vector3.Zero, new Vector3(0.1f, 0.025f, 0.03f), new Color("4e3828"));
            k.Pop();
        }
        Color[] leaf = [new("3f6a2a"), new("4f7a32"), new("5a8a3a")];
        foreach (var (x, y, z, s) in new[] { (0f, 0.42f, 0f, 0.2f), (-0.12f, 0.36f, 0.05f, 0.14f), (0.11f, 0.37f, -0.04f, 0.14f), (0.02f, 0.38f, 0.12f, 0.13f), (-0.03f, 0.52f, -0.06f, 0.13f) })
            k.Blob(heart + new Vector3(x, y, z), new Vector3(s, s * 0.75f, s), leaf[(int)(x * 100 + v) & 1], 0.25f, (int)(z * 100) + v, leaf[2].Darkened(0.3f));
        // Wild thickets and young trees round the edges, a path of stepping stones in.
        foreach (var (x, z, m, s) in new[] { (0.38f, -0.36f, "nature/tree_detailed", 0.22f), (-0.4f, 0.32f, "nature/tree_oak", 0.2f), (0.4f, -0.05f, "nature/plant_bush", 0.25f), (-0.42f, -0.4f, "nature/tree_fat", 0.2f), (0.12f, -0.42f, "nature/plant_bush", 0.22f), (-0.2f, 0.4f, "nature/plant_bush", 0.22f) })
            k.Add(m, MeshKit.At(x, 0.016f, z, x * 9 + v, s));
        for (int i = 0; i < 5; i++)
            k.Box(new Vector3(-0.02f + i * 0.02f, 0.016f, 0.47f - i * 0.07f), new Vector3(0.05f, 0.006f, 0.04f), new Color("a8a294"));
    }

    /// <summary>The grand bazaar: a walled caravanserai court of striped awnings and stalls round a domed pavilion, a watchtower flying the merchants' pennant.</summary>
    private static void Bazaar(MeshKit k, int v)
    {
        var sand = new Color("cdb68a");
        var wall = new Color("c8a878");
        k.Box(Vector3.Zero, new Vector3(0.98f, 0.016f, 0.82f), sand.Darkened(0.05f), sand);
        // Low walls round the court with a broad gate on the street side.
        k.Box(new Vector3(0, 0.016f, -0.4f), new Vector3(0.96f, 0.1f, 0.03f), wall, wall.Lightened(0.08f));
        foreach (float x in new[] { -0.475f, 0.475f }) k.Box(new Vector3(x, 0.016f, 0), new Vector3(0.03f, 0.1f, 0.8f), wall, wall.Lightened(0.08f));
        foreach (float x in new[] { -0.3f, 0.3f }) k.Box(new Vector3(x, 0.016f, 0.4f), new Vector3(0.36f, 0.1f, 0.03f), wall, wall.Lightened(0.08f));
        foreach (float x in new[] { -0.12f, 0.12f }) k.Box(new Vector3(x, 0.016f, 0.4f), new Vector3(0.05f, 0.16f, 0.05f), wall.Darkened(0.08f), wall);
        // Lodging along the back wall: arched rooms under a flat roof.
        k.Box(new Vector3(0, 0.016f, -0.33f), new Vector3(0.92f, 0.12f, 0.12f), wall.Darkened(0.04f), new Color("b89868"));
        for (int i = 0; i < 7; i++) k.Box(new Vector3(-0.39f + i * 0.13f, 0.016f, -0.268f), new Vector3(0.05f, 0.07f, 0.004f), new Color("4a3a2a"));
        // The domed pavilion in the middle of the court.
        k.Box(new Vector3(0, 0.016f, -0.04f), new Vector3(0.22f, 0.1f, 0.22f), new Color("e8dcc0"));
        k.Frustum(new Vector3(0, 0.116f, -0.04f), 0.12f, 0.1f, 0.04f, 10, new Color("3a6a8a"));
        k.Frustum(new Vector3(0, 0.156f, -0.04f), 0.1f, 0.0f, 0.1f, 10, new Color("3a7a9a"), new Color("4a8aaa"));
        k.Frustum(new Vector3(0, 0.25f, -0.04f), 0.008f, 0, 0.06f, 4, new Color(Palette.Gold, MeshKit.Flag.Magic));
        // Striped awnings and stalls round the pavilion.
        string[] stalls = ["town/stall-red", "town/stall-green", "town/stall", "town/stall-red", "town/stall-green", "town/stall"];
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.Tau + 0.5f;
            k.Add(stalls[(i + v) % stalls.Length], MeshKit.At(Mathf.Cos(a) * 0.3f, 0.016f, -0.04f + Mathf.Sin(a) * 0.22f, -a + Mathf.Pi / 2, 0.16f));
        }
        for (int i = 0; i < 3; i++)
        {
            var dye = Dyes[(v + i * 2) % Dyes.Length];
            k.Box(new Vector3(-0.3f + i * 0.3f, 0.12f, 0.27f), new Vector3(0.14f, 0.008f, 0.1f), dye, dye.Lightened(0.2f));
            foreach (float x in new[] { -0.06f, 0.06f }) k.Box(new Vector3(-0.3f + i * 0.3f + x, 0.016f, 0.31f), new Vector3(0.008f, 0.11f, 0.008f), Palette.Timber);
        }
        k.Add("town/cart", MeshKit.At(0.38f, 0.016f, 0.17f, 1.2f, 0.16f));
        k.Add("town/cart", MeshKit.At(-0.38f, 0.016f, 0.12f, -0.9f, 0.16f));
        // A slim watchtower in the corner, flying the merchants' pennant.
        k.Box(new Vector3(0.42f, 0.016f, -0.34f), new Vector3(0.09f, 0.4f, 0.09f), wall.Darkened(0.06f), new Color("b89868"));
        k.Frustum(new Vector3(0.42f, 0.416f, -0.34f), 0.07f, 0, 0.08f, 4, new Color("8e3b2e"), null, Mathf.Pi / 4);
        k.Add("castle/flag-pennant", MeshKit.At(0.42f, 0.49f, -0.34f, 0, 0.12f), Palette.Kenney(Dyes[(v + 1) % Dyes.Length]));
    }

    /// <summary>The paladin chapterhouse: a stone hall with a squat bell tower, white-and-gold banners and a tilting yard.</summary>
    private static void Chapterhouse(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.08f, 0, -0.14f, 0, Fit(4, 2, 0.78f)));
        Walls(k, 4, 2, 0, true, v + 401, true);
        Walls(k, 4, 2, 1, true, v + 403, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 4, 2, 1.0f, new Color("4a5a7a"), Palette.Stone, 0.12f);
        k.Pop();
        var white = Palette.Kenney(new Color("e8e2d0"));
        for (int i = 0; i < 3; i++)
            k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(-1.2f + i * 1.2f, 0.95f, 1.02f)), white);
        k.Pop();
        // The bell tower at the corner, capped in gold.
        var stone = WallStone(new Color("4a5a7a"), 0.9f);
        k.Add("castle/tower-square-base", MeshKit.At(0.36f, 0, -0.22f, 0, 0.26f), stone);
        k.Add("castle/tower-square-mid", MeshKit.At(0.36f, 0.26f, -0.22f, 0, 0.26f), stone);
        k.Add("castle/tower-square-top-roof", MeshKit.At(0.36f, 0.52f, -0.22f, 0, 0.26f), stone);
        k.Frustum(new Vector3(0.36f, 0.8f, -0.22f), 0.02f, 0, 0.1f, 4, Palette.Gold);
        // A sanded tilting yard with a lance rail.
        k.Box(new Vector3(0, 0.004f, 0.3f), new Vector3(0.9f, 0.012f, 0.3f), new Color("b8a070"));
        k.Box(new Vector3(0, 0.012f, 0.3f), new Vector3(0.7f, 0.06f, 0.02f), Palette.Timber);
        k.Box(new Vector3(-0.38f, 0, 0.3f), new Vector3(0.02f, 0.14f, 0.02f), Palette.DarkTimber);
        k.Box(new Vector3(0.38f, 0, 0.3f), new Vector3(0.02f, 0.14f, 0.02f), Palette.DarkTimber);
    }

    /// <summary>The alchemists' quarter: a crooked row of narrow workshops with tall chimneys, glass alembics and coloured fumes.</summary>
    private static void AlchemistsQuarter(MeshKit k, int v)
    {
        var plaster = new[] { new Color("c8b89a"), new Color("b8c0a0"), new Color("c0a8b0") };
        for (int i = 0; i < 3; i++)
        {
            k.Push(MeshKit.At(-0.32f + i * 0.32f, 0, -0.18f + (i % 2) * 0.06f, (i - 1) * 0.08f, 0.15f));
            Walls(k, 2, 2, 0, false, v + 411 + i, true);
            Walls(k, 2, 2, 1, false, v + 415 + i, false);
            k.Push(MeshKit.At(0, 2, 0));
            k.Gable(0, 2, 2, 1.1f, new Color("5a4a6a"), plaster[i], 0.1f);
            k.Pop();
            Chimney(k, 0.5f, 2.3f, -0.4f, true);
            k.Pop();
        }
        // Benches of glassware in the lane, glowing in three colours.
        var glow = new[] { new Color("6af0a0", MeshKit.Flag.Magic), new Color("f06ad0", MeshKit.Flag.Magic), new Color("6ab0f0", MeshKit.Flag.Magic) };
        for (int i = 0; i < 3; i++)
        {
            var at = new Vector3(-0.3f + i * 0.3f, 0, 0.24f);
            k.Box(at, new Vector3(0.18f, 0.06f, 0.07f), Palette.DarkTimber);
            k.Frustum(at + new Vector3(-0.04f, 0.06f, 0), 0.025f, 0.008f, 0.06f, 8, glow[(i + v) % 3]);
            k.Frustum(at + new Vector3(0.04f, 0.06f, 0), 0.02f, 0.02f, 0.05f, 8, glow[(i + v + 1) % 3]);
        }
        k.Add("nature/campfire_logs", MeshKit.At(0.38f, 0.004f, 0.36f, 0, 0.08f));
    }

    /// <summary>The artificers' foundry: a long brick works with a great water-wheel, a tall stack and brass machines in the yard.</summary>
    private static void ArtificersFoundry(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.06f, 0, -0.16f, 0, Fit(4, 2, 0.8f)));
        Walls(k, 4, 2, 0, true, v + 421, true);
        Walls(k, 4, 2, 1, true, v + 423, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 4, 2, 0.8f, new Color("5a5550"), new Color("9a5a40"), 0.1f);
        k.Pop();
        Chimney(k, 1.5f, 2, -0.5f, true);
        Chimney(k, -1.5f, 2, -0.5f, true);
        k.Pop();
        // The great wheel at the side.
        var wheel = new Color("6e5238");
        k.Push(MeshKit.At(-0.46f, 0.2f, -0.12f, Mathf.Pi / 2, 1));
        k.Frustum(Vector3.Zero, 0.2f, 0.2f, 0.05f, 12, wheel, Palette.DarkTimber);
        k.Pop();
        // Brass machines and gears in the yard.
        var brass = new Color("c8a04a");
        k.Box(new Vector3(0.2f, 0, 0.28f), new Vector3(0.14f, 0.12f, 0.1f), brass, brass.Lightened(0.2f));
        k.Frustum(new Vector3(0.2f, 0.12f, 0.28f), 0.04f, 0.02f, 0.1f, 8, brass);
        k.Frustum(new Vector3(-0.1f, 0.02f, 0.3f), 0.07f, 0.07f, 0.02f, 10, brass.Darkened(0.2f));
        k.Frustum(new Vector3(-0.2f, 0.02f, 0.34f), 0.05f, 0.05f, 0.02f, 8, brass.Darkened(0.1f));
        // A brass sentry by the gate.
        k.Box(new Vector3(0.36f, 0, 0.36f), new Vector3(0.04f, 0.12f, 0.03f), brass);
        k.Blob(new Vector3(0.36f, 0.15f, 0.36f), new Vector3(0.025f, 0.025f, 0.025f), brass.Lightened(0.15f));
    }

    /// <summary>The undercity gate: a squat warded gatehouse over a stair going down into the dark, with an iron grate and green lamps.</summary>
    private static void UndercityGate(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, 0.02f, 0, 1.3f));
        // A squat gatehouse of grey stone: a vaulted block between two stubby towers, with a black arch facing the stair.
        var stone = new Color("9a948a");
        var dark = new Color("16141a");
        k.Box(new Vector3(0, 0, -0.2f), new Vector3(0.44f, 0.26f, 0.22f), stone, stone.Darkened(0.15f));
        k.Frustum(new Vector3(0, 0.26f, -0.2f), 0.2f, 0.12f, 0.08f, 4, new Color("4a4650"), null, Mathf.Pi / 4);
        k.Box(new Vector3(0, 0, -0.088f), new Vector3(0.14f, 0.18f, 0.01f), dark);
        foreach (float x in new[] { -0.27f, 0.27f })
        {
            k.Box(new Vector3(x, 0, -0.2f), new Vector3(0.13f, 0.36f, 0.13f), stone.Darkened(0.08f), stone.Darkened(0.2f));
            k.Frustum(new Vector3(x, 0.36f, -0.2f), 0.1f, 0, 0.12f, 4, new Color("4a4650"), null, Mathf.Pi / 4);
        }
        // The stair down: a dark pit in front of the gate, with steps falling into it and an iron grate over its far end.
        k.Box(new Vector3(0, -0.02f, 0.18f), new Vector3(0.3f, 0.03f, 0.28f), dark);
        var step = new Color("6a665c");
        for (int i = 0; i < 4; i++)
            k.Box(new Vector3(0, 0.012f - i * 0.006f, 0.3f - i * 0.05f), new Vector3(0.24f, 0.01f, 0.04f), step.Darkened(i * 0.12f));
        var iron = new Color("2e2c30");
        for (int i = 0; i < 6; i++)
            k.Box(new Vector3(-0.12f + i * 0.048f, 0, 0.06f), new Vector3(0.008f, 0.1f, 0.008f), iron);
        k.Box(new Vector3(0, 0.1f, 0.06f), new Vector3(0.26f, 0.012f, 0.012f), iron);
        // Kerb stones round the pit and the warden's green lamps.
        k.Box(new Vector3(-0.17f, 0, 0.18f), new Vector3(0.03f, 0.03f, 0.3f), Palette.Stone);
        k.Box(new Vector3(0.17f, 0, 0.18f), new Vector3(0.03f, 0.03f, 0.3f), Palette.Stone);
        var lamp = new Color("7af0a0", MeshKit.Flag.Magic);
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            k.Box(new Vector3(x, 0, 0.34f), new Vector3(0.015f, 0.14f, 0.015f), iron);
            k.Blob(new Vector3(x, 0.16f, 0.34f), new Vector3(0.02f, 0.025f, 0.02f), lamp);
        }
        k.Add("castle/flag-pennant", MeshKit.At(0.27f, 0.48f, -0.2f, 0, 0.1f), Palette.Kenney(Dyes[(v + 2) % Dyes.Length]));
        k.Pop();
    }
}
