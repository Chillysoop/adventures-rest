namespace CrownAndCompass.Sim.Systems;

/// <summary>A place in the dark company (darkcompany.json <c>roles</c>): what the town calls them, how they fight, and the class a turned leader takes up.</summary>
public sealed record DarkRoleDef
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";

    /// <summary>The library creature they fight as.</summary>
    public string Monster { get; init; } = "";

    /// <summary>The hero class they take up if they yield and join the town.</summary>
    public string Cls { get; init; } = "";

    /// <summary>Can lead the company.</summary>
    public bool Lead { get; init; }
}

/// <summary>The big bad's own adventuring company (content/game/darkcompany.json).</summary>
public sealed record DarkCompanyRules
{
    /// <summary>Days after the big bad is crowned that it raises the company.</summary>
    public int Form { get; init; } = 12;

    /// <summary>Days between one errand and the next, and before a broken company is raised again.</summary>
    public int[] Every { get; init; } = [16, 28];
    public int[] Reform { get; init; } = [60, 90];

    /// <summary>Power a robbed hoard brings the big bad: a share of what it has, and a flat amount.</summary>
    public double Gain { get; init; } = 0.06;
    public double GainFlat { get; init; } = 15;

    /// <summary>Power the big bad loses when its company is cut down.</summary>
    public double Loss { get; init; } = 0.12;

    /// <summary>Share of a site's hoard left once the company has robbed it.</summary>
    public double Robbed { get; init; } = 0.25;

    /// <summary>Chance its leader yields and joins the town when the company is beaten.</summary>
    public double Turn { get; init; } = 0.35;

    /// <summary>Heroes the player can send to ambush it.</summary>
    public int Party { get; init; } = 4;
    public IReadOnlyList<string> Names { get; init; } = [];
    public List<DarkRoleDef> Roles { get; init; } = [];
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

/// <summary>One of the dark company.</summary>
public sealed class DarkMember
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
}

/// <summary>The town's ambush on the dark company: who went, when they meet it on the road, and when they are home.</summary>
public sealed class DarkAmbush
{
    public List<int> HeroIds { get; } = [];
    public int MeetTick { get; set; }
    public int HomeTick { get; set; }
    public bool Fought { get; set; }
}

public sealed class DarkCompanyState
{
    /// <summary>Raised (true) or not yet / broken (false).</summary>
    public bool Riding { get; set; }
    public string Name { get; set; } = "";
    public List<DarkMember> Members { get; } = [];

    /// <summary>Its errand: the site it rides for and when it gets there, or "" while it rests; and when it rides next.</summary>
    public string Target { get; set; } = "";
    public int SetOutTick { get; set; }
    public int ArriveTick { get; set; }
    public int NextTick { get; set; } = -1;

    public DarkAmbush? Ambush { get; set; }

    /// <summary>Hoards robbed and errands lost, all told, and times it was broken.</summary>
    public int Robbed { get; set; }
    public int Lost { get; set; }
    public int Broken { get; set; }
    public int Raised { get; set; }
}

/// <summary>
/// The dark company, after Shadows of Forbidden Gods' agents: once the big bad is crowned it raises an adventuring
/// company of its own, a blackguard, a hexer, a knife and their like, who ride the roads robbing the hoards of the wilds
/// for their master. Word on the roads tells the town where they are bound, so its heroes can race them to the site
/// (whoever gets there first takes it) or ride out to ambush them. Beaten, the company breaks, its master is weakened
/// and its leader may yield and join the town; the big bad raises another in time. The company is the hands of the big
/// bad's own quests for power.
/// </summary>
public static class DarkCompany
{
    public static DarkCompanyRules Rules(Town town) => town.Content.DarkCompany;

    private static string Text(Town town, string key) => Rules(town).Texts.TryGetValue(key, out var t) ? t : "";

    public static DarkRoleDef? Role(Town town, string id) => Rules(town).Roles.FirstOrDefault(r => r.Id == id);

    public static DarkMember? Leader(Town town) => town.DarkCompany.Members.FirstOrDefault();

    /// <summary>The site it rides for now, or null.</summary>
    public static SiteState? Target(Town town) => town.DarkCompany is { Riding: true, Target: not "" } c ? Quests.Site(town, c.Target) : null;

    /// <summary>Out on the road to a site.</summary>
    public static bool Out(Town town) => Target(town) != null;

    private static Rng Dice(Town town, int salt) => new(unchecked((int)Voices.Hash("dark", town.Seed, town.Tick, salt)));

    private static Menace? Master(Town town) => Saga.BigBad(town) is { Alive: true } m ? m : null;

    /// <summary>The level it fights at: the town's champions', so it is always a match for them.</summary>
    public static int Level(Town town) => Math.Max(3, Saga.ChampionLevel(town));

    public static void Daily(Town town)
    {
        var c = town.DarkCompany;
        var r = Rules(town);
        if (r.Roles.Count == 0) return;
        var master = Master(town);
        if (master == null)
        {
            // Its master is gone: the company scatters with it.
            if (c.Riding)
            {
                c.Riding = false;
                c.Target = "";
            }
            Home(town);
            return;
        }
        if (!c.Riding)
        {
            if (c.NextTick < 0) c.NextTick = town.Tick + r.Form * SimTime.HoursPerDay;
            if (town.Tick >= c.NextTick) Raise(town, master);
            Home(town);
            return;
        }
        if (c.Ambush is { Fought: false } amb && town.Tick >= amb.MeetTick) Waylay(town, master);
        Home(town);
        if (!c.Riding) return;
        if (c.Target != "")
        {
            if (town.Tick >= c.ArriveTick) Arrive(town, master);
            return;
        }
        // Its master's quest comes before any hoard: it rides to fetch what the big bad seeks.
        if (DarkQuests.Current(town) is { } q && Quests.Site(town, q.SiteId) is { Cleared: false } qs) RideFor(town, master, q, qs);
        else if (town.Tick >= c.NextTick) SetOut(town);
    }

    /// <summary>The big bad's company scatters (its master fell or a new era began); a new one is raised in time.</summary>
    public static void Scatter(Town town)
    {
        var c = town.DarkCompany;
        c.Riding = false;
        c.Target = "";
        c.NextTick = -1;
    }

    /// <summary>The errand it rides on is its master's quest.</summary>
    public static bool OnQuest(Town town) => Out(town) && DarkQuests.Current(town)?.SiteId == town.DarkCompany.Target;

    /// <summary>Off to the site of its master's quest, to be there before the quest is due.</summary>
    private static void RideFor(Town town, Menace master, DarkQuest q, SiteState site)
    {
        var c = town.DarkCompany;
        int days = Math.Max(1, Math.Min(Math.Max(2, site.Def.Distance), q.DueDay - town.Day - 1));
        c.Target = site.Def.Id;
        c.SetOutTick = town.Tick;
        c.ArriveTick = town.Tick + days * SimTime.HoursPerDay;
        c.Ambush = null;
        town.Log(LedgerCategory.War, Importance.Notable, Fill(town, Text(town, "quest"), master).Replace("{site}", site.Def.Name).Replace("{days}", days == 1 ? "a day" : $"{days} days"));
    }

    private static void Raise(Town town, Menace master)
    {
        var c = town.DarkCompany;
        var r = Rules(town);
        var dice = Dice(town, 1 + c.Raised);
        c.Members.Clear();
        var leaders = r.Roles.Where(x => x.Lead).ToList();
        var lead = leaders.Count > 0 ? leaders[dice.Range(0, leaders.Count)] : r.Roles[0];
        var rest = r.Roles.Where(x => x != lead).ToList();
        c.Members.Add(new DarkMember { Name = Name(town, dice), Role = lead.Id });
        while (c.Members.Count < Math.Min(4, r.Roles.Count) && rest.Count > 0)
        {
            var pick = rest[dice.Range(0, rest.Count)];
            rest.Remove(pick);
            c.Members.Add(new DarkMember { Name = Name(town, dice), Role = pick.Id });
        }
        var names = r.Names.Where(n => n != c.Name).ToList();
        c.Name = names.Count > 0 ? names[dice.Range(0, names.Count)] : "the dark company";
        c.Riding = true;
        c.Target = "";
        c.Ambush = null;
        c.NextTick = town.Tick + dice.Range(2, 6) * SimTime.HoursPerDay;
        c.Raised++;
        var leader = c.Members[0];
        town.Log(LedgerCategory.War, Importance.Major, Fill(town, Text(town, c.Raised == 1 ? "formed" : "reformed"), master).Replace("{leader}", leader.Name).Replace("{title}", lead.Title));
        Records.Pin(town, "dark", c.Name);
    }

    private static string Name(Town town, Rng dice)
    {
        var races = town.Content.Races.Where(x => x.Given.Count > 0).ToList();
        if (races.Count == 0) return "Someone";
        var race = races[dice.Range(0, races.Count)];
        string given = race.Given[dice.Range(0, race.Given.Count)];
        return race.Family.Count > 0 ? $"{given} {race.Family[dice.Range(0, race.Family.Count)]}" : given;
    }

    /// <summary>Where it rides next: a site still holding its hoard, the richest the town knows of first.</summary>
    private static void SetOut(Town town)
    {
        var c = town.DarkCompany;
        var master = Master(town)!;
        var dice = Dice(town, 77);
        var open = town.Sites
            .Where(s => !s.Cleared && !s.Def.Aloft && s.Def.Foe == "" && s.Def.Id != master.SeatId && !master.Holdings.Contains(s.Def.Id) && s.Def.Loot[^1] > 0)
            .OrderByDescending(s => s.Known != SiteKnowledge.Unheard)
            .ThenByDescending(s => s.Def.Loot[^1])
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .Take(3)
            .ToList();
        if (open.Count == 0)
        {
            c.NextTick = town.Tick + 10 * SimTime.HoursPerDay;
            return;
        }
        var site = open[dice.Range(0, open.Count)];
        int days = Math.Max(2, site.Def.Distance);
        c.Target = site.Def.Id;
        c.SetOutTick = town.Tick;
        c.ArriveTick = town.Tick + days * SimTime.HoursPerDay;
        c.Ambush = null;
        town.Log(LedgerCategory.War, Importance.Notable, Fill(town, Text(town, "sets"), master).Replace("{site}", site.Def.Name).Replace("{days}", days == 1 ? "a day" : $"{days} days"));
    }

    /// <summary>It reaches the site: too late, beaten to it by the town's heroes, or in and out with the hoard.</summary>
    private static void Arrive(Town town, Menace master)
    {
        var c = town.DarkCompany;
        var r = Rules(town);
        var site = Target(town)!;
        var first = town.Parties.Where(p => p.SiteId == site.Def.Id && !p.Returned && p.ArriveTick <= town.Tick).OrderBy(p => p.ArriveTick).ThenBy(p => p.Id).FirstOrDefault();
        string text;
        if (site.Cleared)
        {
            text = Text(town, "late");
            c.Lost++;
        }
        else if (first != null)
        {
            text = Text(town, "beaten").Replace("{party}", first.Name);
            c.Lost++;
        }
        else if (DarkQuests.Current(town) is { } q && q.SiteId == site.Def.Id)
        {
            // It fetches what its master sought: the quest is done now, not on its day.
            c.Robbed++;
            text = Text(town, "seized");
            town.Log(LedgerCategory.War, Importance.Major, Fill(town, text, master).Replace("{site}", site.Def.Name));
            DarkQuests.Finish(town, master, q);
            c.Target = "";
            c.NextTick = town.Tick + Dice(town, 5).Range(r.Every[0], r.Every[^1] + 1) * SimTime.HoursPerDay;
            return;
        }
        else
        {
            site.Def = site.Def with { Loot = [(int)(site.Def.Loot[0] * r.Robbed), (int)(site.Def.Loot[^1] * r.Robbed)] };
            master.Power += master.Power * r.Gain + r.GainFlat;
            c.Robbed++;
            text = Text(town, "robbed");
        }
        town.Log(LedgerCategory.War, first == null && !site.Cleared ? Importance.Major : Importance.Notable, Fill(town, text, master).Replace("{site}", site.Def.Name));
        c.Target = "";
        c.NextTick = town.Tick + Math.Max(2, site.Def.Distance) * SimTime.HoursPerDay + Dice(town, 5).Range(r.Every[0], r.Every[^1] + 1) * SimTime.HoursPerDay;
    }

    // The player's ambush --------------------------------------------------------------------------

    /// <summary>Heroes who can ride out to ambush it now: home, standing, not on guard.</summary>
    public static List<Agent> Free(Town town) =>
        town.Heroes.Where(a => !a.IsAway && a.Hero!.Hp > 0 && a.Hero.Duty == HeroDuty.None && (a.Hero.JailedUntil ?? 0) <= town.Tick)
            .OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).ToList();

    /// <summary>When heroes setting out now would meet it on the road: halfway between here and its site.</summary>
    public static int? MeetTick(Town town)
    {
        var c = town.DarkCompany;
        if (!Out(town)) return null;
        int left = c.ArriveTick - town.Tick;
        int meet = town.Tick + Math.Max(SimTime.HoursPerDay, left / 2 / SimTime.HoursPerDay * SimTime.HoursPerDay);
        return meet < c.ArriveTick ? meet : null;
    }

    /// <summary>Why the town can't ambush it now, or null when it can.</summary>
    public static string? AmbushBlocked(Town town, IReadOnlyList<int>? heroIds = null)
    {
        var c = town.DarkCompany;
        if (!Out(town)) return "not on the road";
        if (c.Ambush != null) return "already sent";
        if (MeetTick(town) == null) return "too close to its site";
        var free = Free(town);
        if (free.Count == 0) return "no heroes home";
        if (heroIds != null && (heroIds.Count == 0 || heroIds.Any(id => free.All(a => a.Id != id)))) return "those heroes can't go";
        return null;
    }

    /// <summary>Send heroes (the best free, up to the party size, unless named) to ambush it on the road. Returns why not, or null when sent.</summary>
    public static string? SendAmbush(Town town, IReadOnlyList<int>? heroIds = null)
    {
        if (AmbushBlocked(town, heroIds) is { } why) return why;
        var c = town.DarkCompany;
        var r = Rules(town);
        var heroes = heroIds is { Count: > 0 } ? heroIds.Take(r.Party).Select(town.Agent).ToList() : Free(town).Take(r.Party).ToList();
        int meet = MeetTick(town)!.Value;
        var amb = new DarkAmbush { MeetTick = meet, HomeTick = meet + (meet - town.Tick) };
        amb.HeroIds.AddRange(heroes.Select(h => h.Id));
        c.Ambush = amb;
        foreach (var h in heroes) h.Hero!.AwayUntil = amb.HomeTick;
        var site = Target(town)!;
        town.Log(LedgerCategory.War, Importance.Notable, Fill(town, Text(town, "ambush"), Master(town)!).Replace("{heroes}", Expedition.ListNames(heroes.Select(h => h.GivenName).ToList())).Replace("{site}", site.Def.Name), heroes.Select(h => h.Id).ToList());
        return null;
    }

    /// <summary>The company as a place to fight: each of its members in turn, its leader last.</summary>
    public static SiteDef Fight(Town town)
    {
        var c = town.DarkCompany;
        int tier = Level(town);
        var foes = c.Members.Skip(1).Select(m => Role(town, m.Role)).OfType<DarkRoleDef>()
            .Select(role => new EncounterDef { Kind = "combat", Monster = role.Monster, Count = [1, 1], Text = $"the {role.Title}" }).ToList();
        var lead = Role(town, c.Members.FirstOrDefault()?.Role ?? "") ?? Rules(town).Roles[0];
        return new SiteDef
        {
            Id = "dark-company",
            Name = $"the road to {Target(town)?.Def.Name ?? "the wilds"}",
            Tier = tier,
            Kind = "rivals",
            Encounters = foes,
            Boss = new EncounterDef { Kind = "combat", Monster = lead.Monster, Count = [1, 1], Text = $"{c.Members.FirstOrDefault()?.Name ?? "their leader"} the {lead.Title}" },
            Loot = [20 * tier, 50 * tier],
        };
    }

    private static void Waylay(Town town, Menace master)
    {
        var c = town.DarkCompany;
        var r = Rules(town);
        var amb = c.Ambush!;
        amb.Fought = true;
        var heroes = amb.HeroIds.Select(town.Agent).Where(a => !a.Dead && !a.Departed).ToList();
        var site = Target(town);
        string names = Expedition.ListNames(heroes.Select(h => h.GivenName).ToList());
        if (heroes.Count == 0) return;
        if (site == null || town.Tick > c.ArriveTick)
        {
            town.Log(LedgerCategory.War, Importance.Notable, Fill(town, Text(town, "missed"), master).Replace("{heroes}", names).Replace("{site}", site?.Def.Name ?? "the wilds"), amb.HeroIds);
            return;
        }
        var fight = Fight(town);
        var result = Expedition.Run(town.Rng, town.Content, fight, heroes);
        var dead = result.Dead.ToList();
        foreach (int id in dead)
        {
            var d = town.Agent(id);
            d.Dead = true;
            Population.Depart(town, d);
            foreach (var other in town.Residents) if (other.OpinionOf(id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
        }
        var living = heroes.Where(h => !dead.Contains(h.Id)).ToList();
        Wounds.Home(town, result, living, fight.Name);
        foreach (var h in heroes) h.Hero!.Kills += result.Kills.Get(h.Id, 0);
        int share = living.Count == 0 ? 0 : result.Xp / living.Count;
        foreach (var h in living)
        {
            Quests.GainXp(town, h, share);
            h.Gold += living.Count == 0 ? 0 : result.Gold / living.Count;
        }
        bool won = result.Outcome == ExpeditionOutcome.Cleared;
        town.Chronicles.Add(new Chronicle(town.Chronicles.Count + 1, $"ambush on {c.Name}", fight.Name, heroes.Select(m => m.Describe()).ToList(), SimTime.DayOf(amb.MeetTick - (amb.HomeTick - amb.MeetTick)), town.Day, result.Outcome, result.Lines, won)
        {
            Beats = result.Beats,
            MemberIds = heroes.Select(m => m.Id).ToList(),
            Dead = dead,
        });
        if (!won)
        {
            town.Log(LedgerCategory.War, Importance.Major, Fill(town, Text(town, "lost"), master).Replace("{heroes}", names).Replace("{site}", site.Def.Name) + $" (Chronicle {town.Chronicles.Count})", amb.HeroIds);
            return;
        }
        foreach (var h in living) Notoriety.Deed(town, h, "story", 4);
        // Cut down on its way to fetch the big bad's prize: the quest fails with it.
        bool quest = DarkQuests.Current(town) is { } q && q.SiteId == site.Def.Id;
        master.Power = Math.Max(0, master.Power * (1 - r.Loss));
        town.Log(LedgerCategory.War, Importance.Major, Fill(town, Text(town, "won"), master).Replace("{heroes}", names).Replace("{site}", site.Def.Name) + $" (Chronicle {town.Chronicles.Count})", amb.HeroIds);
        Records.Pin(town, "victory", c.Name);
        if (quest) DarkQuests.Foil(town, master, DarkQuests.Current(town)!, null);
        if (Leader(town) is { } leader && Role(town, leader.Role) is { } lr && Dice(town, 31).Chance(r.Turn)
            && town.Content.Classes.FirstOrDefault(x => x.Id == lr.Cls) is { } cls)
        {
            var hero = Quests.CreateHero(town, cls, Math.Clamp(Level(town) - 1, 1, 20));
            var parts = leader.Name.Split(' ', 2);
            hero.GivenName = parts[0];
            hero.FamilyName = parts.Length > 1 ? parts[1] : "";
            hero.Hero!.Past = $"Once {lr.Title} of {c.Name}, until they yielded on the road to {site.Def.Name}.";
            town.Log(LedgerCategory.War, Importance.Major, Fill(town, Text(town, "yields"), master).Replace("{leader}", leader.Name), [hero.Id]);
        }
        c.Riding = false;
        c.Target = "";
        c.Broken++;
        c.NextTick = town.Tick + Dice(town, 9).Range(r.Reform[0], r.Reform[^1] + 1) * SimTime.HoursPerDay;
    }

    /// <summary>The ambush party is home.</summary>
    private static void Home(Town town)
    {
        var amb = town.DarkCompany.Ambush;
        if (amb == null || !amb.Fought || town.Tick < amb.HomeTick) return;
        foreach (var h in amb.HeroIds.Select(town.Agent))
            if (!h.Dead && !h.Departed && h.Hero!.AwayUntil == amb.HomeTick) h.Hero.AwayUntil = null;
        town.DarkCompany.Ambush = null;
    }

    private static string Fill(Town town, string text, Menace master) =>
        Saga.Cap(text.Replace("{company}", town.DarkCompany.Name).Replace("{Company}", Saga.Cap(town.DarkCompany.Name)).Replace("{Master}", Saga.Cap(master.Leader)).Replace("{master}", master.Leader).Replace("{town}", town.Name));
}
