using CrownAndCompass.Sim.Agents;
using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.World;

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
    {
        "the price of timber", "dwarven beards", "elven manners", "the mayor's taxes", "their cooking",
        "who really killed the wolf last winter", "the smell of the tannery", "a game of dice", "their mother",
        "the proper way to pour ale", "orcs", "a debt from three weeks ago",
    };

    public static void Hourly(Town town)
    {
        if (!SimTime.IsEvening(town.Hour)) return;
        foreach (var tavern in town.Taverns.ToList())
        {
            var patrons = town.Agents
                .Where(a => !a.Departed && a.Activity == Activity.Drinking && a.LocationId == tavern.Id)
                .ToList();
            if (patrons.Count < 2) continue;

            foreach (var p in patrons)
            {
                if (!town.Rng.Chance(0.5)) continue;
                var partner = p.CourtingId is int c && patrons.Any(x => x.Id == c) && town.Rng.Chance(0.6)
                    ? town.Agent(c)
                    : town.Rng.Pick(patrons.Where(x => x.Id != p.Id).ToList());
                Interact(town, tavern, p, partner);
            }
        }
    }

    private static int Mod(int score) => Dice.Modifier(score);

    private static void Interact(Town town, Building tavern, Agent p, Agent q)
    {
        var rng = town.Rng;
        int before = q.OpinionOf(p.Id);
        int beforeMutual = p.OpinionOf(q.Id);

        int roll = rng.D20();
        int total = roll + Mod(p.Abilities.Cha) + (p.Has(Trait.Gregarious) ? 2 : 0) - (p.Has(Trait.Shy) ? 2 : 0)
                    - (p.Drinks >= 3 ? 2 : 0) + before / 25;

        int delta = roll == 20 ? 15 : total >= 15 ? 8 : total >= 9 ? 3 : total >= 5 ? -3 : (q.Has(Trait.HotTempered) ? -16 : -10);
        int after = q.ChangeOpinion(p.Id, delta);
        int mutual = p.ChangeOpinion(q.Id, delta > 0 ? delta : delta / 2);

        // Friendship.
        if (after >= FriendThreshold && mutual >= FriendThreshold && (before < FriendThreshold || beforeMutual < FriendThreshold))
            town.Log(LedgerCategory.Social, Importance.Routine,
                $"{p.Name} and {q.Name} have become fast friends over ale at {tavern.Name}.", p.Id, q.Id);

        // A grudge begins.
        if (after <= RivalThreshold && before > RivalThreshold)
            town.Log(LedgerCategory.Social, Importance.Notable,
                $"{p.Name} says something about {rng.Pick(Topics)} that {q.Name} will not forgive. A grudge begins at {tavern.Name}.", p.Id, q.Id);

        // Brawl.
        if (after <= RivalThreshold && (q.Has(Trait.HotTempered) || q.Drinks >= 3) && rng.Chance(0.35))
        {
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
                    p.CourtingId = q.Id; q.CourtingId = p.Id;
                    town.Log(LedgerCategory.Romance, Importance.Notable,
                        $"Tongues wag at {tavern.Name}: {p.Name} and {q.Name} have been seen walking out together.", p.Id, q.Id);
                }
            }
            else if (p.CourtingId == q.Id && after >= MarryThreshold && mutual >= MarryThreshold && rng.Chance(0.25))
            {
                Marry(town, p, q);
            }
        }
    }

    private static void Brawl(Town town, Building tavern, Agent attacker, Agent target)
    {
        var rng = town.Rng;
        int ar = rng.D20(), tr = rng.D20();
        int am = Mod(attacker.Abilities.Str), tm = Mod(target.Abilities.Str);
        bool attackerWins = ar + am >= tr + tm;
        var winner = attackerWins ? attacker : target;
        var loser = attackerWins ? target : attacker;

        int damages = rng.Range(3, 11);
        double paid = Math.Min(loser.Gold, damages);
        loser.Gold -= paid;
        tavern.Cash += paid;
        loser.Fatigue = 100;
        loser.Activity = Activity.Sleeping;
        loser.LocationId = loser.HomeId;
        attacker.ChangeOpinion(target.Id, -20);
        target.ChangeOpinion(attacker.Id, -20);

        string crit = (attackerWins ? ar : tr) == 20 ? " A natural 20!" : (attackerWins ? tr : ar) == 1 ? " A natural 1." : "";
        town.Log(LedgerCategory.Brawl, Importance.Notable,
            $"A brawl breaks out at {tavern.Name}: {attacker.Name} swings at {target.Name}. " +
            $"{winner.GivenName} (d20 {(attackerWins ? ar : tr)}{Sign(attackerWins ? am : tm)}) lays {loser.GivenName} (d20 {(attackerWins ? tr : ar)}{Sign(attackerWins ? tm : am)}) out cold.{crit} " +
            $"{loser.GivenName} pays {paid:0} gold for the broken chairs.",
            attacker.Id, target.Id);
    }

    private static string Sign(int m) => m >= 0 ? $"+{m}" : $"{m}";

    private static void Marry(Town town, Agent p, Agent q)
    {
        p.SpouseId = q.Id; q.SpouseId = p.Id;
        p.CourtingId = null; q.CourtingId = null;

        // The couple settles in whichever home one of them owns, else wherever has room.
        Building? home = OwnedHome(town, p) ?? OwnedHome(town, q);
        string oldName = q.FamilyName;
        if (home == null && p.HomeId is int ph && town.Building(ph).OpenBeds > 0) home = town.Building(ph);
        if (home == null && q.HomeId is int qh && town.Building(qh).OpenBeds > 0) home = town.Building(qh);
        if (home != null)
        {
            if (p.HomeId != home.Id) Economy.MoveIn(town, p, home);
            if (q.HomeId != home.Id) Economy.MoveIn(town, q, home);
        }
        q.FamilyName = p.FamilyName;

        town.Log(LedgerCategory.Romance, Importance.Major,
            $"{p.GivenName} {p.FamilyName} and {q.GivenName} (once {oldName}) are wed at the town hall. Half the town turns out to cheer.",
            p.Id, q.Id);
    }

    private static Building? OwnedHome(Town town, Agent a) =>
        town.Buildings.FirstOrDefault(b => b.Complete && b.Def.Housing > 0 && b.OwnerId == a.Id && b.OpenBeds + (b.Residents.Contains(a.Id) ? 1 : 0) >= 1);

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
            town.Log(LedgerCategory.Social, Importance.Routine,
                $"{giver.Name} quietly presses a few coins into the hand of {needy.Name}, who has not eaten in a day.", giver.Id, needy.Id);
        }
    }
}
