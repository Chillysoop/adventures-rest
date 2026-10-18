using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The build bar's Decor tab: set dressing by kind (greenery, street, trade, farm, faith,
/// monuments, martial) and an eraser. Each button says what the piece costs and what it does;
/// picking one puts it in hand on the map (<see cref="Decorator"/>).
/// </summary>
public partial class Main
{
    private static readonly string[] DecorGroups = ["Greenery", "Street", "Trade", "Farm", "Faith", "Monument", "Martial"];
    private string _decorGroup = DecorGroups[0];
    private readonly List<(string? Piece, Button Button)> _decorButtons = [];

    private void ShowDecor()
    {
        _decorButtons.Clear();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        _buildItems.AddChild(col);
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 4);
        col.AddChild(tabs);
        foreach (var g in DecorGroups)
        {
            var t = new Button { Text = g == "Monument" ? "Monuments" : g, ToggleMode = true, ButtonPressed = g == _decorGroup, FocusMode = FocusModeEnum.None };
            t.AddThemeFontSizeOverride("font_size", 12);
            t.Pressed += () =>
            {
                _decorGroup = g;
                ShowBuildGroup(BuildGroup.Decor);
            };
            tabs.AddChild(t);
        }
        var items = new HBoxContainer();
        items.AddThemeConstantOverride("separation", 6);
        col.AddChild(items);
        foreach (var def in _sim.Town.Content.Decor.Pieces.Where(p => p.Group == _decorGroup))
            items.AddChild(DecorButton(def.Id, def.Name));
        items.AddChild(new VSeparator());
        items.AddChild(DecorButton(null, "Clear"));
        RenderBuild();
    }

    private Button DecorButton(string? piece, string label)
    {
        var b = new Button { ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(104, 58), ClipText = true, Text = label };
        b.AddThemeFontSizeOverride("font_size", 12);
        b.Pressed += () => _map.Decorator.Pick(b.ButtonPressed ? new Decorator.DecorTool(piece) : null);
        _decorButtons.Add((piece, b));
        return b;
    }

    private void SyncDecorButtons()
    {
        var tool = _map.Decorator.Tool;
        foreach (var (piece, b) in _decorButtons) b.SetPressedNoSignal(tool != null && tool.Piece == piece);
    }

    private void RenderDecor()
    {
        var town = _sim.Town;
        foreach (var (piece, b) in _decorButtons)
        {
            if (piece == null)
            {
                b.Text = "Clear\nhalf back";
                b.TooltipText = "Drag over decorations to clear them away, for half their price back.";
                continue;
            }
            var def = town.Content.Decor.Piece(piece);
            string? why = Decor.Blocked(town, def);
            b.Text = $"{def.Name}\n{(why ?? $"{Decor.Cost(town, def)}g").Replace("not enough gold", "short of gold")}";
            Ui.GoldButton(b);
            b.Disabled = why != null && !b.ButtonPressed;
            string tip = $"{def.Blurb}\n\n{DecorEffects(def)}";
            b.TooltipText = why == null ? tip : $"{tip}\n({Ui.Cap(why)}.)";
        }
        SyncDecorButtons();
        var def0 = _map.Decorator.Tool?.Piece is string id ? town.Content.Decor.Piece(id) : null;
        _buildHint.Text = _map.Decorator.Tool switch
        {
            null => GroupHints[BuildGroup.Decor],
            { Piece: null } => "Click a decoration or drag over several to clear them. Right-click to put the tool down.",
            _ when def0!.Line > 0 => $"Drag to run a row of {def0.Name.ToLowerInvariant()}, or click for one. R or Shift+wheel turns it. Right-click to put it down.",
            _ when def0!.Scatter => $"Click to plant, or drag to paint {def0.Name.ToLowerInvariant()} freely. R or Shift+wheel turns it. Right-click to put it down.",
            _ => $"Click to set the {def0!.Name.ToLowerInvariant()} down, or press and drag to aim it. R or Shift+wheel turns it. Right-click to put it down.",
        };
    }

    /// <summary>What a piece does, in a line.</summary>
    public static string DecorEffects(DecorDef d)
    {
        var parts = new List<string>();
        if (d.Appeal != 0) parts.Add(d.Appeal > 0 ? $"appeal +{d.Appeal:0.#}" : $"appeal {d.Appeal:0.#}");
        if (d.Rest > 0) parts.Add("rest");
        if (d.Light > 0) parts.Add("lights the street for the watch");
        if (d.Trade > 0) parts.Add("trade nearby");
        if (d.Harvest > 0) parts.Add("harvest nearby");
        if (d.Talk > 0) parts.Add("rumours at a tavern nearby");
        if (d.Food > 0) parts.Add("food for market");
        if (d.Favor > 0) parts.Add("a god's favour");
        if (d.Honor > 0) parts.Add("heroes' loyalty");
        if (d.Draw > 0) parts.Add("draws settlers");
        if (d.Mandate > 0) parts.Add("mandate");
        if (d.Drill > 0) parts.Add("strength at the walls");
        if (d.Deter > 0) parts.Add("deters thieves");
        string line = Ui.Cap(string.Join(" · ", parts));
        if (d.GrowDays > 0) line += $" (grows over {d.GrowDays} days)";
        if (d.Upkeep > 0) line += $". Upkeep {d.Upkeep:0.##}g a day";
        if (d.Population > 0) line += $". Needs {d.Population} residents";
        return line + ".";
    }

    /// <summary>For screenshots: open the Decor tab with a piece in hand, shown at a point in lots (x, y, angle).</summary>
    private void OpenDecor(string piece, double[]? at)
    {
        if (!_buildBar.Visible) ToggleBuildBar();
        var def = piece == "eraser" ? null : _sim.Town.Content.Decor.Piece(piece);
        if (def != null) _decorGroup = def.Group;
        ShowBuildGroup(BuildGroup.Decor);
        _map.Decorator.Pick(new Decorator.DecorTool(def?.Id));
        if (at is { Length: >= 2 })
        {
            if (at.Length >= 3) _map.Decorator.Turn((int)Math.Round(at[2] / (Math.PI / 12)));
            _map.Decorator.Hover(new Vector2(Map.Geo.Margin + (float)at[0], Map.Geo.Margin + (float)at[1]));
        }
        RenderBuild();
    }
}
