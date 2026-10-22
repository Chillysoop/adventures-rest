namespace CrownAndCompass.Sim.Systems;

/// <summary>How often the campaign's graphs take a reading, and how many pins they keep (trends.json <c>records</c>).</summary>
public sealed record RecordRules
{
    /// <summary>Days between readings: a week, so a thousand-day campaign is under 150 points.</summary>
    public int Every { get; init; } = 7;

    /// <summary>Pins on the lines (sieges, charters, the fallen...), the newest kept.</summary>
    public int Pins { get; init; } = 160;
}

/// <summary>One reading of the town's key numbers, kept for the whole campaign.</summary>
public sealed class RecordWeek
{
    public int Day { get; set; }
    public int Population { get; set; }
    public int Heroes { get; set; }
    public double Treasury { get; set; }
    public double Food { get; set; }
    public double Mandate { get; set; }
    public double Unrest { get; set; }
    public double Dread { get; set; }
    public double Defense { get; set; }

    /// <summary>What the enemies abroad muster: the warband and every menace's power.</summary>
    public double Foes { get; set; }
}

/// <summary>A moment pinned on the graphs: what kind ("siege", "charter", "fallen"...), the day and a short line.</summary>
public sealed class RecordPin
{
    public int Day { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class RecordState
{
    /// <summary>Readings, oldest first.</summary>
    public List<RecordWeek> Weeks { get; } = [];
    public List<RecordPin> Pins { get; } = [];
}

/// <summary>
/// The whole campaign at a glance, after Anno's statistics: every week the town's people, heroes, gold, food, Mandate,
/// unrest, dread, defence and the enemies' strength go down as a reading, and the big moments are pinned on the lines.
/// The top bar's month (<see cref="Trends"/>) is the near view; this is the long one. It only watches.
/// </summary>
public static class Records
{
    public static RecordRules Rules(Town town) => town.Content.Trends.Records;

    /// <summary>The series the graphs draw: id, label and how to read it off a week.</summary>
    public static readonly IReadOnlyList<(string Id, string Label, Func<RecordWeek, double> Of)> Series =
    [
        ("people", "People", w => w.Population),
        ("heroes", "Heroes", w => w.Heroes),
        ("gold", "Gold", w => w.Treasury),
        ("food", "Food", w => w.Food),
        ("mandate", "Mandate", w => w.Mandate),
        ("unrest", "Unrest", w => w.Unrest),
        ("dread", "Dread", w => w.Dread),
        ("defence", "Defence", w => w.Defense),
        ("foes", "Foes", w => w.Foes),
    ];

    /// <summary>End of day, after the books close: a reading on the first day and every week after.</summary>
    public static void Close(Town town)
    {
        int day = town.Day - 1;
        var weeks = town.Records.Weeks;
        if (weeks.Count > 0 && day - weeks[^1].Day < Math.Max(1, Rules(town).Every)) return;
        weeks.Add(new RecordWeek
        {
            Day = day,
            Population = town.Population,
            Heroes = town.Heroes.Count,
            Treasury = Js.Round(town.Treasury),
            Food = Js.Round(town.Market.Stock(Good.Food)),
            Mandate = Js.Round(town.Mandate),
            Unrest = Js.Round(town.Taxes.Unrest),
            Dread = Js.Round(town.Saga.Dread),
            Defense = Js.Round(Threat.Defense(town)),
            Foes = Js.Round((Threat.IsActive(town) ? town.Enemy.Strength : 0) + Js.Sum(Menaces.Alive(town), m => m.Power)),
        });
    }

    /// <summary>Pin a moment on the graphs at today.</summary>
    public static void Pin(Town town, string kind, string text)
    {
        var pins = town.Records.Pins;
        pins.Add(new RecordPin { Day = town.Day, Kind = kind, Text = text });
        while (pins.Count > Math.Max(1, Rules(town).Pins)) pins.RemoveAt(0);
    }

    /// <summary>The highest a series has been, for the graph's scale (at least 1).</summary>
    public static double Peak(Town town, Func<RecordWeek, double> of) =>
        town.Records.Weeks.Count == 0 ? 1 : Math.Max(1, town.Records.Weeks.Max(of));
}
