// Headless runner: simulate a town and print its Ledger, so we can judge whether the city feels alive.
// Usage: dotnet run --project dotnet/src/CrownAndCompass.Cli -- [--seed N] [--days N] [--all] [--stats] [--people N] [--chronicles] [--world]
//        [--load file.save] [--save file.save]   (play on from a save, for --days more; write one at the end)
//        [--hand-council]   (the council leaves taxes to a mayor who never touches them, as a hands-off player would)
//        [--legends file.json]   (foes and heroes of earlier runs may come back; this run's are added to the file)
//        [--summary]   (print only one JSON line on how the campaign stands, for scripts/gate.ts)
//        [--storyteller steady|rising|chaotic]   (who tells the campaign: the difficulty; rising is the default)
//        [--founding id]   (where the town is founded: a founding.json id; the river ford is the default)
//        [--endless]   (a win closes an era; only a fall ends the campaign)   [--boons id:rank,...]   (found the town with boons)
//        [--roads]   (print every road to victory's terms each month)   [--claim]   (claim the first road whose terms hold, as a player might)   [--roads-off a,b]   (close these roads; "all" for every one but slay)
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
string? legends = null;
bool summary = false;
bool handTaxes = false;
string storyteller = "rising";
string founding = "";
bool hardcore = false;
bool roads = false;
bool claim = false;
bool endless = false;
var boons = new List<string>();
var roadsOff = new List<string>();

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
        case "--legends":
            legends = i + 1 < args.Length ? args[++i] : null;
            break;
        case "--hand-council":
            handTaxes = true;
            break;
        case "--summary":
            summary = true;
            break;
        case "--founding":
            founding = i + 1 < args.Length ? args[++i] : founding;
            if (Founding.Kind(Content.Default(), founding) == null)
            {
                Console.Error.WriteLine($"Unknown founding {founding}: one of {string.Join(", ", Content.Default().Founding.Kinds.Select(k => k.Id))}");
                return 1;
            }
            break;
        case "--hardcore":
            hardcore = true;
            break;
        case "--storyteller":
            storyteller = i + 1 < args.Length ? args[++i] : storyteller;
            if (Storyteller.Def(Content.Default(), storyteller) == null)
            {
                Console.Error.WriteLine($"Unknown storyteller {storyteller}: one of {string.Join(", ", Content.Default().Storytellers.Storytellers.Select(s => s.Id))}");
                return 1;
            }
            break;
        case "--roads":
            roads = true;
            break;
        case "--endless":
            endless = true;
            break;
        case "--boons":
            boons.AddRange((i + 1 < args.Length ? args[++i] : "").Split(',', StringSplitOptions.RemoveEmptyEntries));
            break;
        case "--claim":
            claim = true;
            break;
        case "--roads-off":
            string off = i + 1 < args.Length ? args[++i] : "";
            roadsOff.AddRange(off == "all" ? Content.Default().Victory.Roads.Select(r => r.Id) : off.Split(','));
            break;
        case "--people":
            people = Number(args, ref i);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument {args[i]}");
            return 1;
    }
}

var past = legends != null && File.Exists(legends) ? Legends.Read(File.ReadAllText(legends)) : [];
var sim = load != null ? SaveGame.ReadFile(load, out _) : new Simulation(new SimulationOptions { Seed = seed, Legends = past, AutoCouncil = !handTaxes, Storyteller = storyteller, RoadsOff = roadsOff, Endless = endless, Boons = boons, Founding = founding, Hardcore = hardcore });
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
    if (claim && Victory.Claimed(town) == null && Victory.Roads(town).FirstOrDefault(r => Victory.ClaimBlocked(town, r.Id) == null) is { } open) sim.ClaimRoad(open.Id);
    var s = sim.History[^1];
    if (stats || s.Day % 10 == 0)
    {
        Print(
            $"  -- end of day {s.Day}: pop {s.Population}, homeless {s.Homeless}, idle {s.Unemployed}, " +
            $"buildings {s.Buildings} (+{s.Sites} rising), treasury {Js.ToFixed(s.Treasury, 0)}, food {Js.ToFixed(s.FoodPrice, 2)}g, " +
            $"mood {Js.ToFixed(s.AverageMood, 0)}, avg purse {Js.ToFixed(s.AverageGold, 0)}g, " +
            $"tax {Js.ToFixed(town.TaxRate * 100, 0)}%, income {Js.ToFixed(Taxes.Income(town), 0)}/services {Js.ToFixed(Taxes.Services(town), 0)}, unrest {Js.ToFixed(town.Taxes.Unrest, 0)}, champions {Saga.ChampionLevel(town)}, defence {Js.ToFixed(Threat.Defense(town), 0)}");
    }
    if (roads && s.Day % 30 == 0)
        Print($"  -- roads, day {s.Day}{(Victory.Ready(town) ? " (big bad crowned)" : "")}: " + string.Join("; ", town.Content.Victory.Roads.Select(r =>
            $"{r.Id} {Js.ToFixed(Victory.Progress(town, r) * 100, 0)}% [{string.Join(", ", Victory.Terms(town, r).Select(t => $"{t.Label} {Js.Str(t.Have)}/{Js.Str(t.Need)}"))}]")) + (Victory.Claimed(town) is { } claimed ? $"; claimed {claimed.Id}, {Victory.DaysLeft(town)} days left" : ""));
}

Print();
Print($"=== {town.Name} after {days} days (seed {seed}) ===");
var final = sim.Snapshot();
Print($"Population {final.Population} ({town.Agents.Count(a => a.Departed)} have left), treasury {Js.ToFixed(final.Treasury, 0)} gold.");
Print(
    town.Outcome != null
        ? $"{Saga.EndTitle(town)}: {town.Saga.EndText}" + (Victory.Won(town) is { } road ? $" (won by {road.Name})" : "")
        : $"The warband stands at {Js.Str(Js.Round(town.Enemy.Strength))} ({town.Enemy.Stage.Name()}); {town.Enemy.Raids} raids, {town.Enemy.Sieges} sieges so far.");
var teller = Storyteller.Of(town);
Print($"Told by {teller.Name} ({teller.Title}): {town.Enemy.Raids} raids and {town.Enemy.Sieges} sieges by the warband; {town.Moods.Inspirations} inspirations and {town.Moods.Breaks} breaks among the townsfolk.");
if (Saga.Summary(town) != "") Print(Saga.Summary(town));
if (Endless.On(town)) Print($"Endless: era {Endless.EraNumber(town)}; " + string.Join("; ", town.Saga.Endless.Eras.Select(e => $"era {e.Number} ended day {e.Day} by {e.Road} against {e.Foe}")));
Print($"A {City.Tier(town).Name.ToLowerInvariant()} of about {City.Souls(town)} souls; wards: {string.Join(", ", town.City.OpenWards.Select(w => City.Ward(town, w).Name))}.");
Print("Treasury books, all time: " + string.Join(", ", town.Accounts.OrderBy(kv => kv.Value).Select(kv => $"{kv.Key} {Js.ToFixed(kv.Value, 0)}")));
Print("Vacant zoned lots: " + string.Join(", ", Planning.Paintable.Select(z => $"{z} {Planning.VacantZoned(town, z)}")) + $"; unpainted open ground {Land.OpenGround(town) / (Land.Res * Land.Res)} tiles");
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
if (legends != null) File.WriteAllText(legends, Legends.Write(Legends.Merge(past, Legends.Record(town, seed))));

if (summary)
{
    // One line for the merge gate: how the campaign ended (or stands), and enough of the state to tell two runs apart.
    output.Clear();
    string outcome = town.Outcome == Outcome.Victory ? "won" : town.Outcome == Outcome.Defeat ? "lost" : "unfinished";
    Print(System.Text.Json.JsonSerializer.Serialize(new
    {
        seed,
        outcome,
        day = town.Day,
        population = final.Population,
        treasury = Js.Round(final.Treasury),
        ledger = entries.Count,
        chronicles = town.Chronicles.Count,
    }));
}

using var stdout = Console.OpenStandardOutput();
var bytes = new UTF8Encoding(false).GetBytes(string.Join("\n", output) + "\n");
stdout.Write(bytes);
return 0;
