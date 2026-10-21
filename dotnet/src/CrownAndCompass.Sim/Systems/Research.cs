namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// One discovery the town's scholars can make (content/game/research.json). It is revealed only when one of its
/// sparks holds in this town, so each run's tree is its own; once known it works like a standing decree, and can
/// add heroes, lore, wards below, defence, better-trained heroes and cheaper scryings.
/// </summary>
public sealed record DiscoveryDef : DecreeDef
{
    /// <summary>crafts, husbandry, healing, trade, war, lore or arcana (town magic, cast by the library's wizards).</summary>
    public string Field { get; init; } = "";

    /// <summary>1 to 5: sets the lore it takes and the gold to commission it.</summary>
    public int Rank { get; init; } = 1;

    /// <summary>Any one of these reveals it: craft:profession:n, folk:race:n, partner:people, building:id, book:school:circle, books:n, wizard:level, known:id, tier:charter.</summary>
    public List<string> Sparks { get; init; } = [];

    /// <summary>All of these must hold to study it (same tokens, plus circle:n, the library's highest spell circle).</summary>
    public List<string> Requires { get; init; } = [];

    /// <summary>More heroes the town can keep.</summary>
    public int Heroes { get; init; }

    /// <summary>Lore a day, before funding.</summary>
    public double Lore { get; init; }

    /// <summary>The share by which the undercity stirs slower.</summary>
    public double Ward { get; init; }

    /// <summary>Added to the town's defence against assaults.</summary>
    public double Defence { get; init; }

    /// <summary>The share of fires set in timber buildings that fail to catch (slate roofs, lime wash, wards).</summary>
    public double Fireproof { get; init; }

    /// <summary>New heroes arrive with this share of the XP to their next level.</summary>
    public double Xp { get; init; }

    /// <summary>The share off the cost of a scrying.</summary>
    public double Scry { get; init; }
}

public sealed record FundingDef
{
    public string Name { get; init; } = "";
    public double Gold { get; init; }
    public double Lore { get; init; }
}

public sealed record InstitutionLore
{
    public string Building { get; init; } = "";
    public double Lore { get; init; }
}

public sealed record LibraryRank
{
    public double Power { get; init; }
    public string Name { get; init; } = "";
}

public sealed record StandInFunding
{
    public double Treasury { get; init; }
    public int Funding { get; init; }
}

/// <summary>Numbers for town magic and research.</summary>
public sealed record ResearchRules
{
    public List<FundingDef> Funding { get; init; } = [];

    /// <summary>Funding costs this much more for each charter past the first.</summary>
    public double FundingPerTier { get; init; } = 0.5;

    /// <summary>Lore and commission gold by rank (index 1 to 5).</summary>
    public double[] Lore { get; init; } = [];
    public double[] Gold { get; init; } = [];

    public List<InstitutionLore> Institutions { get; init; } = [];

    /// <summary>Townsfolk who teach themselves a trade spell (<see cref="HedgeMages"/>).</summary>
    public HedgeRules Hedge { get; init; } = new();
    public Dictionary<string, double> Scholars { get; init; } = [];
    /// <summary>Lore a day the town hall's clerks gather, whatever else the town has.</summary>
    public double BaseLore { get; init; } = 0.5;

    public double WizardLore { get; init; } = 0.15;
    public double CasterLore { get; init; } = 0.05;
    public List<string> Casters { get; init; } = [];
    public double BookLore { get; init; } = 0.08;

    /// <summary>Lore a day from the wizards' library's keeper, times how well the institution is kept (<see cref="Institutions.Strength"/>).</summary>
    public double KeeperLore { get; init; } = 1.5;
    public List<string> Schools { get; init; } = [];
    public double BookFromDelve { get; init; } = 0.12;
    public double BookFromDelvePerTier { get; init; } = 0.05;
    public double BookFromCaravan { get; init; } = 0.06;
    public List<string> BookPeoples { get; init; } = [];
    public double BookPeoplesChance { get; init; } = 0.14;
    public int BastionBookDays { get; init; } = 40;
    public int BastionBookLevel { get; init; } = 5;
    public List<LibraryRank> Ranks { get; init; } = [];
    public int JoinEvery { get; init; } = 45;
    public double JoinPower { get; init; } = 8;
    public double JoinLevelPerPower { get; init; } = 0.12;
    public int JoinLevelMax { get; init; } = 17;
    public List<StandInFunding> StandIn { get; init; } = [];
    public double StandInReserve { get; init; } = 300;
    public List<DiscoveryDef> Discoveries { get; init; } = [];

    public DiscoveryDef? Discovery(string id) => Discoveries.FirstOrDefault(d => d.Id == id);
}

/// <summary>A spellbook in the town's library.</summary>
public sealed class Spellbook
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string School { get; init; } = "";
    public int Circle { get; init; }
    public int Day { get; init; }

    /// <summary>Where it came from, for the Library window.</summary>
    public string From { get; init; } = "";
}

/// <summary>The library, its books and what the scholars are working on.</summary>
public sealed class ResearchState
{
    public List<Spellbook> Books { get; } = [];

    /// <summary>Discoveries the town has made, in order.</summary>
    public List<string> Known { get; } = [];

    /// <summary>Discoveries revealed in this town, in the order they were, with what sparked each.</summary>
    public OrderedMap<string, string> Revealed { get; } = new();

    /// <summary>Lore gathered toward each discovery begun (its commission was paid when it began).</summary>
    public OrderedMap<string, double> Progress { get; } = new();

    public string? Study { get; set; }

    /// <summary>Index into the funding levels.</summary>
    public int Funding { get; set; }

    /// <summary>The last party whose delve was looked over for books.</summary>
    public int LastParty { get; set; }

    /// <summary>Caravans each route had run when last looked over for books.</summary>
    public OrderedMap<int, int> Caravans { get; } = new();

    /// <summary>The day each bastion last lent the town a book.</summary>
    public OrderedMap<int, int> BastionDays { get; } = new();

    public int NextJoinDay { get; set; }

    /// <summary>Wizards the library has drawn to town.</summary>
    public List<int> Drawn { get; } = [];
}

/// <summary>
/// Town magic and research. The council funds its scholars, who gather lore each day from the town's institutions
/// (libraries, temples, guildhalls, a mage tower), its learned townsfolk, the casters living in town and the
/// spellbooks in the library. What they can study grows out of the town itself: smiths spark tempered steel,
/// a trade route to the elves their herb-lore, a book of transmutation plant growth, so no two towns share a tree.
/// Spellbooks come home from delves and caravans and from wizards' bastions; the more the library holds, the
/// stronger the wizards it draws to town, and the higher the circle of town magic they can cast.
/// </summary>
public static class Research
{
    private static ResearchRules Rules(Town town) => town.Content.Research;

    public static DiscoveryDef Def(Town town, string id) => Rules(town).Discovery(id) ?? throw new InvalidDataException($"Unknown discovery {id}");

    public static IEnumerable<DiscoveryDef> Known(Town town) => town.Research.Known.Select(id => Def(town, id));

    public static bool IsKnown(Town town, string id) => town.Research.Known.Contains(id);

    public static double LoreCost(Town town, DiscoveryDef d) => Rules(town).Lore[Math.Clamp(d.Rank, 1, Rules(town).Lore.Length - 1)];

    public static double Commission(Town town, DiscoveryDef d) => Rules(town).Gold[Math.Clamp(d.Rank, 1, Rules(town).Gold.Length - 1)];

    public static double FundingCost(Town town, int level) =>
        Js.Round(Rules(town).Funding[level].Gold * (1 + Rules(town).FundingPerTier * town.City.Tier));

    // Effects of what the town knows -----------------------------------------------------------------

    public static int Heroes(Town town) => Known(town).Sum(d => d.Heroes);

    public static double Defense(Town town) => Js.Sum(Known(town), d => d.Defence);

    public static double Ward(Town town) => Math.Min(0.8, Js.Sum(Known(town), d => d.Ward));

    public static double Fireproof(Town town) => Math.Min(0.9, Js.Sum(Known(town), d => d.Fireproof));

    public static double Xp(Town town) => Math.Min(0.75, Js.Sum(Known(town), d => d.Xp));

    public static double Scry(Town town) => Math.Min(0.6, Js.Sum(Known(town), d => d.Scry));

    // The library ------------------------------------------------------------------------------------

    private static bool IsWizard(Agent a) => a.Hero is { } s && s.Cls.Id == "wizard";

    /// <summary>Wizards living in town and not away on the road.</summary>
    public static List<Agent> Wizards(Town town) =>
        town.Heroes.Where(a => IsWizard(a) && !a.IsAway).OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).ToList();

    /// <summary>The library's power: the circles of its books and the levels of the wizards who read them.</summary>
    public static double Power(Town town) =>
        town.Research.Books.Sum(b => b.Circle) + town.Heroes.Where(IsWizard).Sum(a => a.Hero!.Level);

    public static LibraryRank Rank(Town town)
    {
        double p = Power(town);
        return Rules(town).Ranks.Where(r => r.Power <= p).OrderByDescending(r => r.Power).FirstOrDefault() ?? new LibraryRank { Name = "a shelf of grimoires" };
    }

    /// <summary>The highest circle of spell the town's wizards can cast from its books.</summary>
    public static int Circle(Town town)
    {
        int books = town.Research.Books.Count == 0 ? 0 : town.Research.Books.Max(b => b.Circle);
        int read = town.Heroes.Where(IsWizard).Select(a => Math.Min(9, (a.Hero!.Level + 1) / 2)).DefaultIfEmpty(0).Max();
        return Math.Min(books, read);
    }

    /// <summary>Lore a day from each source, before the funding multiplier.</summary>
    public static List<(string Why, double Lore)> Sources(Town town)
    {
        var r = Rules(town);
        var list = new List<(string, double)> { ("the town hall's clerks", r.BaseLore) };
        foreach (var inst in r.Institutions)
        {
            int n = town.Buildings.Count(b => b.Def.Id == inst.Building && b.Complete && !b.Closed);
            if (n > 0) list.Add((n == 1 ? Name(town, inst.Building) : $"{n} × {Name(town, inst.Building)}", n * inst.Lore));
        }
        if (Institutions.Find(town, "wizards-library") is { } lib && Institutions.Strength(town, lib) > 0)
            list.Add(($"{Institutions.KeeperName(town, lib)}", r.KeeperLore * Institutions.Strength(town, lib)));
        double scholars = 0;
        int learned = 0;
        foreach (var a in town.Residents)
            if (r.Scholars.TryGetValue(a.Profession, out double s))
            {
                scholars += s;
                learned++;
            }
        if (learned > 0) list.Add(($"{learned} learned townsfolk", scholars));
        int hedge = HedgeMages.All(town).Count;
        if (hedge > 0) list.Add(($"{hedge} hedge {(hedge == 1 ? "mage" : "mages")}", hedge * r.Hedge.Lore));
        if (Circles.Lore(town) is (string why, double lore) && lore > 0) list.Add((why, lore));
        foreach (var w in town.Content.City.Wards)
            if (w.Lore > 0 && town.City.OpenWards.Contains(w.Id)) list.Add((w.Name.ToLowerInvariant().StartsWith("the ") ? w.Name : "the " + w.Name, w.Lore));
        double wiz = 0, cast = 0;
        foreach (var a in town.Heroes.Where(a => !a.IsAway))
        {
            if (IsWizard(a)) wiz += r.WizardLore * a.Hero!.Level;
            else if (r.Casters.Contains(a.Hero!.Cls.Id)) cast += r.CasterLore * a.Hero.Level;
        }
        if (wiz > 0) list.Add(("wizards in town", wiz));
        if (cast > 0) list.Add(("other casters in town", cast));
        double books = r.BookLore * town.Research.Books.Sum(b => b.Circle);
        if (books > 0) list.Add(($"{town.Research.Books.Count} spellbooks", books));
        double known = Js.Sum(Known(town), d => d.Lore);
        if (known > 0) list.Add(("what the town already knows", known));
        return list;
    }

    public static double Lore(Town town) =>
        Js.Sum(Sources(town), s => s.Lore) * Rules(town).Funding[town.Research.Funding].Lore * Union.LoreFactor(town);

    private static string Name(Town town, string building) =>
        town.Content.Buildings.FirstOrDefault(b => b.Id == building)?.Name.ToLowerInvariant() ?? building;

    // Sparks and requirements ------------------------------------------------------------------------

    /// <summary>Whether a spark or requirement holds, and the words for it ("your 3 smiths").</summary>
    public static (bool Holds, string Why) Check(Town town, string token)
    {
        var p = token.Split(':');
        int N(int i) => p.Length > i && int.TryParse(p[i], out int n) ? n : 1;
        switch (p[0])
        {
            case "craft":
            {
                int n = town.Residents.Count(a => a.Profession == p[1]);
                return (n >= N(2), $"{n} {Plural(p[1], n)} in town");
            }
            case "folk":
            {
                int n = town.Residents.Count(a => a.Race == p[1]);
                return (n >= N(2), $"{n} {p[1]} {(n == 1 ? "neighbour" : "neighbours")}");
            }
            case "partner":
            {
                var s = Trade.Open(town).Select(r => town.Settlements[r.SettlementId]).FirstOrDefault(s => s.Kin.Id == p[1]);
                return (s != null, s != null ? $"the caravans to {s.Name}" : $"trade with {p[1]}");
            }
            case "building":
            {
                bool has = town.Buildings.Any(b => b.Def.Id == p[1] && b.Complete);
                return (has, $"the town's {Name(town, p[1])}");
            }
            case "book":
            {
                var b = town.Research.Books.Where(b => b.School == p[1] && b.Circle >= N(2)).OrderBy(b => b.Id).FirstOrDefault();
                return (b != null, b != null ? b.Name : $"a book of {p[1]} of the {Ordinal(N(2))} circle");
            }
            case "books":
                return (town.Research.Books.Count >= N(1), "the library's books");
            case "wizard":
            {
                var w = town.Heroes.Where(a => IsWizard(a) && a.Hero!.Level >= N(1)).OrderBy(a => a.Id).FirstOrDefault();
                return (w != null, w != null ? $"{w.Name} the wizard" : $"a wizard of level {N(1)}");
            }
            case "known":
                return (IsKnown(town, p[1]), Rules(town).Discovery(p[1])?.Name.ToLowerInvariant() ?? p[1]);
            case "tier":
                return (City.Reached(town, p[1]), $"the charter of a {City.TierName(town, p[1]).ToLowerInvariant()}");
            case "circle":
                return (Circle(town) >= N(1), $"wizards who can cast the {Ordinal(N(1))} circle");
            default:
                return (false, token);
        }
    }

    /// <summary>The requirements still missing for a revealed discovery.</summary>
    public static List<string> Missing(Town town, DiscoveryDef d) =>
        d.Requires.Select(t => Check(town, t)).Where(c => !c.Holds).Select(c => c.Why).ToList();

    /// <summary>Revealed, unknown and with everything it needs: the council can set the scholars on it.</summary>
    public static bool Available(Town town, DiscoveryDef d) =>
        town.Research.Revealed.ContainsKey(d.Id) && !IsKnown(town, d.Id) && Missing(town, d).Count == 0;

    public static List<DiscoveryDef> Open(Town town) =>
        Rules(town).Discoveries.Where(d => Available(town, d)).OrderBy(d => d.Rank).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

    // Commands ---------------------------------------------------------------------------------------

    /// <summary>Set the scholars on a discovery. The first time pays its commission; progress is kept if they switch.</summary>
    public static bool Study(Town town, string id)
    {
        var d = Rules(town).Discovery(id);
        if (d == null || !Available(town, d) || town.Research.Study == id) return false;
        var st = town.Research;
        if (!st.Progress.ContainsKey(id))
        {
            double cost = Commission(town, d);
            if (town.Treasury < cost) return false;
            town.Book(TreasuryAccount.Research, -cost);
            st.Progress[id] = 0;
        }
        st.Study = id;
        town.Log(LedgerCategory.Town, Importance.Routine, $"The scholars of {town.Name} set to work on {Lower(d.Name)}.");
        return true;
    }

    /// <summary>How much the council pays its scholars each day.</summary>
    public static bool Fund(Town town, int level)
    {
        if (level < 0 || level >= Rules(town).Funding.Count || level == town.Research.Funding) return false;
        town.Research.Funding = level;
        var f = Rules(town).Funding[level];
        town.Log(LedgerCategory.Town, Importance.Routine,
            level == 0 ? "The council stops paying its scholars. They work on, slowly, for love."
                : $"The council funds its scholars: {f.Name}, {Js.Str(FundingCost(town, level))} gold a day.");
        return true;
    }

    // Daily ------------------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        var r = Rules(town);
        var st = town.Research;
        Gather(town);
        Reveal(town);

        // The stipend, and what known discoveries pay or cost.
        double pay = FundingCost(town, st.Funding);
        if (pay > 0)
        {
            if (town.Treasury < pay)
            {
                // Down to what the treasury can pay, if anything.
                while (st.Funding > 0 && FundingCost(town, st.Funding) > town.Treasury) st.Funding--;
                town.Log(LedgerCategory.Town, Importance.Notable, st.Funding == 0
                    ? "The treasury can't pay the scholars. The library's lamps go out early."
                    : $"The treasury can't pay the scholars in full. The council cuts them to {r.Funding[st.Funding].Name}.");
                if (st.Funding > 0) town.Book(TreasuryAccount.Research, -FundingCost(town, st.Funding));
            }
            else town.Book(TreasuryAccount.Research, -pay);
        }
        foreach (var d in Known(town))
        {
            if (d.Gold != 0) town.Book(TreasuryAccount.Research, d.Gold);
            if (d.Tension != 0) town.Tension = Math.Max(0, town.Tension + d.Tension);
        }

        if (st.Study is string id)
        {
            var d = Def(town, id);
            if (!Available(town, d)) st.Study = null;
            else
            {
                st.Progress[id] = st.Progress.Get(id, 0) + Lore(town);
                if (st.Progress[id] >= LoreCost(town, d)) Discover(town, d);
            }
        }
        Join(town);
        if (auto) StandIn(town);
    }

    private static void Discover(Town town, DiscoveryDef d)
    {
        var st = town.Research;
        st.Known.Add(d.Id);
        st.Progress.Remove(d.Id);
        st.Study = null;
        string what = d.Field == "arcana" ? $"The wizards of {town.Name} cast {Lower(d.Name)} for the first time" : $"{town.Name}'s scholars master {Lower(d.Name)}";
        town.Log(LedgerCategory.Town, d.Rank >= 4 ? Importance.Major : Importance.Notable, $"{what}. {d.Text}");
    }

    /// <summary>Spellbooks come home from delves, on caravans and from wizards' bastions.</summary>
    private static void Gather(Town town)
    {
        var r = Rules(town);
        var st = town.Research;
        foreach (var party in town.Parties.Where(p => p.Id > st.LastParty && p.Returned).OrderBy(p => p.Id).ToList())
        {
            st.LastParty = party.Id;
            if (party.Result?.Outcome != ExpeditionOutcome.Cleared || Quests.Site(town, party.SiteId) is not { } site) continue;
            if (!town.Rng.Chance(r.BookFromDelve + r.BookFromDelvePerTier * site.Def.Tier)) continue;
            int circle = Math.Clamp(site.Def.Tier * 2 - 1 + town.Rng.Range(0, 2), 1, 9);
            Add(town, circle, $"brought up from {site.Def.Name} by {party.Name}");
        }
        foreach (var route in Trade.Open(town).OrderBy(x => x.Id))
        {
            int seen = st.Caravans.Get(route.Id, 0);
            if (route.Caravans <= seen) continue;
            st.Caravans[route.Id] = route.Caravans;
            var s = town.Settlements[route.SettlementId];
            double chance = r.BookPeoples.Contains(s.Kin.Id) ? r.BookPeoplesChance : r.BookFromCaravan;
            if (!town.Rng.Chance(chance)) continue;
            int circle = Math.Clamp(town.City.Tier + town.Rng.Range(0, 3), 1, 9);
            Add(town, circle, $"bought from {s.People}");
        }
        foreach (var b in town.Bastions.OrderBy(b => b.Id))
        {
            if (b.LordId is not int lid || town.Agent(lid) is not { Departed: false, Dead: false } lord || !IsWizard(lord) || lord.Hero!.Level < r.BastionBookLevel) continue;
            int last = st.BastionDays.Get(b.Id, b.FoundedDay);
            if (town.Day - last < r.BastionBookDays) continue;
            st.BastionDays[b.Id] = town.Day;
            Add(town, Math.Min(9, (lord.Hero.Level + 1) / 2), $"copied for the town at {b.Name}");
        }
    }

    /// <summary>Put a spellbook on the library's shelves. A story can too.</summary>
    public static Spellbook Add(Town town, int circle, string from, string? school = null)
    {
        var r = Rules(town);
        var st = town.Research;
        school ??= r.Schools[town.Rng.Range(0, r.Schools.Count)];
        var book = new Spellbook
        {
            Id = st.Books.Count + 1,
            Name = BookName(town, school, circle),
            School = school,
            Circle = Math.Clamp(circle, 1, 9),
            Day = town.Day,
            From = from,
        };
        st.Books.Add(book);
        town.Log(LedgerCategory.Town, book.Circle >= 5 ? Importance.Notable : Importance.Routine,
            $"A spellbook for the library: {book.Name}, {from}.");
        return book;
    }

    private static readonly string[] Bindings = ["calfskin", "black oak boards", "sharkskin", "brass clasps", "a dragon's-scale cover", "faded silk", "iron bands", "a moth-eaten wrapping"];

    private static string BookName(Town town, string school, int circle) =>
        $"a grimoire of {school} of the {Ordinal(circle)} circle, in {town.Rng.Pick(Bindings)}";

    /// <summary>Reveal what the town has the makings of.</summary>
    private static void Reveal(Town town)
    {
        var st = town.Research;
        foreach (var d in Rules(town).Discoveries)
        {
            if (st.Revealed.ContainsKey(d.Id)) continue;
            foreach (var spark in d.Sparks)
            {
                var (holds, why) = Check(town, spark);
                if (!holds) continue;
                st.Revealed[d.Id] = why;
                town.Log(LedgerCategory.Town, d.Rank >= 3 ? Importance.Notable : Importance.Routine,
                    $"An idea takes hold in {town.Name}, thanks to {why}: {Lower(d.Name)}. {d.Text}");
                break;
            }
        }
    }

    /// <summary>A library this strong draws a wizard better than any in town.</summary>
    private static void Join(Town town)
    {
        var r = Rules(town);
        var st = town.Research;
        if (town.Day < st.NextJoinDay) return;
        double power = Power(town);
        if (power < r.JoinPower || town.Heroes.Count >= City.HeroCap(town)) return;
        int level = Math.Min(r.JoinLevelMax, 1 + (int)Math.Floor(power * r.JoinLevelPerPower));
        int best = town.Heroes.Where(IsWizard).Select(a => a.Hero!.Level).DefaultIfEmpty(0).Max();
        if (level <= best) return;
        var cls = town.Content.Classes.First(c => c.Id == "wizard");
        var a = Quests.CreateHero(town, cls, level);
        st.Drawn.Add(a.Id);
        st.NextJoinDay = town.Day + r.JoinEvery;
        town.Log(LedgerCategory.Arrival, Importance.Notable,
            $"{a.Describe()} comes to {town.Name} to read in {Rank(town).Name}, and stays. ({a.TraitList()})", [a.Id]);
    }

    private static void StandIn(Town town)
    {
        var r = Rules(town);
        var st = town.Research;
        int want = r.StandIn.Where(s => town.Treasury >= s.Treasury).Select(s => s.Funding).DefaultIfEmpty(0).Max();
        if (want != st.Funding) Fund(town, want);
        if (st.Study == null)
        {
            var next = Open(town).Where(d => st.Progress.ContainsKey(d.Id) || town.Treasury >= Commission(town, d) + r.StandInReserve).FirstOrDefault();
            if (next != null) Study(town, next.Id);
        }
    }

    public static string Ordinal(int n) => n switch
    {
        1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", 7 => "seventh", 8 => "eighth", 9 => "ninth",
        _ => $"{n}th",
    };

    private static string Plural(string s, int n) => n == 1 ? s : s.EndsWith('y') ? s[..^1] + "ies" : s + "s";

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
