namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The mayor's dice and growth, shared by stories and everyday decisions (content/game/stories.json).
/// Every check is a d20 plus the mayor's ability modifier and proficiency against a DC. Inspiration,
/// earned by running the town well, rerolls a miss. Each level brings an advance the player chooses:
/// abilities or a feat at improvement levels, a new skill or expertise in one at the others.
/// </summary>
public static class Sheet
{
    private static StoryRules Rules(Town town) => town.Content.Stories;

    // ---------- Dice ----------

    /// <summary>The die as it counts: a feat's floor lifts low rolls on skills the mayor is trained in.</summary>
    private static int Face(Town town, string skill, int die)
    {
        int floor = skill != "" && Stories.Proficient(town, skill) ? Feats(town).Select(f => f.Floor).DefaultIfEmpty(0).Max() : 0;
        return Math.Max(die, floor);
    }

    private static bool Hits(Town town, string skill, int die, int bonus, int dc)
    {
        int f = Face(town, skill, die);
        return f == 20 || (f != 1 && f + bonus >= dc);
    }

    /// <summary>The chance a check succeeds (a natural 20 always does, a natural 1 never), with advantage and an inspired reroll if it has them.</summary>
    public static double Chance(Town town, string skill, string ability, int dc, bool advantage, bool inspired, int help = 0)
    {
        var (mod, prof, _) = Stories.Bonus(town, skill, ability);
        int faces = 0;
        for (int f = 1; f <= 20; f++)
            if (Hits(town, skill, f, mod + prof + help, dc)) faces++;
        double p = faces / 20.0;
        if (advantage) p = 1 - (1 - p) * (1 - p);
        if (inspired && town.Stories.Mayor.Inspiration > 0) p = 1 - (1 - p) * (1 - p);
        return p;
    }

    public static int PartialMargin(Town town) => Rules(town).PartialMargin + Feats(town).Sum(f => f.PartialMargin);

    /// <summary>
    /// Roll a check of the mayor's, with any help (a party on the sending stone): the d20 (two, keeping the higher, with advantage), and if it misses
    /// and inspiration is asked for and in hand, one spent to throw again. A natural 20 earns inspiration.
    /// </summary>
    public static StoryBeat Roll(Town town, string skill, string ability, int dc, string advantage, bool inspired, bool partial, int help = 0)
    {
        var m = town.Stories.Mayor;
        var beat = new StoryBeat { Tick = town.Tick, Skill = skill != "" ? skill : ability, Dc = dc, Advantage = advantage };
        var (mod, prof, _) = Stories.Bonus(town, skill, ability);
        int Throw()
        {
            int d = town.Rng.D20();
            if (advantage == "") return d;
            int other = town.Rng.D20();
            beat.Other = Math.Min(d, other);
            return Math.Max(d, other);
        }
        int die = Throw();
        if (inspired && m.Inspiration > 0 && !Hits(town, skill, die, mod + prof + help, dc))
        {
            m.Inspiration--;
            beat.Rerolled = Face(town, skill, die);
            die = Throw();
        }
        die = Face(town, skill, die);
        beat.Die = die;
        beat.Modifier = mod;
        beat.Proficiency = prof;
        beat.Help = help;
        beat.Total = die + mod + prof + help;
        beat.Result = die == 20 ? StoryResult.Critical
            : die == 1 ? StoryResult.Fumble
            : beat.Total >= dc ? StoryResult.Success
            : partial && dc - beat.Total <= PartialMargin(town) ? StoryResult.Partial
            : StoryResult.Failure;
        if (die == 20) Inspire(town, 1, "a natural 20");
        return beat;
    }

    public static bool Succeeded(StoryBeat b) => b.Result is StoryResult.Critical or StoryResult.Success or StoryResult.Certain;

    /// <summary>
    /// A light check on an everyday decision (a council motion, a demand answered, an envoy, a crackdown,
    /// a haggle). It only adds to what the decision does anyway. Logged to the Ledger and kept as the last
    /// check so the client can show the die. Null when the content has no check for the decision.
    /// </summary>
    public static StoryBeat? Decide(Town town, string decision, int? dc = null)
    {
        if (!Rules(town).Decisions.TryGetValue(decision, out var d)) return null;
        var beat = Roll(town, d.Skill, "", dc ?? d.Dc, "", false, false);
        beat.Option = decision;
        bool ok = Succeeded(beat);
        beat.Text = ok ? d.Success : d.Failure;
        var st = town.Stories;
        st.LastCheck = beat;
        st.Checks++;
        string skill = Rules(town).Skill(d.Skill)?.Name ?? d.Skill;
        town.Log(LedgerCategory.Story, Importance.Routine, $"Mayor {st.Mayor.Name} rolls {skill}: {beat.Total} against DC {beat.Dc}, {Stories.Word(beat.Result)}.{(beat.Text != "" ? " " + beat.Text : "")}");
        return beat;
    }

    /// <summary>What a decision's check adds when it succeeds (doubled on a natural 20), or 0.</summary>
    public static double Gain(Town town, string decision, StoryBeat? b) =>
        b == null || !Succeeded(b) ? 0 : Rules(town).Decisions[decision].Gain * (b.Result == StoryResult.Critical ? 2 : 1);

    // ---------- Inspiration ----------

    public static int InspirationMax(Town town) => Rules(town).InspirationMax + Feats(town).Sum(f => f.Inspiration);

    /// <summary>Give the mayor inspiration, up to what they can hold, saying why in the Ledger if a reason is given.</summary>
    public static void Inspire(Town town, int n, string why)
    {
        var m = town.Stories.Mayor;
        int before = m.Inspiration;
        m.Inspiration = Math.Clamp(m.Inspiration + n, 0, InspirationMax(town));
        if (why != "" && m.Inspiration > before)
            town.Log(LedgerCategory.Story, Importance.Routine, $"Mayor {m.Name} is inspired by {why} ({m.Inspiration} of {InspirationMax(town)}).");
    }

    /// <summary>How far the town has come at a deed: charters, bounties fulfilled, raids thrown back, menaces ended, contented days.</summary>
    public static int Count(Town town, DeedDef d) => d.Id switch
    {
        "charter" => town.City.Tier,
        "bounties" => town.Bounties.Count(b => b.Status == BountyStatus.Completed),
        "repelled" => town.Stories.Repelled,
        "slain" => town.Saga.Menaces.Count(m => !m.Alive),
        "content" => town.Stories.ContentDays,
        _ => 0,
    };

    /// <summary>A raid or assault thrown back from the town.</summary>
    public static void Repelled(Town town) => town.Stories.Repelled++;

    /// <summary>The day's deeds: each one the town has done again since it was last rewarded earns inspiration.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        var st = town.Stories;
        st.ContentDays = r.Deeds.FirstOrDefault(d => d.Id == "content") is { } c && town.Mandate >= c.Threshold ? st.ContentDays + 1 : 0;
        foreach (var d in r.Deeds)
        {
            int reached = Count(town, d) / Math.Max(1, d.Every);
            // Contented days run in a streak: each full stretch earns one, then the streak starts again.
            if (d.Id == "content")
            {
                if (reached >= 1)
                {
                    st.ContentDays = 0;
                    Inspire(town, 1, d.Text);
                }
                continue;
            }
            // The first look only notes where the town stands (an old save, or a town with a head start).
            if (!st.Deeds.ContainsKey(d.Id))
            {
                st.Deeds[d.Id] = reached;
                continue;
            }
            int had = st.Deeds.Get(d.Id, 0);
            if (reached <= had) continue;
            st.Deeds[d.Id] = reached;
            Inspire(town, reached - had, d.Text);
        }
        foreach (var f in Feats(town))
            if (f.RegainDays > 0 && town.Day % f.RegainDays == 0) Inspire(town, 1, f.Name.ToLowerInvariant());
        if (autoMayor) AutoAdvance(town);
    }

    /// <summary>Progress toward a deed's next inspiration, for the sheet.</summary>
    public static (int Have, int Need) Progress(Town town, DeedDef d)
    {
        int every = Math.Max(1, d.Every);
        if (d.Id == "content") return (town.Stories.ContentDays, every);
        return (Count(town, d) - town.Stories.Deeds.Get(d.Id, 0) * every, every);
    }

    // ---------- Levels ----------

    public static List<FeatDef> Feats(Town town)
    {
        var r = Rules(town);
        var list = new List<FeatDef>();
        foreach (var a in town.Stories.Mayor.Advances)
            if (a.StartsWith("feat:", StringComparison.Ordinal) && r.Feat(a[5..]) is { } f) list.Add(f);
        return list;
    }

    /// <summary>Advances the mayor has earned but not yet chosen.</summary>
    public static int Pending(Town town) => Math.Max(0, town.Stories.Mayor.Level - 1 - town.Stories.Mayor.Advances.Count);

    /// <summary>The level the next advance is for, and whether it is an improvement level (abilities or a feat) or a skill level.</summary>
    public static (int Level, bool Improvement) NextAdvance(Town town)
    {
        int level = town.Stories.Mayor.Advances.Count + 2;
        return (level, Rules(town).Improvements.Contains(level));
    }

    /// <summary>Why an advance can't be taken now, or null if it can.</summary>
    public static string? Refuse(Town town, string advance)
    {
        var r = Rules(town);
        if (Pending(town) == 0) return "no advance to choose";
        var (level, improvement) = NextAdvance(town);
        int colon = advance.IndexOf(':');
        if (colon < 0) return "not an advance";
        string kind = advance[..colon], what = advance[(colon + 1)..];
        var m = town.Stories.Mayor;
        switch (kind)
        {
            case "ability":
            {
                if (!improvement) return "abilities rise at improvement levels";
                var abs = what.Split(',');
                if (abs.Length != 2 || abs.Any(a => !Ability.All.Contains(a))) return "choose two abilities (or one twice)";
                var scores = Stories.Scores(town);
                if (abs.Distinct().Any(a => scores[a] + abs.Count(x => x == a) > 20)) return "abilities stop at 20";
                return null;
            }
            case "feat":
                if (!improvement) return "feats come at improvement levels";
                if (r.Feat(what) == null) return "no such feat";
                return m.Advances.Contains(advance) ? "already taken" : null;
            case "skill":
                if (improvement) return "skills come at the other levels";
                if (r.Skill(what) == null) return "no such skill";
                return Stories.Proficient(town, what) ? "already trained" : null;
            case "expertise":
                if (improvement) return "expertise comes at the other levels";
                if (level < r.ExpertiseFrom) return $"expertise comes from level {r.ExpertiseFrom}";
                if (!Stories.Proficient(town, what)) return "needs the skill trained first";
                return Stories.Expert(town, what) ? "already expert" : null;
        }
        return "not an advance";
    }

    /// <summary>Spend the next advance: "ability:str,cha" (+1 each, or one twice), "feat:id", "skill:id" or "expertise:id".</summary>
    public static bool Advance(Town town, string advance)
    {
        if (Refuse(town, advance) != null) return false;
        var m = town.Stories.Mayor;
        var r = Rules(town);
        var (level, _) = NextAdvance(town);
        m.Advances.Add(advance);
        string what = advance[(advance.IndexOf(':') + 1)..];
        string said = advance.Split(':')[0] switch
        {
            "ability" => $"grows in {string.Join(" and ", what.Split(',').Distinct().Select(a => a.ToUpperInvariant()))}",
            "feat" => $"takes the feat {r.Feat(what)!.Name}",
            "skill" => $"learns {r.Skill(what)!.Name}",
            _ => $"becomes expert in {r.Skill(what)!.Name}",
        };
        town.Log(LedgerCategory.Story, Importance.Routine, $"At level {level}, Mayor {m.Name} {said}.");
        if (r.Feat(what) is { Inspiration: > 0 } f && advance.StartsWith("feat:", StringComparison.Ordinal)) Inspire(town, f.Inspiration, "");
        return true;
    }

    /// <summary>How often each skill comes up in the stories, so the stand-in learns the useful ones.</summary>
    private static int Use(Town town, string skill) => Rules(town).Stories.Sum(d => d.Stages.Sum(s => s.Options.Count(o => o.Rolls && o.Skill == skill)));

    /// <summary>The stand-in mayor spends advances as the old sheet grew: its best abilities at improvement levels, otherwise the most used skill, or expertise in one.</summary>
    private static void AutoAdvance(Town town)
    {
        var r = Rules(town);
        for (int guard = 0; guard < 12 && Pending(town) > 0; guard++)
        {
            var (_, improvement) = NextAdvance(town);
            string? pick;
            if (improvement)
            {
                var bg = r.Background(town.Stories.Mayor.Background);
                var scores = Stories.Scores(town);
                var two = new List<string>();
                foreach (var ab in bg.Abilities)
                    while (two.Count < 2 && scores[ab] + two.Count(x => x == ab) < 20) two.Add(ab);
                pick = two.Count == 2 ? $"ability:{string.Join(",", two)}" : r.Feats.Select(f => "feat:" + f.Id).FirstOrDefault(a => Refuse(town, a) == null);
            }
            else
            {
                var byUse = r.Skills.Select(s => s.Id).OrderByDescending(s => Use(town, s)).ToList();
                int skills = town.Stories.Mayor.Advances.Count(a => a.StartsWith("skill:", StringComparison.Ordinal));
                int experts = town.Stories.Mayor.Advances.Count(a => a.StartsWith("expertise:", StringComparison.Ordinal));
                var learn = byUse.Select(s => "skill:" + s).FirstOrDefault(a => Refuse(town, a) == null);
                var sharpen = byUse.Select(s => "expertise:" + s).FirstOrDefault(a => Refuse(town, a) == null);
                pick = skills > experts && sharpen != null ? sharpen : learn ?? sharpen;
            }
            if (pick == null || !Advance(town, pick)) return;
        }
    }
}
