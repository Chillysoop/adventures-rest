using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

public partial class Main
{
    /// <summary>Famous arms handed out by --skirmish gear, by class, so a shot shows enchantments on the heroes.</summary>
    private static readonly (string Class, string[] Items)[] SkirmishGear =
    [
        ("fighter", ["flame-tongue-weapon", "armor-2"]),
        ("paladin", ["holy-avenger", "shield-2"]),
        ("barbarian", ["berserker-axe"]),
        ("warden", ["frost-brand", "dragon-scale-mail"]),
        ("ranger", ["oathbow"]),
        ("cleric", ["mithral-armor"]),
        ("rogue", ["frost-brand", "ring-of-protection"]),
        ("wizard", ["cloak-of-protection"]),
        ("bard", ["amulet-of-health"]),
        ("reliquarian", ["hammer-of-thunderbolts"]),
    ];

    /// <summary>The middle of the nearest clear three tiles square to the town's centre: nothing built, zoned or worn there.</summary>
    private static Vector2? OpenGround(Town t)
    {
        int c = (int)Town.Center;
        for (int r = 2; r < Town.Grid / 2; r++)
            for (int y = c - r; y <= c + r; y++)
                for (int x = c - r; x <= c + r; x++)
                    if ((Math.Abs(x - c) == r || Math.Abs(y - c) == r) && CrownAndCompass.Client.Showcase.Open(t, x - 1, y - 1, 3, 3))
                        return new Vector2(Map.Geo.Margin + x + 0.5f, Map.Geo.Margin + y + 0.5f);
        return null;
    }

    /// <summary>--skirmish: the heroes fight raiders by the square, the camera on them (see <see cref="Map.Crowd.Skirmish"/>).</summary>
    private void StageSkirmish(string[] spec)
    {
        bool gear = spec.Contains("gear");
        _map.Crowd.SkirmishKind = spec.FirstOrDefault(k => k is not ("gear" or "")) ?? "";
        var t = _sim.Town;
        var at = _opts.Look ??= OpenGround(t) ?? Map.Geo.Center + new Vector2(0, 4);
        _map.Crowd.Skirmish = at;
        _map.LookAt(at, _opts.Cam ?? 6);
        if (!gear) return;
        // A moment after the fight starts, so the map sees them put it on.
        GetTree().CreateTimer(3).Timeout += () => ArmSkirmishers(t);
    }

    private static void ArmSkirmishers(Town t)
    {
        foreach (var (cls, items) in SkirmishGear)
            if (t.Agents.Where(a => a.Hero?.Cls.Id == cls && !a.Dead).OrderBy(a => a.Id).FirstOrDefault() is Agent hero)
                foreach (var id in items)
                    if (t.Content.Library.Item(id) is LibItem it) Gear.Fit(t, hero, it);
    }
}
