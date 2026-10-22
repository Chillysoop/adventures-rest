using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The Lofted City (Systems/Lofted.cs) in its three sizes: on the ground a timber keel cradle with the rock and towers
/// rising in it; finished, a rock isle hanging over its lot on glowing keelstones, held down by anchor chains; and,
/// while it sails, the empty cradle with its chains slack on the ground.
/// </summary>
public partial class TownView
{
    private static readonly Color SkyRock = new("8a7f70");
    private static readonly Color SkyGrass = new("7e9a5a");

    /// <summary>How many towers, houses and keelstones each size carries.</summary>
    private static (int Towers, int Houses, int Keels) LoftedSize(string id) => id switch
    {
        "lofted_city" => (5, 12, 5),
        "lofted_isle" => (3, 8, 3),
        _ => (1, 4, 1),
    };

    /// <summary>The finished city over its lot, chained down.</summary>
    private static void LoftedAloft(MeshKit k, string id)
    {
        // It hangs well clear of the roofs and spreads past its cradle.
        const float lift = 1.3f, spread = 1.7f;
        Cradle(k, false);
        foreach (var (x, z) in new[] { (-0.38f, -0.38f), (0.38f, -0.38f), (-0.38f, 0.38f), (0.38f, 0.38f) })
            Chain(k, new Vector3(x, 0.08f, z), new Vector3(x * 0.9f, lift - 0.1f * spread, z * 0.9f));
        k.Push(MeshKit.At(0, lift, 0, 0, spread));
        LoftedIsle(k, id, 1);
        k.Pop();
    }

    /// <summary>The city rising in its cradle on the ground, in eighths of the work: rock first, then the towers.</summary>
    private static void LoftedRising(MeshKit k, string id, int eighth)
    {
        Cradle(k, true);
        k.Push(MeshKit.At(0, 0.12f, 0));
        LoftedIsle(k, id, eighth / 8f);
        k.Pop();
    }

    /// <summary>The empty cradle while the city sails.</summary>
    private static void LoftedAway(MeshKit k)
    {
        Cradle(k, false);
        foreach (var (x, z) in new[] { (-0.38f, -0.38f), (0.38f, -0.38f), (-0.38f, 0.38f), (0.38f, 0.38f) })
            k.Box(new Vector3(x * 0.8f, 0.02f, z * 0.8f), new Vector3(0.16f, 0.03f, 0.04f), new Color("4a4a50"));
    }

    /// <summary>A rock isle: an upturned cone of rock hung on keelstones, grass on top, houses and Skywright towers. Grown is how far along it is.</summary>
    private static void LoftedIsle(MeshKit k, string id, float grown)
    {
        var (towers, houses, keels) = LoftedSize(id);
        float r = 0.42f;
        // The rock: an upturned cone, ragged in two tiers.
        k.Frustum(new Vector3(0, -0.34f, 0), 0, r * 0.55f, 0.18f, 9, SkyRock.Darkened(0.25f));
        k.Frustum(new Vector3(0, -0.16f, 0), r * 0.55f, r, 0.16f, 9, SkyRock.Darkened(0.1f), SkyRock, 0.2f);
        // Keelstones under it, glowing.
        for (int i = 0; i < keels; i++)
        {
            float a = i * Mathf.Tau / keels + 0.3f, d = keels == 1 ? 0 : r * 0.42f;
            k.Frustum(new Vector3(Mathf.Cos(a) * d, -0.3f + (keels == 1 ? -0.06f : 0.06f), Mathf.Sin(a) * d), 0, 0.05f, 0.12f, 6, new Color("8fd0ff", MeshKit.Flag.Magic));
        }
        if (grown < 0.25f) return;
        k.Frustum(Vector3.Zero, r, r * 0.97f, 0.02f, 9, SkyGrass.Darkened(0.1f), SkyGrass, 0.2f);
        // Houses round the rim, more as the work goes on.
        int shown = (int)Math.Round(houses * Math.Clamp((grown - 0.25f) / 0.5f, 0, 1));
        for (int i = 0; i < shown; i++)
        {
            float a = i * Mathf.Tau / houses, d = r * 0.72f;
            k.Push(MeshKit.At(Mathf.Cos(a) * d, 0.02f, Mathf.Sin(a) * d, -a, 1));
            k.Box(Vector3.Zero, new Vector3(0.07f, 0.06f, 0.05f), Palette.Plaster);
            k.Push(MeshKit.At(0, 0.06f, 0));
            k.Pyramid(0, 0.08f, 0.06f, 0.05f, new Color("4a6a9a"), 0.01f);
            k.Pop();
            k.Pop();
        }
        if (grown < 0.6f) return;
        // Skywright towers: slim, white, blue-capped, the tallest in the middle.
        for (int i = 0; i < towers; i++)
        {
            float a = i * Mathf.Tau / Math.Max(1, towers - 1) + 0.5f, d = i == 0 ? 0 : r * 0.42f, h = i == 0 ? 0.42f : 0.26f;
            h *= Math.Clamp((grown - 0.6f) / 0.4f, 0.3f, 1);
            var at = new Vector3(Mathf.Cos(a) * d, 0.02f, Mathf.Sin(a) * d);
            k.Frustum(at, 0.05f, 0.04f, h, 8, Ashlar);
            k.Frustum(at + new Vector3(0, h, 0), 0.06f, 0, 0.1f, 8, new Color("3a5a9a"));
            k.Frustum(at + new Vector3(0, h * 0.7f, 0), 0.055f, 0.055f, 0.015f, 8, new Color(Palette.Gold, MeshKit.Flag.Magic));
        }
    }

    /// <summary>The timber cradle the city is built in: trestles round a stone bed.</summary>
    private static void Cradle(MeshKit k, bool working)
    {
        k.Box(Vector3.Zero, new Vector3(0.96f, 0.03f, 0.96f), Palette.Dirt.Darkened(0.1f));
        k.Frustum(new Vector3(0, 0.03f, 0), 0.3f, 0.26f, 0.04f, 9, Palette.Stone);
        foreach (var (x, z) in new[] { (-0.42f, 0f), (0.42f, 0f), (0f, -0.42f), (0f, 0.42f) })
        {
            k.Box(new Vector3(x, 0, z), new Vector3(0.03f, working ? 0.32f : 0.14f, 0.03f), Palette.Timber);
            k.Box(new Vector3(x * 0.8f, working ? 0.28f : 0.12f, z * 0.8f), new Vector3(Math.Abs(z) > 0 ? 0.3f : 0.12f, 0.02f, Math.Abs(x) > 0 ? 0.3f : 0.12f), new Color("c8ab7e"));
        }
        if (!working) return;
        k.Box(new Vector3(0.36f, 0.03f, 0.44f), new Vector3(0.12f, 0.06f, 0.07f), Palette.Stone, Palette.Stone.Lightened(0.15f));
        k.Add("nature/log_stack", MeshKit.At(-0.36f, 0.03f, 0.44f, Mathf.Pi / 2, 0.45f));
    }

    /// <summary>An anchor chain from the ground to the isle: links as small dark boxes.</summary>
    private static void Chain(MeshKit k, Vector3 a, Vector3 b)
    {
        const int links = 10;
        for (int i = 0; i < links; i++)
        {
            var p = a + (b - a) * ((i + 0.5f) / links);
            k.Box(p, new Vector3(0.018f, (b - a).Length() / links * 0.8f, 0.018f), new Color("3a3a40"));
        }
        k.Frustum(a - new Vector3(0, 0.08f, 0), 0.05f, 0.04f, 0.1f, 6, Ashlar2);
    }
}
