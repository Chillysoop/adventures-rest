using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// A field battle played back on the open ground out along the road the foe came by, from the town's record of it
/// (<see cref="Field"/>): the two lines form up and close, the blocks press and thin round by round (a unit that failed
/// its order wavers back, one that ran streams home), the fallen lie where the lines met, the foe's leader stands
/// behind its host (and lies there if the heroes got to them), and the side that broke runs. It plays in game time over
/// the hours after the battle, or again from the start for half a minute when the player asks to watch it.
/// </summary>
public partial class Crowd
{
    /// <summary>A battle the player asked to watch, and when (real seconds) its replay began.</summary>
    private int _watchField = -1;
    private double _watchFrom;
    private const double WatchSeconds = 30;

    /// <summary>For shots: hold the last field battle's playback at this point.</summary>
    public static float? FieldU;
    private bool _fieldAimed;

    public void WatchField(FieldBattle b)
    {
        _watchField = b.Id;
        _watchFrom = Time.GetTicksMsec() / 1000.0;
    }

    /// <summary>Where the lines meet: out from the wall on the side the foe came, along the road.</summary>
    public (Vector2 At, Vector2 Out) FieldSpot(Town town, FieldBattle b)
    {
        // From the sim's wall line, so the spot is the same before the wall walk is built.
        var wall = OnWall(town, null, b.Approach, 0.5f);
        var o = Outward(wall);
        return (wall + o * 16f, o);
    }

    /// <summary>The battle playing now and how far through (0 to 1).</summary>
    private (FieldBattle B, float U)? FieldNow(Town town)
    {
        var fields = town.Army.Fields;
        if (fields.Count == 0) return null;
        if (_watchField >= 0 && fields.FirstOrDefault(f => f.Id == _watchField) is { } w)
        {
            double u = (Time.GetTicksMsec() / 1000.0 - _watchFrom) / WatchSeconds;
            if (u <= 1.1) return (w, (float)Math.Min(1, u));
            _watchField = -1;
        }
        if (FieldU is float held) return (fields[^1], held);
        double at = town.Tick + map.HourFraction;
        double hours = town.Content.Army.Field.Hours;
        var b = fields[^1];
        double v = (at - b.Tick) / hours;
        return v is >= 0 and <= 1.3 ? (b, (float)Math.Min(1, v)) : null;
    }

    private static readonly Color FoeLeader = new("2a1210");

    private void FieldPlay(Town town, float now, List<(Vector3 P, float Yaw, Color C)> banners)
    {
        if (FieldNow(town) is not var (b, u)) return;
        var gen = map.Gen;
        var (spot, o) = FieldSpot(town, b);
        if (FieldU != null && !_fieldAimed)
        {
            _fieldAimed = true;
            map.LookAt(spot);
        }
        var side = o.Orthogonal();
        int rounds = Math.Max(1, b.Theirs.Left.Count);
        // 0–0.15 forming and closing, the rounds to 0.85, then the rout.
        float close = Ease(Seg(u, 0, 0.15f));
        float fight = Seg(u, 0.15f, 0.85f);
        float rout = Ease(Seg(u, 0.85f, 1));
        int round = Math.Min(rounds - 1, (int)(fight * rounds));
        bool fighting = u > 0.15f && u < 0.85f;
        bool weRan = b.End == FieldEnd.Lost, theyRan = b.End is FieldEnd.Won or FieldEnd.Slain;
        float yawOut = Mathf.Atan2(o.X, o.Y), yawIn = Mathf.Atan2(-o.X, -o.Y);

        // Our line, nearer the town, unit blocks side by side.
        float gap = 2.6f - close * 2.0f;
        int n = b.Ours.Count;
        float width = 0;
        var widths = new float[n];
        for (int i = 0; i < n; i++)
        {
            int men = Math.Clamp(b.Ours[i].Men, 4, 40);
            widths[i] = Math.Min(8, (men + 3) / 4) * 0.3f + 0.5f;
            width += widths[i];
        }
        float x0 = -width / 2;
        int fallen = 0;
        for (int i = 0; i < n; i++)
        {
            var unit = b.Ours[i];
            float x = x0 + widths[i] / 2;
            x0 += widths[i];
            if (unit.Turned) continue;
            int men = Math.Clamp(unit.Men, 4, 40);
            double left = u < 0.15f ? unit.Start : unit.Left.Count > round ? unit.Left[round] : unit.Left.LastOrDefault(unit.Start);
            int alive = (int)Math.Round(men * Math.Clamp(left / Math.Max(1, unit.Start), 0, 1));
            fallen += men - alive;
            bool ran = unit.Routed > 0 && round + 1 >= unit.Routed && fighting || weRan && u >= 0.85f || unit.Routed > 0 && u >= 0.85f;
            bool muddled = fighting && unit.Muddled.Count > round && unit.Muddled[round];
            var (body, skin) = unit.Kind switch
            {
                "regiment" => (UnitColour(town.Content.Army.Units.First(x => x.Id == unit.Look)), Palette.RaceColor("human")),
                "ally" => (AllyColourOf(town, unit.Look), Palette.RaceColor("human")),
                _ => (new Color("d8b040"), Palette.RaceColor("human")),
            };
            float back = muddled ? 0.5f : 0;
            float flee = ran ? (u >= 0.85f ? rout : 0.6f) * 9f : 0;
            int cols = Math.Min(8, (men + 3) / 4);
            for (int k = 0; k < alive; k++)
            {
                int row = k / cols, col = k % cols;
                var p = spot - o * (gap / 2 + 0.35f + row * 0.32f + back + flee) + side * (x + (col - (cols - 1) / 2f) * 0.3f);
                p += new Vector2(Geo.Hash(unit.Men * 7 + k * 13 + i) - 0.5f, Geo.Hash(k * 31 + i) - 0.5f) * 0.1f;
                float lunge = fighting && !ran ? Mathf.Max(0, Mathf.Sin(now * 5 + k * 0.7f)) * 0.12f : 0;
                p += o * lunge;
                bool mounted = unit.Role is "horse" or "beast" or "flier";
                var at = gen.At(p);
                if (mounted)
                {
                    _mounts.Add((at, ran ? yawIn : yawOut, unit.Role == "flier" ? new Color("c89a3a") : new Color(k % 2 == 0 ? "5a3a24" : "2a1e14")));
                    at.Y += 0.15f;
                }
                Put(at, ran ? yawIn : yawOut, body, skin, unit.Kind == "heroes" ? 1.25f : 1.05f);
            }
        }
        banners.Add((gen.At(spot - o * (gap / 2 + 2.2f + (weRan ? rout * 9 : 0))), Mathf.Sin(now * 0.7f) * 0.4f, new Color("b8322a")));

        // Their host, a deep block across from ours, and its leader behind it.
        var (fbody, fskin, chief) = HostColours(b.Kind);
        // A shade darker than at the walls, so the host reads apart from the town's own red.
        fbody = fbody.Darkened(0.4f);
        var foe = b.Theirs;
        int fmen = Math.Clamp((int)Math.Round(foe.Start / 3), 12, 120);
        double fleft = u < 0.15f ? foe.Start : foe.Left.Count > round ? foe.Left[round] : foe.Left.LastOrDefault(foe.Start);
        int falive = (int)Math.Round(fmen * Math.Clamp(fleft / Math.Max(1, foe.Start), 0, 1));
        int fcols = Math.Max(8, (int)(width / 0.3f));
        float fflee = theyRan && u >= 0.85f ? rout * 12f : 0;
        for (int k = 0; k < falive; k++)
        {
            int row = k / fcols, col = k % fcols;
            var p = spot + o * (gap / 2 + 0.35f + row * 0.32f + fflee) + side * ((col - (fcols - 1) / 2f) * 0.3f);
            p += new Vector2(Geo.Hash(k * 17 + 5) - 0.5f, Geo.Hash(k * 23 + 9) - 0.5f) * 0.12f;
            float lunge = fighting ? Mathf.Max(0, Mathf.Sin(now * 5 + k * 0.9f + 1)) * 0.12f : 0;
            p -= o * lunge;
            Put(gen.At(p), theyRan && u >= 0.85f ? yawOut : yawIn, fbody, fskin, b.Kind == "dragon" ? 1.3f : 1.05f);
        }
        int rows = (falive + fcols - 1) / Math.Max(1, fcols);
        var lead = spot + o * (gap / 2 + 1.2f + rows * 0.32f + fflee);
        bool leaderDown = b.End == FieldEnd.Slain && u > 0.15f + 0.7f / rounds * 1.5f;
        if (leaderDown) PutFallen(gen.At(lead), yawIn, chief, fskin, 1.4f);
        else Put(gen.At(lead), yawIn, chief, fskin, 1.4f);
        banners.Add((gen.At(lead + o * 0.6f + side * 0.6f), Mathf.Sin(now * 0.7f + 1) * 0.4f, chief.Lightened(0.15f)));

        // The fallen of both sides lie where the lines met.
        int ffallen = fmen - falive;
        for (int k = 0; k < fallen + ffallen && k < 160; k++)
        {
            bool ours = k < fallen;
            var p = spot + side * ((Geo.Hash(k * 41 + 3) - 0.5f) * width) + o * ((Geo.Hash(k * 59 + 7) - (ours ? 0.8f : 0.2f)) * 1.4f);
            PutFallen(gen.At(p), Geo.Hash(k * 3) * Mathf.Tau, ours ? new Color("6a5a4a") : fbody, ours ? Palette.RaceColor("human") : fskin);
        }
    }

    private static Color AllyColourOf(Town town, string peopleId) =>
        town.Settlements.FirstOrDefault(s => s.Kin.Id == peopleId) is { } s ? HostColour(s) : new Color("7a6a5a");
}
