using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// The odds of a mayor's roll before it is made, as a tabletop DM tells you the roll: a short line for a button or
/// a menu entry ("d20+5 vs 13 · 60%") and the breakdown for its hover (bonus, proficiency, who helps, the DC).
/// </summary>
public static class Odds
{
    /// <summary>"d20+5 ▲ vs 13 · 60%", ▲ for advantage.</summary>
    public static string Line(Town t, string skill, int dc, bool advantage, string ability = "")
    {
        var (mod, prof, _) = Stories.Bonus(t, skill, ability);
        double p = Sheet.Chance(t, skill, ability, dc, advantage, false);
        return $"d20{Sign(mod + prof)}{(advantage ? " ▲" : "")} vs {dc} · {Pct(p)}";
    }

    /// <summary>The hover: the skill's bonus and where it comes from, any help, the DC and why, and the chance.</summary>
    public static string Tip(Town t, string skill, int dc, string advantage, string dcWhy = "", string ability = "")
    {
        var (mod, prof, ab) = Stories.Bonus(t, skill, ability);
        double p = Sheet.Chance(t, skill, ability, dc, advantage != "", false);
        var lines = new List<string>
        {
            $"{Skills.Name(t, skill != "" ? skill : ability)}: d20 {Sign(mod)} {ab.ToUpperInvariant()}{(prof != 0 ? $" {Sign(prof)} proficiency" : "")}",
        };
        if (advantage != "") lines.Add($"Advantage: {advantage}");
        lines.Add($"DC {dc}{(dcWhy != "" ? $": {dcWhy}" : "")}");
        lines.Add($"{Pct(p)} to succeed · a natural 20 always does, a 1 never");
        return string.Join("\n", lines);
    }

    /// <summary>The odds of a light everyday check (stories.json decisions), for a button's hover.</summary>
    public static string Decision(Town t, string decision, int? dc = null)
    {
        if (!t.Content.Stories.Decisions.TryGetValue(decision, out var d)) return "";
        string help = Sheet.HelpText(t, d.Skill);
        int at = dc ?? d.Dc;
        return $"Rolls {Line(t, d.Skill, at, help != "")}\n{Tip(t, d.Skill, at, help)}";
    }

    /// <summary>One menu entry per approach: its icon, "Persuade · d20+5 vs 13 · 60%", and its hover.</summary>
    public static List<(string Icon, string Text, string Tip)> Approaches(Town t, Func<TalkDef, int> dc, Func<TalkDef, string> why)
    {
        var list = new List<(string, string, string)>();
        foreach (var a in Negotiation.Rules(t).Approaches)
        {
            int at = dc(a);
            string help = Sheet.HelpText(t, a.Skill);
            list.Add((a.Icon, $"{a.Name} · {Line(t, a.Skill, at, help != "")}", $"{a.Text}\n{Tip(t, a.Skill, at, help, why(a))}"));
        }
        return list;
    }

    public static string Sign(int m) => m >= 0 ? $"+{m}" : $"−{-m}";

    public static string Pct(double p) => $"{Math.Round(p * 100)}%";
}
