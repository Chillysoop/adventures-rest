// Headless runner: simulate a town and print its Ledger, so we can judge whether the city feels alive.
// Usage: dotnet run --project dotnet/src/CrownAndCompass.Cli -- [--seed N] [--days N] [--all] [--stats] [--people N] [--chronicles] [--world]
//        [--load file.save] [--save file.save]   (play on from a save, for --days more; write one at the end)
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
bool world = false;
string? load = null;
string? save = null;

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
        case "--world":
            world = true;
            break;
        case "--load":
            load = i + 1 < args.Length ? args[++i] : null;
            break;
        case "--save":
            save = i + 1 < args.Length ? args[++i] : null;
            break;
        case "--people":
            people = Number(args, ref i);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument {args[i]}");
            return 1;
    }
}

var sim = load != null ? SaveGame.ReadFile(load, out _) : new Simulation(new SimulationOptions { Seed = seed });
var town = sim.Town;
seed = town.Seed;
var minImportance = all ? Importance.Routine : Importance.Notable;
var output = new List<string>();
void Print(string s = "") => output.Add(s);

if (load == null) foreach (var e in town.Ledger.Entries) if (e.Importance >= minImportance) Print(e.Format());
town.Ledger.OnWrite(e =>
{
    if (e.Importance >= minImportance) Print(e.Format());
});

for (int d = 0; d < days && town.Outcome == null; d++)
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
    town.Outcome != null
        ? $"{Saga.EndTitle(town)}: {town.Saga.EndText}"
        : $"The warband stands at {Js.Str(Js.Round(town.Enemy.Strength))} ({town.Enemy.Stage.Name()}); {town.Enemy.Raids} raids, {town.Enemy.Sieges} sieges so far.");
if (Saga.Summary(town) != "") Print(Saga.Summary(town));
Print($"A {City.Tier(town).Name.ToLowerInvariant()} of about {City.Souls(town)} souls; wards laid out: {string.Join(", ", town.City.OpenWards.Select(w => City.Ward(town, w).Name))}.");
Print("Treasury books, all time: " + string.Join(", ", town.Accounts.OrderBy(kv => kv.Value).Select(kv => $"{kv.Key} {Js.ToFixed(kv.Value, 0)}")));
Print("Vacant zoned lots: " + string.Join(", ", Planning.Paintable.Select(z => $"{z} {Planning.VacantZoned(town, z)}")) + $"; unzoned open {town.Lots.Count(l => l.Zone == Zone.None && City.IsOpen(town, l))}");
Print("Demand: " + string.Join(", ", town.Content.Buildings.Where(d => d.Buildable && d.IsBusiness).Select(d => $"{d.Id} {Js.ToFixed(Property.Demand(town, d.Id), 2)}")));
Print("Houses: " + string.Join(", ", town.Buildings.Where(b => b.Complete && b.Def.Housing > 0).GroupBy(b => b.Def.Id).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()} (content {Js.ToFixed(g.Average(b => b.Contentment), 2)})")));
Print("Coverage: " + string.Join(", ", town.City.Coverage.Select(kv => $"{kv.Key} {Js.ToFixed(kv.Value, 2)}")) + "; next charter needs: " + string.Join("; ", City.Missing(town)));
Print("Ambitions: " + string.Join(", ", town.Workers.GroupBy(a => a.Ambition).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()} (avg {Js.ToFixed(g.Average(a => a.Gold), 0)}g, max {Js.ToFixed(g.Max(a => a.Gold), 0)}g)")) + $"; idle {town.Workers.Count(a => a.JobId == null)} (avg {Js.ToFixed(town.Workers.Where(a => a.JobId == null).Select(a => a.Gold).DefaultIfEmpty(0).Average(), 0)}g); open jobs {town.Buildings.Sum(b => b.OpenJobs)}");
Print($"Road crews: {Economy.Hall(town)?.Workers.Count}/{Economy.Hall(town)?.Crew}; upkeep bill {Js.ToFixed(Economy.UpkeepBill(town), 0)}/day");
Print("Sites: " + string.Join(", ", town.Buildings.Where(b => b.IsSite).Select(b => $"{b.Name} [{b.Def.Id}] {Js.ToFixed(b.BuildProgress, 0)}/{b.Def.BuildHours} budget {Js.ToFixed(b.LaborBudget, 0)} workers {b.Workers.Count} owner {town.OwnerName(b.OwnerId)}")));
Print("Market: " + string.Join(", ", Goods.All.Select(g => $"{g} {Js.ToFixed(town.Market.Stock(g), 0)}/{Js.ToFixed(town.Market.Target(g), 0)} @{Js.ToFixed(town.Market.Price(g), 2)}")));
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

if (world)
{
    Print("=== The wilds ===");
    foreach (var r in town.Regions)
    {
        string seen = r.Visited ? "visited" : r.Known ? "rumored" : "unknown";
        Print($"{r.Name} ({r.Theme.Name}, ring {r.Ring + 1}, {r.Days} days out at {Js.Str(r.Bearing)} degrees, levels {r.Levels[0]}-{r.Levels[^1]}, {seen})");
        if (r.SettlementId is int sid)
        {
            var st = town.Settlements[sid];
            Print($"  {st.Name}, {(st.Kin.Kind[0] is 'a' or 'e' or 'i' or 'o' or 'u' ? "an" : "a")} {st.Kin.Kind} of {st.Kin.People}: standing {Js.Str(Js.Round(st.Standing))}{(st.Allied ? ", allied" : "")}");
        }
        foreach (var id in r.SiteIds)
        {
            var site = Quests.Site(town, id)!;
            var boss = town.Content.Monster(site.Def.Boss.Monster);
            Print($"  {site.Def.Name}: level {site.Def.Tier}, boss {boss.Name}{(site.Cleared ? ", cleared" : "")}");
        }
    }
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

if (save != null) SaveGame.WriteFile(save, sim);

using var stdout = Console.OpenStandardOutput();
var bytes = new UTF8Encoding(false).GetBytes(string.Join("\n", output) + "\n");
stdout.Write(bytes);
return 0;
