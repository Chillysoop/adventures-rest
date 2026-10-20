using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// What flies over the town: griffons and eagles wheeling above the Griffon Roost, and the town's own dragon once its
/// egg hatches (<see cref="Eggs"/>), a wyrmling circling the keep that grows over the year into a great wyrm.
/// </summary>
public partial class TownView
{
    private readonly List<MeshInstance3D> _fliers = [];
    private MeshInstance3D? _dragon;
    private string _dragonKey = "";

    /// <summary>A flat wing whose face looks up on either side of the body (the left one mirrored, so it winds the same way).</summary>
    private static void Wing(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, float side)
    {
        if (side > 0) k.Quad(a, b, c, d, col);
        else k.Quad(a, d, c, b, col);
    }

    /// <summary>A bird or griffon with its wings spread, nose along +Z.</summary>
    private static Mesh Flier(Color body, Color wing)
    {
        var k = new MeshKit();
        k.Blob(new Vector3(0, 0, 0), new Vector3(0.05f, 0.04f, 0.1f), body);
        k.Blob(new Vector3(0, 0.02f, 0.09f), new Vector3(0.03f, 0.03f, 0.035f), wing.Lightened(0.3f));
        foreach (float s in new[] { -1f, 1f })
            Wing(k, new Vector3(0, 0.01f, 0.04f), new Vector3(s * 0.22f, 0.04f, -0.02f), new Vector3(s * 0.2f, 0.03f, -0.08f), new Vector3(0, 0.01f, -0.04f), wing, s);
        k.Quad(new Vector3(0, 0.005f, -0.08f), new Vector3(0.04f, 0.005f, -0.16f), new Vector3(-0.04f, 0.005f, -0.16f), new Vector3(0, 0.005f, -0.08f), wing.Darkened(0.2f));
        return k.Build();
    }

    /// <summary>A dragon: long body and neck, a horned head, broad bat wings and a tail, nose along +Z.</summary>
    private static Mesh Dragon(Color scale)
    {
        var k = new MeshKit();
        var belly = scale.Lightened(0.35f);
        k.Blob(new Vector3(0, 0, 0), new Vector3(0.09f, 0.08f, 0.2f), scale, 0, 0, belly);
        k.Blob(new Vector3(0, 0.07f, 0.22f), new Vector3(0.035f, 0.035f, 0.08f), scale);
        k.Blob(new Vector3(0, 0.1f, 0.31f), new Vector3(0.045f, 0.04f, 0.06f), scale);
        foreach (float s in new[] { -1f, 1f })
        {
            k.Frustum(new Vector3(s * 0.025f, 0.13f, 0.29f), 0.012f, 0, 0.06f, 4, belly);
            Wing(k, new Vector3(s * 0.05f, 0.05f, 0.08f), new Vector3(s * 0.42f, 0.12f, 0.02f), new Vector3(s * 0.36f, 0.06f, -0.16f), new Vector3(s * 0.05f, 0.04f, -0.08f), scale.Darkened(0.15f), s);
        }
        k.Blob(new Vector3(0, -0.01f, -0.26f), new Vector3(0.03f, 0.03f, 0.14f), scale);
        k.Frustum(new Vector3(0, -0.01f, -0.36f), 0.02f, 0, 0.1f, 4, scale.Darkened(0.2f));
        return k.Build();
    }

    /// <summary>Each frame: wheel the fliers over the roost and the dragon over the keep.</summary>
    public void Fliers(float dt)
    {
        var town = map.Sim.Town;
        float now = (float)Time.GetTicksMsec() / 1000f;
        var roost = town.Buildings.FirstOrDefault(b => b.Def.Id == "griffon_roost" && b.Complete);
        int want = roost == null ? 0 : 3 + Math.Min(2, town.Heroes.Count(h => h.Hero!.Mount is "giant-eagle" or "giant-owl" or "griffon" && !h.IsAway));
        while (_fliers.Count < want)
        {
            int i = _fliers.Count;
            var mesh = i % 3 == 2 ? Flier(new Color("6b4a2a"), new Color("8a6a40")) : Flier(new Color("a07a48"), new Color("e8dcc0"));
            var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = Mats.Props };
            AddChild(m);
            _fliers.Add(m);
        }
        for (int i = 0; i < _fliers.Count; i++)
        {
            var f = _fliers[i];
            f.Visible = i < want;
            if (!f.Visible || roost == null) continue;
            var lot = town.Lot(roost.LotId);
            var c = Geo.LotPos(lot);
            float r = 1.4f + i * 0.5f, w = 0.35f / (1 + i * 0.25f), a = now * w + i * 2.1f;
            var at = new Vector3(c.X + Mathf.Cos(a) * r, Floor(lot) + 2.2f + i * 0.35f + 0.15f * Mathf.Sin(now * 1.3f + i), c.Y + Mathf.Sin(a) * r);
            // Nose along the circle, banked into the turn.
            float yaw = Mathf.Atan2(-Mathf.Sin(a), Mathf.Cos(a));
            f.Transform = new Transform3D(Basis.FromEuler(new Vector3(0, yaw, -0.35f)).Scaled(Vector3.One * 2.2f), at);
        }

        if (town.Egg is not { HatchedDay: not null } egg || Eggs.Kind(town, egg.Kind) is not { } kind)
        {
            if (_dragon != null) _dragon.Visible = false;
            return;
        }
        string key = $"{kind.Id}|{egg.Grown}";
        if (_dragon == null || key != _dragonKey)
        {
            _dragon?.QueueFree();
            _dragon = new MeshInstance3D { Mesh = Dragon(new Color(kind.Hex)), MaterialOverride = Mats.Props };
            AddChild(_dragon);
            _dragonKey = key;
        }
        _dragon.Visible = true;
        var home = town.Buildings.FirstOrDefault(b => b.Def.Id == "keep" && b.Complete) ?? town.Buildings.FirstOrDefault(b => b.Def.Id == "town_hall");
        var hlot = home != null ? town.Lot(home.LotId) : null;
        var hc = hlot != null ? Geo.LotPos(hlot) : Geo.Center;
        // A wyrmling grows toward its full size over the year it takes to be grown.
        float grow = egg.Grown ? 1 : Mathf.Clamp((town.Day - egg.HatchedDay.Value) / (float)Math.Max(1, town.Content.Eggs.GrowDays), 0, 1);
        float size = Mathf.Lerp(1.2f, 4.5f, grow);
        float rad = Mathf.Lerp(2.2f, 6, grow), ang = now * Mathf.Lerp(0.3f, 0.16f, grow);
        var pos = new Vector3(hc.X + Mathf.Cos(ang) * rad, (hlot != null ? Floor(hlot) : 0) + Mathf.Lerp(2.6f, 5, grow) + 0.25f * Mathf.Sin(now * 0.9f), hc.Y + Mathf.Sin(ang) * rad);
        _dragon.Transform = new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.Atan2(-Mathf.Sin(ang), Mathf.Cos(ang)), -0.25f)).Scaled(Vector3.One * size), pos);
    }
}
