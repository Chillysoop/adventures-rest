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

    /// <summary>Any one of these, held for a while, reveals it (<see cref="Sparks"/>): newcomers, trade, buildings, books and wizards, the wilds (found:, slain:, cleared:, delves:), allies, what is known.</summary>
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

    /// <summary>Tower guns it brings (<see cref="Cannons"/>): 1 bombards, 2 arcane guns.</summary>
    public int Cannons { get; init; }
}

public sealed record InsightRules
{
    public double Cleared { get; init; } = 20;
    public double Find { get; init; } = 25;
    public double Book { get; init; } = 12;
    public double Newcomer { get; init; } = 15;
    public double Wizard { get; init; } = 4;

    /// <summary>Lore the first of a kind of monster part brings, a grade; and a magic item come home, a tier.</summary>
    public double Part { get; init; } = 6;
    public double Item { get; init; } = 5;
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

/// <summary>A step on the way to a discovery: at this share of its lore the lead scholar rolls (research.json "stages").</summary>
public sealed record StageDef
{
    public double At { get; init; } = 1;
    public string Name { get; init; } = "";

    /// <summary>The skill rolled; "" for the field's own (<see cref="ResearchRules.FieldSkills"/>).</summary>
    public string Skill { get; init; } = "";
}

/// <summary>What can go wrong on a natural 1: a line and what it does (fire, imp, lost).</summary>
public sealed record MishapDef
{
    public string Effect { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>Numbers for town magic and research.</summary>
public sealed record ResearchRules
{
    /// <summary>The steps each discovery is worked through, each a roll by the lead scholar; the last is the breakthrough.</summary>
    public List<StageDef> Stages { get; init; } = [];
    public Dictionary<string, string> FieldSkills { get; init; } = [];

    /// <summary>A step's DC: a base, more by rank and by step.</summary>
    public int StageDc { get; init; } = 8;

    /// <summary>The town hall's clerks' bonus when no scholar can lead a step.</summary>
    public int Clerks { get; init; } = 2;
    public int DcPerRank { get; init; } = 1;
    public int DcPerStage { get; init; } = 1;

    /// <summary>Shares of the discovery's lore: a natural 20's head start, a miss's setback, a natural 1's.</summary>
    public double StageCrit { get; init; } = 0.15;
    public double Setback { get; init; } = 0.06;
    public double MishapSetback { get; init; } = 0.12;

    /// <summary>The share of the library a mishap's fire wrecks, and the tension an escaped imp adds.</summary>
    public double MishapFire { get; init; } = 0.15;
    public double MishapTension { get; init; } = 5;
    public List<MishapDef> Mishaps { get; init; } = [];

    /// <summary>{scholar}, {step}, {discovery}, {roll}.</summary>
    public string StageWon { get; init; } = "";
    public string StageCritText { get; init; } = "";
    public string StageLost { get; init; } = "";

    public List<FundingDef> Funding { get; init; } = [];

    /// <summary>Funding costs this much more for each charter past the first.</summary>
    public double FundingPerTier { get; init; } = 0.5;

    /// <summary>Lore each discovery takes by rank (index 1 to 5).</summary>
    public double[] Lore { get; init; } = [];

    /// <summary>Days a spark must hold before the idea takes shape, by rank (index 1 to 5).</summary>
    public int[] Brew { get; init; } = [];

    /// <summary>Lore a find brings the scholars: per tier of a site a company cleared, per explorers' find, per circle of a new
    /// spellbook, per learned newcomer, per level of a wizard the library draws.</summary>
    public InsightRules Insight { get; init; } = new();

    /// <summary>Notes kept in the Library's feed.</summary>
    public int FeedKept { get; init; } = 40;

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

/// <summary>An idea taking shape: a spark has held this many days (<see cref="ResearchRules.Brew"/>).</summary>
public sealed class Lead
{
    public int Days { get; set; }
    public string Why { get; set; } = "";
    public string Icon { get; set; } = "";
}

/// <summary>One line of the Library's feed: an idea that took shape, a find that moved the scholars, a book, a discovery.</summary>
public sealed class ResearchNote
{
    public int Day { get; init; }
    public string Icon { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>Lore it brought, if any.</summary>
    public double Lore { get; init; }
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

    /// <summary>Steps passed on each discovery begun (<see cref="ResearchRules.Stages"/>).</summary>
    public OrderedMap<string, int> Steps { get; } = new();

    public string? Study { get; set; }

    /// <summary>The icon of the source that revealed each discovery (<see cref="Sparks"/>).</summary>
    public OrderedMap<string, string> RevealIcon { get; } = new();

    /// <summary>Ideas taking shape: discoveries whose spark holds, not yet revealed.</summary>
    public OrderedMap<string, Lead> Leads { get; } = new();

    /// <summary>Lore from finds that came in while the scholars had nothing to work on, for the next study.</summary>
    public double Notes { get; set; }

    /// <summary>Why the library's ideas appear and what moved the scholars, newest last.</summary>
    public List<ResearchNote> Feed { get; } = [];

    /// <summary>Creatures of each type the town's companies have brought down at a cleared site, and the latest who did.</summary>
    public OrderedMap<string, int> Slain { get; } = new();
    public OrderedMap<string, string> Slayers { get; } = new();

    /// <summary>The last explorers' find and the last townsperson looked over for what they brought the scholars.</summary>
    public int LastFind { get; set; }
    public int LastAgent { get; set; }

    /// <summary>Kinds of monster part the scholars have studied, and the last armory piece they looked over.</summary>
    public List<string> Studied { get; } = [];
    public int LastStock { get; set; }

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

    public static int BrewDays(Town town, DiscoveryDef d) => Rules(town).Brew.Length == 0 ? 0 : Rules(town).Brew[Math.Clamp(d.Rank, 1, Rules(town).Brew.Length - 1)];

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
        if (Lofted.Lore(town) is > 0 and double sky) list.Add(("the Lofted City's keelstone-wrights", sky));
        return list;
    }

    public static double Lore(Town town) =>
        Js.Sum(Sources(town), s => s.Lore) * Rules(town).Funding[town.Research.Funding].Lore * Union.LoreFactor(town);

    private static string Name(Town town, string building) =>
        town.Content.Buildings.FirstOrDefault(b => b.Id == building)?.Name.ToLowerInvariant() ?? building;

    // Sparks and requirements ------------------------------------------------------------------------

    /// <summary>Whether a spark or requirement holds, and the words for it ("Brunhild, a dwarf smith").</summary>
    public static (bool Holds, string Why) Check(Town town, string token)
    {
        var sp = Sparks.Check(town, token);
        return (sp.Holds, sp.Why);
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

    /// <summary>
    /// Set the scholars on a discovery. Gold can't buy it: they work from what the town has (scholars, books, mages,
    /// finds), and notes banked while they were idle go into it. Progress is kept if they switch.
    /// </summary>
    public static bool Study(Town town, string id)
    {
        var d = Rules(town).Discovery(id);
        if (d == null || !Available(town, d) || town.Research.Study == id) return false;
        var st = town.Research;
        st.Progress[id] = st.Progress.Get(id, 0) + st.Notes;
        st.Notes = 0;
        st.Study = id;
        town.Log(LedgerCategory.Town, Importance.Routine, $"The scholars of {town.Name} set to work on {Lower(d.Name)}.");
        return true;
    }

    /// <summary>Lore a find brings: into what the scholars are working on, or banked as notes for the next study.</summary>
    public static void Insight(Town town, string icon, string text, double lore)
    {
        var st = town.Research;
        if (lore <= 0) return;
        if (st.Study is string id) st.Progress[id] = st.Progress.Get(id, 0) + lore;
        else st.Notes += lore;
        Note(town, icon, text, lore);
    }

    /// <summary>A line for the Library's feed.</summary>
    public static void Note(Town town, string icon, string text, double lore = 0)
    {
        var feed = town.Research.Feed;
        feed.Add(new ResearchNote { Day = town.Day, Icon = icon, Text = text, Lore = Js.Round(lore) });
        int kept = Rules(town).FeedKept;
        if (feed.Count > kept) feed.RemoveRange(0, feed.Count - kept);
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
        Learn(town);
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
                if (Steps(town, d) && st.Progress[id] >= LoreCost(town, d)) Discover(town, d);
            }
        }
        Join(town);
        if (auto) StandIn(town);
    }

    // Steps ------------------------------------------------------------------------------------------

    /// <summary>The skill a step is rolled with: its own, or the discovery's field's.</summary>
    public static string StepSkill(Town town, DiscoveryDef d, StageDef stage) =>
        stage.Skill != "" ? stage.Skill : Rules(town).FieldSkills.GetValueOrDefault(d.Field, "investigation");

    public static int StepDc(Town town, DiscoveryDef d, int step) => Rules(town).StageDc + Rules(town).DcPerRank * d.Rank + Rules(town).DcPerStage * step;

    /// <summary>The next step on a discovery, or null when every step is passed.</summary>
    public static StageDef? NextStep(Town town, DiscoveryDef d)
    {
        int at = town.Research.Steps.Get(d.Id, 0);
        var stages = Rules(town).Stages;
        return at < stages.Count ? stages[at] : null;
    }

    /// <summary>Who leads the work at a skill: the best at it among the heroes at home and the learned townsfolk; null leaves it to the clerks.</summary>
    public static Agent? Lead(Town town, string skill)
    {
        var r = Rules(town);
        Agent? best = null;
        int bestBonus = int.MinValue;
        foreach (var a in town.Residents)
        {
            if (a.Hero != null ? a.IsAway : !r.Scholars.ContainsKey(a.Profession)) continue;
            int b = Skills.Bonus(town, a, skill);
            if (b > bestBonus) (best, bestBonus) = (a, b);
        }
        return best;
    }

    /// <summary>Where the scholars work: the first of the research institutions standing, for the dice to pop over.</summary>
    private static Building? Desk(Town town) =>
        Rules(town).Institutions.Select(i => town.Buildings.FirstOrDefault(b => b.Def.Id == i.Building && b.Complete)).FirstOrDefault(b => b != null);

    /// <summary>
    /// Research goes in steps, each a roll along the way: as the lore reaches a step's mark the lead scholar rolls its
    /// skill against its DC. A success goes on (a natural 20 jumps ahead); a miss is a dead end that costs some of the
    /// lore; a natural 1 is a mishap (a fire in the library, an imp loose in town, notes lost). The last step is the
    /// breakthrough. True when every step is passed.
    /// </summary>
    private static bool Steps(Town town, DiscoveryDef d)
    {
        var r = Rules(town);
        var st = town.Research;
        if (r.Stages.Count == 0) return true;
        double cost = LoreCost(town, d);
        for (int guard = 0; guard < r.Stages.Count; guard++)
        {
            int at = st.Steps.Get(d.Id, 0);
            if (at >= r.Stages.Count) return true;
            var stage = r.Stages[at];
            if (st.Progress.Get(d.Id, 0) < stage.At * cost) return false;
            string skill = StepSkill(town, d, stage);
            var lead = Lead(town, skill);
            var check = Skills.Roll(town, lead, skill, StepDc(town, d, at), "research", building: Desk(town)?.Id, extra: lead == null ? r.Clerks : 0);
            string Fill(string text) => text.Replace("{scholar}", lead?.Name ?? "the town hall's clerks").Replace("{step}", stage.Name)
                .Replace("{discovery}", Lower(d.Name)).Replace("{roll}", Skills.Said(town, check));
            if (check.Success)
            {
                st.Steps[d.Id] = at + 1;
                if (check.Critical) st.Progress[d.Id] = st.Progress.Get(d.Id, 0) + r.StageCrit * cost;
                Note(town, "d20", Saga.Cap(Fill(check.Critical ? r.StageCritText : r.StageWon)));
                continue;
            }
            double back = (check.Fumble ? r.MishapSetback : r.Setback) * cost;
            st.Progress[d.Id] = Math.Max(0, Math.Min(st.Progress.Get(d.Id, 0), stage.At * cost) - back);
            string text = Saga.Cap(Fill(r.StageLost));
            if (check.Fumble && r.Mishaps.Count > 0) text += " " + Mishap(town, r.Mishaps[town.Day % r.Mishaps.Count]);
            Note(town, "d20", text);
            town.Log(LedgerCategory.Town, check.Fumble ? Importance.Notable : Importance.Routine, text, lead != null ? [lead.Id] : []);
            return false;
        }
        return st.Steps.Get(d.Id, 0) >= r.Stages.Count;
    }

    /// <summary>A natural 1 on a step: what goes wrong.</summary>
    private static string Mishap(Town town, MishapDef m)
    {
        var r = Rules(town);
        switch (m.Effect)
        {
            case "fire" when Desk(town) is { } desk && Blows.Burns(desk):
                Blows.Wreck(town, desk, r.MishapFire);
                return m.Text.Replace("{place}", desk.Name);
            case "fire":
                return "";
            case "imp":
                town.Tension += r.MishapTension;
                return m.Text;
            default:
                return m.Text;
        }
    }

    private static void Discover(Town town, DiscoveryDef d)
    {
        var st = town.Research;
        st.Known.Add(d.Id);
        st.Progress.Remove(d.Id);
        st.Steps.Remove(d.Id);
        st.Study = null;
        string what = d.Field == "arcana" ? $"The wizards of {town.Name} cast {Lower(d.Name)} for the first time" : $"{town.Name}'s scholars master {Lower(d.Name)}";
        Note(town, Sparks.Known, d.Name);
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
            string type = Sparks.CreatureType(town, site.Def.Boss.Monster);
            if (type.Length > 0)
            {
                st.Slain[type] = st.Slain.Get(type, 0) + 1;
                st.Slayers[type] = $"{party.Name} at {site.Def.Name}";
            }
            Insight(town, type.Length > 0 ? Sparks.Slain : Sparks.Wilds, $"{party.Name}{(party.Name.EndsWith('s') ? "'" : "'s")} notes from {site.Def.Name}", r.Insight.Cleared * Math.Max(1, site.Def.Tier));
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
        Insight(town, Sparks.Book, Cap(book.Name), r.Insight.Book * book.Circle);
        town.Log(LedgerCategory.Town, book.Circle >= 5 ? Importance.Notable : Importance.Routine,
            $"A spellbook for the library: {book.Name}, {from}.");
        return book;
    }

    private static readonly string[] Bindings = ["calfskin", "black oak boards", "sharkskin", "brass clasps", "a dragon's-scale cover", "faded silk", "iron bands", "a moth-eaten wrapping"];

    private static string BookName(Town town, string school, int circle) =>
        $"a grimoire of {school} of the {Ordinal(circle)} circle, in {town.Rng.Pick(Bindings)}";

    /// <summary>
    /// What the town has the makings of takes shape over days: a spark that holds starts a lead, and once it has held
    /// <see cref="BrewDays"/> the idea is revealed, naming who or what sparked it.
    /// </summary>
    private static void Reveal(Town town)
    {
        var st = town.Research;
        foreach (var d in Rules(town).Discoveries)
        {
            if (st.Revealed.ContainsKey(d.Id)) continue;
            Spark? lit = null;
            foreach (var token in d.Sparks)
            {
                var sp = Sparks.Check(town, token);
                if (sp.Holds)
                {
                    lit = sp;
                    break;
                }
            }
            if (lit is not { } spark) continue;
            if (!st.Leads.TryGetValue(d.Id, out var lead))
            {
                lead = new Lead();
                st.Leads[d.Id] = lead;
            }
            lead.Days++;
            lead.Why = spark.Why;
            lead.Icon = spark.Icon;
            if (lead.Days < BrewDays(town, d)) continue;
            st.Leads.Remove(d.Id);
            st.Revealed[d.Id] = spark.Why;
            st.RevealIcon[d.Id] = spark.Icon;
            Note(town, spark.Icon, $"{Cap(spark.Why)}: {Lower(d.Name)}");
            town.Log(LedgerCategory.Town, d.Rank >= 3 ? Importance.Notable : Importance.Routine,
                $"An idea takes hold in {town.Name}, thanks to {spark.Why}: {Lower(d.Name)}. {d.Text}");
        }
    }

    /// <summary>What explorers' finds and learned newcomers bring the scholars.</summary>
    private static void Learn(Town town)
    {
        var r = Rules(town);
        var st = town.Research;
        foreach (var f in town.Finds.Where(f => f.Id > st.LastFind && f.Known).OrderBy(f => f.Id).ToList())
        {
            st.LastFind = f.Id;
            Insight(town, Sparks.Wilds, $"explorers' word of {Explore.What(town, f)}", r.Insight.Find);
        }
        // What companies carry home: the first of each kind of part, and each magic piece that comes into the armory.
        foreach (var id in town.Spoils.Trophies.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            if (st.Studied.Contains(id) || town.Content.Spoils.Trophy(id) is not { } t) continue;
            st.Studied.Add(id);
            Insight(town, Sparks.Slain, $"the scholars' study of {Spoils.Count(t, 1)}", r.Insight.Part * Math.Max(1, t.Grade));
        }
        foreach (var s in town.Armory.Stock.Where(s => s.Id > st.LastStock).OrderBy(s => s.Id).ToList())
        {
            st.LastStock = s.Id;
            if (s.Item.Tier <= 0 || s.From.StartsWith("made at", StringComparison.Ordinal) || s.From == "the town hall's own") continue;
            Insight(town, Sparks.Relic, $"{Agent.Article(Gear.Plainly(s.Item))} {Gear.Plainly(s.Item)} the scholars looked over", r.Insight.Item * s.Item.Tier);
        }
        bool first = st.LastAgent == 0;
        foreach (var a in town.Residents.Where(a => a.Id > st.LastAgent).OrderBy(a => a.Id).ToList())
        {
            st.LastAgent = a.Id;
            if (first || a.Hero != null || !r.Scholars.ContainsKey(a.Profession)) continue;
            Insight(town, Sparks.Newcomer, $"{a.Name}, a newly come {a.Profession}", r.Insight.Newcomer);
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
        Insight(town, Sparks.Magic, $"{a.Name}, a wizard of level {level}", r.Insight.Wizard * level);
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
            var next = Open(town).FirstOrDefault();
            if (next != null) Study(town, next.Id);
        }
    }

    public static string Ordinal(int n) => n switch
    {
        1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", 7 => "seventh", 8 => "eighth", 9 => "ninth",
        _ => $"{n}th",
    };

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
