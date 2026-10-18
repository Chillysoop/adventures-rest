namespace CrownAndCompass.Sim;

public enum Importance
{
    Routine = 1,
    Notable = 2,
    Major = 3,
}

public enum LedgerCategory
{
    Arrival,
    Departure,
    Property,
    Construction,
    Business,
    Social,
    Brawl,
    Romance,
    Hardship,
    Quest,
    Council,
    War,
    Town,
    Rumor,
    Faith,
    Faction,
}

public sealed record LedgerEntry(int Id, int Tick, LedgerCategory Category, Importance Importance, string Text, IReadOnlyList<int> Agents)
{
    public string Format()
    {
        string day = SimTime.DayOf(Tick).ToString().PadLeft(3);
        string hour = SimTime.HourOf(Tick).ToString().PadLeft(2, '0');
        return $"Day {day} {hour}:00  [{Category}] {Text}";
    }
}

/// <summary>
/// The town chronicle: everything notable that happens, in order. This is what the player reads
/// to follow the living city, and what we read to judge whether the sim produces stories.
/// </summary>
public sealed class Ledger
{
    private readonly List<LedgerEntry> _entries = [];
    /// <summary>The client's and the CLI's hooks; not part of a save, so they subscribe again after a load.</summary>
    [NotSaved]
    private readonly List<Action<LedgerEntry>> _listeners = [];

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    public void OnWrite(Action<LedgerEntry> fn) => _listeners.Add(fn);

    public LedgerEntry Write(int tick, LedgerCategory category, Importance importance, string text, IReadOnlyList<int>? agents = null)
    {
        var entry = new LedgerEntry(_entries.Count + 1, tick, category, importance, text, agents ?? []);
        _entries.Add(entry);
        foreach (var fn in _listeners) fn(entry);
        return entry;
    }

    public List<LedgerEntry> About(int agentId) => _entries.Where(e => e.Agents.Contains(agentId)).ToList();
}
