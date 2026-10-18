namespace CrownAndCompass.Sim.Systems;

/// <summary>What the town knows of a site: nothing, tavern talk, or a first-hand report or sight.</summary>
public enum SiteKnowledge
{
    Unheard,
    Rumoured,
    Confirmed,
}

/// <summary>
/// A site as it really is (Def, Cleared) and as the town believes it to be (Known, Believed,
/// BelievedTier, ThoughtCleared). Only a party's report or live sight brings belief back to the truth.
/// </summary>
public sealed class SiteState(SiteDef def)
{
    public SiteDef Def { get; set; } = def;
    public bool Cleared { get; set; }
    public int Attempts { get; set; }

    /// <summary>The region of the wilds it lies in (<see cref="World"/>), or null for sites raised later.</summary>
    public int? RegionId { get; set; }

    /// <summary>Built by world generation rather than written by hand in sites.json.</summary>
    public bool Generated { get; init; }

    public SiteKnowledge Known { get; set; }

    /// <summary>Who the town thinks lives there ("goblins", "an ogre"), from the last rumour or report.</summary>
    public string Believed { get; set; } = "";
    public int BelievedTier { get; set; }

    /// <summary>The town thinks the site is cleared (it may have been resettled since).</summary>
    public bool ThoughtCleared { get; set; }

    /// <summary>How far the town trusts what it believes, 0 to 100: the teller's reliability, 100 once confirmed.</summary>
    public int Trust { get; set; }

    /// <summary>When it was last cleared.</summary>
    public int? ClearedTick { get; set; }

    // The lair, off-screen (see Lairs) ------------------------------------------------------------

    /// <summary>How many live there and how bold they are; swells the fights and drives what they do.</summary>
    public double Strength { get; set; }

    /// <summary>When the lair next acts on the world; null until it has woken.</summary>
    public int? NextActTick { get; set; }

    /// <summary>Raids put off while the heroes were home.</summary>
    public int Waited { get; set; }

    /// <summary>Paid tribute buys quiet until this tick.</summary>
    public int? QuietUntil { get; set; }

    /// <summary>When something new moves into the cleared site, unless the town holds it.</summary>
    public int? ResettleTick { get; set; }

    /// <summary>The town keeps an outpost here: nothing resettles it, and the town sees it live.</summary>
    public bool Held { get; set; }
}

public enum BountyStatus
{
    Open,
    Taken,
    Completed,
    Failed,
    Expired,
}

public sealed class Bounty
{
    public int Id { get; init; }
    public required string SiteId { get; init; }
    public double Reward { get; init; }
    public int PostedTick { get; init; }
    public int ExpiresTick { get; init; }
    public BountyStatus Status { get; set; } = BountyStatus.Open;
    public int? PartyId { get; set; }

    /// <summary>The building whose coffers hold the escrow (the temple), or null for the treasury.</summary>
    public int? FunderId { get; init; }
}

public sealed class Party
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<int> Members { get; init; }
    /// <summary>The bounty it took, or null for a company out on its own account.</summary>
    public int? BountyId { get; init; }

    /// <summary>The settlement's plea it is answering, if any.</summary>
    public int? PleaId { get; set; }
    public required string SiteId { get; init; }
    public int DepartTick { get; init; }
    public int ReturnTick { get; set; }
    public bool Returned { get; set; }

    /// <summary>Sending-stone messages exchanged on the road; each costs one of the party's spell slots.</summary>
    public int Sendings { get; set; }

    /// <summary>Set when a sending called the party home before it reached the site.</summary>
    public int? RecalledTick { get; set; }
}

public sealed record Chronicle(
    int Id,
    string PartyName,
    string SiteName,
    IReadOnlyList<string> Members,
    int DepartDay,
    int ReturnDay,
    ExpeditionOutcome Outcome,
    IReadOnlyList<string> Lines)
{
    public string Format()
    {
        string head = $"Chronicle {Id}: the {PartyName} at {SiteName} (days {DepartDay} to {ReturnDay}, {Outcome.ToString().ToLowerInvariant()})";
        return string.Join("\n", new[] { head }.Concat(Members.Select(m => $"  {m}")).Concat(Lines.Select(l => $"  - {l}")));
    }
}

/// <summary>
/// Heroes, the bounty board and expeditions. The mayor never sends a hero anywhere: the town
/// posts bounties with gold held in escrow, and heroes decide for themselves whether the odds and
/// the pay are worth it, forming parties with people they like.
/// </summary>
public static class Quests
{
    private const int BountyDays = 10;
    private const double Lodging = 1;
    private static readonly int[] XpLevels =
        [0, 300, 900, 2700, 6500, 14000, 23000, 34000, 48000, 64000, 85000, 100000, 120000, 140000, 165000, 195000, 225000, 265000, 305000, 355000];

    private static readonly string[] PartyAdjectives = ["Ashen", "Iron", "Crimson", "Silver", "Gallows", "Lucky", "Broken", "Midnight", "Copper", "Wandering"];
    private static readonly string[] PartyNouns = ["Blades", "Lanterns", "Owls", "Shields", "Hounds", "Crows", "Compasses", "Wolves", "Pilgrims", "Daggers"];

    public static void InitSites(Town town)
    {
        foreach (var def in town.Content.Sites)
        {
            var s = new SiteState(def);
            town.Sites.Add(s);
            // The settlers know the local menace; everything else waits for tavern talk.
            if (def.Known) Rumors.Hear(town, s, Rumors.Occupants(town.Content, def), def.Tier);
        }
    }

    public static SiteState? Site(Town town, string id) => town.Sites.FirstOrDefault(s => s.Def.Id == id);

    private static bool IsActive(Bounty b) => b.Status is BountyStatus.Open or BountyStatus.Taken;

    /// <summary>
    /// Post a bounty, holding the reward in escrow from the treasury, or from a funder's coffers (the
    /// temple). Returns null if the payer can't cover it or the town knows of no such danger.
    /// </summary>
    public static Bounty? PostBounty(Town town, string siteId, double reward, Building? funder = null)
    {
        var s = Site(town, siteId);
        double purse = funder?.Cash ?? town.Treasury;
        if (s == null || s.Known == SiteKnowledge.Unheard || s.ThoughtCleared || reward <= 0 || purse < reward) return null;
        if (town.Bounties.Any(b => b.SiteId == siteId && IsActive(b))) return null;
        if (funder != null) funder.Cash -= reward;
        else town.Book(TreasuryAccount.Bounties, -reward);
        var b = new Bounty
        {
            Id = town.Bounties.Count + 1,
            SiteId = siteId,
            Reward = reward,
            PostedTick = town.Tick,
            ExpiresTick = town.Tick + BountyDays * SimTime.HoursPerDay,
            FunderId = funder?.Id,
        };
        town.Bounties.Add(b);
        if (funder == null) town.Log(LedgerCategory.Town, Importance.Notable, $"The council posts a bounty of {Js.Str(reward)} gold on {s.Def.Name}.");
        return b;
    }

    /// <summary>Hand an unearned reward back to whoever put it up.</summary>
    private static void Refund(Town town, Bounty b)
    {
        if (b.FunderId != null) town.Building(b.FunderId.Value).Cash += b.Reward;
        else town.Book(TreasuryAccount.Bounties, b.Reward);
    }

    public static int SuggestedReward(int tier) => 50 * tier + 30 * tier * tier;

    public static Agent CreateHero(Town town, ClassDef cls, int level, string? race = null)
    {
        var a = Population.CreateAgent(town, new NewAgentOptions { Profession = cls.Name, Gold = town.Rng.Range(10, 40), Race = race });
        a.Ambition = Ambition.None;
        a.DreamBusiness = null;
        // Heroes favor their class's key ability: reroll it once and keep the better.
        int reroll = Abilities.Roll(town.Rng)[cls.Primary];
        a.Abilities[cls.Primary] = Math.Max(a.Abilities[cls.Primary], reroll);
        var sheet = new HeroSheet
        {
            Cls = cls,
            Level = level,
            Xp = XpLevels[level - 1],
            MaxHp = 1,
            Hp = 1,
        };
        sheet.MaxHp = Expedition.HeroMaxHp(sheet, a.Abilities.Con);
        sheet.Hp = sheet.MaxHp;
        Gear.Equip(town.Content.Library, sheet);
        a.Hero = sheet;
        Loyalty.Arrive(town, a);
        return a;
    }

    private static ClassDef PickClass(Town town)
    {
        var classes = town.Content.Classes;
        int total = classes.Sum(c => c.Weight);
        int roll = town.Rng.Range(0, total);
        foreach (var c in classes)
        {
            if (roll < c.Weight) return c;
            roll -= c.Weight;
        }
        return classes[0];
    }

    public static void Daily(Town town, bool autoMayor, bool companies = true)
    {
        ReturnParties(town);
        HeroArrivals(town);
        LodgingAndRest(town);
        Gear.Shop(town);
        ExpireBounties(town);
        if (autoMayor) AutoPost(town);
        FormParties(town);
        if (companies) Campaigns.Daily(town);
    }

    private static void HeroArrivals(Town town)
    {
        var taverns = town.Taverns;
        if (taverns.Count == 0) return;
        int heroes = town.Heroes.Count;
        if (heroes >= City.HeroCap(town)) return;
        int open = town.Bounties.Count(b => b.Status == BountyStatus.Open);
        double chance = Math.Min(0.9, 0.12 + 0.25 * open + (heroes < 4 ? 0.2 : 0));
        if (!town.Rng.Chance(chance)) return;

        // More taverns, more talk on the roads: seasoned heroes hear of the town and come.
        double green = Math.Max(0.4, 0.7 - town.Content.Rumors.BetterHeroesPerTavern * (taverns.Count - 1));
        int level = (town.Rng.Chance(green) ? 1 : town.Rng.Chance(0.8) ? 2 : 3) + Saga.ArrivalBonus(town);
        var a = CreateHero(town, PickClass(town), level);
        string where = taverns[town.Rng.Range(0, taverns.Count)].Name;
        town.Log(LedgerCategory.Arrival, Importance.Notable, $"{a.Describe()}, walks into {where} looking for paid work. ({a.TraitList()})", [a.Id]);
    }

    private static void LodgingAndRest(Town town)
    {
        var taverns = town.Taverns;
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            if (s.AwayUntil != null) continue;
            // A bed above the tavern.
            s.Lodging = taverns.Count > 0 && a.Gold >= Lodging;
            if (s.Lodging)
            {
                var t = taverns[a.Id % taverns.Count];
                a.Gold -= Lodging;
                t.Cash += Lodging;
            }
            // A long rest heals; a bed heals faster, and the shrine or temple more.
            int healed = (int)Math.Ceiling(s.MaxHp * (s.Lodging ? 0.5 : 0.25));
            if (s.Hp < s.MaxHp) healed = Shrines.Rest(town, a, healed);
            s.Hp = Math.Min(s.MaxHp, s.Hp + healed);
        }
    }

    private static void ExpireBounties(Town town)
    {
        foreach (var b in town.Bounties)
        {
            if (b.Status != BountyStatus.Open || town.Tick < b.ExpiresTick) continue;
            b.Status = BountyStatus.Expired;
            Refund(town, b);
            town.Log(LedgerCategory.Town, Importance.Routine, $"No one takes the bounty on {Site(town, b.SiteId)!.Def.Name}; the council withdraws it.");
        }
    }

    /// <summary>Stand-in mayor: put idle treasury gold to work on the easiest uncleared site.</summary>
    private static void AutoPost(Town town)
    {
        if (town.Bounties.Any(IsActive)) return;
        if (town.Heroes.Count == 0 && town.Taverns.Count == 0) return;
        var target = town.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared && s.Trust >= 50)
            // The trouble near town, which feeds the warband, comes before the far wilds.
            .OrderBy(s => s.Generated)
            .ThenBy(s => s.BelievedTier)
            .ThenBy(s => s.Attempts)
            .FirstOrDefault();
        if (target == null) return;
        double reward = SuggestedReward(target.BelievedTier) + 20 * target.Attempts;
        if (town.Treasury < reward + 100) return;
        PostBounty(town, target.Def.Id, reward);
    }

    private static double Interest(Town town, Agent a, Bounty bounty, int partySize)
    {
        // Heroes judge a site by what the town believes of it, so a rumour that undersells it is deadly.
        int tier = Math.Max(1, Site(town, bounty.SiteId)!.BelievedTier);
        double share = bounty.Reward / Math.Max(1, partySize);
        return
            share / (tier * 40) +
            (a.Has(Trait.Greedy) ? 0.2 : 0) +
            (a.Has(Trait.HotTempered) ? 0.15 : 0) +
            (a.Has(Trait.Ambitious) ? 0.1 : 0) -
            (a.Has(Trait.Lazy) ? 0.3 : 0) +
            town.Rng.Next() * 0.3;
    }

    public static bool Available(Agent a)
    {
        var s = a.Hero!;
        return s.AwayUntil == null && s.Hp >= s.MaxHp * 0.7 && !a.Departed;
    }

    private static void FormParties(Town town)
    {
        foreach (var bounty in town.Bounties)
        {
            if (bounty.Status != BountyStatus.Open) continue;
            var s = Site(town, bounty.SiteId)!;
            int tier = Math.Max(1, s.BelievedTier);
            var pool = town.Heroes.Where(Available).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
            if (pool.Count == 0) continue;

            var leader = pool.FirstOrDefault(h => Interest(town, h, bounty, 3) >= 0.6);
            if (leader == null) continue;

            var members = new List<Agent> { leader };
            int levels = leader.Hero!.Level;
            var recruits = pool
                .Where(h => h.Id != leader.Id)
                .OrderByDescending(h => leader.OpinionOf(h.Id))
                .ThenByDescending(h => h.Hero!.Level)
                .ThenBy(h => h.Id)
                .ToList();
            foreach (var r in recruits)
            {
                if (members.Count >= 4 || (members.Count >= 3 && levels >= 3 * tier + 2)) break;
                if (leader.OpinionOf(r.Id) < -10 || r.OpinionOf(leader.Id) < -10) continue;
                if (Interest(town, r, bounty, members.Count + 1) < 0.5) continue;
                members.Add(r);
                levels += r.Hero!.Level;
            }
            // Heroes won't walk into a site they can't handle; the bounty waits for a stronger company.
            if (levels < 3 * tier || members.Count < 3) continue;

            var party = Launch(town, members, s, bounty);
            town.Log(
                LedgerCategory.Quest,
                Importance.Notable,
                $"The {party.Name} ({string.Join(", ", members.Select(m => $"{m.GivenName} the {m.Hero!.Cls.Name}"))}) take the bounty on {s.Def.Name} and march out at dawn.",
                members.Select(m => m.Id).ToList());
        }
    }

    /// <summary>Send a party out to a site, taking its bounty (if given) and any settlement's open plea for it.</summary>
    internal static Party Launch(Town town, List<Agent> members, SiteState s, Bounty? bounty)
    {
        var leader = members[0];
        // A company keeps its name, unless another band under that name is already on the road.
        string? name = leader.Hero!.PartyName;
        if (name == null || town.Parties.Any(p => !p.Returned && p.Name == name)) name = PartyName(town);
        int travel = s.Def.Distance * 2 + 1;
        var party = new Party
        {
            Id = town.Parties.Count + 1,
            Name = name,
            Members = members.Select(m => m.Id).ToList(),
            BountyId = bounty?.Id,
            SiteId = s.Def.Id,
            DepartTick = town.Tick,
            ReturnTick = town.Tick + travel * SimTime.HoursPerDay,
        };
        town.Parties.Add(party);
        if (bounty != null)
        {
            bounty.Status = BountyStatus.Taken;
            bounty.PartyId = party.Id;
        }
        var plea = town.Pleas.FirstOrDefault(p => p.SiteId == s.Def.Id && p.Status == PleaStatus.Open);
        if (plea != null)
        {
            plea.Status = PleaStatus.Taken;
            plea.PartyId = party.Id;
            party.PleaId = plea.Id;
        }
        s.Attempts++;
        Shrines.Outfit(town, members);
        foreach (var m in members)
        {
            m.Hero!.PartyName = name;
            m.Hero.AwayUntil = party.ReturnTick;
            m.Activity = Activity.Idle;
            m.LocationId = null;
        }
        return party;
    }

    private static string PartyName(Town town)
    {
        for (int i = 0; i < 20; i++)
        {
            string name = $"{town.Rng.Pick(PartyAdjectives)} {town.Rng.Pick(PartyNouns)}";
            if (town.Parties.All(p => p.Name != name)) return name;
        }
        return $"Company No. {town.Parties.Count + 1}";
    }

    private static void ReturnParties(Town town)
    {
        foreach (var party in town.Parties)
        {
            if (party.Returned || town.Tick < party.ReturnTick) continue;
            party.Returned = true;
            var s = Site(town, party.SiteId)!;
            var bounty = party.BountyId is int bid ? town.Bounties[bid - 1] : null;
            var members = party.Members.Select(town.Agent).ToList();
            if (party.RecalledTick != null)
            {
                Knowledge.ComeHome(town, party, s, bounty, members);
                continue;
            }
            if (s.Cleared)
            {
                // The rumour that sent them was false: the place stands empty.
                foreach (var m in members) m.Hero!.AwayUntil = null;
                if (bounty != null)
                {
                    bounty.Status = BountyStatus.Failed;
                    Refund(town, bounty);
                }
                if (party.PleaId is int pid) World.Reopen(town.Pleas[pid - 1]);
                s.ClearedTick = town.Tick;
                Rumors.Confirm(town, s, $"the {party.Name}");
                town.Log(
                    LedgerCategory.Quest,
                    Importance.Notable,
                    $"The {party.Name} find {s.Def.Name} empty, as cold as the day it was cleared. The tavern was wrong" + (bounty != null ? "; the bounty goes back to the treasury." : "."),
                    members.Select(m => m.Id).ToList());
                continue;
            }
            var result = Expedition.Run(town.Rng, town.Content, s.Def, members, party.Sendings, Lairs.Swell(town, s));

            var chronicle = new Chronicle(
                town.Chronicles.Count + 1,
                party.Name,
                s.Def.Name,
                members.Select(m => m.Describe()).ToList(),
                SimTime.DayOf(party.DepartTick),
                town.Day,
                result.Outcome,
                result.Lines);
            town.Chronicles.Add(chronicle);

            // Bodies carried home can be raised at the temple; a wiped party leaves nobody to carry them.
            double pay = result.Gold + (result.Outcome == ExpeditionOutcome.Cleared ? bounty?.Reward ?? 0 : 0);
            var rites = new List<(string Text, int Id)>();
            var raised = result.Outcome == ExpeditionOutcome.Wiped ? [] : Shrines.Raise(town, result.Dead, party.Name, ref pay, rites);
            var dead = result.Dead.Where(id => !raised.Contains(id)).ToList();
            foreach (int id in raised) Loyalty.Sway(town.Agent(id), town.Content.Loyalty.Raised);
            var survivors = members.Where(m => !dead.Contains(m.Id)).ToList();
            foreach (var m in members)
            {
                var sheet = m.Hero!;
                sheet.AwayUntil = null;
                sheet.Expeditions++;
                sheet.Kills += result.Kills.Get(m.Id, 0);
            }

            // The dead are mourned; the living split the take.
            foreach (int id in dead)
            {
                var d = town.Agent(id);
                d.Dead = true;
                Population.Depart(town, d);
                foreach (var other in town.Residents) if (other.OpinionOf(id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
            }

            if (result.Outcome != ExpeditionOutcome.Wiped) Rumors.Confirm(town, s, $"the {party.Name}");
            if (result.Outcome == ExpeditionOutcome.Cleared)
            {
                s.Cleared = true;
                s.ThoughtCleared = true;
                s.ClearedTick = town.Tick;
                if (bounty != null) bounty.Status = BountyStatus.Completed;
            }
            else if (bounty != null)
            {
                bounty.Status = BountyStatus.Failed;
                Refund(town, bounty);
            }
            pay += World.AfterDelve(town, party, s, result.Outcome);
            if (survivors.Count > 0)
            {
                double share = pay / survivors.Count;
                double townShare = result.Outcome == ExpeditionOutcome.Cleared && bounty != null ? bounty.Reward / survivors.Count : 0;
                // Clearing a site is a milestone worth as much again as a hard fight there.
                int xp = result.Xp + (result.Outcome == ExpeditionOutcome.Cleared ? World.Milestone(town, s) : 0);
                int xpShare = (int)Math.Floor((double)xp / survivors.Count);
                foreach (var m in survivors)
                {
                    m.Gold += share;
                    m.Hero!.Pay += townShare;
                    GainXp(town, m, xpShare);
                    m.Hero!.Renown += result.Outcome == ExpeditionOutcome.Cleared ? 2 * s.Def.Tier : 0;
                    foreach (var o in survivors) if (o.Id != m.Id) m.ChangeOpinion(o.Id, 15);
                }
            }

            string names = Expedition.ListNames(members.Select(m => m.GivenName).ToList());
            string deadNames = Expedition.ListNames(result.Dead.Select(id => town.Agent(id).GivenName).ToList());
            string text;
            if (result.Outcome == ExpeditionOutcome.Cleared)
            {
                text = $"The {party.Name} return from {s.Def.Name} victorious with {Js.Str(Js.Round(pay))} gold to split" +
                       (result.Dead.Count > 0 ? $", but {deadNames} did not come home." : ".");
            }
            else if (result.Outcome == ExpeditionOutcome.Wiped)
            {
                text = $"The {party.Name} never return from {s.Def.Name}. {names} {(members.Count == 1 ? "is" : "are")} presumed dead.";
            }
            else
            {
                text = $"The {party.Name} limp back from {s.Def.Name} empty-handed" +
                       (result.Dead.Count > 0 ? $", carrying {(result.Dead.Count == 1 ? $"{deadNames}'s body" : $"the bodies of {deadNames}")}." : ".");
            }
            town.Log(LedgerCategory.Quest, Importance.Major, $"{text} (Chronicle {chronicle.Id})", members.Select(m => m.Id).ToList());
            foreach (var (rite, id) in rites) town.Log(LedgerCategory.Faith, Importance.Major, rite, [id]);
            Lairs.AfterDelve(town, s, result);
            foreach (var it in result.Items)
            {
                if (it.HeroId == null) Watch.ReceiveRelic(town, it.Item, party.Name);
                else if (survivors.Any(m => m.Id == it.HeroId)) Gear.Claim(town, town.Agent(it.HeroId.Value), it.Item);
            }
        }
    }

    private static void GainXp(Town town, Agent a, int xp)
    {
        var s = a.Hero!;
        s.Xp += xp;
        while (s.Level < XpLevels.Length && s.Xp >= XpLevels[s.Level])
        {
            s.Level++;
            int before = s.MaxHp;
            s.MaxHp = Expedition.HeroMaxHp(s, a.Abilities.Con);
            s.Hp += s.MaxHp - before;
            // Crossing into a new tier of play: the realm, then the world, start to know the name.
            string fame = s.Level switch
            {
                5 => " Bards in other towns have started singing about them.",
                11 => " Kings send letters to them by name.",
                17 => " The whole world knows the name.",
                _ => "",
            };
            town.Log(LedgerCategory.Quest, fame != "" ? Importance.Major : Importance.Notable, $"{a.Name} reaches level {s.Level}.{fame}", [a.Id]);
        }
    }
}
