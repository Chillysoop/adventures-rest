using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The town's info lens, after Dawn of Man's view filter: the map greys out (the grade shader's <c>lens</c>) and one
/// thing about the town is drawn over it in colour. Room shows where each zone's next buildings would go; Work the homes
/// of the idle and the places still hiring; Growth which houses can go up a storey and which wait on a service, food
/// or beauty; Appeal how pleasant each street is; Danger what raiders can reach and whether a guard flag covers it;
/// Neighbourhoods each part of town outlined with its name and figures, homes coloured from dear ground (gold) to cheap (red).
/// Hovering a building names the reason it is coloured so.
/// </summary>
public partial class Lens(MapView map) : Control
{
    public enum Mode { Room, Work, Growth, Appeal, Danger, Neighbourhoods }

    public static readonly Mode[] Modes = [Mode.Room, Mode.Work, Mode.Growth, Mode.Appeal, Mode.Danger, Mode.Neighbourhoods];

    public static string Name(Mode m) => m switch
    {
        Mode.Room => "Room",
        Mode.Work => "Work",
        Mode.Growth => "Growth",
        Mode.Appeal => "Appeal",
        Mode.Neighbourhoods => "Neighbourhoods",
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
            Mode.Neighbourhoods => Quarters(town, marks),
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
            // Counts only: no outlines of buildings that haven't grown yet.
            int room = Land.OpenPlots(town, z, 120);
            parts.Add($"{Planning.ZoneName(z)} {(room >= 120 ? "120+" : room.ToString())}");
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
        return $"{ready} houses can grow (green), {waiting} wait on what their street lacks (amber); most need {top.Key} ({top.Value}). Grey houses wait on the charter, their ground or their owner's purse.";
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

    private static string Quarters(Town town, List<Mark> marks)
    {
        int dear = 0, cheap = 0;
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            if (!b.Complete || b.Def.Housing <= 0) continue;
            var lot = town.Lot(b.LotId);
            double land = Neighbourhood.Land(town, lot), mood = Neighbourhood.Mood(town, lot);
            if (land >= 1.15) dear++;
            else if (land <= 0.9) cheap++;
            float k = (float)Math.Clamp((land - 0.7) / 0.8, 0, 1);
            var color = Red.Lerp(Grey, Math.Min(1, k * 2)).Lerp(new Color("ffd75e"), Math.Max(0, k * 2 - 1));
            var why = Neighbourhood.Why(town, lot.X, lot.Y);
            string place = Neighbourhood.Of(town, b) is { } q ? $" · {q.Name}" : "";
            marks.Add(new Mark(lot, color, $"{b.Name}{place}: rent ×{Neighbourhood.Rent(town, lot):0.00}, mood {(mood >= -0.05 ? "+" : "−")}{Math.Abs(mood):0.0}{(why.Count > 0 ? " · " + string.Join(", ", why) : "")}"));
        }
        int n = town.City.Quarters.Count;
        return n == 0 ? "No neighbourhoods yet: they form where buildings cluster."
            : $"{n} neighbourhoods. Homes on dear ground glow gold ({dear}), cheap ground red ({cheap}). Shops near homes, temples and fine houses raise a street; workshops and tenements lower it.";
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
        if (Showing == Mode.Neighbourhoods) Outlines(town);
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

    /// <summary>Each neighbourhood outlined, with its name and figures at its middle.</summary>
    private void Outlines(Town town)
    {
        var border = new Color("f6eedb", 0.7f);
        foreach (var (q, hull) in Planner.QuarterHulls(town))
        {
            var screen = hull.Select(Screen).ToArray();
            DrawPolyline([.. screen, screen[0]], border, 2.5f, true);
            var mid = map.ToScreen(Planner.FromTown(hull.Average(p => p.X), hull.Average(p => p.Y)), 0.6f);
            var st = Neighbourhood.Stats(town, q);
            string figures = $"{CrownAndCompass.Client.Main.CharacterName(st.Character)} · {st.Residents} living · rent ×{st.Rent:0.00} · mood {(st.Mood >= -0.05 ? "+" : "−")}{Math.Abs(st.Mood):0.0}";
            Label(mid, q.Name.ToUpperInvariant(), 15, new Color("f6eedb"));
            Label(mid + new Vector2(0, 17), figures, 12, new Color("e8dcc4"));
        }
    }

    private void Label(Vector2 at, string text, int size, Color c)
    {
        var w = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        DrawStringOutline(Ui.Bold, at - new Vector2(w / 2, 0), text, HorizontalAlignment.Left, -1, size, 4, new Color(0.08f, 0.06f, 0.04f, 0.85f));
        DrawString(Ui.Bold, at - new Vector2(w / 2, 0), text, HorizontalAlignment.Left, -1, size, c);
    }

    private Vector2 Screen(Vector2 town) => map.ToScreen(Planner.FromTown(town.X, town.Y), 0.06f);

    private void Footprint(Vector2 c, float w, float d, float yaw, Color fill, Color edge)
    {
        float s = Mathf.Sin(yaw), co = Mathf.Cos(yaw);
        var u = new Vector2(co, s) * (w / 2);
        var v = new Vector2(-s, co) * (d / 2);
        Vector2[] p = [Screen(c - u - v), Screen(c + u - v), Screen(c + u + v), Screen(c - u + v)];
        // Off the edge of the view a footprint can fold flat; there is nothing to fill.
        if (p.Any(q => !q.IsFinite()) || Mathf.Abs((p[2] - p[0]).Cross(p[3] - p[1])) < 1) return;
        DrawColoredPolygon(p, fill);
        DrawPolyline([.. p, p[0]], edge, 1.5f, true);
    }
}
