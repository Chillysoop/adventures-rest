namespace CrownAndCompass.Sim.Systems;

/// <summary>An adventuring company as the town hall knows it: its name and how its dealings with the town have gone.</summary>
public sealed class CompanyState
{
    public required string Name { get; init; }

    /// <summary>Goodwill (or bad blood) from dealings with the council, -MaxDealings to MaxDealings.</summary>
    public double Dealings { get; set; }
    public int Bounties { get; set; }
    public int ReportsSold { get; set; }
    public double GoldPaid { get; set; }
    public int Refusals { get; set; }

    /// <summary>The company's name abroad: fame from its deeds, infamy from its members' (<see cref="Notoriety"/>).</summary>
    public int Renown { get; set; }
    public int Infamy { get; set; }

    /// <summary>Turned down an offer and will not hear another before this tick.</summary>
    public int? SulkUntil { get; set; }

    /// <summary>Sworn to hand the magic it finds to the town armory, for a finder's fee (a share of its worth) (<see cref="Armory"/>).</summary>
    public bool Pact { get; set; }
    public double PactFee { get; set; }
    public int PactDay { get; set; }
    public int Handed { get; set; }

    /// <summary>Good deeds to the company's name (ruins cleared, pleas answered, captives freed, the walls held), and the latest of them (<see cref="Notoriety.Record"/>).</summary>
    public int GoodDeeds { get; set; }
    public List<DeedNote> Deeds { get; } = [];

    /// <summary>Jobs the council offered the company by name (<see cref="Roster"/>), taken and turned down.</summary>
    public int Offers { get; set; }
    public int Declined { get; set; }

    /// <summary>The last job they turned down: no asking again today for the same place unless the pay goes up.</summary>
    public string DeclinedSite { get; set; } = "";
    public double DeclinedReward { get; set; }
    public int DeclinedDay { get; set; } = -1;

    /// <summary>How battered its tabards and banner are, 0 new to 1 rags (<see cref="Heraldry"/>), the ventures that did it, and the day it last came home.</summary>
    public double Wear { get; set; }
    public int Ventures { get; set; }
    public int MendedDay { get; set; }

    /// <summary>The town has heard the company is proud of its rags.</summary>
    public bool Proud { get; set; }

    /// <summary>On the town's retainer (<see cref="Retainers"/>): its daily wage, the day it signed, and days its wage went unpaid.</summary>
    public bool Retained { get; set; }
    public double Wage { get; set; }
    public int RetainedDay { get; set; }
    public int Unpaid { get; set; }

    /// <summary>The day the company came down to one member, or null (<see cref="Muster"/>).</summary>
    public int? LoneSince { get; set; }

    /// <summary>Its fallen whose places recruits have taken, counted in the order they fell.</summary>
    public int Filled { get; set; }
}

/// <summary>A good deed on a company's record.</summary>
public sealed class DeedNote
{
    public int Day { get; init; }
    public string What { get; init; } = "";
}

/// <summary>What the council answered a company's asking price with.</summary>
public enum IntelDeal
{
    Bought,
    Haggled,
    Refused,
}

/// <summary>
/// The fog of war's market. A party only reports to the council when it went out on a council
/// bounty; a company out on its own account (or on a plea, or the temple's coin) comes home and keeps
/// what it saw. The council can buy that report: the price is what the report is worth (new country,
/// sites the town has wrong, fresher is better) times what the company thinks of the town, which is
/// its members' loyalty plus how the council has dealt with it. The council can pay what they ask or
/// haggle; a company that dislikes the town may refuse to sell at all, and one insulted by an offer
/// sulks for a while.
/// </summary>
public static class Intel
{
    /// <summary>Appended to the ledger line of a party that keeps its report.</summary>
    public const string Keeping = " No council bounty paid for the trip, so they keep what they saw to themselves unless the town buys it.";

    /// <summary>Why a company keeps its report: no bounty at all, or one somebody other than the council put up.</summary>
    public static string KeepingFor(Party party) =>
        party.BountyId != null ? " Their bounty wasn't the council's, so they keep what they saw to themselves unless the town buys it." : Keeping;

    /// <summary>Whether the council paid for this trip with a bounty from the treasury.</summary>
    public static bool Commissioned(Town town, Party party) =>
        (party.BountyId is int bid && town.Bounties[bid - 1] is { FunderId: null, PatronId: null }) || party.SurveyId != null;

    public static CompanyState Company(Town town, string name)
    {
        var c = town.Companies.FirstOrDefault(x => x.Name == name);
        if (c != null) return c;
        c = new CompanyState { Name = name };
        town.Companies.Add(c);
        return c;
    }

    public static CompanyState? Find(Town town, string name) => town.Companies.FirstOrDefault(x => x.Name == name);

    /// <summary>Shift the council's standing with a company.</summary>
    public static void Deal(Town town, string name, double points)
    {
        var c = Company(town, name);
        double max = town.Content.World.Intel.MaxDealings;
        c.Dealings = Math.Clamp(c.Dealings + points, -max, max);
    }

    /// <summary>Heroes riding under a company's name today, strongest first.</summary>
    public static List<Agent> Members(Town town, string name) =>
        town.Heroes.Where(h => h.Hero!.PartyName == name && !h.Departed && !h.Dead).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();

    /// <summary>Every company with a member still riding, friendliest first.</summary>
    public static List<(string Name, List<Agent> Members)> MembersByCompany(Town town) =>
        town.Heroes.Where(h => !h.Departed && !h.Dead && h.Hero!.PartyName != null).Select(h => h.Hero!.PartyName!)
            .Concat(town.Companies.Select(c => c.Name))
            .Distinct()
            .Select(n => (Name: n, Members: Members(town, n)))
            .Where(x => x.Members.Count > 0)
            .OrderByDescending(x => Relation(town, x.Name, x.Members))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>What these heroes, as this company, think of the town, 0 to 100.</summary>
    public static double Relation(Town town, string name, IReadOnlyList<Agent> members)
    {
        double loyalty = members.Count > 0 ? Js.Sum(members, m => m.Hero!.Loyalty) / members.Count : town.Content.Loyalty.Start;
        return Math.Clamp(loyalty + (Find(town, name)?.Dealings ?? 0), 0, 100);
    }

    public static double Relation(Town town, string name) => Relation(town, name, Members(town, name));

    /// <summary>The members of a returned party who are still around to tell it.</summary>
    public static List<Agent> Tellers(Town town, Party party) =>
        party.Members.Select(town.Agent).Where(a => !a.Dead && !a.Departed).ToList();

    /// <summary>Reports the town could buy: withheld, unsold, not forgotten, someone left to tell them.</summary>
    public static List<Party> ForSale(Town town)
    {
        int forget = town.Content.World.Intel.ForgetDays * SimTime.HoursPerDay;
        return town.Parties
            .Where(p => p.Returned && p.Withheld && p.SoldTick == null && town.Tick - p.ReturnTick <= forget && Tellers(town, p).Count > 0)
            .OrderByDescending(p => p.ReturnTick)
            .ThenBy(p => p.Id)
            .ToList();
    }

    /// <summary>The report describes a region the town has never had described.</summary>
    public static bool NewRegion(Town town, Party party) =>
        party.RecalledTick == null && World.RegionOf(town, Quests.Site(town, party.SiteId)!) is { Visited: false };

    /// <summary>Nothing the town has heard of the site since the party came home: the report would still be news.</summary>
    private static bool Newer(SiteState site, Party party) =>
        (site.ReportTick ?? -1) < party.ReturnTick && (!site.ThoughtCleared || (site.ClearedTick ?? -1) < party.ReturnTick);

    /// <summary>The report sets the town right about the site: first-hand word it lacks, or belief the report contradicts.</summary>
    public static bool Corrects(Town town, Party party)
    {
        if (party.RecalledTick != null) return false;
        var site = Quests.Site(town, party.SiteId)!;
        return Newer(site, party) && (site.Known != SiteKnowledge.Confirmed || site.ThoughtCleared != party.SawCleared);
    }

    /// <summary>Gold of new ground in a report: country never described, and a site the town has wrong.</summary>
    public static double NewGround(Town town, Party party)
    {
        var r = town.Content.World.Intel;
        var site = Quests.Site(town, party.SiteId)!;
        double gold = 0;
        if (NewRegion(town, party)) gold += r.NewRegion + r.PerRing * World.RegionOf(town, site)!.Ring;
        if (Corrects(town, party)) gold += r.NewSite + r.PerTier * Math.Max(1, site.Def.Tier);
        return gold;
    }

    /// <summary>The report shows the town country never described, or a site it has no first-hand word on.</summary>
    public static bool Unknown(Town town, Party party) =>
        NewRegion(town, party) || (Corrects(town, party) && Quests.Site(town, party.SiteId)!.Known != SiteKnowledge.Confirmed);

    /// <summary>Share of its worth a report keeps with age.</summary>
    public static double Freshness(Town town, Party party)
    {
        var r = town.Content.World.Intel;
        double days = (town.Tick - party.ReturnTick) / (double)SimTime.HoursPerDay;
        return Math.Max(r.MinFresh, 1 - days / r.FreshDays);
    }

    /// <summary>What the report is worth to the town.</summary>
    public static double Worth(Town town, Party party) =>
        (town.Content.World.Intel.Base + NewGround(town, party)) * Freshness(town, party);

    /// <summary>What the company asks for it: friends sell cheap, the disaffected dear, the greedy dearer.</summary>
    public static double Ask(Town town, Party party)
    {
        var r = town.Content.World.Intel;
        var tellers = Tellers(town, party);
        double rel = Relation(town, party.Name, tellers);
        double markup = r.Hostile + (r.Friendly - r.Hostile) * rel / 100 + (tellers.Count > 0 && tellers[0].Has(Trait.Greedy) ? r.Greedy : 0) + Notoriety.IntelMarkup(town, party.Name);
        return Math.Max(1, Js.Round(Worth(town, party) * markup));
    }

    /// <summary>Why the company won't sell today, or null if it will.</summary>
    public static string? WontSell(Town town, Party party)
    {
        var c = Find(town, party.Name);
        if (c?.SulkUntil is int until && town.Tick < until) return "still sore about the last offer";
        if (Relation(town, party.Name, Tellers(town, party)) < town.Content.World.Intel.RefuseBelow) return "won't deal with the council";
        return null;
    }

    public static double HaggleOffer(Town town, Party party) => Math.Max(1, Js.Round(Ask(town, party) * town.Content.World.Intel.Haggle));

    /// <summary>
    /// Buy a company's report: at its asking price, or haggled down (the company may turn the offer
    /// down, and sulk). Null when it is not for sale, the company won't deal, or the treasury is short.
    /// </summary>
    /// <summary>The haggling DC at which a mayor with no bonus succeeds with the given chance.</summary>
    public static int HaggleDc(double chance) => Math.Clamp(21 - (int)Math.Round(chance * 20), 2, 21);

    public static IntelDeal? Buy(Town town, int partyId, bool haggle)
    {
        var r = town.Content.World.Intel;
        var party = town.Parties.FirstOrDefault(p => p.Id == partyId);
        if (party == null || !ForSale(town).Contains(party) || WontSell(town, party) != null) return null;
        if (Stewards.Intercept(town, "bounties", new StewardAsk { Area = "bounties", Kind = "intel", Title = $"Buy {party.Name}'s report · {Js.Str(Ask(town, party))}g", Why = "• They have seen ground the town hasn't\n• Reports reveal sites in the fog", I1 = partyId, Cost = Ask(town, party) })) return null;
        double ask = Ask(town, party);
        double price = haggle ? HaggleOffer(town, party) : ask;
        if (town.Treasury < price) return null;
        var tellers = Tellers(town, party);
        var company = Company(town, party.Name);
        var site = Quests.Site(town, party.SiteId)!;
        if (haggle)
        {
            double rel = Relation(town, party.Name, tellers);
            double chance = Math.Clamp((rel - r.RefuseBelow) / (100 - r.RefuseBelow), 0, 1) - (tellers[0].Has(Trait.Greedy) ? 0.2 : 0);
            // The mayor haggles in person: a Persuasion check, at a DC an untrained mayor would make that often.
            var check = Sheet.Decide(town, "haggle", HaggleDc(chance));
            if (check != null ? !Sheet.Succeeded(check) : !town.Rng.Chance(chance))
            {
                Deal(town, party.Name, r.Insult);
                company.Refusals++;
                company.SulkUntil = town.Tick + r.SulkDays * SimTime.HoursPerDay;
                town.Log(
                    LedgerCategory.Quest,
                    Importance.Notable,
                    $"{tellers[0].GivenName} of the {party.Name} laughs at the council's {Js.Str(price)} gold for their account of {site.Def.Name}. \"{Js.Str(ask)}, or find out for yourselves.\"",
                    tellers.Select(m => m.Id).ToList());
                return IntelDeal.Refused;
            }
        }
        town.Book(TreasuryAccount.Intel, -price);
        double share = price / tellers.Count;
        foreach (var m in tellers)
        {
            m.Gold += share;
            m.Hero!.Pay += share;
        }
        Deal(town, party.Name, haggle ? r.Grumble : r.Fair);
        company.ReportsSold++;
        company.GoldPaid += price;
        party.SoldTick = town.Tick;
        party.SoldFor = price;
        Learn(town, party);
        var region = World.RegionOf(town, site);
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"The council pays the {party.Name} {Js.Str(price)} gold{(haggle ? ", haggled down from " + Js.Str(ask) + "," : "")} for their account of the road to {site.Def.Name}{(region != null ? $" in {region.Name}" : "")}, and the clerks ink it onto the town's map.",
            tellers.Select(m => m.Id).ToList());
        return haggle ? IntelDeal.Haggled : IntelDeal.Bought;
    }

    /// <summary>A bought report reaches the council, as of the day the party came home.</summary>
    private static void Learn(Town town, Party party)
    {
        party.Withheld = false;
        if (party.RecalledTick != null) return;
        var site = Quests.Site(town, party.SiteId)!;
        // A newer first-hand word on the site stands; the old report only fills in what the town lacks.
        if (Newer(site, party))
            Rumors.Confirm(town, site, $"the {party.Name}", party.SawCleared, party.ReturnTick);
        if (World.RegionOf(town, site) is Region region) World.Describe(town, party, region);
    }

    /// <summary>The stand-in mayor buys reports of country or sites the town knows nothing first-hand about, while the treasury allows.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        if (!autoMayor) return;
        var r = town.Content.World.Intel;
        foreach (var p in ForSale(town).OrderBy(p => p.ReturnTick).ThenBy(p => p.Id))
        {
            if (!Unknown(town, p) || WontSell(town, p) != null) continue;
            if (town.Treasury < Ask(town, p) + r.Reserve) continue;
            Buy(town, p.Id, haggle: false);
        }
    }
}
