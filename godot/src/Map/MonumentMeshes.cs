using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The parts monuments grow (Systems/Monuments.cs): the temple's nave, bell tower, almshouse, crypt, cloister and spire;
/// the keep's curtain wall, chapel, gatehouse, great hall, barracks wing and high tower; the town hall's bell tower,
/// chamber, counting house, archive and banner court. Each fills its own footprint, the larger side spanning the model.
/// </summary>
public partial class TownView
{
    private static readonly Color Ashlar = new("c8c0b0");
    private static readonly Color Ashlar2 = new("b8b0a0");

    /// <summary>A long stone nave, buttressed, with tall windows down both sides and a slate roof.</summary>
    private static void TempleNave(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, 0, 0, Fit(4, 3, 0.94f)));
        TallWalls(k, 4, 2, 2, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 4, 2, 1.1f, Slate, Ashlar, 0.12f);
        k.Pop();
        foreach (float x in new[] { -1.5f, -0.5f, 0.5f, 1.5f })
            foreach (float z in new[] { -1.08f, 1.08f })
                k.Box(new Vector3(x, 0, z), new Vector3(0.18f, 1.4f, 0.22f), Ashlar2, Ashlar);
        k.Pop();
    }

    /// <summary>A square stone bell tower with an open belfry, a gilded bell and a pyramid roof.</summary>
    private static void BellTower(MeshKit k, Color roof, float tall)
    {
        k.Box(Vector3.Zero, new Vector3(0.62f, tall, 0.62f), Ashlar2, Ashlar);
        foreach (var (x, z) in new[] { (-0.27f, -0.27f), (0.27f, -0.27f), (-0.27f, 0.27f), (0.27f, 0.27f) })
            k.Box(new Vector3(x, tall, z), new Vector3(0.1f, 0.4f, 0.1f), Ashlar2);
        k.Box(new Vector3(0, tall, 0), new Vector3(0.5f, 0.04f, 0.5f), Ashlar);
        k.Frustum(new Vector3(0, tall + 0.08f, 0), 0.1f, 0.05f, 0.18f, 8, new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Box(new Vector3(0, tall + 0.4f, 0), new Vector3(0.7f, 0.06f, 0.7f), Ashlar2);
        k.Pyramid(tall + 0.46f, 0.66f, 0.66f, 0.55f, roof, 0.04f);
        foreach (float side in new[] { -1f, 1f })
            Pane(k, new Vector3(0, tall * 0.6f, side * 0.315f), new Vector3(0, 0, side), 0.06f, 0.12f);
    }

    private static void TempleBellTower(MeshKit k) => BellTower(k, Slate, 1.6f);

    /// <summary>The almshouse: a long plastered timber house with a bread oven's smoking chimney and benches by the door.</summary>
    private static void Almshouse(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.12f, 0, Fit(3, 2, 0.84f)));
        Walls(k, 3, 2, 0, false, v + 401, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 3, 2, 0.95f, RoofOf(v + 1), Palette.Plaster, 0.12f);
        k.Pop();
        Chimney(k, 1.1f, 1, -0.3f, true);
        k.Pop();
        foreach (float x in new[] { -0.25f, 0.25f })
            k.Box(new Vector3(x, 0, 0.36f), new Vector3(0.22f, 0.05f, 0.06f), Palette.Timber);
    }

    /// <summary>The crypt: a low stone vault in a grassy mound, a stair down under a little pillared canopy.</summary>
    private static void Crypt(MeshKit k)
    {
        k.Blob(new Vector3(0, -0.05f, -0.08f), new Vector3(0.44f, 0.22f, 0.36f), new Color("6f8a4a"), 0.05f, 7, new Color("5a7240"));
        k.Box(new Vector3(0, 0, 0.18f), new Vector3(0.36f, 0.06f, 0.3f), Ashlar2, Ashlar);
        k.Box(new Vector3(0, 0.06f, 0.16f), new Vector3(0.18f, 0.01f, 0.2f), new Color("2a2620"));
        foreach (float x in new[] { -0.15f, 0.15f })
            foreach (float z in new[] { 0.06f, 0.3f })
                k.Frustum(new Vector3(x, 0.06f, z), 0.025f, 0.022f, 0.24f, 6, Ashlar);
        k.Box(new Vector3(0, 0.3f, 0.18f), new Vector3(0.38f, 0.04f, 0.32f), Ashlar2);
        k.Pyramid(0.34f, 0.38f, 0.32f, 0.12f, Slate, 0.02f);
        k.Box(new Vector3(0, 0.46f, 0.18f), new Vector3(0.02f, 0.1f, 0.02f), Palette.Gold);
    }

    /// <summary>The cloister: an arcaded walk round a green garth with a well in the middle.</summary>
    private static void Cloister(MeshKit k)
    {
        k.Box(new Vector3(0, 0, 0), new Vector3(0.94f, 0.03f, 0.94f), Ashlar2, Ashlar);
        k.Box(new Vector3(0, 0.03f, 0), new Vector3(0.56f, 0.01f, 0.56f), new Color("6f9a4a"));
        k.Frustum(new Vector3(0, 0.03f, 0), 0.06f, 0.06f, 0.07f, 10, Ashlar2, new Color("3a5a7a"));
        for (int s = 0; s < 4; s++)
        {
            k.Push(new Transform3D(Basis.FromEuler(new Vector3(0, s * Mathf.Pi / 2, 0)), Vector3.Zero));
            k.Box(new Vector3(0, 0.03f, -0.4f), new Vector3(0.94f, 0.26f, 0.14f), Ashlar, Ashlar);
            for (int i = 0; i < 5; i++)
                k.Frustum(new Vector3(-0.3f + i * 0.15f, 0.03f, -0.31f), 0.018f, 0.018f, 0.2f, 6, Ashlar2);
            k.Box(new Vector3(0, 0.23f, -0.33f), new Vector3(0.82f, 0.04f, 0.1f), Ashlar2);
            k.Pop();
        }
        foreach (float z in new[] { -0.4f, 0.4f })
        {
            k.Push(MeshKit.At(0, 0.29f, z, 0, 1));
            k.Gable(0, 0.98f, 0.18f, 0.1f, RoofOf(1), Ashlar, 0.02f);
            k.Pop();
        }
        foreach (float x in new[] { -0.4f, 0.4f })
        {
            k.Push(MeshKit.At(x, 0.29f, 0, Mathf.Pi / 2, 1));
            k.Gable(0, 0.62f, 0.18f, 0.1f, RoofOf(1), Ashlar, 0.02f);
            k.Pop();
        }
    }

    /// <summary>The great spire: a tall stone tower and a slate spire seen for miles, a gilded finial on top.</summary>
    private static void Spire(MeshKit k, Color roof)
    {
        k.Box(Vector3.Zero, new Vector3(0.6f, 2.2f, 0.6f), Ashlar2, Ashlar);
        for (float y = 0.5f; y < 2.1f; y += 0.6f)
            foreach (float side in new[] { -1f, 1f })
                Pane(k, new Vector3(0, y, side * 0.305f), new Vector3(0, 0, side), 0.05f, 0.12f);
        foreach (var (x, z) in new[] { (-0.28f, -0.28f), (0.28f, -0.28f), (-0.28f, 0.28f), (0.28f, 0.28f) })
            k.Frustum(new Vector3(x, 2.2f, z), 0.05f, 0, 0.3f, 4, roof);
        k.Frustum(new Vector3(0, 2.2f, 0), 0.34f, 0, 2.2f, 8, roof, null, Mathf.Pi / 8);
        k.Frustum(new Vector3(0, 4.35f, 0), 0.04f, 0, 0.3f, 4, new Color(Palette.Gold, MeshKit.Flag.Magic));
    }

    private static readonly Func<Color, Color> CastleStone = WallStone(new Color("4f5f8a"), 0.85f);

    /// <summary>A run of curtain wall with a squat tower at each end.</summary>
    private static void CurtainWall(MeshKit k)
    {
        CurtainRun(k, new Vector3(-0.36f, 0, 0), new Vector3(0.36f, 0, 0), 0.12f, 0.34f, 0.3f, CastleStone);
        foreach (float x in new[] { -0.38f, 0.38f })
            TowerStack(k, x, 0, 0.2f, ["tower-square-base", "tower-square-mid", "tower-square-top-roof"], CastleStone);
    }

    /// <summary>The castle chapel: a small stone chapel with a bellcote over its west gable.</summary>
    private static void CastleChapel(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, 0, Mathf.Pi / 2, Fit(3, 2, 0.94f)));
        Walls(k, 3, 2, 0, true, v + 421, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 3, 2, 1.0f, Slate, Ashlar, 0.1f);
        k.Pop();
        k.Box(new Vector3(1.4f, 1.6f, 0), new Vector3(0.24f, 0.5f, 0.3f), Ashlar2);
        k.Frustum(new Vector3(1.4f, 1.72f, 0), 0.07f, 0.04f, 0.14f, 8, new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Pyramid(2.1f, 0.3f, 0.36f, 0.25f, Slate, 0.02f);
        k.Pop();
    }

    /// <summary>The castle gatehouse: twin towers and a portcullised gate between them.</summary>
    private static void CastleGatehouse(MeshKit k)
    {
        foreach (float x in new[] { -0.3f, 0.3f })
            TowerStack(k, x, 0, 0.3f, ["tower-square-base", "tower-square-mid-windows", "tower-square-top"], CastleStone);
        k.Add("castle/gate", new Transform3D(Basis.FromScale(new Vector3(0.55f, 0.5f, 0.5f)), new Vector3(0, 0, 0)), CastleStone);
        k.Box(new Vector3(0, 0.42f, 0), new Vector3(0.36f, 0.12f, 0.3f), new Color("8f8a80"));
        k.Add("castle/flag-wide", MeshKit.At(0, 0.98f, 0, Mathf.Pi / 2, 0.35f), Palette.Kenney(new Color("b8322a")));
    }

    /// <summary>The great hall: a tall stone hall under a steep roof, red banners along the front.</summary>
    private static void GreatHall(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.04f, 0, Fit(4, 3, 0.94f)));
        TallWalls(k, 4, 2, 2, true);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 4, 2, 1.4f, new Color("7e4430"), Ashlar, 0.12f);
        k.Pop();
        Chimney(k, -1.5f, 2, -0.5f, true);
        var red = Palette.Kenney(new Color("b8322a"));
        foreach (float x in new[] { -1.2f, 0, 1.2f })
            k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(x, 0.95f, 1.02f)), red);
        k.Pop();
    }

    /// <summary>The high tower: a tall round-cornered castle tower flying the lord's flag.</summary>
    private static void HighTower(MeshKit k)
    {
        TowerStack(k, 0, 0, 0.62f, ["tower-square-base", "tower-square-mid-windows", "tower-square-mid", "tower-square-mid-windows", "tower-square-top"], CastleStone);
        k.Add("castle/flag-wide", MeshKit.At(0, 2.7f, 0, Mathf.Pi / 2, 0.6f), Palette.Kenney(new Color("b8322a")));
    }

    /// <summary>The town hall's bell tower: a timber belfry on a stone base.</summary>
    private static void HallBellTower(MeshKit k) => BellTower(k, RoofOf(1), 1.2f);

    /// <summary>The banner court: a paved yard ringed with flagpoles in every company's colours, a stone plinth at the head.</summary>
    private static void BannerCourt(MeshKit k, int v)
    {
        k.Box(Vector3.Zero, new Vector3(0.94f, 0.03f, 0.68f), Ashlar2, Ashlar);
        k.Box(new Vector3(0, 0.03f, -0.22f), new Vector3(0.3f, 0.12f, 0.12f), Ashlar2, Ashlar);
        for (int i = 0; i < 6; i++)
        {
            float x = -0.4f + i * 0.16f;
            foreach (float z in new[] { -0.3f, 0.3f })
            {
                k.Box(new Vector3(x, 0.03f, z), new Vector3(0.016f, 0.5f, 0.016f), Palette.DarkTimber);
                k.Box(new Vector3(x + 0.04f, 0.42f, z), new Vector3(0.08f, 0.1f, 0.006f), Dyes[(v + i + (z > 0 ? 3 : 0)) % Dyes.Length]);
            }
        }
    }
}
