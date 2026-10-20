using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client.Map;

public partial class Crowd
{
    /// <summary>
    /// A hero fighting a foe standing at foe: facing it and swinging, casting or loosing; the sparks, the
    /// bolt or the arrow when the blow lands; and a flash when the foe's blow lands on them.
    /// </summary>
    public void Fight(Agent a, Vector2 at, Vector3 foe, float now, float scale = 1)
    {
        var p = map.Gen.At(at);
        var d = foe - p;
        float yaw = Mathf.Atan2(d.X, d.Z);
        var (act, u) = Heroes.Fighting(a, now);
        // The foe strikes back now and then.
        float back = (now / 1.9f + a.Id * 0.41f) % 1;
        float flash = back < 0.12f ? 1 - back / 0.12f : 0;
        Heroes.Put(a, p, yaw, act, u, flash, scale);
        var flat = new Vector3(d.X, 0, d.Z).Normalized();
        if (flash > 0.9f && _struck.Add(a.Id)) Fx.Hit(p + new Vector3(0, 0.3f * scale, 0) + flat * 0.06f, new Color("ff6a4a"), 0.6f);
        else if (flash == 0) _struck.Remove(a.Id);
        if (!Heroes.Lands(a.Id, act, u)) return;
        var look = Heroes.LookOf(a);
        var hand = p + new Vector3(0, 0.4f * scale, 0) + flat * 0.12f * scale;
        switch (act)
        {
            case Heroes.Act.Cast:
                Fx.Bolt(hand + new Vector3(0, 0.15f * scale, 0), foe, look.Spell);
                break;
            case Heroes.Act.Shoot:
                Fx.Arrow(hand, foe);
                break;
            default:
                Fx.Hit(foe - flat * 0.12f, look.Glow is Color g && look.Aura ? g : new Color("ffe0a0"), look.Attack == Heroes.Act.Swing2 ? 1.3f : 1);
                break;
        }
    }

    private readonly HashSet<int> _struck = [];

    /// <summary>One of a host, in the chunky style (<see cref="Heroes.FoeLook"/>), scaled as the old figures were.</summary>
    private void Foe(string kind, Vector3 p, float yaw, Color body, Color skin, float scale, Heroes.Act act, float u, float flash = 0, bool chief = false) =>
        Heroes.PutLook(Heroes.FoeLook(kind, chief, body, skin), new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(Vector3.One * scale * 0.95f), p), act == Heroes.FoeAttack ? Heroes.FoeLook(kind, chief, body, skin).Attack : act, u, flash);

    /// <summary>One of a host fallen on their back.</summary>
    private void FoeFallen(string kind, Vector3 p, float yaw, Color body, Color skin, float scale, bool chief = false)
    {
        var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromEuler(new Vector3(-Mathf.Pi / 2, 0, 0));
        Heroes.PutLook(Heroes.FoeLook(kind, chief, body, skin) with { Legend = null }, new Transform3D(basis.Scaled(Vector3.One * scale * 0.95f), p + new Vector3(0, 0.03f, 0)), Heroes.Act.Idle, 0);
    }

    /// <summary>The kind of host the --skirmish heroes fight (--skirmish undead, cult, beasts...).</summary>
    public string SkirmishKind = "";

    /// <summary>For shots and clips (--skirmish): the town's heroes, one of each class, fighting a ring of raiders at this spot.</summary>
    public Vector2? Skirmish;
    private readonly List<Agent> _skirmishers = [];

    private bool Skirmishing(int id) => _skirmishers.Any(a => a.Id == id);

    private void SkirmishCast(Town town)
    {
        _skirmishers.Clear();
        if (Skirmish == null) return;
        _skirmishers.AddRange(town.Agents.Where(a => a.Hero != null && !a.Dead).GroupBy(a => a.Hero!.Cls.Id).Select(g => g.OrderBy(a => a.Id).First()).OrderBy(a => a.Id).Take(8));
    }

    /// <summary>Where the k-th of n skirmishers fights, and where their foe stands.</summary>
    private (Vector2 Hero, Vector2 Foe) SkirmishSpot(int k, int n, Agent a)
    {
        float ang = k * Mathf.Tau / Math.Max(1, n) + 0.3f;
        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        var foe = Skirmish!.Value + dir * 0.9f;
        return (foe + dir * Heroes.Reach(a), foe);
    }

    private void SkirmishFoes(float now)
    {
        for (int k = 0; k < _skirmishers.Count; k++)
        {
            var (hero, foe) = SkirmishSpot(k, _skirmishers.Count, _skirmishers[k]);
            var d = hero - foe;
            Foe(SkirmishKind, map.Gen.At(foe), Mathf.Atan2(d.X, d.Y), k % 3 == 0 ? new Color("4a2018") : new Color("8a3a2a"), SkirmishKind == "undead" ? new Color("cfc9b4") : OrcSkin, 1.0f, Heroes.FoeAttack, now / Heroes.Blow + k * 0.29f);
        }
    }

    private void SkirmishHeroes(float now)
    {
        for (int k = 0; k < _skirmishers.Count; k++)
        {
            var a = _skirmishers[k];
            var (hero, foe) = SkirmishSpot(k, _skirmishers.Count, a);
            Fight(a, hero, map.Gen.At(foe, 0.3f), now);
        }
    }
}
