namespace CrownAndCompass.Sim.Systems;

/// <summary>Who tells the campaign (content/game/storytellers.json): the difficulty chosen when a town is founded.</summary>
public sealed record StorytellerRules
{
    /// <summary>The storyteller a town has when none was chosen (and every save from before there were any).</summary>
    public string Default { get; init; } = "rising";
    public List<StorytellerDef> Storytellers { get; init; } = [];
}

/// <summary>
/// One storyteller: multipliers on the pacing levers that already exist (the Director's pace, raid timing and
/// strength, menaces' growth and strikes, crises, story events, inspirations and breaks). 1 everywhere is the game
/// as it was before storytellers. Swing makes a storyteller roll for each: bigger and smaller, sooner and later.
/// </summary>
public sealed record StorytellerDef
{
    public string Id { get; init; } = "";

    /// <summary>The one-word difficulty ("Steady"), the storyteller's name, their character and what playing under them is like.</summary>
    public string Title { get; init; } = "";
    public string Name { get; init; } = "";
    public string Character { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>On the Director's pace for the warband, and its pace when the town is hurting (Rising's 0.5).</summary>
    public double Pace { get; init; } = 1;
    public double Breather { get; init; } = 0.5;

    /// <summary>Tension past which the Director may send a hero to help, and the chance each day it does.</summary>
    public double HelpTension { get; init; } = 45;
    public double Help { get; init; } = 0.25;

    /// <summary>On the days between raids, the raiders who come, the days between menaces' strikes and their growth.</summary>
    public double RaidEvery { get; init; } = 1;
    public double RaidStrength { get; init; } = 1;
    public double StrikeEvery { get; init; } = 1;
    public double MenaceGrowth { get; init; } = 1;

    /// <summary>On the days a power takes to ripen into a crisis, and the quiet before a new power rises.</summary>
    public double Ripen { get; init; } = 1;
    public double Lull { get; init; } = 1;

    /// <summary>On the daily chance of a story event, and of townsfolk's inspirations and breaks (<see cref="Moods"/>).</summary>
    public double Stories { get; init; } = 1;
    public double Inspire { get; init; } = 1;
    public double Breaks { get; init; } = 1;

    /// <summary>How far each of the above may swing either way, rolled each time (0 for none).</summary>
    public double Swing { get; init; }

    /// <summary>Daily chances of a windfall out of nowhere (a hero, a patron's purse) and of trouble out of nowhere (raiders or a menace stirred early).</summary>
    public double Boon { get; init; }
    public double Bane { get; init; }
}

/// <summary>
/// The storyteller the player chose for this town. Every system that paces the campaign asks it for a multiplier,
/// and Rising answers 1 without a roll, so a Rising town plays exactly as the game did before storytellers.
/// </summary>
public static class Storyteller
{
    public static StorytellerRules Rules(Town town) => town.Content.Storytellers;

    public static StorytellerDef? Def(Content c, string id) => c.Storytellers.Storytellers.FirstOrDefault(s => s.Id == id);

    /// <summary>The town's storyteller, or the default for a town from before there were any.</summary>
    public static StorytellerDef Of(Town town) =>
        Def(town.Content, town.Storyteller ?? "") ?? Def(town.Content, Rules(town).Default) ?? new StorytellerDef { Id = "rising" };

    /// <summary>A multiplier with the storyteller's swing on it: rolled only for a storyteller who swings.</summary>
    private static double Swung(Town town, double m)
    {
        double sw = Of(town).Swing;
        return sw <= 0 ? m : m * (1 + sw * (2 * town.Rng.Next() - 1));
    }

    /// <summary>The warband's daily growth from the Director.</summary>
    public static double Pace(Town town) => Swung(town, Of(town).Pace);

    public static double RaidEvery(Town town) => Swung(town, Of(town).RaidEvery);

    public static double RaidStrength(Town town) => Swung(town, Of(town).RaidStrength);

    public static double StrikeEvery(Town town) => Swung(town, Of(town).StrikeEvery);

    public static double MenaceGrowth(Town town) => Of(town).MenaceGrowth;

    /// <summary>
    /// What the storyteller does on its own each day: a swinging one may send a windfall (a hero riding in, a
    /// stranger's purse) or trouble (raiders sooner than they were due, a menace stirred to strike).
    /// </summary>
    public static void Daily(Town town)
    {
        var st = Of(town);
        if (st.Boon <= 0 && st.Bane <= 0) return;
        var rng = town.Rng;
        if (st.Boon > 0 && rng.Chance(st.Boon))
        {
            if (rng.Chance(0.5)) Director.SendHelp(town);
            else Director.SendStranger(town);
        }
        if (st.Bane > 0 && rng.Chance(st.Bane)) Trouble(town);
    }

    /// <summary>Trouble from nowhere: the warband's raiders come early, or the nearest menace strikes sooner than it meant to.</summary>
    private static void Trouble(Town town)
    {
        var en = town.Enemy;
        if (en.Stage == ThreatStage.Raids && en.NextRaidTick is int next && next > town.Tick + SimTime.HoursPerDay)
        {
            en.NextRaidTick = town.Tick + SimTime.HoursPerDay;
            town.Log(LedgerCategory.War, Importance.Notable, $"Smoke on the hills: riders of {town.Content.Enemy.Name} are out sooner than anyone looked for. Expect them tomorrow night.");
            return;
        }
        var m = Menaces.Alive(town).Where(m => m.Origin != Menaces.FromWarband && m.Stage >= MenaceStage.Rising && m.NextStrikeTick > town.Tick + SimTime.HoursPerDay)
            .OrderBy(m => m.NextStrikeTick).ThenBy(m => m.Id).FirstOrDefault();
        if (m == null) return;
        m.NextStrikeTick = town.Tick + SimTime.HoursPerDay;
        town.Log(LedgerCategory.War, Importance.Notable, $"Something has stirred {m.Name} early. Travellers say it is on the move.");
    }
}
