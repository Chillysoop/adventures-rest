namespace CrownAndCompass.Sim.Systems;

/// <summary>A foe met by a ship at sea or in the air: its name, strength, speed in the approach, whether it boards, and the menace it is, if any.</summary>
public readonly record struct ShipFoe(string Name, double Power, double Speed, bool Boards, Menace? Menace = null);

/// <summary>
/// One fight for every ship of the town's fleet, at sea or in the air (Conner, 2026-10-22: "three-step ship fights shared with
/// airships, heroes as marines"). The approach: whoever wins it strikes first, and a captain who wins it against a foe far too
/// strong slips away. The volleys: the ship's guns against the foe's blows, which on an airship may tear the gasbag. Then the
/// boarding, if the foe boards: the crew, the guards aboard and the hero marines at the rail. The numbers are sky.json's
/// (<see cref="SkyFleetRules"/>), shared by both realms; the dice are the caller's.
/// </summary>
public static class ShipFight
{
    /// <summary>
    /// Fight it out: "evaded", "won", "boarded" (they boarded and were thrown off), "repelled" (they fought their way on and
    /// were driven off at a cost), "lost", "downed" (an airship out of lift) or "wrecked". Hull, lift, the marines' hit
    /// points, the captain's prowess, the prize and the menace's power change here; the caller tells it and acts on it.
    /// </summary>
    public static string Resolve(Town town, Ship s, ShipFoe foe, Region? region, IReadOnlyList<int> marines, double guards, bool storm, Rng rng, out int volleys)
    {
        var r = town.Content.Sky;
        var d = Fleet.Def(town, s);
        bool aloft = Fleet.Aloft(town, s);
        double skill = r.ProwessFight * (s.Prowess - 1);
        double ours = d.Guns * 4 + d.Hull * 0.1 + skill * 2;
        s.Fights++;
        volleys = 0;
        bool first = rng.D20() + d.Agility + skill >= 10 + foe.Speed + (storm ? r.StormApproach : 0);
        if (first && foe.Power > ours * r.EvadeAt) return "evaded";
        double theirs = foe.Power, start = foe.Power;
        for (int round = 0; round < r.Rounds + (first ? 1 : 0); round++)
        {
            volleys++;
            bool free = first && round == 0;
            theirs -= d.Guns * (1 + 0.1 * (s.Prowess - 1)) * (r.Low + rng.Next() * (r.High - r.Low)) * 2;
            if (theirs <= start * r.Break) break;
            if (free) continue;
            double blow = foe.Power * 0.25 * (r.Low + rng.Next() * (r.High - r.Low));
            if (aloft && rng.Chance(r.Gasbag)) s.Lift = Math.Max(0, s.Lift - blow / 6);
            else s.Hull = Math.Max(0, s.Hull - blow);
            if (s.Hull <= 0 || aloft && s.Lift <= 0 || s.Hull < d.Hull * 0.3) break;
        }
        string outcome;
        if (s.Hull <= 0) outcome = "wrecked";
        else if (aloft && s.Lift <= 0) outcome = "downed";
        else if (theirs <= start * r.Break) outcome = "won";
        else if (foe.Boards)
        {
            var aboard = marines.Select(town.Agent).Where(a => !a.Dead && a.Hero != null).ToList();
            double rail = d.Crew * r.CrewFight + guards + Js.Sum(aboard, a => a.Hero!.Level * r.HeroLevel);
            bool held = rng.D20() + rail >= rng.D20() + theirs;
            foreach (var a in aboard) a.Hero!.Hp = Math.Max(1, a.Hero.Hp - (int)Math.Round(a.Hero.MaxHp * r.HeroHurt * (held ? 0.5 : 1)));
            outcome = held ? "boarded" : "repelled";
            if (!held)
            {
                s.Hull = Math.Max(1, s.Hull - foe.Power * 0.5);
                if (aloft) s.Lift = Math.Max(0, s.Lift - 2);
            }
        }
        else outcome = "lost";
        if (outcome is "won" or "boarded")
        {
            s.Won++;
            s.Prowess = Math.Min(5, s.Prowess + (aloft ? r.Learn : Fleet.Rules(town).Learn * 2));
            town.Book(TreasuryAccount.Trade, r.Prize * (1 + (region?.Ring ?? 0)));
            foreach (var a in marines.Select(town.Agent).Where(a => !a.Dead && a.Hero != null)) Notoriety.Deed(town, a, aloft ? "sky" : "sea", r.Fame);
            if (foe.Menace is { } m) m.Power = Math.Max(1, m.Power * (1 - r.MenaceHurt));
        }
        return outcome;
    }
}
