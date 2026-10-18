namespace CrownAndCompass.Sim.Systems;

/// <summary>Sending-stone quests (content/game/sendings.json): when parties out on bounties ask the mayor how to proceed.</summary>
public sealed record SendingRules
{
    /// <summary>Chance a day that a party on the road with a spell slot to spare asks the mayor something.</summary>
    public double Chance { get; init; } = 0.25;

    /// <summary>Sendings a party sends on one venture, at most, before aids add more.</summary>
    public int PerTrip { get; init; } = 1;

    /// <summary>Hours a party must still be from the site (or from home) for a sending to open, besides any waits in the quest.</summary>
    public int LeadHours { get; init; } = 10;

    /// <summary>Hours the mayor has to answer before the party does as it thinks best.</summary>
    public int DueHours { get; init; } = 36;

    /// <summary>Share of the party's best hand at a check that it adds to the mayor's roll (rounded down): help over the stone, not the hero rolling.</summary>
    public double PartyShare { get; init; } = 0.5;

    /// <summary>Which of the party's delving skills (trap, hazard, puzzle, parley, perception) helps with each of the mayor's skills.</summary>
    public Dictionary<string, string> Kinds { get; init; } = [];

    /// <summary>Town buildings that help the mayor answer: a bonus on the rolls and, through option needs, more choices.</summary>
    public List<SendingAid> Aids { get; init; } = [];

    /// <summary>The quests: stories of source "sending", each bound to the party that sent it.</summary>
    public List<StoryDef> Stories { get; init; } = [];
}

/// <summary>A building that helps answer a sending: its bonus (on some skills, or all), and more sendings a trip.</summary>
public sealed record SendingAid
{
    public string Building { get; init; } = "";

    /// <summary>How the roll names it ("the Adventurers' Hall's maps").</summary>
    public string Name { get; init; } = "";
    public int Bonus { get; init; }

    /// <summary>The skills it helps with; empty for every skill.</summary>
    public List<string> Skills { get; init; } = [];

    /// <summary>More sendings each party sends on a venture while it stands (contacts, a stone of its own).</summary>
    public int Trips { get; init; }
}

/// <summary>Who helps with a check besides the mayor: the party's best hand at it, and the town's buildings.</summary>
public sealed record Help(string Name, int Bonus);

/// <summary>
/// Sendings: the one way the town hears from a party out in the fog. A party with a spell slot to spare
/// spends it on the sending stone to ask the mayor how to proceed: a one-off question, or a quest that
/// branches over several sendings. The card pauses the game and must be answered. Each choice is a d20
/// check of the mayor's sheet plus the party's best hand at it (their ability, class training and gear
/// for the matching delving skill) plus the town's aids (the Adventurers' Hall and others in
/// sendings.json), which also unlock choices through option needs (building:id). What comes of it
/// changes the venture: an edge on the party's checks and blows at the site, more or fewer foes, hurt
/// or rested heroes, spell slots spent, gold found, a party turned back. None of it reveals the map:
/// only what the party says comes home early.
/// </summary>
public static class Sendings
{
    public const string Source = "sending";

    private static SendingRules Rules(Town town) => town.Content.Sendings;

    /// <summary>Whether a story is a sending: it pauses the game and must be answered.</summary>
    public static bool Is(StoryDef d) => d.Source == Source;

    public static bool Is(Town town, Story s) => Is(Stories.DefOf(town, s));

    /// <summary>Sendings waiting on the mayor now, oldest first.</summary>
    public static List<Story> Waiting(Town town) => Stories.Pending(town).Where(s => Is(town, s)).ToList();

    /// <summary>The party a story is bound to, while it is still out.</summary>
    public static Party? PartyOf(Town town, Story s) =>
        s.PartyId is int id && town.Parties.FirstOrDefault(p => p.Id == id) is { Returned: false } p ? p : null;

    /// <summary>Sendings a party may send on one venture: the base, plus what the town's aids add.</summary>
    public static int Trips(Town town) => Rules(town).PerTrip + Standing(town).Sum(a => a.Trips);

    /// <summary>The aids standing in town (completed and open), each once.</summary>
    public static List<SendingAid> Standing(Town town) =>
        Rules(town).Aids.Where(a => town.Buildings.Any(b => b.Def.Id == a.Building && b.Complete && !b.Closed)).ToList();

    /// <summary>
    /// Who helps with a check on a sending: the party member best at the matching delving skill, then
    /// each aid in town that helps with the skill. Empty for a story with no party out on it.
    /// </summary>
    public static List<Help> Helpers(Town town, Story s, StoryOption o)
    {
        var list = new List<Help>();
        if (!o.Rolls || PartyOf(town, s) is not { } party) return list;
        string skill = o.Skill;
        string ability = skill != "" ? town.Content.Stories.Skill(skill)?.Ability ?? o.Ability : o.Ability;
        string kind = Rules(town).Kinds.GetValueOrDefault(skill, "");
        Agent? best = null;
        int bestBonus = int.MinValue;
        foreach (int id in party.Members)
        {
            var a = town.Agent(id);
            if (a.Dead || a.Hero == null) continue;
            int b = Expedition.HeroBonus(town.Content, a, ability, kind);
            if (b > bestBonus)
            {
                best = a;
                bestBonus = b;
            }
        }
        if (best != null && (int)Math.Floor(bestBonus * Rules(town).PartyShare) is var share and not 0) list.Add(new Help(best.GivenName, share));
        foreach (var aid in Standing(town))
            if (aid.Bonus != 0 && (aid.Skills.Count == 0 || aid.Skills.Contains(skill))) list.Add(new Help(aid.Name, aid.Bonus));
        return list;
    }

    public static int HelpTotal(Town town, Story s, StoryOption o) => Helpers(town, s, o).Sum(h => h.Bonus);

    /// <summary>Hours a quest needs before the party gets where it is going: its waits, and the lead.</summary>
    private static int Lead(Town town, StoryDef d) =>
        Rules(town).LeadHours + d.Stages.Sum(st => st.Options.Select(o => Math.Max(o.Success.After, Math.Max(o.Failure.After, o.Partial?.After ?? 0))).DefaultIfEmpty(0).Max());

    /// <summary>
    /// Hourly: a party on a bounty with a slot to spare may send word (a day's chance spread over the
    /// hours), and quests waiting on the party's next sending come back. The stand-in answers at once.
    /// </summary>
    public static void Hourly(Town town, bool autoMayor)
    {
        var r = Rules(town);
        if (r.Stories.Count > 0 && town.Outcome == null)
        {
            var busy = town.Stories.Open.Where(s => s.PartyId != null).Select(s => s.PartyId!.Value).ToHashSet();
            int trips = Trips(town);
            foreach (var party in town.Parties.OrderBy(p => p.Id))
            {
                if (party.Returned || party.BountyId == null || party.RecalledTick != null || busy.Contains(party.Id)) continue;
                if (party.Asked >= trips || Knowledge.SlotsLeft(town, party) == 0) continue;
                if (!town.Rng.Chance(r.Chance / SimTime.HoursPerDay)) continue;
                Open(town, party);
            }
        }
        if (autoMayor)
            foreach (var s in Waiting(town)) Stories.Answer(town, s);
    }

    /// <summary>Open a sending from this party now, if one fits where it is; tests and scripted shots call it directly.</summary>
    public static Story? Open(Town town, Party party, string? defId = null)
    {
        var r = Rules(town);
        var fits = new List<(StoryDef Def, Story Bound)>();
        foreach (var d in r.Stories)
        {
            if (defId != null && d.Id != defId) continue;
            if (town.Day < d.MinDay || town.Population < d.MinPopulation) continue;
            if (defId == null && town.Stories.LastDay.Get(d.Id, -9999) + d.RepeatDays > town.Day) continue;
            int left = Abroad(town, party) ? party.ArriveTick - town.Tick : Homeward(town, party) ? party.ReturnTick - town.Tick : 0;
            if (left < Lead(town, d)) continue;
            var seed = new Story { PartyId = party.Id, HeroId = party.Members.FirstOrDefault(id => !town.Agent(id).Dead), SiteId = party.SiteId };
            if (Stories.BindFrom(town, d, seed) is { } bound) fits.Add((d, bound));
        }
        if (fits.Count == 0) return null;
        double total = Js.Sum(fits, f => f.Def.Weight);
        double roll = town.Rng.Next() * total;
        var pick = fits[^1];
        foreach (var f in fits)
        {
            roll -= f.Def.Weight;
            if (roll < 0)
            {
                pick = f;
                break;
            }
        }
        party.Asked++;
        party.Sendings++;
        return Stories.BeginSending(town, pick.Def, pick.Bound, r.DueHours);
    }

    /// <summary>On the road out, not yet at the site.</summary>
    public static bool Abroad(Town town, Party p) => !p.Returned && !p.Resolved && p.RecalledTick == null && town.Tick < p.ArriveTick;

    /// <summary>On the road home from the site, alive.</summary>
    public static bool Homeward(Town town, Party p) =>
        !p.Returned && p.Resolved && !p.Empty && p.RecalledTick == null && p.Result is { Outcome: not ExpeditionOutcome.Wiped } && town.Tick >= p.LeaveTick;

    // ---------- Effects on the party ----------

    /// <summary>Apply a sending's effect on its party; returns what the Ledger says of it, or "".</summary>
    public static string Apply(Town town, Story s, string key, double v)
    {
        if (PartyOf(town, s) is not { } party) return "";
        var members = party.Members.Select(town.Agent).Where(a => !a.Dead && a.Hero != null).ToList();
        switch (key)
        {
            case "edge":
                if (party.Resolved) return "";
                party.Edge += (int)v;
                return v > 0 ? $"the {party.Name} go in with an edge" : $"the {party.Name} go in off balance";
            case "swell":
                if (party.Resolved) return "";
                party.Swell += (int)v;
                return v > 0 ? "more foes waiting" : "fewer foes waiting";
            case "partyHurt":
                foreach (var a in members) a.Hero!.Hp = Math.Max(1, a.Hero.Hp - (int)v);
                return $"the {party.Name} hurt";
            case "rest":
                foreach (var a in members) a.Hero!.Hp = Math.Min(a.Hero.MaxHp, a.Hero.Hp + (int)v);
                return $"the {party.Name} rested";
            case "slots":
                party.Sendings += (int)v;
                return $"{Js.Str(v)} spell {(v == 1 ? "slot" : "slots")} spent";
            case "loot":
                party.Bonus += v;
                return $"+{Js.Str(v)} gold for the {party.Name}";
            case "partyLoyalty":
                foreach (var a in members) a.Hero!.Loyalty = Math.Clamp(a.Hero.Loyalty + v, 0, 100);
                return $"the {party.Name}{(party.Name.EndsWith('s') ? "'" : "'s")} loyalty {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}";
            case "turnBack":
                if (v <= 0 || !Knowledge.CanRecall(town, party)) return "";
                Knowledge.TurnBack(town, party);
                return $"the {party.Name} turn for home";
        }
        return "";
    }
}
