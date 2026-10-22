namespace CrownAndCompass.Sim.Systems;

/// <summary>One way to talk someone round: the mayor's skill, what it is rolled against, and what it costs them.</summary>
public sealed record TalkDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Skill { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>"insight" (their passive Insight) or "wis" (10 + their Wisdom save).</summary>
    public string Against { get; init; } = "insight";
    public int Dc { get; init; }

    /// <summary>Their regard moved on a success, more on a natural 20, on a failure, and the Mandate a failure costs.</summary>
    public double Win { get; init; }
    public double Crit { get; init; }
    public double Lose { get; init; }
    public double LoseMandate { get; init; }
    public string Text { get; init; } = "";
}

public sealed record SwayRules
{
    /// <summary>DC lower by a point for each this much loyalty over 50 (higher under it).</summary>
    public double LoyaltyPer { get; init; } = 10;
    public string Text { get; init; } = "";
    public string Won { get; init; } = "";
    public string Lost { get; init; } = "";
}

public sealed record GuildTalkRules
{
    /// <summary>The DC with no master to face; a point higher for each standingPer under 50.</summary>
    public int Dc { get; init; } = 10;
    public double StandingPer { get; init; } = 10;

    /// <summary>Days a natural 1 brings a strike nearer by.</summary>
    public int FumbleStrike { get; init; } = 3;
    public string Won { get; init; } = "";
    public string Lost { get; init; } = "";

    /// <summary>What a won talk does, for {want}: a demand dropped, a strike called off.</summary>
    public string Dropped { get; init; } = "";
    public string Ended { get; init; } = "";
}

public sealed record AllyTalkRules
{
    public double StandingPer { get; init; } = 10;
    public string Won { get; init; } = "";
    public string Lost { get; init; } = "";
}

/// <summary>Numbers for talking people round (content/game/negotiation.json).</summary>
public sealed record NegotiationRules
{
    /// <summary>The loyalty a trained councillor needs to lend the mayor a hand.</summary>
    public double Help { get; init; } = 50;
    public List<TalkDef> Approaches { get; init; } = [];
    public SwayRules Sway { get; init; } = new();
    public GuildTalkRules Guild { get; init; } = new();
    public AllyTalkRules Ally { get; init; } = new();

    public TalkDef? Approach(string id) => Approaches.FirstOrDefault(a => a.Id == id);
}

/// <summary>
/// The mayor talking someone round, by an approach the player picks: persuade, intimidate or deceive. One roll of the
/// mayor's skill against the other side's passive Insight or Wisdom; each approach weighs what it costs their regard
/// on a success and a failure. Councils (<see cref="Council.Sway"/>), guilds (<see cref="Guilds.Negotiate"/>) and
/// holding-out allies (<see cref="AllyCalls.Negotiate"/>) use it. A trained, loyal councillor lends a hand
/// (<see cref="Sheet.Helper"/>).
/// </summary>
public static class Negotiation
{
    public static NegotiationRules Rules(Town town) => town.Content.Negotiation;

    public static TalkDef? Approach(Town town, string id) => Rules(town).Approach(id);

    /// <summary>The DC against someone with this passive Insight and Wisdom save bonus, plus <paramref name="adjust"/>.</summary>
    public static int Dc(TalkDef a, int insight, int wisSave, int adjust) => (a.Against == "wis" ? 10 + wisSave : insight) + a.Dc + adjust;

    /// <summary>The DC against a person.</summary>
    public static int Dc(Town town, TalkDef a, Agent them, int adjust) =>
        Dc(a, Skills.Passive(town, them, "insight"), Skills.SaveBonus(town, them, Ability.Wis), adjust);

    /// <summary>The mayor's chance with this approach against a DC, with a helper's advantage if one is at hand.</summary>
    public static double Odds(Town town, TalkDef a, int dc) => Sheet.Chance(town, a.Skill, "", dc, Sheet.Helper(town, a.Skill) != null, false);

    /// <summary>Roll it: the mayor's check, kept as the last check so the client shows the die.</summary>
    public static StoryBeat Roll(Town town, TalkDef a, int dc, string option)
    {
        var beat = Sheet.Roll(town, a.Skill, "", dc, Sheet.HelpText(town, a.Skill), false, false);
        beat.Option = option;
        town.Stories.LastCheck = beat;
        town.Stories.Checks++;
        return beat;
    }

    /// <summary>What their regard moves after the roll.</summary>
    public static double Shift(TalkDef a, StoryBeat b) => Sheet.Succeeded(b) ? a.Win + (b.Result == StoryResult.Critical ? a.Crit : 0) : a.Lose;

    /// <summary>A failed lie found out costs Mandate.</summary>
    public static void Settle(Town town, TalkDef a, StoryBeat b)
    {
        if (!Sheet.Succeeded(b) && a.LoseMandate > 0) town.Mandate = Math.Clamp(town.Mandate - a.LoseMandate, 0, 100);
    }

    /// <summary>"Persuasion 17 against DC 13, a success".</summary>
    public static string Said(Town town, StoryBeat b) =>
        $"{Skills.Name(town, b.Skill)} {b.Total} against DC {b.Dc}{(b.Advantage != "" ? $", {b.Advantage}" : "")}, {Stories.Word(b.Result)}";
}
