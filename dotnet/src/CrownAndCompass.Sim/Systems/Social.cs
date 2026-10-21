namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Interactions outside the mayor's view: tavern talk, friendships, grudges, brawls, courtship
/// and marriage. Every contested moment is a visible d20 roll so the Ledger reads like a table.
/// </summary>
public static class Social
{
    public const int FriendThreshold = 40;
    public const int RivalThreshold = -30;
    public const int CourtThreshold = 60;
    public const int MarryThreshold = 85;

    private static readonly string[] Topics =
    [
        "the price of timber",
        "dwarven beards",
        "elven manners",
        "the mayor's taxes",
        "their cooking",
        "who really killed the wolf last winter",
        "the smell of the tannery",
        "a game of dice",
        "their mother",
        "the proper way to pour ale",
        "orcs",
        "a debt from three weeks ago",
    ];

    public static void Hourly(Town town)
    {
        if (!SimTime.IsEvening(town.Hour)) return;
        foreach (var tavern in town.Taverns)
        {
            var patrons = town.Agents.Where(a => !a.Departed && a.Activity == Activity.Drinking && a.LocationId == tavern.Id).ToList();
            tavern.Talk += patrons.Count;
            if (patrons.Count < 2) continue;

            foreach (var p in patrons)
            {
                if (!town.Rng.Chance(0.5)) continue;
                int? courting = p.CourtingId;
                var partner =
                    courting != null && patrons.Any(x => x.Id == courting) && town.Rng.Chance(0.6)
                        ? town.Agent(courting.Value)
                        : town.Rng.Pick(patrons.Where(x => x.Id != p.Id).ToList());
                Interact(town, tavern, p, partner);
            }
            Skills.Perform(town, tavern, patrons);
            Skills.Pickpocket(town, tavern, patrons);
            Taverns.Hour(town, tavern, patrons);
        }
    }

    private static void Interact(Town town, Building tavern, Agent p, Agent q)
    {
        var rng = town.Rng;
        int before = q.OpinionOf(p.Id);
        int beforeMutual = p.OpinionOf(q.Id);

        // Persuasion: the ale, their nature and what the listener already thinks of them all count.
        var talk = Skills.Roll(town, p, town.Content.Skills.Check("talk").Skill, 9, "talk", q.Id, tavern.Id,
            (p.Has(Trait.Gregarious) ? 2 : 0) - (p.Has(Trait.Shy) ? 2 : 0) - (p.Drinks >= 3 ? 2 : 0) + before / 25 + Taverns.Talk(town, tavern, p));
        int roll = talk.Die;
        int total = talk.Total;

        int delta = roll == 20 ? 15 : total >= 15 ? 8 : total >= 9 ? 3 : total >= 5 ? -3 : q.Has(Trait.HotTempered) ? -16 : -10;
        int after = q.ChangeOpinion(p.Id, delta);
        int mutual = p.ChangeOpinion(q.Id, delta > 0 ? delta : delta / 2);

        // Friendship.
        if (after >= FriendThreshold && mutual >= FriendThreshold && (before < FriendThreshold || beforeMutual < FriendThreshold))
            town.Log(LedgerCategory.Social, Importance.Routine, $"{p.Name} and {q.Name} have become fast friends over ale at {tavern.Name}.", [p.Id, q.Id]);

        // A grudge begins.
        if (after <= RivalThreshold && before > RivalThreshold)
            town.Log(
                LedgerCategory.Social,
                Importance.Notable,
                $"{p.Name} says something about {rng.Pick(Topics)} that {q.Name} will not forgive. A grudge begins at {tavern.Name}.",
                [p.Id, q.Id]);

        // Brawl.
        if (after <= RivalThreshold && (q.Has(Trait.HotTempered) || q.Drinks >= 3) && rng.Chance(Math.Min(1, 0.35 * Taverns.Brawl(town, tavern, q) * Curses.Brawl(town.Content, q))))
        {
            if (Skills.Calm(town, tavern, q)) return;
            if (town.Watch > 0 && rng.Chance(Council.WatchStrength(town)))
            {
                var face = Skills.Intimidate(town, tavern, q);
                if (face.Success)
                {
                    town.Log(LedgerCategory.Brawl, Importance.Routine, $"The watch steps in at {tavern.Name} and stares {q.Name} down before they can swing at {p.Name} ({Skills.Said(town, face)}).", [q.Id, p.Id]);
                    return;
                }
                town.Log(LedgerCategory.Brawl, Importance.Routine, $"The watch shouts at {q.Name} to sit down, and {q.GivenName} swings anyway ({Skills.Said(town, face)}).", [q.Id, p.Id]);
            }
            Brawl(town, tavern, q, p);
            return;
        }

        // Courtship and marriage.
        if (p.SpouseId == null && q.SpouseId == null)
        {
            if (p.CourtingId == null && q.CourtingId == null && after >= CourtThreshold && mutual >= CourtThreshold)
            {
                double chance = 0.08 * (p.Has(Trait.Romantic) ? 2 : 1) * (q.Has(Trait.Romantic) ? 2 : 1);
                if (rng.Chance(chance))
                {
                    p.CourtingId = q.Id;
                    q.CourtingId = p.Id;
                    town.Log(LedgerCategory.Romance, Importance.Notable, $"Tongues wag at {tavern.Name}: {p.Name} and {q.Name} have been seen walking out together.", [p.Id, q.Id]);
                }
            }
            else if (p.CourtingId == q.Id && after >= MarryThreshold && mutual >= MarryThreshold && rng.Chance(0.25))
            {
                Marry(town, p, q);
            }
        }
    }

    private static string Sign(int m) => m >= 0 ? $"+{m}" : $"-{-m}";

    internal static void Brawl(Town town, Building tavern, Agent attacker, Agent target)
    {
        var rng = town.Rng;
        string skill = town.Content.Skills.Check("brawl").Skill;
        var (ac, tc) = Skills.Contest(town, attacker, skill, target, skill, "brawl", tavern.Id);
        int ar = ac.Die, tr = tc.Die, am = ac.Bonus, tm = tc.Bonus;
        bool attackerWins = ac.Success;
        var winner = attackerWins ? attacker : target;
        var loser = attackerWins ? target : attacker;
        var (wr, wm, lr, lm) = attackerWins ? (ar, am, tr, tm) : (tr, tm, ar, am);

        int damages = rng.Range(3, 11);
        double paid = Math.Min(loser.Gold, damages);
        loser.Gold -= paid;
        tavern.Cash += paid;
        loser.Fatigue = 100;
        loser.Activity = Activity.Sleeping;
        loser.LocationId = loser.HomeId;
        attacker.ChangeOpinion(target.Id, -20);
        target.ChangeOpinion(attacker.Id, -20);
        var around = town.Agents.Where(a => !a.Departed && a.Activity == Activity.Drinking && a.LocationId == tavern.Id && a.Id != winner.Id);
        var healer = Skills.Tend(town, tavern, loser, around);

        string crit = wr == 20 ? " A natural 20!" : lr == 1 ? " A natural 1." : "";
        town.Log(
            LedgerCategory.Brawl,
            Importance.Notable,
            $"A brawl breaks out at {tavern.Name}: {attacker.Name} swings at {target.Name}. " +
            $"{winner.GivenName} (d20 {wr}{Sign(wm)}) lays {loser.GivenName} (d20 {lr}{Sign(lm)}) out cold.{crit} " +
            $"{loser.GivenName} pays {Js.ToFixed(paid, 0)} gold for the broken chairs." +
            (healer != null ? $" {healer.Name} sees to the bruises." : ""),
            [attacker.Id, target.Id]);
        Taverns.Bloodshed(town, tavern, winner, loser, wr);
    }

    private static void Marry(Town town, Agent p, Agent q)
    {
        p.SpouseId = q.Id;
        q.SpouseId = p.Id;
        p.CourtingId = null;
        q.CourtingId = null;

        // The couple settles in whichever home one of them owns, else wherever has room.
        Building? Fits(Building? b) =>
            b != null && b.OpenBeds >= (p.HomeId == b.Id ? 0 : 1) + (q.HomeId == b.Id ? 0 : 1) ? b : null;
        var home =
            Fits(OwnedHome(town, p)) ??
            Fits(OwnedHome(town, q)) ??
            Fits(p.HomeId != null ? town.Building(p.HomeId.Value) : null) ??
            Fits(q.HomeId != null ? town.Building(q.HomeId.Value) : null);
        string oldName = q.FamilyName;
        if (home != null)
        {
            if (p.HomeId != home.Id) Economy.MoveIn(town, p, home);
            if (q.HomeId != home.Id) Economy.MoveIn(town, q, home);
        }
        q.FamilyName = p.FamilyName;

        town.Log(
            LedgerCategory.Romance,
            Importance.Major,
            $"{p.GivenName} {p.FamilyName} and {q.GivenName} (once {oldName}) are wed at the town hall. Half the town turns out to cheer.",
            [p.Id, q.Id]);
    }

    private static Building? OwnedHome(Town town, Agent a) =>
        town.Buildings.FirstOrDefault(
            b => b.Complete && b.Def.Housing > 0 && b.OwnerId == a.Id && b.OpenBeds + (b.Residents.Contains(a.Id) ? 1 : 0) >= 1);

    /// <summary>Daily acts of charity from generous townsfolk.</summary>
    public static void Daily(Town town)
    {
        foreach (var giver in town.Residents.Where(a => a.Has(Trait.Generous) && a.Gold > 40).ToList())
        {
            var needy = town.Residents
                .Where(a => a.Id != giver.Id && a.Hunger > 60 && a.Gold < 2)
                .OrderByDescending(a => giver.OpinionOf(a.Id))
                .ThenBy(a => a.Id)
                .FirstOrDefault();
            if (needy == null) continue;
            giver.Gold -= 6;
            needy.Gold += 6;
            needy.ChangeOpinion(giver.Id, 25);
            giver.ChangeOpinion(needy.Id, 5);
            town.Log(LedgerCategory.Social, Importance.Routine, $"{giver.Name} quietly presses a few coins into the hand of {needy.Name}, who has not eaten in a day.", [giver.Id, needy.Id]);
        }
    }
}
