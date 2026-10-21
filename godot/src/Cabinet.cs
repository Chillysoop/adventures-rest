using System.Text.Json.Nodes;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The cabinet: boons the player's won campaigns left them (Boons.cs), kept in the profile's "cabinet" section
/// with the best rank of each and which ones are switched off. The New game screen shows them as toggles grouped
/// by kind, beside the Endless switch; the ones on go into the new town's SimulationOptions.
/// </summary>
public partial class Main
{
    private VBoxContainer _cabinet = null!;
    private bool _endless;

    private static readonly (string Kind, string Name)[] BoonKinds =
        [("military", "Military"), ("economy", "Economy"), ("tech", "Tech"), ("adventuring", "Adventuring"), ("diplomacy", "Diplomacy")];

    /// <summary>The cabinet's boons by id at their best rank, and those the player switched off.</summary>
    private static (Dictionary<string, int> Boons, HashSet<string> Off) ReadCabinet()
    {
        var boons = new Dictionary<string, int>();
        var off = new HashSet<string>();
        if (ReadProfile()["cabinet"] is not JsonObject c) return (boons, off);
        if (c["boons"] is JsonObject b)
            foreach (var (id, v) in b)
                if (v is JsonValue jv && jv.TryGetValue(out int rank)) boons[id] = rank;
        if (c["off"] is JsonArray a)
            foreach (var v in a)
                if (v is JsonValue jv && jv.TryGetValue(out string? id) && id != null) off.Add(id);
        return (boons, off);
    }

    private static void WriteCabinet(Dictionary<string, int> boons, HashSet<string> off)
    {
        var b = new JsonObject();
        foreach (var (id, rank) in boons.OrderBy(kv => kv.Key, StringComparer.Ordinal)) b[id] = rank;
        var a = new JsonArray();
        foreach (var id in off.Order(StringComparer.Ordinal)) a.Add(id);
        WriteProfileSection("cabinet", new JsonObject { ["boons"] = b, ["off"] = a });
    }

    /// <summary>The boons switched on for a new town, as "id:rank".</summary>
    private static List<string> ChosenBoons()
    {
        var (boons, off) = ReadCabinet();
        return boons.Where(kv => !off.Contains(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}:{kv.Value}").ToList();
    }

    /// <summary>A won campaign the player ran fills the cabinet: each boon it earned, kept at its best rank. Returns them.</summary>
    private List<(CabinetBoonDef Boon, int Rank)> RecordWin()
    {
        if (_sim == null || _sim.Options.AutoBounties || _opts.Screenshot != null) return [];
        var earned = Boons.Earned(_sim.Town);
        if (earned.Count == 0) return earned;
        var (boons, off) = ReadCabinet();
        foreach (var (b, rank) in earned) boons[b.Id] = Math.Max(rank, boons.GetValueOrDefault(b.Id));
        WriteCabinet(boons, off);
        return earned;
    }

    private static string Stars(int rank) => new('★', Math.Clamp(rank, 1, Boons.MaxRank));

    /// <summary>Build the cabinet's box on the New game screen; it is filled each time the screen shows.</summary>
    private void BuildCabinet(VBoxContainer parent)
    {
        _cabinet = new VBoxContainer();
        _cabinet.AddThemeConstantOverride("separation", 4);
        parent.AddChild(_cabinet);
    }

    private void FillCabinet()
    {
        foreach (var c in _cabinet.GetChildren()) c.QueueFree();
        var content = Sim.Content.Default();
        var (boons, off) = ReadCabinet();
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 16);
        head.AddChild(Ui.Label("Cabinet", Ui.Ink, Ui.Display, 22));
        var endless = new CheckButton { Text = "Endless", ButtonPressed = _endless, FocusMode = FocusModeEnum.None, TooltipText = "A win ends an era, not the campaign.\nA worse foe comes. Only a fall ends it." };
        endless.Toggled += on => _endless = on;
        head.AddChild(endless);
        _cabinet.AddChild(head);
        if (boons.Count == 0)
        {
            _cabinet.AddChild(Ui.Label("Win a campaign to earn boons.", Ui.Ink2, Ui.Body, 13));
            _cabinet.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
            return;
        }
        foreach (var (kind, name) in BoonKinds)
        {
            var mine = content.Boons.Boons.Where(b => b.Kind == kind && boons.ContainsKey(b.Id)).ToList();
            if (mine.Count == 0) continue;
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("v_separation", 4);
            row.AddThemeConstantOverride("h_separation", 6);
            var label = Ui.Label(name, Ui.Ink2, Ui.Medium, 13);
            label.CustomMinimumSize = new Vector2(96, 0);
            row.AddChild(label);
            foreach (var b in mine)
            {
                string id = b.Id;
                int rank = boons[id];
                var t = new Button
                {
                    Text = $" {b.Name} {Stars(rank)}",
                    Icon = Ui.Icon(b.Icon),
                    ExpandIcon = false,
                    ToggleMode = true,
                    ButtonPressed = !off.Contains(id),
                    FocusMode = FocusModeEnum.None,
                    TooltipText = Boons.Text(b, rank),
                };
                t.AddThemeConstantOverride("icon_max_width", 22);
                t.Modulate = off.Contains(id) ? new Color(1, 1, 1, 0.45f) : Colors.White;
                t.Toggled += on =>
                {
                    t.Modulate = on ? Colors.White : new Color(1, 1, 1, 0.45f);
                    var (bs, o) = ReadCabinet();
                    if (on) o.Remove(id);
                    else o.Add(id);
                    WriteCabinet(bs, o);
                };
                row.AddChild(t);
            }
            _cabinet.AddChild(row);
        }
        _cabinet.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    }
}
