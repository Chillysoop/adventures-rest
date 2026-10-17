using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Core;

// Headless runner: simulate a town and print its Ledger, so we can judge whether the city feels alive.
// Usage: dotnet run --project src/CrownAndCompass.Cli -- [--seed N] [--days N] [--all] [--stats] [--people N]

ulong seed = 1;
int days = 60;
bool all = false, stats = false;
int people = 3;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seed": seed = ulong.Parse(args[++i]); break;
        case "--days": days = int.Parse(args[++i]); break;
        case "--all": all = true; break;
        case "--stats": stats = true; break;
        case "--people": people = int.Parse(args[++i]); break;
        default:
            Console.Error.WriteLine($"Unknown argument {args[i]}");
            return 1;
    }
}

var sim = new Simulation(new SimulationOptions { Seed = seed });
var town = sim.Town;
var minImportance = all ? Importance.Routine : Importance.Notable;

town.Ledger.Written += e =>
{
    if (e.Importance >= minImportance) Console.WriteLine(e);
};
foreach (var e in town.Ledger.Entries.Where(e => e.Importance >= minImportance)) Console.WriteLine(e);

int lastReported = 0;
for (int d = 0; d < days; d++)
{
    sim.RunDays(1);
    var s = sim.History[^1];
    if (stats || s.Day % 10 == 0 && s.Day != lastReported)
    {
        lastReported = s.Day;
        Console.WriteLine(
            $"  -- end of day {s.Day}: pop {s.Population}, homeless {s.Homeless}, idle {s.Unemployed}, " +
            $"buildings {s.Buildings} (+{s.Sites} rising), treasury {s.Treasury:0}, food {s.FoodPrice:0.00}g, " +
            $"mood {s.AverageMood:0}, avg purse {s.AverageGold:0}g");
    }
}

Console.WriteLine();
Console.WriteLine($"=== {town.Name} after {days} days (seed {seed}) ===");
var final = sim.Snapshot();
Console.WriteLine($"Population {final.Population} ({town.Agents.Count(a => a.Departed)} have left), treasury {final.Treasury:0} gold.");
foreach (var g in town.Buildings.Where(b => b.Complete).GroupBy(b => b.Def.Name).OrderBy(g => g.Key))
    Console.WriteLine($"  {g.Key}: {g.Count()}");
Console.WriteLine($"Ledger entries: {town.Ledger.Entries.Count} " +
                  $"({town.Ledger.Entries.Count(e => e.Importance == Importance.Major)} major, " +
                  $"{town.Ledger.Entries.Count(e => e.Importance == Importance.Notable)} notable)");

Console.WriteLine();
Console.WriteLine("=== People worth knowing ===");
var notable = town.Residents
    .OrderByDescending(a => town.Ledger.About(a.Id).Count(e => e.Importance >= Importance.Notable && e.Id > 1))
    .ThenBy(a => a.Id)
    .Take(people);
foreach (var a in notable)
{
    string home = a.HomeId is int h ? town.Building(h).Name : "nowhere (sleeps rough)";
    string job = a.JobId is int j ? town.Building(j).Name : "no work";
    string spouse = a.SpouseId is int s ? $", married to {town.Agent(s).GivenName}" : "";
    var friends = a.Opinions.Where(kv => kv.Value >= 40).Select(kv => town.Agent(kv.Key).GivenName).ToList();
    var enemies = a.Opinions.Where(kv => kv.Value <= -30).Select(kv => town.Agent(kv.Key).GivenName).ToList();
    Console.WriteLine($"{a.Describe()} ({a.TraitList()}){spouse}");
    Console.WriteLine($"  {a.Abilities}; {a.Gold:0} gold; lives at {home}; works at {job}.");
    if (friends.Count > 0) Console.WriteLine($"  Friends: {string.Join(", ", friends)}");
    if (enemies.Count > 0) Console.WriteLine($"  Grudges: {string.Join(", ", enemies)}");
    foreach (var e in town.Ledger.About(a.Id).Where(e => e.Id > 1)) Console.WriteLine($"    {e}");
    Console.WriteLine();
}
return 0;
