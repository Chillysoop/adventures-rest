namespace CrownAndCompass.Sim.Systems;

/// <summary>A commissioned scrying: live sight of one site until EndTick.</summary>
public sealed record Scrying(string SiteId, int CasterId, int StartTick, int EndTick);

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

    /// <summary>Casters in town able to scry, strongest first.</summary>
    public static List<Agent> Scryers(Town town) =>
        town.Heroes
            .Where(a => !a.IsAway && IsScryer(town, a) && a.Hero!.Level >= town.Content.Knowledge.ScryMinLevel)
            .OrderByDescending(a => a.Hero!.Level)
            .ThenBy(a => a.Id)
            .ToList();

    public static double ScryCost(Town town, SiteDef site) =>
        town.Content.Knowledge.ScryBaseCost + town.Content.Knowledge.ScryCostPerDay * site.Distance;

    public static Scrying? ActiveScrying(Town town, string siteId) =>
        town.Scryings.LastOrDefault(s => s.SiteId == siteId && town.Tick >= s.StartTick && town.Tick < s.EndTick);

    /// <summary>
    /// Scry a site: free from the Scrying Pool while it has a scrying left today, otherwise paid to the
    /// strongest caster in town. Null when nobody can or the treasury is short.
    /// </summary>
    public static Scrying? CommissionScrying(Town town, string siteId)
    {
        var site = Quests.Site(town, siteId);
        if (site == null || ActiveScrying(town, siteId) != null) return null;
        int hours = town.Content.Knowledge.ScryHours;
        if (Watch.FreeScryingsLeft(town) > 0)
        {
            town.FreeScryingsUsed++;
            var free = new Scrying(siteId, 0, town.Tick, town.Tick + hours * SimTime.HoursPerDay / 24);
            town.Scryings.Add(free);
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The water in the Scrying Pool stills and shows {site.Def.Name}. For {hours} hours the town can watch it.");
            return free;
        }
        var caster = Scryers(town).FirstOrDefault();
        double cost = ScryCost(town, site.Def);
        if (caster == null || town.Treasury < cost) return null;

        town.Book(TreasuryAccount.Scrying, -cost);
        caster.Gold += cost;
        var scry = new Scrying(siteId, caster.Id, town.Tick, town.Tick + hours * SimTime.HoursPerDay / 24);
        town.Scryings.Add(scry);
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"{caster.Name} the {caster.Hero!.Cls.Name} takes {Js.Str(Js.Round(cost))} gold to scry {site.Def.Name}. For {hours} hours the town can watch it.",
            [caster.Id]);
        return scry;
    }

    /// <summary>Spell slots a party carries for sendings: each caster's base slots plus more with level.</summary>
    public static int Slots(Town town, Party party)
    {
        var k = town.Content.Knowledge;
        int slots = 0;
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
        var site = Quests.Site(town, party.SiteId)!;
        int elapsed = town.Tick - party.DepartTick;
        int outbound = site.Def.Distance * SimTime.HoursPerDay;
        if (party.RecalledTick != null || elapsed >= outbound + SimTime.HoursPerDay) return Leg.Homeward;
        return elapsed < outbound ? Leg.Outbound : Leg.AtSite;
    }

    /// <summary>A party can still be called home until it goes into the site.</summary>
    public static bool CanRecall(Town town, Party party) => !party.Returned && party.RecalledTick == null && LegOf(town, party) == Leg.Outbound;

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
            int walked = town.Tick - party.DepartTick;
            party.RecalledTick = town.Tick;
            party.ReturnTick = town.Tick + walked;
            foreach (int id in party.Members) town.Agent(id).Hero!.AwayUntil = party.ReturnTick;
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
    internal static void ComeHome(Town town, Party party, SiteState site, Bounty bounty, List<Agent> members)
    {
        foreach (var m in members) m.Hero!.AwayUntil = null;
        site.Attempts = Math.Max(0, site.Attempts - 1);
        bounty.Status = BountyStatus.Open;
        bounty.PartyId = null;
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"The {party.Name} walk back into town, called home from the road to {site.Def.Name}. The bounty is posted again.",
            members.Select(m => m.Id).ToList());
    }
}
