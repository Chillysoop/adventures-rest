using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Sea tests start with ships the day they are laid down: put enough townsfolk on the harbour's books to sail them.</summary>
internal static class Hands
{
    public static void Man(Town t)
    {
        var h = Fleet.Harbour(t)!;
        h.Crew = Fleet.CrewNeeded(t);
        foreach (var a in t.Workers.Where(a => a.JobId != h.Id).OrderBy(a => a.Id).Take(Math.Max(0, Fleet.CrewNeeded(t) - h.Workers.Count)).ToList())
        {
            if (a.JobId is int j) t.Buildings.First(b => b.Id == j).Workers.Remove(a.Id);
            a.JobId = h.Id;
            h.Workers.Add(a.Id);
        }
    }
}
