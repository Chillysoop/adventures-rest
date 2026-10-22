namespace CrownAndCompass.Sim.Systems;

/// <summary>Notable townsfolk and the ways the mayor wins them over (content/game/notables.json).</summary>
public sealed record NotableRules
{
    public int FirstDay { get; init; } = 4;

    /// <summary>Days between one notable rising and the next.</summary>
    public int RiseDays { get; init; } = 5;

    /// <summary>Regard at which each band after the first begins (Bitter, Cool, Neutral, Warm, Friend).</summary>
    public List<double> Bands { get; init; } = [];
    public List<string> BandNames { get; init; } = [];

    /// <summary>The regard a notable rises with, low and high.</summary>
    public int[] Start { get; init; } = [0, 0];

    /// <summary>Regard a day drifts back towards nothing: a friend left alone cools.</summary>
    public double Drift { get; init; } = 0.3;

    /// <summary>Days between the mayor's visits to the same notable.</summary>
    public int VisitDays { get; init; } = 4;

    /// <summary>Added to a visit's DC while the notable is cool or bitter.</summary>
    public int CoolDc { get; init; } = 2;

    /// <summary>Regard lost on a failed visit or favour, and on a natural 1.</summary>
    public double Fail { get; init; } = 3;
    public double Fumble { get; init; } = 10;

    /// <summary>A gift's gold and the regard it buys, without a roll.</summary>
    public double Gift { get; init; } = 30;
    public double GiftGain { get; init; } = 6;

    /// <summary>Days between a notable's favours (low, high), how long each stays open, and the regard lost by saying no (twice that for letting it lapse).</summary>
    public int[] AskDays { get; init; } = [9, 15];
    public int AskOpen { get; init; } = 5;
    public double Refuse { get; init; } = 4;

    /// <summary>A quest a notable hands out: the share of a site's usual bounty they put up, the least they will, and the renown the company earns.</summary>
    public double QuestShare { get; init; } = 0.6;
    public double QuestMin { get; init; } = 40;
    public int QuestFame { get; init; } = 3;

    /// <summary>Days between tips by band (0: none): only the warm pass on what they hear.</summary>
    public List<int> TipDays { get; init; } = [];

    /// <summary>Days a notable stays bitter before turning their back on the town.</summary>
    public int LeaveDays { get; init; } = 30;

    /// <summary>Council loyalty a day a notable holding a seat gives while warm (and takes while bitter).</summary>
    public double Seat { get; init; } = 0.3;

    /// <summary>Guild standing a day a guild's master gives while warm (twice as a friend, taken away while bitter).</summary>
    public double Guild { get; init; } = 0.5;

    /// <summary>The kind every guild master rises as, unless they are a notable already.</summary>
    public string GuildMaster { get; init; } = "guild-master";

    /// <summary>Lines each notable keeps of what passed between them and the mayor.</summary>
    public int Remember { get; init; } = 6;

    public List<ApproachDef> Approaches { get; init; } = [];
    public Dictionary<string, HearsDef> Hears { get; init; } = [];
    public List<NotableDef> Kinds { get; init; } = [];

    public ApproachDef? Approach(string id) => Approaches.FirstOrDefault(a => a.Id == id);
    public NotableDef? Kind(string id) => Kinds.FirstOrDefault(k => k.Id == id);
}

/// <summary>A way of spending time with a notable: a check of one of the mayor's skills.</summary>
public sealed record ApproachDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Skill { get; init; } = "";
    public int Dc { get; init; } = 12;
    public double Gain { get; init; } = 10;

}

/// <summary>A kind of news a notable picks up for the town.</summary>
public sealed record HearsDef
{
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
}

/// <summary>A favour a notable asks: gold to give, or a check of the mayor's to make.</summary>
public sealed record AskDef
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public double Gold { get; init; }
    public string Skill { get; init; } = "";
    public int Dc { get; init; } = 12;
    public double Gain { get; init; } = 10;

    /// <summary>A quest for a company (its terms: slay, fetch, rescue...), "" for a favour of the mayor's. {site} names the place.</summary>
    public string Quest { get; init; } = "";
}

/// <summary>A kind of notable: who it rises from, how they like to be won over, what they hear and what they give.</summary>
public sealed record NotableDef
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Face { get; init; } = "";

    /// <summary>The trades they rise from, and what marks the best of them: "practice" or "gold".</summary>
    public List<string> Trades { get; init; } = [];
    public string Rank { get; init; } = "practice";

    /// <summary>Not a resident: someone out past the fields, named from <see cref="Names"/>, living at <see cref="Place"/>.</summary>
    public bool Outsider { get; init; }
    public List<string> Names { get; init; } = [];
    public string Place { get; init; } = "";
    public int MinDay { get; init; }

    public List<string> Approaches { get; init; } = [];
    public string Hears { get; init; } = "";

    /// <summary>A library item they give the armory the first time they count the mayor a friend.</summary>
    public string Boon { get; init; } = "";
    public string BoonText { get; init; } = "";
    public Dictionary<string, double> Warm { get; init; } = [];
    public Dictionary<string, double> Friend { get; init; } = [];
    public Dictionary<string, double> Bitter { get; init; } = [];
    public List<AskDef> Asks { get; init; } = [];
}

/// <summary>Someone in or near the town worth knowing, and how they stand with the mayor.</summary>
public sealed class Notable
{
    public int Id { get; init; }
    public string Kind { get; init; } = "";

    /// <summary>The resident they are, or null for an outsider (named by <see cref="Name"/>).</summary>
    public int? AgentId { get; init; }
    public string Name { get; set; } = "";
    public int Since { get; init; }

    /// <summary>What they think of the mayor, -100 to 100.</summary>
    public double Regard { get; set; }
    public int NextVisit { get; set; }
    public int Visits { get; set; }

    /// <summary>The favour they are asking (an ask id), "" for none, the day it lapses and the day they next ask.</summary>
    public string Ask { get; set; } = "";

    /// <summary>The site a quest they ask is for, and the bounty it went up as once the mayor posted it.</summary>
    public string AskSite { get; set; } = "";
    public int? QuestId { get; set; }
    public int AskUntil { get; set; }
    public int NextAsk { get; set; }
    public int Favours { get; set; }

    public int NextTip { get; set; }
    public int Tips { get; set; }
    public bool BoonGiven { get; set; }
    public int BitterDays { get; set; }

    /// <summary>Gone: dead, moved away, or turned their back on the town.</summary>
    public bool Gone { get; set; }

    /// <summary>What passed between them and the mayor, and what they passed on, newest last.</summary>
    public List<string> Memories { get; } = [];
}

/// <summary>
/// Notable townsfolk: the influential people who aren't heroes. The best smith, the richest merchant, the player the
/// whole town comes to see, the innkeeper, a respected priest, a hedge healer, and the reclusive wizard in the tower past
/// the fields each rise on their own as the town gets them. The mayor wins them over by visiting (a check of whichever
/// of the mayor's skills the notable is open to: a song for the player, arcana for the wizard) or with gifts, and by
/// answering the favours they ask. Warm notables are the town's information network: each hears one kind of news
/// (places in the wilds, the truth of tavern talk, enemies on the move, traitors, far country) and passes it on, more
/// often and more truly as regard grows. Friends give their boons; bitter ones sour the town and in the end walk away.
/// Left alone, regard drifts back to nothing, so nothing here moves a town nobody tends: the stand-in mayor leaves
/// them be, and no daily roll uses the town's dice.
/// </summary>
public static class Notables
{
    private static NotableRules Rules(Town town) => town.Content.Notables;

    public static NotableDef DefOf(Town town, Notable n) => Rules(town).Kind(n.Kind) ?? throw new KeyNotFoundException($"Unknown notable kind {n.Kind}");

    public static Notable? Find(Town town, int id) => town.Notables.FirstOrDefault(n => n.Id == id);

    /// <summary>Notables the town has now, in the order they rose.</summary>
    public static List<Notable> Active(Town town) => town.Notables.Where(n => !n.Gone).ToList();

    public static string NameOf(Town town, Notable n) => n.AgentId is int a ? town.Agent(a).Name : n.Name;

    /// <summary>The band their regard is in, 0 (Bitter) to 4 (Friend).</summary>
    public static int Band(Town town, double regard)
    {
        var b = Rules(town).Bands;
        int i = 0;
        while (i < b.Count && regard >= b[i]) i++;
        return i;
    }

    public static int Band(Town town, Notable n) => Band(town, n.Regard);

    public static string BandName(Town town, Notable n) => Rules(town).BandNames[Band(town, n)];

    public const int Bitter = 0, Cool = 1, Warm = 3, Friend = 4;

    /// <summary>Daily dice of the notables' own, seeded from the seed, the day and the notable: they never move the town's.</summary>
    private static Rng Dice(Town town, int salt)
    {
        uint h = unchecked((uint)town.Seed * 0x9E3779B1u ^ (uint)town.Day * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du);
        h ^= h >> 15;
        h = unchecked(h * 0x2C1B3C6Du);
        h ^= h >> 12;
        return new((int)(h & 0x7FFFFFFF));
    }

    // Rising ---------------------------------------------------------------------------------------

    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.Kinds.Count == 0 || town.Day < r.FirstDay) return;
        foreach (var n in Active(town)) Keep(town, n);
        Rise(town);
    }

    /// <summary>One new notable at most every few days: the first kind the town has someone for and doesn't know yet.</summary>
    private static void Rise(Town town)
    {
        var r = Rules(town);
        // A guild's master is someone worth knowing the day they take the chair, unless they are already.
        if (r.Kind(r.GuildMaster) is { } gm)
            foreach (var g in town.Guilds.OrderBy(g => g.Id, StringComparer.Ordinal))
                if (g.MasterId is int m && !town.Notables.Any(n => !n.Gone && n.AgentId == m))
                {
                    Add(town, gm, m, "");
                    return;
                }
        var last = town.Notables.Count == 0 ? -999 : town.Notables.Where(n => n.Kind != r.GuildMaster).Select(n => n.Since).DefaultIfEmpty(-999).Max();
        if (town.Day - last < r.RiseDays) return;
        foreach (var d in r.Kinds)
        {
            if (d.Id == r.GuildMaster || town.Day < d.MinDay || town.Notables.Any(n => !n.Gone && n.Kind == d.Id)) continue;
            if (d.Outsider)
            {
                if (d.Names.Count == 0) continue;
                // An outsider who walked away doesn't come back: the next of the kind has another name.
                var used = town.Notables.Where(n => n.Kind == d.Id).Select(n => n.Name).ToHashSet();
                var free = d.Names.Where(x => !used.Contains(x)).ToList();
                if (free.Count == 0) continue;
                Add(town, d, null, free[Dice(town, town.Notables.Count).Range(0, free.Count)]);
                return;
            }
            if (Candidate(town, d) is { } a)
            {
                Add(town, d, a.Id, "");
                return;
            }
        }
    }

    /// <summary>The best resident at the kind's trades not already a notable: the most practised, or the richest.</summary>
    public static Agent? Candidate(Town town, NotableDef d)
    {
        var taken = town.Notables.Where(n => !n.Gone && n.AgentId != null).Select(n => n.AgentId!.Value).ToHashSet();
        var pool = town.Workers.Where(a => !a.Dead && !taken.Contains(a.Id) && d.Trades.Contains(Trade(town, a)));
        return (d.Rank == "gold" ? pool.OrderByDescending(a => a.Gold) : pool.OrderByDescending(a => a.Practice).ThenByDescending(a => a.Gold))
            .ThenBy(a => a.Id)
            .FirstOrDefault();
    }

    private static string Trade(Town town, Agent a) =>
        a.JobId is int j && town.Building(j).Def.Profession is string p ? p : a.Profession;

    private static void Add(Town town, NotableDef d, int? agentId, string name)
    {
        var r = Rules(town);
        var dice = Dice(town, town.Notables.Count + 1);
        var n = new Notable
        {
            Id = town.Notables.Count + 1,
            Kind = d.Id,
            AgentId = agentId,
            Name = name,
            Since = town.Day,
            Regard = dice.Range(r.Start[0], r.Start[1] + 1),
            NextAsk = town.Day + dice.Range(r.AskDays[0], r.AskDays[1] + 1),
        };
        town.Notables.Add(n);
        string who = NameOf(town, n);
        string where = d.Outsider && d.Place != "" ? $", who keeps to {d.Place}," : "";
        town.Log(LedgerCategory.Town, Importance.Notable, $"{who}{where} is someone worth knowing: {TitleOf(town, n, d)}.", agentId != null ? [agentId.Value] : []);
    }

    // The day ---------------------------------------------------------------------------------------

    private static void Keep(Town town, Notable n)
    {
        var r = Rules(town);
        var d = DefOf(town, n);
        if (n.AgentId is int id && town.Agent(id) is { } a && (a.Dead || a.Departed))
        {
            n.Gone = true;
            town.Log(LedgerCategory.Town, Importance.Notable, $"The town has lost its {d.Title.ToLowerInvariant()}: {a.Name} is {(a.Dead ? "dead" : "gone")}.", [id]);
            return;
        }

        if (n.Kind == r.GuildMaster && GuildOf(town, n) == null)
        {
            n.Gone = true;
            town.Log(LedgerCategory.Town, Importance.Routine, $"{NameOf(town, n)} no longer speaks for a guild.", Agents(n));
            return;
        }

        // Left alone, feeling fades both ways.
        n.Regard = n.Regard > 0 ? Math.Max(0, n.Regard - r.Drift) : Math.Min(0, n.Regard + r.Drift);

        if (n.Ask != "" && town.Day >= n.AskUntil)
        {
            Shift(n, -r.Refuse * 2);
            Remember(town, n, $"Day {town.Day}: asked a favour, and heard nothing back.");
            n.Ask = "";
        }
        if (n.Ask == "" && town.Day >= n.NextAsk && d.Asks.Count > 0)
        {
            var dice = Dice(town, n.Id * 3);
            // Only those on speaking terms ask: a neglected notable cools and stops asking, but never sours on their own.
            if (Band(town, n) > Cool)
            {
                // A quest needs a place to send a company and the gold to pay it; otherwise they ask another favour.
                var asks = d.Asks.Where(a => a.Quest == "" || QuestSite(town, n, a) != null).ToList();
                if (asks.Count > 0)
                {
                    var ask = asks[dice.Range(0, asks.Count)];
                    n.Ask = ask.Id;
                    n.AskSite = ask.Quest != "" ? QuestSite(town, n, ask)!.Def.Id : "";
                    n.AskUntil = town.Day + r.AskOpen;
                    town.Log(LedgerCategory.Town, Importance.Notable, $"{NameOf(town, n)} {AskText(town, n, ask)}, and hopes the mayor will help.", Agents(n));
                }
            }
            n.NextAsk = town.Day + r.AskOpen + dice.Range(r.AskDays[0], r.AskDays[1] + 1);
        }

        int band = Band(town, n);
        var effects = band == Friend ? d.Friend : band >= Warm ? d.Warm : band == Bitter ? d.Bitter : null;
        if (effects != null) Give(town, effects);
        if (band >= Warm && SeatOf(town, n) is { } seat) seat.Loyalty = Math.Min(100, seat.Loyalty + r.Seat);
        if (band == Bitter && SeatOf(town, n) is { } sour) sour.Loyalty = Math.Max(0, sour.Loyalty - r.Seat);
        // A guild follows its master: won over, they talk the members round; soured, they stir them up.
        if (GuildOf(town, n) is { } guild && !Guilds.OnStrike(town, guild))
        {
            double lean = band == Friend ? 2 * r.Guild : band >= Warm ? r.Guild : band == Bitter ? -r.Guild : 0;
            guild.Standing = Math.Clamp(guild.Standing + lean, 0, 100);
        }

        if (band == Friend && !n.BoonGiven && d.Boon != "" && town.Content.Library.Item(d.Boon) is { } item)
        {
            n.BoonGiven = true;
            Armory.Add(town, item, NameOf(town, n));
            Remember(town, n, $"Day {town.Day}: {d.BoonText}.");
            town.Log(LedgerCategory.Town, Importance.Notable, $"{NameOf(town, n)} counts the mayor a friend and {d.BoonText}: {item.Name}.", Agents(n));
        }

        int every = band < r.TipDays.Count ? r.TipDays[band] : 0;
        if (every > 0 && town.Day >= n.NextTip)
        {
            if (n.NextTip > 0 && Tip(town, n)) n.Tips++;
            n.NextTip = town.Day + every;
        }
        else if (every == 0) n.NextTip = 0;

        n.BitterDays = band == Bitter ? n.BitterDays + 1 : 0;
        if (n.BitterDays >= r.LeaveDays)
        {
            n.Gone = true;
            town.Log(LedgerCategory.Town, Importance.Notable, $"{NameOf(town, n)} will have nothing more to do with the mayor: the town has lost its {d.Title.ToLowerInvariant()}.", Agents(n));
        }
    }

    private static void Give(Town town, Dictionary<string, double> effects)
    {
        if (effects.TryGetValue("gold", out double g) && g != 0) town.Book(TreasuryAccount.Notables, g > 0 ? g : -Math.Min(Math.Max(0, town.Treasury), -g));
        if (effects.TryGetValue("mandate", out double m) && m != 0) town.Mandate = Math.Clamp(town.Mandate + m, 0, 100);
    }

    /// <summary>The guild this notable is master of, if any.</summary>
    public static Guild? GuildOf(Town town, Notable n) => n.AgentId is int id ? town.Guilds.FirstOrDefault(g => g.MasterId == id) : null;

    /// <summary>The town's guild master, the town's master smith: what the town calls them.</summary>
    public static string TitleOf(Town town, Notable n) => TitleOf(town, n, DefOf(town, n));

    private static string TitleOf(Town town, Notable n, NotableDef d) =>
        n.Kind == Rules(town).GuildMaster && GuildOf(town, n) is { } g ? $"master of {Guilds.Name(town, g)}" : $"the town's {d.Title.ToLowerInvariant()}";

    private static Seat? SeatOf(Town town, Notable n) => n.AgentId is int id ? town.Seats.FirstOrDefault(s => s.HolderId == id) : null;

    private static int[] Agents(Notable n) => n.AgentId is int a ? [a] : [];

    private static void Shift(Notable n, double v) => n.Regard = Math.Clamp(n.Regard + v, -100, 100);

    private static void Remember(Town town, Notable n, string line)
    {
        n.Memories.Add(line);
        while (n.Memories.Count > Rules(town).Remember) n.Memories.RemoveAt(0);
    }

    // The information network --------------------------------------------------------------------------

    /// <summary>What they heard, passed on to the town: true from a friend, likelier wrong from a warm acquaintance. Whether there was anything to tell.</summary>
    private static bool Tip(Town town, Notable n)
    {
        var d = DefOf(town, n);
        var dice = Dice(town, n.Id * 5 + 1);
        string who = NameOf(town, n);
        int reliability = (int)Math.Clamp(55 + n.Regard / 2, 5, 95);
        bool truth = Band(town, n) == Friend || dice.Chance(reliability / 100.0);
        string? said = null, memo = "";
        switch (d.Hears)
        {
            case "sites":
            {
                var site = town.Sites.Where(s => !s.Held && s.Known == SiteKnowledge.Unheard).OrderBy(s => s.Def.Distance).ThenBy(s => s.Def.Id, StringComparer.Ordinal).FirstOrDefault();
                if (site == null) break;
                // Word of a place carries word of the country it lies in.
                if (World.RegionOf(town, site) is { Known: false } region) region.Known = true;
                string occupants = Rumors.Occupants(town.Content, site.Def);
                int tier = truth ? site.Def.Tier : Math.Max(1, site.Def.Tier + (dice.Chance(0.5) ? -1 : 1));
                said = $"{who} heard of {site.Def.Name}, {Rumors.Away(site.Def.Distance)}: {occupants} there";
                memo = $"told of {site.Def.Name}";
                Rumors.Plant(town, site, who, n.AgentId, Cap(said) + ".", reliability, truth, occupants, tier);
                break;
            }
            case "rumours":
            {
                // The innkeeper has heard every telling: what the town only half believes, set straight.
                var site = town.Sites.Where(s => !s.Held && s.Known == SiteKnowledge.Rumoured && (s.Believed != Rumors.Occupants(town.Content, s.Def) || s.BelievedTier != s.Def.Tier || s.Trust < 80))
                    .OrderBy(s => s.Def.Distance).ThenBy(s => s.Def.Id, StringComparer.Ordinal).FirstOrDefault();
                if (site == null) break;
                string occupants = Rumors.Occupants(town.Content, site.Def);
                said = $"{who} sets the talk of {site.Def.Name} straight: {occupants}";
                memo = $"set the talk of {site.Def.Name} straight";
                Rumors.Plant(town, site, who, n.AgentId, Cap(said) + ".", Math.Max(80, reliability), true, occupants, site.Def.Tier);
                break;
            }
            case "foes":
            {
                var m = town.Saga.Menaces.Where(x => x.Alive && !Discovery.Known(town, x)).OrderBy(x => x.Id).FirstOrDefault();
                if (m == null) break;
                Discovery.Meet(town, m, Discovery.Heard);
                said = $"{who} heard from heroes at the forge of {m.Name}";
                memo = $"warned of {m.Name}";
                town.Log(LedgerCategory.War, Importance.Notable, Cap(said) + ".", Agents(n));
                break;
            }
            case "plots":
            {
                var mole = town.Moles.OrderBy(x => x.Id).FirstOrDefault();
                if (mole == null) break;
                mole.Suspicion += truth ? 15 : 5;
                said = $"{who} hears things nobody else does: someone on the council answers to another master";
                memo = "warned of a traitor on the council";
                town.Log(LedgerCategory.Council, Importance.Notable, Cap(said) + ".", Agents(n));
                break;
            }
            case "regions":
            {
                var region = town.Regions.Where(x => !x.Known).OrderBy(x => x.Ring).ThenBy(x => x.Id).FirstOrDefault();
                if (region == null) break;
                region.Known = true;
                said = $"{who}'s carters bring word of {region.Name}";
                memo = $"brought word of {region.Name}";
                town.Log(LedgerCategory.Town, Importance.Notable, Cap(said) + ".", Agents(n));
                break;
            }
        }
        if (said == null) return false;
        Remember(town, n, $"Day {town.Day}: {memo}.");
        return true;
    }

    // The mayor's part -----------------------------------------------------------------------------

    public static int VisitIn(Town town, Notable n) => Math.Max(0, n.NextVisit - town.Day);

    public static int DcOf(Town town, Notable n, ApproachDef a) => a.Dc + (Band(town, n) <= Cool ? Rules(town).CoolDc : 0);

    /// <summary>Why the mayor can't visit with this approach now, or null.</summary>
    public static string? VisitBlocked(Town town, Notable n, string approach)
    {
        if (n.Gone) return "gone";
        if (VisitIn(town, n) > 0) return $"in {VisitIn(town, n)} days";
        if (approach == "gift") return town.Treasury < Rules(town).Gift ? "no gold" : null;
        if (!DefOf(town, n).Approaches.Contains(approach) || Rules(town).Approach(approach) == null) return "not their way";
        return null;
    }

    /// <summary>
    /// The mayor spends time with a notable: a check of the approach's skill (or, for "gift", gold and no roll). A success
    /// warms them, a natural 20 twice over; a miss cools them a little, a natural 1 a lot. The die shows like any decision's.
    /// </summary>
    public static StoryBeat? Visit(Town town, int id, string approach)
    {
        var r = Rules(town);
        if (Find(town, id) is not { } n || VisitBlocked(town, n, approach) != null) return null;
        n.NextVisit = town.Day + r.VisitDays;
        n.Visits++;
        string who = NameOf(town, n);
        if (approach == "gift")
        {
            town.Book(TreasuryAccount.Notables, -r.Gift);
            Shift(n, r.GiftGain);
            Remember(town, n, $"Day {town.Day}: a gift from the mayor.");
            town.Log(LedgerCategory.Town, Importance.Routine, $"The mayor sends {who} a gift ({Js.Str(r.Gift)} gold).", Agents(n));
            return null;
        }
        var a = r.Approach(approach)!;
        var beat = Roll(town, a.Skill, DcOf(town, n, a), a.Name);
        double v = Moved(r, beat, a.Gain);
        Shift(n, v);
        beat.Text = $"{who}: {(v >= 0 ? "+" : "−")}{Js.Str(Math.Abs(v))} regard.";
        Remember(town, n, $"Day {town.Day}: {a.Name.ToLowerInvariant()}, {(v > 0 ? "warmly met" : "it went badly")}.");
        town.Log(LedgerCategory.Town, Importance.Routine, $"The mayor goes to {who} to {Lower(a.Name)}: {Stories.Word(beat.Result).ToLowerInvariant()} ({(v >= 0 ? "+" : "−")}{Js.Str(Math.Abs(v))} regard).", Agents(n));
        return beat;
    }

    private static double Moved(NotableRules r, StoryBeat beat, double gain) => beat.Result switch
    {
        StoryResult.Critical => gain * 2,
        StoryResult.Success => gain,
        StoryResult.Fumble => -r.Fumble,
        _ => -r.Fail,
    };

    private static StoryBeat Roll(Town town, string skill, int dc, string what)
    {
        var beat = Sheet.Roll(town, skill, "", dc, "", false, false);
        beat.Option = what;
        var st = town.Stories;
        st.LastCheck = beat;
        st.Checks++;
        return beat;
    }

    public static AskDef? AskOf(Town town, Notable n) => n.Ask == "" ? null : DefOf(town, n).Asks.FirstOrDefault(a => a.Id == n.Ask);

    /// <summary>What they ask, with the place named: "wants ore dug from the Old Mine".</summary>
    public static string AskText(Town town, Notable n, AskDef ask) =>
        ask.Text.Replace("{site}", Quests.Site(town, n.AskSite)?.Def.Name ?? "a place in the wilds");

    public static string? AnswerBlocked(Town town, Notable n)
    {
        if (n.Gone || AskOf(town, n) is not { } ask) return "nothing asked";
        if (ask.Gold > 0 && town.Treasury < ask.Gold) return "no gold";
        if (ask.Quest != "" && (Quests.Site(town, n.AskSite) is not { } s || !Open(town, s))) return "the job is gone";
        return null;
    }

    // Quests ---------------------------------------------------------------------------------------

    /// <summary>A known place still held, with no job on it, that the terms can be done at.</summary>
    private static bool Open(Town town, SiteState s) =>
        s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared &&
        (!Undercity.IsUnder(town, s) || Undercity.Stirring(town, s)) &&
        !town.Bounties.Any(b => b.SiteId == s.Def.Id && Quests.IsActive(b));

    /// <summary>The gold a notable puts up for a quest at a site.</summary>
    public static double QuestReward(Town town, SiteState s) =>
        Js.Round(Quests.SuggestedReward(Math.Max(1, s.BelievedTier)) * Rules(town).QuestShare);

    /// <summary>The place a resident notable would send a company for this quest: the gentlest one they can pay for, nearest first.</summary>
    private static SiteState? QuestSite(Town town, Notable n, AskDef ask)
    {
        if (n.AgentId is not int id) return null;
        var term = town.Content.Bounties.Term(ask.Quest);
        double purse = town.Agent(id).Gold;
        return town.Sites
            .Where(s => Open(town, s) && Systems.Terms.Blocked(town, s, term) == null)
            .Where(s => QuestReward(town, s) >= Rules(town).QuestMin && QuestReward(town, s) <= purse)
            .OrderBy(s => s.BelievedTier)
            .ThenBy(s => s.Def.Distance)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>The mayor takes the quest: the council posts it on the board, the notable's gold held against it.</summary>
    private static bool PostQuest(Town town, Notable n, AskDef ask)
    {
        if (n.AgentId is not int id || Quests.Site(town, n.AskSite) is not { } s) return false;
        var patron = town.Agent(id);
        double reward = QuestReward(town, s);
        var b = Quests.PostBounty(town, s.Def.Id, reward, terms: ask.Quest, patron: patron);
        if (b == null) return false;
        b.NotableId = n.Id;
        n.QuestId = b.Id;
        town.Log(LedgerCategory.Quest, Importance.Notable,
            $"{Cap(NameOf(town, n))} puts up {Js.Str(reward)} gold on {s.Def.Name}, and the council posts it on the board: {NameOf(town, n)} {AskText(town, n, ask)}. Renown suggested: {Notoriety.Suggested(town, s)}.", [id]);
        return true;
    }

    /// <summary>A company comes home from a notable's quest: done, and they think the better of the mayor who found them the blades.</summary>
    public static void QuestHome(Town town, Bounty b, Party party, bool done)
    {
        var r = Rules(town);
        if (b.NotableId is not int nid || Find(town, nid) is not { } n) return;
        n.QuestId = null;
        var ask = DefOf(town, n).Asks.FirstOrDefault(a => a.Quest == b.Terms) ?? DefOf(town, n).Asks.FirstOrDefault(a => a.Quest != "");
        string site = Quests.Site(town, b.SiteId)?.Def.Name ?? "the wilds";
        if (!done)
        {
            Remember(town, n, $"Day {town.Day}: the {party.Name} came back from {site} without the job done.");
            return;
        }
        double gain = ask?.Gain ?? 10;
        Shift(n, gain);
        n.Favours++;
        Remember(town, n, $"Day {town.Day}: the {party.Name} saw to {site} for them.");
        Notoriety.Record(town, party.Name, $"did {NameOf(town, n)}'s job at {site}");
        Notoriety.CompanyDeed(town, party.Name, r.QuestFame);
        town.Log(LedgerCategory.Quest, Importance.Notable,
            $"{Cap(NameOf(town, n))} thanks the mayor: the {party.Name} saw to {site}. (+{Js.Str(gain)} regard, +{r.QuestFame} renown for the {party.Name})",
            [.. Agents(n), .. party.Members]);
    }

    /// <summary>Nobody took a notable's quest before it lapsed.</summary>
    public static void QuestLapsed(Town town, Bounty b)
    {
        if (b.NotableId is not int nid || Find(town, nid) is not { } n) return;
        n.QuestId = null;
        Shift(n, -Rules(town).Refuse);
        Remember(town, n, $"Day {town.Day}: their job at {Quests.Site(town, b.SiteId)?.Def.Name ?? "the wilds"} went begging.");
    }

    /// <summary>Help with the favour a notable asked (gold, or a check of the mayor's), or turn it down.</summary>
    public static StoryBeat? Answer(Town town, int id, bool help)
    {
        var r = Rules(town);
        if (Find(town, id) is not { } n || AskOf(town, n) is not { } ask) return null;
        string who = NameOf(town, n);
        if (!help)
        {
            n.Ask = "";
            Shift(n, -r.Refuse);
            Remember(town, n, $"Day {town.Day}: the mayor said no to a favour.");
            town.Log(LedgerCategory.Town, Importance.Routine, $"The mayor turns {who} down.", Agents(n));
            return null;
        }
        if (AnswerBlocked(town, n) != null) return null;
        if (ask.Quest != "")
        {
            if (!PostQuest(town, n, ask)) return null;
            n.Ask = "";
            Remember(town, n, $"Day {town.Day}: the mayor posted their job on the board.");
            return null;
        }
        n.Ask = "";
        StoryBeat? beat = null;
        double v = ask.Gain;
        if (ask.Gold > 0) town.Book(TreasuryAccount.Notables, -ask.Gold);
        if (ask.Skill != "")
        {
            beat = Roll(town, ask.Skill, ask.Dc, ask.Text);
            v = Moved(r, beat, ask.Gain);
            beat.Text = $"{who}: {(v >= 0 ? "+" : "−")}{Js.Str(Math.Abs(v))} regard.";
        }
        if (v > 0) n.Favours++;
        Shift(n, v);
        Remember(town, n, $"Day {town.Day}: {(v > 0 ? "the mayor did them a favour" : "the mayor tried to help, and made it worse")}.");
        town.Log(LedgerCategory.Town, Importance.Routine, $"The mayor helps {who}{(ask.Gold > 0 ? $" ({Js.Str(ask.Gold)} gold)" : "")}: {(v >= 0 ? "+" : "−")}{Js.Str(Math.Abs(v))} regard.", Agents(n));
        return beat;
    }

    /// <summary>The chance a visit (or a favour's check) succeeds.</summary>
    public static double Chance(Town town, string skill, int dc) => Sheet.Chance(town, skill, "", dc, false, false);

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
