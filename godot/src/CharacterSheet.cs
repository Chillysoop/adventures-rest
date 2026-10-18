using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

public partial class Main
{
    /// <summary>What each kind of everyday check was about, for the sheet's latest rolls.</summary>
    private static readonly Dictionary<string, string> CheckKinds = new()
    {
        ["talk"] = "tavern talk",
        ["brawl"] = "a brawl",
        ["calm"] = "keeping the peace",
        ["tend"] = "seeing to bruises",
        ["haggle"] = "haggling over supplies",
        ["craft"] = "a day at the trade",
        ["perform"] = "a song at the tavern",
        ["pickpocket"] = "a hand in a purse",
        ["steal"] = "the town strongbox",
        ["intimidate"] = "facing a brawler down",
        ["bargain"] = "haggling over gear",
    };

    private int _rollsSeen = -1;

    /// <summary>
    /// Pop a little d20 over each townsperson or hero who rolled a check since the last frame. Idle tavern talk
    /// only shows on a natural 20 or 1, so the few dice on screen go to haggles, songs, brawls and thieves.
    /// </summary>
    private void SyncTownRolls()
    {
        var t = _sim.Town;
        if (_rollsSeen < 0 || _rollsSeen > t.CheckSerial) _rollsSeen = t.CheckSerial;
        if (t.CheckSerial == _rollsSeen) return;
        foreach (var c in t.Checks)
        {
            if (c.Serial <= _rollsSeen || c.AgentId is not int id) continue;
            if (c.Kind == "talk" && !c.Critical && !c.Fumble) continue;
            var result = c.Critical ? StoryResult.Critical : c.Fumble ? StoryResult.Fumble : c.Success ? StoryResult.Success : StoryResult.Failure;
            _map.Rolls.Show(id, Skills.Name(t, c.Skill), c.Die, c.Bonus, result);
        }
        _rollsSeen = t.CheckSerial;
    }

    /// <summary>
    /// Anyone's character sheet: ability scores, proficiency, all eighteen skills with their bonus (trained ones
    /// marked, expert ones doubly), and the last few d20s they rolled around town.
    /// </summary>
    private void InspectSheet(Pane p, Town t, Agent a)
    {
        p.Heading("Character sheet");
        p.Add($"ab{a.Abilities}", () => AbilityRow(a.Abilities));
        if (a.Hero is HeroSheet hs)
        {
            string bg = hs.Background != "" && t.Content.Library.Background(hs.Background) is { } b ? $" · {b.Name.ToLowerInvariant()} background" : "";
            p.Text($"{Tag($"Proficiency +{Skills.HeroProficiency(hs.Level)}", Ui.Ink)}  {Ui.C(Ui.Ink2, $"[font_size=12]Level {hs.Level} {Ui.Esc(hs.Cls.Name.ToLowerInvariant())}{Ui.Esc(bg)}; half proficiency on everything else[/font_size]")}", 13);
        }
        else
        {
            var r = t.Content.Skills.Townsfolk;
            int practised = Skills.Practised(t, a);
            string grade = practised >= r.PracticeMax ? "masterful" : practised > 0 ? "practised" : "learning";
            int toNext = practised >= r.PracticeMax ? 0 : r.PracticeStep - a.Practice % Math.Max(1, r.PracticeStep);
            p.Text($"{Tag($"Trade +{r.Proficiency + practised}", Ui.Ink)}  {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(a.Profession))}, {grade}{(toNext > 0 ? $" ({toNext} good turns at the trade to the next step)" : "")}. Untrained at everything else.[/font_size]")}", 13);
        }
        var cells = new System.Text.StringBuilder("[table=2]");
        foreach (var s in Skills.All(t))
        {
            bool trained = Skills.Trained(t, a, s.Id), expert = Skills.Expert(t, a, s.Id);
            int bonus = Skills.Bonus(t, a, s.Id);
            string mark = expert ? "◆" : trained ? "●" : "○";
            string line = $"{mark} {Ui.Esc(s.Name)} {(trained ? "[b]" : "")}{Skills.Sign(bonus)}{(trained ? "[/b]" : "")}";
            cells.Append("[cell]").Append(Ui.C(trained ? Ui.Ink : Ui.Ink2, $"[font_size=12]{line}[/font_size]")).Append("[/cell]");
        }
        cells.Append("[/table]");
        p.Text(cells.ToString(), 12);
        var rolls = Skills.Recent(t, a.Id).Take(5).ToList();
        if (rolls.Count == 0) return;
        p.Heading("Latest rolls");
        foreach (var c in rolls)
        {
            string die = c.Critical ? Ui.C(Ui.Gold, "[b]20[/b]") : c.Fumble ? Ui.C(Ui.Bad, "[b]1[/b]") : c.Die.ToString();
            string result = c.Success ? Ui.C(Ui.Good, "success") : Ui.C(Ui.Bad, "failure");
            string what = CheckKinds.GetValueOrDefault(c.Kind, c.Kind);
            p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {c.Tick / 24 + 1}, {c.Tick % 24:00}:00[/font_size]")}  {Ui.Esc(Skills.Name(t, c.Skill))}: d20 {die} {Skills.Sign(c.Bonus)} = {c.Total} vs {c.Dc}, {result} {Ui.C(Ui.Ink2, $"[font_size=11]· {Ui.Esc(what)}[/font_size]")}", 12);
        }
    }
}
