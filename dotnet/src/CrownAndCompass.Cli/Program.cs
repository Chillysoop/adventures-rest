// Headless runner: simulate a town and print its Ledger, so we can judge whether the city feels alive.
// Usage: dotnet run --project dotnet/src/CrownAndCompass.Cli -- [--seed N] [--days N] [--all] [--stats] [--people N] [--chronicles]
// Prints exactly what `npm run sim` prints for the same arguments.

using System.Globalization;
using System.Text;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

int seed = 1;
int days = 60;
bool all = false;
bool stats = false;
int people = 3;
bool chronicles = false;

static int Number(string[] args, ref int i) => i + 1 < args.Length ? int.Parse(args[++i], CultureInfo.InvariantCulture) : 0;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seed":
            seed = Number(args, ref i);
            break;
        case "--days":
            days = Number(args, ref i);
            break;
        case "--all":
            all = true;
            break;
        case "--stats":
            stats = true;
            break;
        case "--chronicles":
            chronicles = true;
            break;
        case "--people":
            people = Number(args, ref i);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument {args[i]}");
            return 1;
    }
}

var sim = new Simulation(new SimulationOptions { Seed = seed });
var town = sim.Town;
var minImportance = all ? Importance.Routine : Importance.Notable;
var output = new List<string>();
void Print(string s = "") => output.Add(s);

foreach (var e in town.Ledger.Entries) if (e.Importance >= minImportance) Print(e.Format());
town.Ledger.OnWrite(e =>
{
    if (e.Importance >= minImportance) Print(e.Format());
});

for (int d = 0; d < days; d++)
{
    sim.RunDays(1);
    var s = sim.History[^1];
    if (stats || s.Day % 10 == 0)
    {
        Print(
            $"  -- end of day {s.Day}: pop {s.Population}, homeless {s.Homeless}, idle {s.Unemployed}, " +
            $"buildings {s.Buildings} (+{s.Sites} rising), treasury {Js.ToFixed(s.Treasury, 0)}, food {Js.ToFixed(s.FoodPrice, 2)}g, " +
            $"mood {Js.ToFixed(s.AverageMood, 0)}, avg purse {Js.ToFixed(s.AverageGold, 0)}g");
    }
}

Print();
Print($"=== {town.Name} after {days} days (seed {seed}) ===");
var final = sim.Snapshot();
Print($"Population {final.Population} ({town.Agents.Count(a => a.Departed)} have left), treasury {Js.ToFixed(final.Treasury, 0)} gold.");
Print(
    town.Outcome == Outcome.Victory
        ? "The Last Stand is won: the warlord is dead."
        : town.Outcome == Outcome.Defeat
            ? "The town has fallen to the warband."
            : $"The warband stands at {Js.Str(Js.Round(town.Enemy.Strength))} ({town.Enemy.Stage.Name()}); {town.Enemy.Raids} raids, {town.Enemy.Sieges} sieges so far.");
var counts = new OrderedMap<string, int>();
foreach (var b in town.Buildings) if (b.Complete) counts[b.Def.Name] = counts.Get(b.Def.Name, 0) + 1;
foreach (var (name, n) in counts.OrderBy(kv => kv.Key, StringComparer.Ordinal)) Print($"  {name}: {n}");
var entries = town.Ledger.Entries;
Print(
    $"Ledger entries: {entries.Count} ({entries.Count(e => e.Importance == Importance.Major)} major, " +
    $"{entries.Count(e => e.Importance == Importance.Notable)} notable)");

Print();
Print("=== People worth knowing ===");
int NotableCount(int id) => town.Ledger.About(id).Count(e => e.Importance >= Importance.Notable && e.Id > 1);
var notable = town.Residents.OrderByDescending(a => NotableCount(a.Id)).ThenBy(a => a.Id).Take(people).ToList();
foreach (var a in notable)
{
    string home = a.HomeId != null ? town.Building(a.HomeId.Value).Name : "nowhere (sleeps rough)";
    string job = a.JobId != null ? town.Building(a.JobId.Value).Name : "no work";
    string spouse = a.SpouseId != null ? $", married to {town.Agent(a.SpouseId.Value).GivenName}" : "";
    var friends = a.Opinions.Where(kv => kv.Value >= 40).Select(kv => town.Agent(kv.Key).GivenName).ToList();
    var enemies = a.Opinions.Where(kv => kv.Value <= -30).Select(kv => town.Agent(kv.Key).GivenName).ToList();
    Print($"{a.Describe()} ({a.TraitList()}){spouse}");
    Print($"  {a.Abilities}; {Js.ToFixed(a.Gold, 0)} gold; lives at {home}; works at {job}.");
    if (friends.Count > 0) Print($"  Friends: {string.Join(", ", friends)}");
    if (enemies.Count > 0) Print($"  Grudges: {string.Join(", ", enemies)}");
    foreach (var e in town.Ledger.About(a.Id).Where(e => e.Id > 1)) Print($"    {e.Format()}");
    Print();
}

if (chronicles)
{
    Print("=== Chronicles ===");
    foreach (var c in town.Chronicles)
    {
        Print(c.Format());
        Print();
    }
}

using var stdout = Console.OpenStandardOutput();
var bytes = new UTF8Encoding(false).GetBytes(string.Join("\n", output) + "\n");
stdout.Write(bytes);
return 0;
