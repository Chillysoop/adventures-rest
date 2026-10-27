namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Standing companies. A hero who has ridden with a company stays with it: it rides out with its own members and
/// the newcomers it takes on, and never borrows another company's blades. A newcomer joins a company short of a
/// full roll (one that has lost someone, first), the most renowned that will have them; one nobody takes on
/// bands with other newcomers under a new name. A hero left alone in a company joins another once no recruit
/// has come for a while. No two companies riding at once share a word of their names. Reads no dice but the
/// name pick.
/// </summary>
public static class Muster
{
    private static CompanyRules Rules(Town town) => town.Content.World.Companies;

    /// <summary>Whether the hero's company is on the road (a party or a survey), so those left at home wait for it.</summary>
    public static bool Out(Town town, Agent a) =>
        a.Hero!.PartyName is string n &&
        (town.Parties.Any(p => !p.Returned && p.Name == n) || town.Surveys.Any(s => Explore.Out(s) && s.Name == n));

    /// <summary>Fit to ride and not waiting on their company to come home.</summary>
    public static bool Free(Town town, Agent a) => Quests.Available(a) && !Out(town, a);

    /// <summary>Whether a hero may ride in a leader's band: they share a company, or the hero has none and the leader's has room.</summary>
    public static bool Rides(Town town, Agent leader, Agent h, int recruited = 0)
    {
        string? mine = leader.Hero!.PartyName, theirs = h.Hero!.PartyName;
        if (theirs != null) return theirs == mine;
        return mine == null || Intel.Members(town, mine).Count + recruited < Rules(town).MaxMembers;
    }

    /// <summary>A name no company has worn, sharing no word with a company that still rides, and never one word twice ("Lantern Lanterns").</summary>
    public static string Name(Town town)
    {
        var r = Rules(town);
        var used = town.Companies.Select(c => c.Name).Concat(town.Parties.Select(p => p.Name)).Concat(town.Surveys.Select(s => s.Name)).ToHashSet();
        var words = Intel.MembersByCompany(town).SelectMany(x => x.Name.Split(' ')).ToHashSet();
        for (int i = 0; i < 40 && r.Adjectives.Count > 0 && r.Nouns.Count > 0; i++)
        {
            string adj = town.Rng.Pick(r.Adjectives), noun = town.Rng.Pick(r.Nouns);
            string name = $"{adj} {noun}";
            if (!used.Contains(name) && !words.Contains(adj) && !words.Contains(noun) && !noun.StartsWith(adj, StringComparison.Ordinal)) return name;
        }
        return $"Company No. {town.Companies.Count + 1}";
    }

    public static void Daily(Town town)
    {
        var r = Rules(town);
        var companies = Intel.MembersByCompany(town);
        foreach (var (name, members) in companies)
        {
            var c = Intel.Company(town, name);
            if (members.Count == 1) c.LoneSince ??= town.Day;
            else c.LoneSince = null;
        }
        // Newcomers in town first, then the last of a company nobody has come to fill.
        var seekers = town.Heroes
            .Where(h => !h.Departed && !h.Dead && !h.IsAway && h.Hero!.Duty == HeroDuty.None)
            .Where(h => h.Hero!.PartyName == null || Lone(town, h))
            .OrderBy(h => h.Hero!.PartyName == null ? 0 : 1)
            .ThenByDescending(h => h.Hero!.Level)
            .ThenBy(h => h.Id)
            .ToList();
        foreach (var h in seekers)
        {
            var into = Short(town, h);
            if (into == null) continue;
            Join(town, h, into);
        }
    }

    /// <summary>The last member of a company that has waited long enough for a recruit.</summary>
    private static bool Lone(Town town, Agent h) =>
        Intel.Find(town, h.Hero!.PartyName!) is { LoneSince: int since } && town.Day - since >= Rules(town).LoneDays &&
        Intel.Members(town, h.Hero.PartyName!).Count == 1;

    /// <summary>The company short of a full roll that would take this hero: one that lost someone first, then the most renowned.</summary>
    private static string? Short(Town town, Agent h)
    {
        var r = Rules(town);
        int level = h.Hero!.Level;
        return Intel.MembersByCompany(town)
            .Where(x => x.Name != h.Hero.PartyName && x.Members.Count < r.MaxMembers)
            .Where(x => x.Members.Count > 1 || h.Hero.PartyName == null)
            .Where(x => x.Members[0].Hero!.Level - level <= r.JoinSpread && level - x.Members[0].Hero!.Level <= r.JoinSpread)
            .Where(x => x.Members.All(m => m.OpinionOf(h.Id) >= -10 && h.OpinionOf(m.Id) >= -10))
            .OrderByDescending(x => Fallen(town, x.Name).Count > (Intel.Find(town, x.Name)?.Filled ?? 0))
            .ThenByDescending(x => Intel.Find(town, x.Name)?.Renown ?? 0)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.Name)
            .FirstOrDefault();
    }

    /// <summary>The company's fallen, in the order they fell.</summary>
    private static List<Legacy> Fallen(Town town, string company) =>
        town.Legacies.Where(l => !l.Retired && town.Agent(l.HeroId).Hero?.PartyName == company).ToList();

    private static void Join(Town town, Agent h, string company)
    {
        var c = Intel.Company(town, company);
        string? left = h.Hero!.PartyName;
        var fallen = Fallen(town, company);
        string place = "";
        if (fallen.Count > c.Filled)
        {
            place = $", taking {fallen[c.Filled].Name.Split(' ')[0]}'s place";
            c.Filled++;
        }
        h.Hero.PartyName = company;
        string from = left != null ? $", the last of the {left}," : "";
        town.Log(LedgerCategory.Quest, Importance.Notable, $"{h.Name}{from} joins the {company}{place}.", [h.Id, .. Intel.Members(town, company).Select(m => m.Id)]);
    }
}
