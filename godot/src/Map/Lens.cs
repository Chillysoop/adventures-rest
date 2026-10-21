using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The town's info lens, after Dawn of Man's view filter: the map greys out (the grade shader's <c>lens</c>) and one
/// thing about the town is drawn over it in colour. Room shows where each zone's next buildings would go; Work the homes
/// of the idle and the places still hiring; Growth which houses can go up a storey and which wait on a service, food
/// or beauty; Appeal how pleasant each street is; Danger what raiders can reach and whether a guard flag covers it.
/// Hovering a building names the reason it is coloured so.
/// </summary>
public partial class Lens(MapView map) : Control
{
    public enum Mode { Room, Work, Growth, Appeal, Danger }

    public static readonly Mode[] Modes = [Mode.Room, Mode.Work, Mode.Growth, Mode.Appeal, Mode.Danger];

    public static string Name(Mode m) => m switch
    {
        Mode.Room => "Room",
        Mode.Work => "Work",
        Mode.Growth => "Growth",
        Mode.Appeal => "Appeal",
        _ => "Danger",
    };

    public bool On { get; private set; }

    public Mode Showing { get; private set; } = Mode.Room;

    public event Action? Changed;

    public void Show(bool on, Mode? mode = null)
    {
        On = on;
        if (mode is Mode m) Showing = m;
        _marks = null;
        QueueRedraw();
        Changed?.Invoke();
    }

    /// <summary>One coloured footprint and what it means, worked out once a game hour.</summary>
    private sealed record Mark(Lot Lot, Color Color, string Why, float Size = 1);

    private (Mode Mode, int Tick, int Plan, List<Mark> Marks, string Summary)? _marks;

    /// <summary>A line saying what the lens shows now, for the bar above it.</summary>
    public string Summary => On && map.Sim != null ? Marks(map.Sim.Town).Summary : "";

    private static readonly Color Green = new("7fd36a"), Amber = new("f0b23c"), Red = new("ef5f4f"), Grey = new("b8b0a0"), Blue = new("6fb6ff");

    private (Mode Mode, int Tick, int Plan, List<Mark> Marks, string Summary) Marks(Town town)
    {
        if (_marks is { } m && m.Mode == Showing && m.Tick == town.Tick && m.Plan == town.Plan.Version) return m;
        var marks = new List<Mark>();
        string summary = Showing switch
        {
            Mode.Room => Room(town, marks),
            Mode.Work => Work(town, marks),
            Mode.Growth => Growth(town, marks),
            Mode.Appeal => Appeal(town, marks),
            _ => Danger(town, marks),
        };
        _marks = (Showing, town.Tick, town.Plan.Version, marks, summary);
        return _marks.Value;
    }

    private static string Room(Town town, List<Mark> marks)
    {
        var parts = new List<string>();
        foreach (var z in Planning.Paintable)
        {
            var lots = Land.Plots(town, z, 120);
            foreach (var l in lots) marks.Add(new Mark(l, Planner.ZoneColor(z), $"Room for a {Planning.ZoneName(z)} building"));
            parts.Add($"{Planning.ZoneName(z)} {(lots.Count >= 120 ? "120+" : lots.Count.ToString())}");
        }
        return "Room on painted land: " + string.Join(", ", parts) + ". Paint more where a zone runs out.";
    }

    private static string Work(Town town, List<Mark> marks)
    {
        var idle = new Dictionary<int, int>();
        int idleCount = 0;
        foreach (var a in town.Workers)
        {
            if (a.JobId != null || a.Hero != null) continue;
            idleCount++;
            if (a.HomeId is int h) idle[h] = idle.GetValueOrDefault(h) + 1;
        }
        int open = 0;
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            if (b.Razed) continue;
            if (b.OpenJobs > 0)
            {
                open += b.OpenJobs;
                marks.Add(new Mark(town.Lot(b.LotId), Green, $"{b.Name}: hiring {b.OpenJobs}"));
            }
            if (idle.TryGetValue(b.Id, out int n))
                marks.Add(new Mark(town.Lot(b.LotId), Red, $"{b.Name}: {n} without work", 1 + Math.Min(n, 4) * 0.08f));
        }
        if (idleCount == 0) return open == 0 ? "Everyone has work, and nobody is hiring." : $"Everyone has work. {open} places are still hiring (green).";
        return open >= idleCount
            ? $"{idleCount} without work (red homes) and {open} places hiring (green): they will find each other."
            : $"{idleCount} without work (red homes) and only {open} places hiring. Paint industry, farmland or shops, or build a workshop.";
    }

    private static string Growth(Town town, List<Mark> marks)
    {
        int ready = 0, waiting = 0;
        var why = new Dictionary<string, int>();
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            if (!b.Complete || City.Housing(town, b.Def.Id) == null) continue;
            string? blocked = City.UpgradeBlocked(town, b);
            var lot = town.Lot(b.LotId);
            if (blocked == null)
            {
                ready++;
                marks.Add(new Mark(lot, Green, $"{b.Name}: ready to grow when its household can afford it"));
            }
            else if (Lacks(blocked) is string key)
            {
                waiting++;
                why[key] = why.GetValueOrDefault(key) + 1;
                marks.Add(new Mark(lot, Amber, $"{b.Name} {blocked}"));
            }
            else if (blocked != "it is as grand as houses get")
                marks.Add(new Mark(lot, Grey, $"{b.Name}: {blocked}"));
            else marks.Add(new Mark(lot, Blue, $"{b.Name}: as grand as houses get"));
        }
        if (waiting == 0) return $"{ready} houses can grow a storey (green). None wait on services.";
        var top = why.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
        return $"{ready} houses can grow (green), {waiting} wait on what their street lacks (amber); most need {top.Key} ({top.Value}). Grey houses wait on the charter, their ward or their owner's purse.";
    }

    /// <summary>What a house's street lacks for its next storey (a service, food variety, beauty), from <see cref="City.NeedsBlocked"/>'s line; null for any other reason.</summary>
    private static string? Lacks(string blocked)
    {
        if (blocked.Contains(" within ", StringComparison.Ordinal)) return blocked.Split(" within ")[0].Replace("needs ", "");
        if (blocked.Contains("kinds of food", StringComparison.Ordinal)) return "more kinds of food";
        if (blocked.StartsWith("needs something pleasant", StringComparison.Ordinal)) return "something pleasant nearby";
        return null;
    }

    private static string Appeal(Town town, List<Mark> marks)
    {
        double sum = 0;
        int n = 0;
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            if (!b.Complete) continue;
            double a = Decor.AppealOf(town, b);
            sum += a;
            n++;
            float t = (float)Math.Clamp(a / 3, 0, 1);
            marks.Add(new Mark(town.Lot(b.LotId), new Color("6a5f52").Lerp(new Color("ffd75e"), t), $"{b.Name}: appeal {a:0.0}"));
        }
        return n == 0 ? "Nothing built yet." : $"Brighter streets are more pleasant. Average appeal {sum / n:0.0}; trees, fountains and gardens raise it.";
    }

    private static string Danger(Town town, List<Mark> marks)
    {
        int bare = 0, flagged = 0;
        var marked = town.Sightings.Where(s => s.Kind == Sightings.Band && s.UntilTick > town.Tick && s.TargetId != null).Select(s => s.TargetId!.Value).ToHashSet();
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            if (!b.Complete || !Threat.Exposed(town, b)) continue;
            var lot = town.Lot(b.LotId);
            string where = b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp" ? "out in the fields" : "outside the walls";
            if (marked.Contains(b.Id)) marks.Add(new Mark(lot, Red, $"{b.Name}: raiders were seen heading here", 1.3f));
            else if (Garrison.Covered(town, b))
            {
                flagged++;
                marks.Add(new Mark(lot, Amber, $"{b.Name}: {where}, under a guard flag"));
            }
            else
            {
                bare++;
                marks.Add(new Mark(lot, Red, $"{b.Name}: {where} with no guard flag; raiders can reach it"));
            }
        }
        string seen = marked.Count > 0 ? $" Raiders are heading for {marked.Count} (bright red)." : "";
        return bare + flagged == 0
            ? "Nothing stands where raiders can reach it." + seen
            : $"{bare} buildings raiders can reach unguarded (red), {flagged} under a guard flag (amber).{seen} Press G to plant a flag.";
    }

    public override void _Draw()
    {
        if (!On || map.Sim == null) return;
        var town = map.Sim.Town;
        var (_, _, _, marks, _) = Marks(town);
        foreach (var m in marks)
        {
            float w = m.Lot.W / (float)Land.Res * m.Size, d = m.Lot.D / (float)Land.Res * m.Size;
            Footprint(new Vector2((float)m.Lot.X, (float)m.Lot.Y), w, d, (float)m.Lot.Yaw, new Color(m.Color, 0.5f), m.Color);
        }
        // The building under the cursor says why it is coloured so.
        var mouse = GetLocalMousePosition();
        Mark? best = null;
        float bestD = 26;
        foreach (var m in marks)
        {
            float dd = Screen(new Vector2((float)m.Lot.X, (float)m.Lot.Y)).DistanceTo(mouse);
            if (dd < bestD)
            {
                bestD = dd;
                best = m;
            }
        }
        if (best != null)
        {
            var at = mouse + new Vector2(0, -18);
            var w = Ui.Bold.GetStringSize(best.Why, HorizontalAlignment.Left, -1, 13).X;
            DrawRect(new Rect2(at - new Vector2(w / 2 + 8, 16), new Vector2(w + 16, 22)), new Color(0.08f, 0.06f, 0.04f, 0.85f));
            DrawString(Ui.Bold, at - new Vector2(w / 2, 0), best.Why, HorizontalAlignment.Left, -1, 13, best.Color.Lerp(new Color("f6eedb"), 0.5f));
        }
    }

    private Vector2 Screen(Vector2 town) => map.ToScreen(Planner.FromTown(town.X, town.Y), 0.06f);

    private void Footprint(Vector2 c, float w, float d, float yaw, Color fill, Color edge)
    {
        float s = Mathf.Sin(yaw), co = Mathf.Cos(yaw);
        var u = new Vector2(co, s) * (w / 2);
        var v = new Vector2(-s, co) * (d / 2);
        Vector2[] p = [Screen(c - u - v), Screen(c + u - v), Screen(c + u + v), Screen(c - u + v)];
        DrawColoredPolygon(p, fill);
        DrawPolyline([.. p, p[0]], edge, 1.5f, true);
    }
}
