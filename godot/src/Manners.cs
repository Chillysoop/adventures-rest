using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Each faction's manners (<see cref="Manners"/>): its personality as icon chips (how to win them on hover), and the
/// overtures it answers to, as one roll menu under its row in the Diplomacy window.
/// </summary>
public partial class Main
{
    /// <summary>A faction's manners as chips: icon and name, how to win them on hover.</summary>
    internal static string MannerChips(Town t, string key) =>
        Ui.Chips(Manners.Of(t, key).Select(m => Ui.Chip(m.Icon, m.Name, Ui.Gold, m.Tip, 14)).ToArray());

    /// <summary>The overtures a people or power answers to, with each roll's odds against their manners.</summary>
    private void RenderOvertures(Pane p, Town t, string key)
    {
        var list = Manners.Overtures(t, key);
        if (list.Count == 0) return;
        var items = new List<(string Icon, string Text, string Tip)>();
        foreach (var (m, o) in list)
        {
            string cost = o.Cost > 0 ? $" · {Js.Str(o.Cost)}g" : "";
            string? no = Manners.Refuse(t, key, m.Id);
            int dc = Manners.Dc(t, key, o);
            string help = o.Skill != "" ? Sheet.HelpText(t, o.Skill) : "";
            string odds = o.Skill != "" ? Odds.Line(t, o.Skill, dc, help != "") : "sure";
            string why = o.Skill != "" ? Manners.Why(t, key, o.Skill) : "";
            string text = $"{o.Name} · {(no != null && no.StartsWith("again", StringComparison.Ordinal) ? no : odds)}{cost}";
            string tip = $"{m.Name}: {m.Tip}" +
                         (o.Skill != "" ? $"\n{Odds.Tip(t, o.Skill, dc, help, why != "" ? $"by their ways, {why}" : "")}" : "") +
                         $"\nStanding +{Js.Str(o.Win)}{(o.Lose != 0 && o.Skill != "" ? $", {(o.Lose > 0 ? "+" : "−")}{Js.Str(Math.Abs(o.Lose))} on a miss" : "")} · again in {o.Every} days" +
                         (no != null ? $"\nNot now: {no}" : "");
            items.Add((o.Icon, text, tip));
        }
        p.RollMenus(("Overtures", "standing", "Win them their way: one overture per manner", items,
            i => Act(() => _sim.Overture(key, list[i].Manner.Id))));
    }
}
