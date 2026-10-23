namespace CrownAndCompass.Sim.Systems;

/// <summary>Numbers and content for the city's institutions (content/game/institutions.json).</summary>
public sealed record InstitutionRules
{
    /// <summary>How generously the council funds an institution: unfunded, modest, generous. The second is where a new one starts.</summary>
    public List<PatronageDef> Patronage { get; init; } = [];

    /// <summary>The keeper's regard for the town when the institution opens.</summary>
    public double StartRegard { get; init; } = 10;

    /// <summary>How far regard moves each day toward what the funding earns.</summary>
    public double RegardDrift { get; init; } = 0.5;

    /// <summary>Days before the mayor can change an institution's choice again, and the regard it costs when they do.</summary>
    public int ChangeDays { get; init; } = 30;
    public double ChangeRegard { get; init; } = 15;

    /// <summary>Regard lost when the mayor lets one of the keeper's requests run its course unanswered.</summary>
    public double IgnoredRegard { get; init; } = 6;

    /// <summary>Funding grows with the town: patronage gold is multiplied by 1 + population / this.</summary>
    public double FundingPerPeople { get; init; } = 200;

    /// <summary>Gold the stand-in mayor keeps back before raising an institution, on top of twice its cost.</summary>
    public double StewardReserve { get; init; } = 600;

    public List<InstitutionDef> Institutions { get; init; } = [];

    /// <summary>Lasting little features a story can leave at an institution (a mimic bouncer, a living bridge).</summary>
    public List<FixtureDef> Fixtures { get; init; } = [];

    /// <summary>What the institutions ask of the mayor: story events, added to stories.json's at load.</summary>
    public List<StoryDef> Requests { get; init; } = [];

    /// <summary>What the town remembers of its dealings with them, added to stories.json's precedents at load.</summary>
    public List<PrecedentDef> Precedents { get; init; } = [];

    public InstitutionDef? Def(string id) => Institutions.FirstOrDefault(i => i.Id == id);
    public FixtureDef? Fixture(string id) => Fixtures.FirstOrDefault(f => f.Id == id);
}

/// <summary>A level of funding: the daily gold it costs, how much of the service it buys, and the regard it earns.</summary>
public sealed record PatronageDef
{
    public string Name { get; init; } = "";
    public double Gold { get; init; }
    public double Scale { get; init; } = 1;

    /// <summary>The regard the keeper settles at under this funding.</summary>
    public double Regard { get; init; }
}

/// <summary>
/// What an institution does for the town each day, at modest funding and a keeper of middling regard. Shares
/// the decree's fields (mood, settlers, build speed, gold, tension, yields) and adds its own.
/// </summary>
public sealed record ServiceDef : DecreeDef
{
    /// <summary>Added each day to the favour of the institution's god (its choice's deity, else the temple's).</summary>
    public double Favour { get; init; }

    /// <summary>Added to dread each day.</summary>
    public double Dread { get; init; }

    /// <summary>Added to the town's defence.</summary>
    public double Defence { get; init; }

    /// <summary>More heroes the town can keep.</summary>
    public double Heroes { get; init; }

    /// <summary>Share of a hero's hit points healed by each night's rest in town.</summary>
    public double Heal { get; init; }

    /// <summary>Share taken off the chance a caravan is waylaid.</summary>
    public double Escort { get; init; }

    /// <summary>More trade routes the charter allows.</summary>
    public double Routes { get; init; }

    /// <summary>Airships that carry a company over rivers, lakes and peaks (<see cref="Passage.Board"/>).</summary>
    public double Airship { get; init; }

    /// <summary>Share taken off the stir the undercity gathers each day.</summary>
    public double Hush { get; init; }

    /// <summary>Share fewer townsfolk break under a low mood: the calm a monastery teaches (<see cref="Moods"/>).</summary>
    public double Calm { get; init; }

    /// <summary>More heroes of these classes come to the town (weight added to the class's own when a hero is drawn).</summary>
    public IReadOnlyDictionary<string, double> Calling { get; init; } = new Dictionary<string, double>();

    /// <summary>Hours sooner a sky watch of riders spots raiders on their way and brings the news (<see cref="Institutions.SkyWatch"/>).</summary>
    public double SkyWatch { get; init; }

    /// <summary>A faction it works with or against, and what it does each day to that faction's standing and heat.</summary>
    public string Faction { get; init; } = "";
    public double Standing { get; init; }
    public double FactionHeat { get; init; }
}

/// <summary>
/// A city institution: a building, the character who runs it, a service it gives the town, a choice the
/// mayor makes about it, and the requests it brings (its stories in <see cref="InstitutionRules.Requests"/>).
/// </summary>
public sealed record InstitutionDef
{
    public string Id { get; init; } = "";

    /// <summary>"the Temple District".</summary>
    public string Name { get; init; } = "";

    /// <summary>The building it lives in.</summary>
    public string Building { get; init; } = "";

    /// <summary>It grows up around a building of that kind the town already has (the temple, the oldest tavern), rather than being raised for it.</summary>
    public bool Adopt { get; init; }

    /// <summary>The town tier at which an adopted institution forms.</summary>
    public string Tier { get; init; } = "";

    /// <summary>What its keeper is called: "High Priestess", "Hallmaster".</summary>
    public string Title { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>The icon in godot/assets/icons (scripts/icons.ts) its card and slot show.</summary>
    public string Icon { get; init; } = "";

    /// <summary>
    /// Any one of these reveals it before the charter does (<see cref="Research.Check"/>'s tokens: craft:priest:2,
    /// hero:paladin, heroes:n, routes:n, delves:n, building:id, folk:elf:3...). Until then the window shows a blank slot.
    /// </summary>
    public List<string> Sparks { get; init; } = [];
    public ServiceDef Service { get; init; } = new();

    /// <summary>The choice the mayor makes about it: which god's temple gets the land, the tavern's style, the order's oath.</summary>
    public ChoiceDef? Choice { get; init; }

    /// <summary>Who might be running it: one is picked when it opens.</summary>
    public List<KeeperDef> Keepers { get; init; } = [];
}

public sealed record ChoiceDef
{
    /// <summary>"Patron of the district".</summary>
    public string Label { get; init; } = "";
    public string Text { get; init; } = "";
    public List<ChoiceOption> Options { get; init; } = [];
}

public sealed record ChoiceOption
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>The god this option honours, whose favour the service feeds.</summary>
    public string Deity { get; init; } = "";
    public ServiceDef Service { get; init; } = new();
}

/// <summary>The character who runs an institution.</summary>
public sealed record KeeperDef
{
    public string Name { get; init; } = "";

    /// <summary>A line about who they are, for the institution's card.</summary>
    public string Text { get; init; } = "";
}

/// <summary>A lasting feature a story leaves at an institution, with its own small service.</summary>
public sealed record FixtureDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public string Institution { get; init; } = "";
    public ServiceDef Service { get; init; } = new();
}

/// <summary>An institution the town has, and how it stands with its keeper.</summary>
public sealed class InstitutionState
{
    public string Def { get; set; } = "";
    public int? BuildingId { get; set; }
    public int Founded { get; set; }

    /// <summary>The keeper running it, by their index among the def's keepers.</summary>
    public int Keeper { get; set; }

    /// <summary>The funding level, by index into the patronage levels.</summary>
    public int Patronage { get; set; } = 1;

    /// <summary>The mayor's choice for it (an option id), or "" until made.</summary>
    public string Choice { get; set; } = "";
    public int ChoiceDay { get; set; } = -9999;

    /// <summary>What the keeper thinks of the town, -100 to 100.</summary>
    public double Regard { get; set; }

    /// <summary>Lasting features stories have left here.</summary>
    public List<string> Fixtures { get; } = [];

    /// <summary>Its building stands empty or closed: no service until another opens.</summary>
    public bool Shut { get; set; }

    /// <summary>Requests the mayor answered, and those left to run their course.</summary>
    public int Answered { get; set; }
    public int Ignored { get; set; }
}

/// <summary>
/// The city's institutions (GDD rule C1: a city grows by its institutions). A temple district, an
/// adventurers' hall, a grand bazaar, a druid grove, a famous tavern: each is a building with a named
/// keeper who runs it, a service it gives the town every day (mood, settlers, yields, defence, healing,
/// safer roads, more heroes, a god's favour), a choice the mayor makes about it (whose temple gets the
/// land, the tavern's style), and requests it brings now and then as story events. The council funds
/// each one: generous patronage buys more of the service and the keeper's goodwill, an unfunded one
/// gives little and sours. The keeper's regard rises with answered requests and funding, and scales
/// the service. Stories can leave lasting fixtures at an institution.
/// </summary>
public static class Institutions
{
    public static InstitutionRules Rules(Town town) => town.Content.Institutions;

    public static InstitutionState? Find(Town town, string id) => town.Institutions.FirstOrDefault(i => i.Def == id);

    public static InstitutionDef DefOf(Town town, InstitutionState s) => Rules(town).Def(s.Def) ?? throw new KeyNotFoundException($"Unknown institution {s.Def}");

    /// <summary>The institution living in a building, if any.</summary>
    public static InstitutionState? At(Town town, Building b) => town.Institutions.FirstOrDefault(i => i.BuildingId == b.Id);

    /// <summary>The institution a building kind houses, if any.</summary>
    public static InstitutionDef? For(Town town, string buildingId) => Rules(town).Institutions.FirstOrDefault(i => i.Building == buildingId);

    public static KeeperDef Keeper(Town town, InstitutionState s)
    {
        var keepers = DefOf(town, s).Keepers;
        return keepers.Count == 0 ? new KeeperDef { Name = "the keeper" } : keepers[Math.Clamp(s.Keeper, 0, keepers.Count - 1)];
    }

    /// <summary>"High Priestess Ilse Varrow".</summary>
    public static string KeeperName(Town town, InstitutionState s)
    {
        var d = DefOf(town, s);
        string name = Keeper(town, s).Name;
        return d.Title == "" ? name : $"{d.Title} {name}";
    }

    /// <summary>The keeper's given name, for a sentence: "Ilse".</summary>
    public static string KeeperGiven(Town town, InstitutionState s)
    {
        string n = Keeper(town, s).Name;
        int sp = n.IndexOf(' ');
        return sp > 0 ? n[..sp] : n;
    }

    public static ChoiceOption? Chosen(Town town, InstitutionState s) =>
        s.Choice == "" ? null : DefOf(town, s).Choice?.Options.FirstOrDefault(o => o.Id == s.Choice);

    public static PatronageDef Level(Town town, InstitutionState s)
    {
        var p = Rules(town).Patronage;
        return p.Count == 0 ? new PatronageDef() : p[Math.Clamp(s.Patronage, 0, p.Count - 1)];
    }

    /// <summary>What a level of funding costs a day in a town this size.</summary>
    public static double Cost(Town town, PatronageDef p) => Js.Round(p.Gold * (1 + town.Population / Rules(town).FundingPerPeople) * 10) / 10;

    /// <summary>How much of its service an institution gives: its funding, times its keeper's regard (0.75 at -100 to 1.25 at 100); none while shut.</summary>
    public static double Strength(Town town, InstitutionState s) => s.Shut ? 0 : Level(town, s).Scale * (1 + s.Regard / 400);

    /// <summary>Every part of an institution's service: its own, its choice's, and its fixtures'.</summary>
    public static IEnumerable<ServiceDef> Parts(Town town, InstitutionState s)
    {
        yield return DefOf(town, s).Service;
        if (Chosen(town, s) is { } o) yield return o.Service;
        foreach (var f in s.Fixtures)
            if (Rules(town).Fixture(f) is { } fd) yield return fd.Service;
    }

    /// <summary>No institution open and no relic in the hall: every service sum is nothing.</summary>
    private static bool Idle(Town town) => town.Institutions.Count == 0 && town.Relics.Count == 0;

    /// <summary>Sum one number of every open institution's service, each scaled by its strength, and the hall's relics' at full.</summary>
    private static double Sum(Town town, Func<ServiceDef, double> field)
    {
        double total = 0;
        foreach (var h in Regalia.HallServices(town)) total += field(h);
        foreach (var s in town.Institutions)
        {
            if (s.Shut) continue;
            double part = 0;
            foreach (var p in Parts(town, s)) part += field(p);
            if (part != 0) total += part * Strength(town, s);
        }
        return total;
    }

    public static double Mood(Town town) => Idle(town) ? 0 : Sum(town, d => d.Mood);
    public static double Arrivals(Town town) => Idle(town) ? 0 : Sum(town, d => d.Arrivals);
    public static double Defence(Town town) => Idle(town) ? 0 : Sum(town, d => d.Defence);
    public static int Heroes(Town town) => Idle(town) ? 0 : (int)Math.Floor(Sum(town, d => d.Heroes) + 0.05);
    /// <summary>Airships the town can send a company off in: one as soon as the dock flies any at all.</summary>
    public static int Airships(Town town)
    {
        // Once the dock has its fleet, its ships at the mast are what can fly (<see cref="Sky"/>).
        if (town.SkyFleet.Granted) return Sky.Ships(town).Count(s => Sky.Moored(town, s) && s.Lift >= 1);
        double n = Idle(town) ? 0 : Sum(town, d => d.Airship);
        return n < 0.05 ? 0 : Math.Max(1, (int)Math.Floor(n + 0.05));
    }

    /// <summary>Share off the chance a low mood breaks someone, at most a half.</summary>
    public static double Calm(Town town) => Idle(town) ? 0 : Math.Clamp(Sum(town, d => d.Calm), 0, 0.5);

    /// <summary>Weight institutions add to drawing a hero of this class.</summary>
    public static int Calling(Town town, string cls) => Idle(town) ? 0 : (int)Math.Round(Sum(town, d => d.Calling.GetValueOrDefault(cls)));

    /// <summary>Hours sooner than the fields the town's riders in the sky see a raid coming: at most two days.</summary>
    public static int SkyWatch(Town town) => (int)Math.Clamp(Math.Round((Idle(town) ? 0 : Sum(town, d => d.SkyWatch)) + Ruins.SkyWatch(town) + Sky.PatrolHours(town)), 0, 48);

    public static int Routes(Town town) => Idle(town) ? 0 : (int)Math.Floor(Sum(town, d => d.Routes) + 0.05);

    /// <summary>Share off the chance a caravan is waylaid, at most a half.</summary>
    public static double Escort(Town town) => Idle(town) ? 0 : Math.Min(0.5, Sum(town, d => d.Escort));

    /// <summary>Extra hit points a night's rest in town heals.</summary>
    public static int Rest(Town town, int maxHp) => Idle(town) ? 0 : (int)Math.Ceiling(maxHp * Sum(town, d => d.Heal) - 1e-9);

    /// <summary>Share off the stir the undercity gathers, at most three fifths.</summary>
    public static double Hush(Town town) => Idle(town) ? 0 : Math.Clamp(Sum(town, d => d.Hush), -0.5, 0.6);

    public static double Build(Town town) => Idle(town) ? 1 : 1 + Sum(town, d => d.Build);

    /// <summary>What each good's makers produce, multiplied: a yield of 1.1 at full strength is a tenth more.</summary>
    public static double Yield(Town town, Good good)
    {
        if (Idle(town)) return 1;
        string key = good.ToString();
        double f = 1;
        foreach (var h in Regalia.HallServices(town))
            if (h.Yields.TryGetValue(key, out double hy)) f *= hy;
        foreach (var s in town.Institutions)
        {
            if (s.Shut) continue;
            double k = Strength(town, s);
            foreach (var p in Parts(town, s))
                if (p.Yields.TryGetValue(key, out double y)) f *= 1 + (y - 1) * k;
        }
        return f;
    }

    // ---------- Discovery ----------

    /// <summary>Whether the town knows of this institution yet: a spark held, the charter allows its building, or it opened.</summary>
    public static bool Seen(Town town, string id) => town.InstitutionsSeen.ContainsKey(id);

    /// <summary>A build bar item for an institution's own building the town hasn't discovered yet, which the bar keeps hidden.</summary>
    public static bool Hidden(Town town, string buildingId) =>
        Rules(town).Institutions.Any(d => !d.Adopt && d.Building == buildingId && !Seen(town, d.Id));

    /// <summary>The charter that lets it rise: its building's tier, or the tier an adopted one forms at.</summary>
    public static string Charter(Town town, InstitutionDef d) =>
        town.Content.Buildings.FirstOrDefault(b => b.Id == d.Building) is { Tier: not "" } bd && !d.Adopt ? bd.Tier : d.Tier;

    /// <summary>Learn of the institutions the town now has the makings of, and what showed the way.</summary>
    private static void Reveal(Town town)
    {
        foreach (var d in Rules(town).Institutions)
        {
            if (Seen(town, d.Id)) continue;
            string? why = null;
            foreach (var spark in d.Sparks)
            {
                var (holds, w) = Research.Check(town, spark);
                if (holds) { why = w; break; }
            }
            string tier = Charter(town, d);
            // A charter brings talk only of what the town could build: no harbour inland.
            if (why == null && tier != "" && City.Reached(town, tier) && !(town.Content.Buildings.FirstOrDefault(b => b.Id == d.Building) is { Coast: true } && !Fleet.Coastal(town))) why = $"the charter of a {City.TierName(town, tier).ToLowerInvariant()}";
            if (why == null && Find(town, d.Id) != null) why = "its opening";
            if (why == null) continue;
            town.InstitutionsSeen[d.Id] = why;
            if (Find(town, d.Id) == null)
                town.Log(LedgerCategory.Town, Importance.Notable, $"Talk of {d.Name}, thanks to {why}.");
        }
    }

    // ---------- Founding ----------

    /// <summary>Daily: institutions open in new buildings, shut when theirs close, charge their funding, move the town, and drift in regard.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        ServeHall(town);
        if (r.Institutions.Count == 0) return;
        Reveal(town);
        Found(town);
        foreach (var s in town.Institutions)
        {
            Keep(town, s);
            if (s.Shut) continue;
            var level = Level(town, s);
            double cost = Cost(town, level);
            if (cost > 0)
            {
                if (town.Treasury < cost && s.Patronage > 0)
                {
                    s.Patronage = 0;
                    town.Log(LedgerCategory.Town, Importance.Notable, $"The treasury can't pay for {DefOf(town, s).Name}. {KeeperName(town, s)} will make do with what the faithful and the patrons give.");
                    continue;
                }
                town.Book(TreasuryAccount.Institutions, -cost);
            }
            s.Regard += Math.Clamp(level.Regard - s.Regard, -r.RegardDrift, r.RegardDrift);
            Serve(town, s);
        }
        if (autoMayor) Steward(town);
    }

    /// <summary>Open an institution in each building that houses one and has none yet; an adopted one forms around the oldest building of its kind once the town is big enough.</summary>
    private static void Found(Town town)
    {
        foreach (var d in Rules(town).Institutions)
        {
            if (Find(town, d.Id) != null) continue;
            if (d.Adopt && d.Tier != "" && City.TierIndex(town, City.Tier(town).Id) < City.TierIndex(town, d.Tier)) continue;
            var b = town.Buildings.FirstOrDefault(b => b.Def.Id == d.Building && b.Complete && !b.Closed);
            if (b == null) continue;
            var s = new InstitutionState
            {
                Def = d.Id,
                BuildingId = b.Id,
                Founded = town.Day,
                Keeper = d.Keepers.Count == 0 ? 0 : town.Rng.Range(0, d.Keepers.Count),
                Regard = Rules(town).StartRegard,
            };
            town.Institutions.Add(s);
            if (!Seen(town, d.Id)) town.InstitutionsSeen[d.Id] = "its opening";
            var k = Keeper(town, s);
            town.Log(LedgerCategory.Town, Importance.Major,
                $"{Cap(d.Name)} opens its doors{(d.Adopt ? $" at {b.Name}" : "")}, with {KeeperName(town, s)} in charge. {k.Text}{(d.Choice != null ? $" The council must decide {Lower(d.Choice.Label)}." : "")}");
        }
    }

    /// <summary>An institution follows its building: it shuts while the building is closed, and moves to another of its kind if one stands.</summary>
    private static void Keep(Town town, InstitutionState s)
    {
        var d = DefOf(town, s);
        bool open = s.BuildingId is int id && town.Building(id) is { Complete: true, Closed: false } && town.Building(id).Def.Id == d.Building;
        if (open)
        {
            if (s.Shut)
            {
                s.Shut = false;
                town.Log(LedgerCategory.Town, Importance.Notable, $"{Cap(d.Name)} opens again, and {KeeperName(town, s)} is back at work.");
            }
            return;
        }
        var next = town.Buildings.FirstOrDefault(b => b.Def.Id == d.Building && b.Complete && !b.Closed);
        if (next != null)
        {
            s.BuildingId = next.Id;
            return;
        }
        if (!s.Shut)
        {
            s.Shut = true;
            town.Log(LedgerCategory.Town, Importance.Notable, $"{Cap(d.Name)} has no roof over it, and shuts until it has one again.");
        }
    }

    /// <summary>The parts of the service that act once a day: gold, tension, dread, a god's favour, a faction's standing.</summary>
    private static void Serve(Town town, InstitutionState s)
    {
        double k = Strength(town, s);
        var chosen = Chosen(town, s);
        foreach (var p in Parts(town, s))
        {
            if (p.Gold != 0) town.Book(TreasuryAccount.Institutions, p.Gold * k);
            if (p.Tension != 0) town.Tension = Math.Max(0, town.Tension + p.Tension * k);
            if (p.Dread != 0) town.Saga.Dread = Math.Clamp(town.Saga.Dread + p.Dread * k, 0, 100);
            if (p.Favour != 0)
            {
                var god = Faith.God(town, chosen?.Deity) ?? Faith.TempleGod(town) ?? Faith.CharterGod(town);
                town.Faiths.Favor[god.Id] = Math.Max(0, town.Faiths.Favor.Get(god.Id, 0) + p.Favour * k);
            }
            if (p.Faction != "" && Factions.Find(town, p.Faction) is { Active: true } f)
            {
                f.Standing = Math.Clamp(f.Standing + p.Standing * k, -100, 100);
                f.Heat = Math.Clamp(f.Heat + p.FactionHeat * k, 0, 100);
            }
        }
    }

    /// <summary>The hall's relics' daily parts: gold, tension and dread, at full.</summary>
    private static void ServeHall(Town town)
    {
        foreach (var p in Regalia.HallServices(town))
        {
            if (p.Gold != 0) town.Book(TreasuryAccount.Institutions, p.Gold);
            if (p.Tension != 0) town.Tension = Math.Max(0, town.Tension + p.Tension);
            if (p.Dread != 0) town.Saga.Dread = Math.Clamp(town.Saga.Dread + p.Dread, 0, 100);
        }
    }

    // ---------- The mayor's levers ----------

    /// <summary>Set how generously the council funds an institution (0 unfunded, 1 modest, 2 generous).</summary>
    public static bool Fund(Town town, string id, int level)
    {
        if (Find(town, id) is not { } s || level < 0 || level >= Rules(town).Patronage.Count || level == s.Patronage) return false;
        var p = Rules(town).Patronage[level];
        bool up = level > s.Patronage;
        s.Patronage = level;
        town.Log(LedgerCategory.Town, Importance.Routine,
            up ? $"The council raises its patronage of {DefOf(town, s).Name} to {p.Name.ToLowerInvariant()}: {Js.Str(Cost(town, p))} gold a day. {KeeperName(town, s)} is grateful."
               : $"The council cuts its patronage of {DefOf(town, s).Name} to {p.Name.ToLowerInvariant()}. {KeeperName(town, s)} takes it badly.");
        return true;
    }

    /// <summary>Why the mayor can't change an institution's choice now, or null if they can.</summary>
    public static string? ChoiceBlocked(Town town, InstitutionState s)
    {
        if (DefOf(town, s).Choice == null) return "it has nothing to choose";
        int wait = s.ChoiceDay + Rules(town).ChangeDays - town.Day;
        if (s.Choice != "" && wait > 0) return $"chosen lately: {wait} {(wait == 1 ? "day" : "days")} until it can change";
        return s.Choice != "" ? Council.Short(town, "institution") : null;
    }

    /// <summary>Make the institution's choice: the first is free, changing it later costs the keeper's regard.</summary>
    public static bool Choose(Town town, string id, string option)
    {
        if (Find(town, id) is not { } s || ChoiceBlocked(town, s) != null || s.Choice == option) return false;
        var d = DefOf(town, s);
        var o = d.Choice!.Options.FirstOrDefault(x => x.Id == option);
        if (o == null) return false;
        bool change = s.Choice != "";
        if (change) Council.Spend(town, "institution");
        s.Choice = option;
        s.ChoiceDay = town.Day;
        if (change) s.Regard = Math.Max(-100, s.Regard - Rules(town).ChangeRegard);
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"{Cap(d.Choice.Label)}: Mayor {town.Stories.Mayor.Name} chooses {o.Name}. {o.Text}{(change ? $" {KeeperName(town, s)} has to undo a season's work, and says so." : "")}");
        return true;
    }

    /// <summary>A request is settled: the keeper remembers whether the mayor answered it at all.</summary>
    public static void Heard(Town town, InstitutionState s, bool ignored)
    {
        if (ignored)
        {
            s.Ignored++;
            Esteem(town, s, -Rules(town).IgnoredRegard);
        }
        else s.Answered++;
    }

    /// <summary>A story moves the keeper's regard for the town.</summary>
    public static void Esteem(Town town, InstitutionState s, double v) => s.Regard = Math.Clamp(s.Regard + v, -100, 100);

    /// <summary>A story leaves a lasting feature at its institution. Returns a note for the Ledger, or "" if it has one already.</summary>
    public static string Fix(Town town, string fixture)
    {
        var f = Rules(town).Fixture(fixture) ?? throw new InvalidDataException($"Unknown fixture {fixture}");
        if (Find(town, f.Institution) is not { } s || s.Fixtures.Contains(f.Id)) return "";
        s.Fixtures.Add(f.Id);
        return f.Name;
    }

    /// <summary>What a service does, in short phrases for the Institutions window, at a strength (1 is modest funding).</summary>
    public static List<string> Describe(Town town, ServiceDef d, double k = 1) => Lines(town, d, k).Select(l => l.Text).ToList();

    /// <summary>
    /// The same phrases with an icon each (godot/assets/icons) and whether they help the town, so the window can
    /// show them as coloured bullets.
    /// </summary>
    public static List<(string Icon, string Text, bool Good)> Lines(Town town, ServiceDef d, double k = 1)
    {
        var said = new List<(string, string, bool)>();
        string N(double v, int places = 0) => Js.ToFixed(Math.Abs(v * k), places);
        if (d.Heroes > 0) said.Add(("heroes", $"room for {Js.Str(Math.Max(1, Math.Floor(d.Heroes * k + 0.05)))} more {(d.Heroes * k >= 1.95 ? "heroes" : "hero")}", true));
        if (d.Routes > 0) said.Add(("standing", "one more trade route", true));
        if (d.Airship > 0) said.Add(("tab-campaign", "an airship that carries a company over rivers, lakes and peaks", true));
        if (d.Defence != 0) said.Add(("defence", $"{(d.Defence > 0 ? "+" : "−")}{N(d.Defence)} defence on the walls", d.Defence > 0));
        if (d.Heal > 0) said.Add(("potions", $"heroes heal {N(d.Heal * 100)}% more a night", true));
        if (d.Escort > 0) said.Add(("threat", $"{N(d.Escort * 100)}% fewer caravans waylaid", true));
        if (d.Calm > 0) said.Add(("population", $"{N(d.Calm * 100)}% fewer townsfolk break under a dark mood", true));
        foreach (var (cls, w) in d.Calling.OrderBy(c => c.Key, StringComparer.Ordinal))
            if (w > 0) said.Add(("heroes", $"draws {town.Content.Classes.FirstOrDefault(c => c.Id == cls)?.Name.ToLowerInvariant() ?? cls} heroes", true));
        if (d.SkyWatch > 0) said.Add(("threat", $"a sky watch that sees raiders {N(d.SkyWatch)} hours sooner", true));
        if (d.Hush != 0) said.Add(("undead", $"the undercity stirs {N(d.Hush * 100)}% {(d.Hush > 0 ? "slower" : "faster")}", d.Hush > 0));
        foreach (var (g, y) in d.Yields.OrderBy(y => y.Key, StringComparer.Ordinal))
            said.Add((g.ToLowerInvariant(), $"{(y >= 1 ? "+" : "−")}{Js.ToFixed(Math.Abs((y - 1) * k * 100), 0)}% {g.ToLowerInvariant()}", y >= 1));
        if (d.Build != 0) said.Add(("tools", $"builders {N(d.Build * 100)}% {(d.Build > 0 ? "faster" : "slower")}", d.Build > 0));
        if (d.Mood != 0) said.Add(("population", d.Mood > 0 ? $"+{N(d.Mood, 1)} mood" : $"−{N(d.Mood, 1)} mood", d.Mood > 0));
        if (d.Arrivals != 0) said.Add(("homeless", d.Arrivals > 0 ? "draws settlers" : "puts settlers off", d.Arrivals > 0));
        if (d.Gold != 0) said.Add(("gold", $"{(d.Gold > 0 ? "+" : "−")}{N(d.Gold, 1)} gold a day", d.Gold > 0));
        if (d.Tension != 0) said.Add(("unrest", d.Tension < 0 ? "calmer streets" : "rowdier streets", d.Tension < 0));
        if (d.Dread < 0) said.Add(("dread", "eases dread", true));
        if (d.Favour > 0) said.Add(("favour", "the gods' favour", true));
        if (d.Faction != "" && town.Content.Factions.Factions.FirstOrDefault(f => f.Ref == d.Faction) is { } fd)
        {
            string name = town.Content.Library.Faction(fd.Ref)?.Name ?? fd.Ref;
            if (d.Standing != 0) said.Add(("standing", $"{(d.Standing > 0 ? "warms" : "cools")} {name}", d.Standing > 0));
            if (d.FactionHeat < 0) said.Add(("suspicion", $"keeps {name} out of sight", true));
        }
        return said;
    }

    /// <summary>How the keeper feels, in a word.</summary>
    public static string Feeling(double regard) => regard switch
    {
        >= 60 => "devoted",
        >= 25 => "warm",
        >= -10 => "civil",
        >= -40 => "cool",
        _ => "bitter",
    };

    // ---------- The stand-in mayor ----------

    /// <summary>
    /// The stand-in raises the institutions the town can afford one at a time, makes each one's choice
    /// (the temple district takes the temple's own god), and funds them by what the treasury holds.
    /// </summary>
    private static void Steward(Town town)
    {
        var r = Rules(town);
        foreach (var s in town.Institutions)
        {
            var d = DefOf(town, s);
            if (s.Choice == "" && d.Choice is { Options.Count: > 0 } c)
            {
                var temple = Faith.TempleGod(town);
                var pick = c.Options.FirstOrDefault(o => temple != null && o.Deity == temple.Id) ?? c.Options[0];
                Choose(town, d.Id, pick.Id);
            }
            // Up when the treasury is deep, down only when it runs thin, so the funding doesn't flicker.
            int want = s.Patronage;
            if (town.Treasury > 4000) want = 2;
            else if (town.Treasury > 400 && want < 1) want = 1;
            if (town.Treasury < 2500 && want > 1) want = 1;
            if (town.Treasury < 150) want = 0;
            want = Math.Clamp(want, 0, r.Patronage.Count - 1);
            if (want != s.Patronage) Fund(town, d.Id, want);
        }
        if (town.Buildings.Any(b => b.IsSite && For(town, b.Def.Id) is { Adopt: false })) return;
        foreach (var d in r.Institutions.Where(d => !d.Adopt && Seen(town, d.Id) && Find(town, d.Id) == null))
        {
            var bd = town.Content.Buildings.FirstOrDefault(b => b.Id == d.Building);
            if (bd == null || Planning.Blocked(town, bd) != null) continue;
            if (town.Buildings.Any(b => b.Def.Id == bd.Id)) continue;
            if (town.Treasury < r.StewardReserve + 2 * Planning.Cost(town, bd)) continue;
            var lot = Planning.CivicLot(town, bd);
            if (lot != null && Planning.Place(town, bd.Id, lot) != null) return;
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
