namespace CrownAndCompass.Sim.Systems;

/// <summary>A commissioned scrying: live sight of one site until EndTick.</summary>
public sealed record Scrying(string SiteId, int CasterId, int StartTick, int EndTick);

/// <summary>What a caster asks for a scrying, by their loyalty to the town.</summary>
public enum ScryPrice
{
    Free,
    Gold,
    Favour,
}

/// <summary>One caster's offer to scry a site: free, for gold, or as a favour the town will owe.</summary>
public sealed record ScryOffer(Agent Caster, ScryPrice Price, double Gold);

/// <summary>Where a party is on its trip, as the town would reckon it from the calendar.</summary>
public enum Leg
{
    Outbound,
    AtSite,
    Homeward,
}

/// <summary>
/// The fog of war's levers. Adventuring happens out of sight: a party that leaves town is gone
/// until it returns to report. The mayor can pay a caster in town to scry a site, which shows it
/// live for a few hours, and can reach a party on the road by sending stone, which costs one of
/// its casters' spell slots (a cleric then has one heal fewer in the dungeon).
/// </summary>
public static class Knowledge
{
    /// <summary>Whether a hero's class can cast a library spell.</summary>
    public static bool Casts(Town town, Agent a, string spellId) =>
        a.Hero != null && (town.Content.Library.Spell(spellId)?.Classes.Contains(a.Hero.Cls.Id) ?? false);

    private static bool IsScryer(Town town, Agent a) => Casts(town, a, town.Content.Knowledge.ScrySpell);

    private static bool IsSender(Town town, Agent a) => Casts(town, a, town.Content.Knowledge.SendSpell);

    /// <summary>Whether the hero can cast a sending themselves.</summary>
    public static bool Sends(Town town, Agent a) => IsSender(town, a);

    /// <summary>Casters in town able to scry, strongest first.</summary>
    public static List<Agent> Scryers(Town town) =>
        town.Heroes
            .Where(a => !a.IsAway && IsScryer(town, a) && a.Hero!.Level >= town.Content.Knowledge.ScryMinLevel)
            .OrderByDescending(a => a.Hero!.Level)
            .ThenBy(a => a.Id)
            .ToList();

    public static double ScryCost(Town town, SiteDef site) =>
        Js.Round((town.Content.Knowledge.ScryBaseCost + town.Content.Knowledge.ScryCostPerDay * site.Distance) * (1 - Research.Scry(town)));

    public static Scrying? ActiveScrying(Town town, string siteId) =>
        town.Scryings.LastOrDefault(s => s.SiteId == siteId && town.Tick >= s.StartTick && town.Tick < s.EndTick);

    /// <summary>Scryers rested enough to cast today: each needs a few days after a scrying, so more casters mean more scryings.</summary>
    public static List<Agent> Rested(Town town) => Scryers(town).Where(a => (a.Hero!.ScryRestUntil ?? 0) <= town.Tick).ToList();

    /// <summary>Scryings the town can call on now: the pool's free ones left today and every rested caster.</summary>
    public static int Ready(Town town) => Watch.FreeScryingsLeft(town) + Rested(town).Count;

    /// <summary>What this caster asks: the loyal scry for nothing, the rest for gold, and the disaffected only as a favour.</summary>
    public static ScryOffer Offer(Town town, Agent caster, SiteDef site)
    {
        var p = town.Content.Knowledge.ScryPrice;
        double loyalty = caster.Hero!.Loyalty;
        if (loyalty >= p.Free) return new ScryOffer(caster, ScryPrice.Free, 0);
        if (loyalty >= p.Favour) return new ScryOffer(caster, ScryPrice.Gold, ScryCost(town, site));
        return new ScryOffer(caster, ScryPrice.Favour, 0);
    }

    /// <summary>Every rested caster's offer for a site, cheapest first (free, gold, a favour), then the strongest.</summary>
    public static List<ScryOffer> Offers(Town town, SiteDef site) =>
        Rested(town).Select(a => Offer(town, a, site)).OrderBy(o => o.Price).ThenByDescending(o => o.Caster.Hero!.Level).ThenBy(o => o.Caster.Id).ToList();

    /// <summary>
    /// Scry a site: free from the Scrying Pool while it has a scrying left today, otherwise by the rested caster
    /// asking least (or the one named), on their terms. Null when nobody can or the treasury is short.
    /// </summary>
    public static Scrying? CommissionScrying(Town town, string siteId, int? casterId = null)
    {
        var site = Quests.Site(town, siteId);
        // A scryer needs a name and a direction to look in; the unheard-of stay hidden.
        if (site == null || site.Known == SiteKnowledge.Unheard || ActiveScrying(town, siteId) != null) return null;
        var k = town.Content.Knowledge;
        int hours = k.ScryHours;
        int end = town.Tick + hours * SimTime.HoursPerDay / 24;
        if (casterId == null && Watch.FreeScryingsLeft(town) > 0)
        {
            town.FreeScryingsUsed++;
            Discovery.AtSite(town, siteId, Discovery.Scried);
            var free = new Scrying(siteId, 0, town.Tick, end);
            town.Scryings.Add(free);
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The water in the Scrying Pool stills and shows {site.Def.Name}. For {hours} hours the town can watch it.");
            return free;
        }
        var offer = Offers(town, site.Def).FirstOrDefault(o => casterId == null || o.Caster.Id == casterId);
        if (offer == null || town.Treasury < offer.Gold) return null;

        var caster = offer.Caster;
        caster.Hero!.ScryRestUntil = town.Tick + k.ScryRestDays * SimTime.HoursPerDay;
        if (offer.Gold > 0)
        {
            town.Book(TreasuryAccount.Scrying, -offer.Gold);
            caster.Gold += offer.Gold;
        }
        if (offer.Price == ScryPrice.Favour)
        {
            Loyalty.Sway(caster, -k.ScryPrice.FavourLoyalty);
            // The favour is called in later as a story (scryer-favour).
            caster.Hero.Owed++;
        }
        var scry = new Scrying(siteId, caster.Id, town.Tick, end);
        town.Scryings.Add(scry);
        Discovery.AtSite(town, siteId, Discovery.Scried);
        string who = $"{caster.Name} the {caster.Hero.Cls.Name}";
        string terms = offer.Price switch
        {
            ScryPrice.Free => $"{who} scries {site.Def.Name} for the town and asks nothing for it.",
            ScryPrice.Gold => $"{who} takes {Js.Str(Js.Round(offer.Gold))} gold to scry {site.Def.Name}.",
            _ => $"{who} scries {site.Def.Name} as a favour, and lets the mayor know it is one.",
        };
        town.Log(LedgerCategory.Quest, Importance.Notable, $"{terms} For {hours} hours the town can watch it.", [caster.Id]);
        return scry;
    }

    /// <summary>Spell slots a party carries for sendings: each caster's base slots plus more with level, and a town sending stone's.</summary>
    public static int Slots(Town town, Party party)
    {
        var k = town.Content.Knowledge;
        int slots = Armory.StoneSlots(town, party);
        foreach (int id in party.Members)
        {
            var a = town.Agent(id);
            if (IsSender(town, a)) slots += k.SlotsPerCaster + k.SlotsPerTwoLevels * (a.Hero!.Level / 2);
        }
        return slots;
    }

    public static int SlotsLeft(Town town, Party party) => Math.Max(0, Slots(town, party) - party.Sendings);

    public static Leg LegOf(Town town, Party party)
    {
        if (party.RecalledTick != null || (party.Resolved && town.Tick >= party.LeaveTick)) return Leg.Homeward;
        return town.Tick < party.ArriveTick ? Leg.Outbound : Leg.AtSite;
    }

    /// <summary>A party can still be called home until it goes into the site.</summary>
    public static bool CanRecall(Town town, Party party) => !party.Returned && party.RecalledTick == null && LegOf(town, party) == Leg.Outbound;

    /// <summary>The party turns round where it stands and walks home.</summary>
    internal static void TurnBack(Town town, Party party)
    {
        party.RecalledTick = town.Tick;
        Roads.Recall(town, party);
        foreach (int id in party.Members) town.Agent(id).Hero!.AwayUntil = party.ReturnTick;
    }

    /// <summary>
    /// Send a message by sending stone to a party on the road. With recall, the party turns back;
    /// otherwise it answers with where it is. Returns the answer, or null when the party is home or
    /// has no spell slot left.
    /// </summary>
    public static string? Send(Town town, int partyId, bool recall)
    {
        var party = town.Parties.FirstOrDefault(p => p.Id == partyId);
        if (party == null || party.Returned || SlotsLeft(town, party) == 0) return null;
        if (recall && !CanRecall(town, party)) return null;

        var site = Quests.Site(town, party.SiteId)!;
        var leader = town.Agent(party.Members[0]);
        party.Sendings++;
        string answer;
        if (recall)
        {
            TurnBack(town, party);
            answer = $"\"Understood. Turning back from the road to {site.Def.Name}.\"";
        }
        else
        {
            int daysOut = (int)Math.Ceiling((party.ReturnTick - town.Tick) / (double)SimTime.HoursPerDay);
            answer = LegOf(town, party) switch
            {
                Leg.Outbound => $"\"On the road to {site.Def.Name}. All hale. Home in {daysOut} {(daysOut == 1 ? "day" : "days")} if the gods allow.\"",
                Leg.AtSite => $"\"At {site.Def.Name}. We go in at first light. Pray for us.\"",
                _ => $"\"Turning for home from {site.Def.Name}. We will tell it all at the tavern.\"",
            };
        }
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"The mayor's sending stone reaches the {party.Name}. {leader.GivenName} answers: {answer}",
            party.Members);
        return answer;
    }

    /// <summary>A recalled party walks home without setting foot in the site; the bounty goes back on the board.</summary>
    internal static void ComeHome(Town town, Party party, SiteState site, Bounty? bounty, List<Agent> members)
    {
        foreach (var m in members) m.Hero!.AwayUntil = null;
        site.Attempts = Math.Max(0, site.Attempts - 1);
        if (bounty != null)
        {
            bounty.Status = BountyStatus.Open;
            bounty.PartyId = null;
        }
        if (party.PleaId is int pid) World.Reopen(town.Pleas[pid - 1]);
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"The {party.Name} walk back into town, called home from the road to {site.Def.Name}." + (bounty != null ? " The bounty is posted again." : "") + (party.Withheld ? Intel.Keeping : ""),
            members.Select(m => m.Id).ToList());
    }
}
