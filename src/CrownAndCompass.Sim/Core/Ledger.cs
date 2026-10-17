namespace CrownAndCompass.Sim.Core;

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
    Town,
}

public sealed record LedgerEntry(
    int Id,
    long Tick,
    LedgerCategory Category,
    Importance Importance,
    string Text,
    IReadOnlyList<int> Agents)
{
    public int Day => (int)(Tick / SimTime.HoursPerDay) + 1;
    public int Hour => (int)(Tick % SimTime.HoursPerDay);

    public override string ToString() => $"Day {Day,3} {Hour:00}:00  [{Category}] {Text}";
}

/// <summary>
/// The town chronicle: everything notable that happens, in order. This is what the player reads
/// to follow the living city, and what we read to judge whether the sim produces stories.
/// </summary>
public sealed class Ledger
{
    private readonly List<LedgerEntry> _entries = new();
    public IReadOnlyList<LedgerEntry> Entries => _entries;

    public event Action<LedgerEntry>? Written;

    public LedgerEntry Write(long tick, LedgerCategory category, Importance importance, string text, params int[] agents)
    {
        var entry = new LedgerEntry(_entries.Count + 1, tick, category, importance, text, agents);
        _entries.Add(entry);
        Written?.Invoke(entry);
        return entry;
    }

    public IEnumerable<LedgerEntry> About(int agentId) => _entries.Where(e => e.Agents.Contains(agentId));
}
