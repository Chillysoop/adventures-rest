namespace CrownAndCompass.Sim.Systems;

/// <summary>The venture's side of <see cref="Dungeons"/>: walking a site's map, its room features, and the approach chosen at the door.</summary>
public static partial class Expedition
{
    /// <summary>One step on the main way: its encounter and, on a mapped site, its room.</summary>
    private sealed record Step(EncounterDef E, DungeonRoom? Room, bool Boss);

    public sealed partial class Venture
    {
        /// <summary>The site's map, when the delve walks one (companies); null for a fight settled on the spot.</summary>
        public DungeonState? Map;

        /// <summary>The rooms as they met them, for the delve card.</summary>
        public readonly List<RoomVisit> Visits = [];

        /// <summary>How they went in: storm, sneak, parley or siege (or the bounty's terms).</summary>
        public string Approach = "";

        /// <summary>They reach the heart unseen and strike first.</summary>
        private bool _strikeFirst;

        private DungeonRules Dungeon => _content.Dungeons;

        /// <summary>The main way of this delve: the map's rooms in order, or a draw from the deck for a fight settled on the spot.</summary>
        private List<Step> Steps(SiteDef site)
        {
            var steps = new List<Step>();
            if (Map != null)
            {
                var way = Dungeons.MainWay(Map);
                // A tracker finds a back way round the first room still held.
                bool skipped = !_tracked;
                foreach (var room in way)
                {
                    if (room.Enc == Dungeons.Boss)
                    {
                        steps.Add(new Step(site.Boss, room, true));
                        continue;
                    }
                    if (!skipped && !room.Cleared)
                    {
                        skipped = true;
                        continue;
                    }
                    steps.Add(new Step(Encounter(site, room), room, false));
                }
                return steps;
            }
            var deck = site.Encounters.ToList();
            int count = Math.Min(deck.Count, 2 + site.Tier - (_tracked ? 1 : 0));
            for (int i = 0; i < count; i++)
            {
                int k = _rng.Range(0, deck.Count);
                steps.Add(new Step(deck[k], null, false));
                deck.RemoveAt(k);
            }
            steps.Add(new Step(site.Boss, null, true));
            return steps;
        }

        private EncounterDef Encounter(SiteDef site, DungeonRoom room) =>
            room.Enc == Dungeons.Boss ? site.Boss
            : room.Enc == Dungeons.Cache || room.Enc >= site.Encounters.Count ? Dungeons.CacheOf(_content, site)
            : site.Encounters[room.Enc];

        /// <summary>Walk through a room already cleared, springing any trap the occupants set in it since.</summary>
        private void Pass(SiteDef site, DungeonRoom room)
        {
            Clock += Dungeon.PassHours;
            if (!room.Trapped) return;
            room.Trapped = false;
            var r = Dungeon.Reactions;
            int line = Lines.Count;
            Encounter(new EncounterDef { Kind = "trap", Dc = r.TrapDc + Math.Max(1, site.Tier) / 2, DamageDie = r.TrapDie, Text = $"a new trap in {Dungeons.Name(site, room)}" });
            Visits.Add(new RoomVisit(room.Id, line, Lines.Count - 1, "trap"));
        }

        /// <summary>
        /// A room's feature: someone in the party has the answer, or it costs them (to hit and armour for the room, the
        /// foes first, hours, a check against a fall). Returns the heroes it weakened, to put right after the room.
        /// </summary>
        private (List<Combatant> Who, int Attack, int Ac, bool First) Feature(SiteDef site, DungeonRoom? room)
        {
            var none = (new List<Combatant>(), 0, 0, false);
            if (room == null || Dungeon.Feature(room.Feature) is not { } f) return none;
            var who = Standing.Where(c => f.Races.Contains(c.Agent.Race) || (f.Classes.Contains(c.Sheet.Cls.Id) && c.Sheet.Level >= f.MinLevel))
                .OrderBy(c => c.Agent.Id).FirstOrDefault();
            if (who != null)
            {
                Log($"{Cap(f.Text)}. {Cap(f.Answer.Replace("{name}", who.Agent.GivenName, StringComparison.Ordinal))}.");
                Mark("feature", who);
                Xp += 10;
                return none;
            }
            Log($"{Cap(f.Text)}, and nobody has the knack of it.");
            Mark("feature");
            Clock += f.Hours;
            if (f.Check is { } check)
            {
                int dc = check.Dc + check.PerTier * Math.Max(1, site.Tier);
                var hurt = new List<string>();
                foreach (var c in Standing)
                {
                    int roll = _rng.D20();
                    if (roll + Rng.Modifier(c.Agent.Abilities[check.Ability]) >= dc) continue;
                    int dmg = 0;
                    for (int k = 0; k < Scale; k++) dmg += _rng.Roll(1, check.Die);
                    Hurt(c, dmg);
                    hurt.Add($"{c.Agent.GivenName} ({dmg})");
                }
                if (hurt.Count > 0) Log($"Not everyone makes it over cleanly: {string.Join(", ", hurt)}.");
                if (Party.Any(c => c.Down && !c.Dead)) AfterCombat();
            }
            var weak = Standing;
            foreach (var c in weak)
            {
                c.Attack += f.Attack;
                c.Ac += f.Ac;
            }
            return (weak, f.Attack, f.Ac, f.MonstersFirst);
        }

        private static void Unfeature((List<Combatant> Who, int Attack, int Ac, bool First) feat)
        {
            foreach (var c in feat.Who)
            {
                c.Attack -= feat.Attack;
                c.Ac -= feat.Ac;
            }
        }

        /// <summary>
        /// Look into the side rooms off a room, while they're fit for it: found on a check unless the place is mapped,
        /// fought or searched like any room, never retried. Returns false if a side room beat them.
        /// </summary>
        private bool Sides(SiteDef site, DungeonRoom room)
        {
            if (Map == null || Terms != null || Approach == "sneak") return true;
            foreach (var side in Dungeons.SidesOf(Map, room))
            {
                if (side.Cleared) continue;
                if (Wiped || HpFraction() < Dungeon.SideHealthy) return true;
                if (!Map.Mapped)
                {
                    var look = BestCheck(SkillKind.Perception);
                    int dc = Dungeon.FindDc + Dungeon.FindPerTier * Math.Max(1, site.Tier);
                    if (look.Total < dc) continue;
                    Log($"{look.Name} spots a way off to the side (d20 {look.Roll}, {look.Total} vs DC {dc}).");
                    Mark("find", look, dc);
                }
                Clock += 1;
                int line = Lines.Count;
                var e = Encounter(site, side);
                var feat = Feature(site, side);
                bool won = true;
                if (e.Kind == "combat") won = Combat(e, feat.First);
                else
                {
                    Encounter(e);
                    won = !EncounterLost;
                }
                Unfeature(feat);
                MarkLost();
                if (side.Enc == Dungeons.Cache && won && Standing.Count > 0 && _rng.Chance(Dungeon.CacheItem))
                {
                    string found = Treasure(site, true);
                    if (found != "") Log($"Among it{found}.");
                }
                if (won && !Wiped) side.Cleared = true;
                Visits.Add(new RoomVisit(side.Id, line, Lines.Count - 1, won ? "cleared" : "lost"));
                if (!won || Wiped) return false;
            }
            return true;
        }

        /// <summary>
        /// At the door: storm it, slip in by a back way, talk to the master, or camp round it and starve it out, by
        /// what the party is good at, what it knows of the place, how outmatched it is and the leader's temper.
        /// </summary>
        public void Choose(SiteDef site, bool mapped, bool canParley)
        {
            if (Terms != null)
            {
                Approach = Terms.Id;
                return;
            }
            var a = Dungeon.Approaches;
            double avg = Party.Average(c => c.Sheet.Level);
            double underdog = Math.Max(0, site.Tier - avg);
            var leader = Party[0].Agent;
            bool bold = leader.Has(Trait.HotTempered) || leader.Has(Trait.Ambitious);
            // The party's make-up leans the choice: the classes that suit a way in, and a big company's patience for a siege.
            double Lean(DoorDef d) =>
                (Party.Any(c => d.Classes.Contains(c.Sheet.Cls.Id)) ? d.Class : 0) + (Party.Count >= d.Crowd ? d.Crowded : 0);
            var options = new List<(string Id, double W)> { ("storm", a.Storm.Weight + (bold ? a.Storm.Bold : 0) + Lean(a.Storm)) };
            if (!bold)
            {
                var quiet = Standing.Select(c => SkillBonus(c, SkillKind.Trap)).DefaultIfEmpty(0).Max();
                if (Party.Count <= a.Sneak.MaxParty && quiet >= a.Sneak.Min)
                    options.Add(("sneak", Lean(a.Sneak) + a.Sneak.Weight + (mapped ? a.Sneak.Mapped : 0) + underdog * a.Sneak.Underdog));
                var voice = Standing.Select(c => SkillBonus(c, SkillKind.Parley)).DefaultIfEmpty(0).Max();
                if (canParley && voice >= a.Parley.Min && _content.Monster(site.Boss.Monster).Int >= a.Parley.MinInt)
                    options.Add(("parley", Lean(a.Parley) + a.Parley.Weight + underdog * a.Parley.Underdog));
                if (Party.Count >= a.Siege.MinParty && a.Siege.Kinds.Contains(site.Kind))
                    options.Add(("siege", Lean(a.Siege) + a.Siege.Weight + (mapped ? a.Siege.Mapped : 0) + underdog * a.Siege.Underdog));
            }
            var pick = options.Count == 1 ? options[0] : options.OrderByDescending(o => o.W + _rng.Next() * a.Noise).First();
            Approach = pick.Id;
            if (Approach == "parley") Terms = _content.Bounties.Term("parley");
            if (a.Texts.TryGetValue(Approach, out var text) && (Approach != "storm" || options.Count > 1))
            {
                var face = Approach == "sneak" ? Standing.OrderByDescending(c => SkillBonus(c, SkillKind.Trap)).ThenBy(c => c.Agent.Id).First() : Party[0];
                Log(text.Replace("{name}", face.Agent.GivenName, StringComparison.Ordinal).Replace("{site}", site.Name, StringComparison.Ordinal));
                Mark("approach", face);
            }
            if (Approach == "siege") Siege(site);
        }

        /// <summary>Camp round the place for days, thinning every fight inside, and beat off a sally on some nights.</summary>
        private void Siege(SiteDef site)
        {
            var s = Dungeon.Approaches.Siege;
            for (int day = 0; day < s.Days && !Wiped; day++)
            {
                double from = Clock;
                if (_rng.Chance(s.Sally) && site.Encounters.FirstOrDefault(e => e.Kind == "wander") is { } sally)
                {
                    Log("They sally out at dusk to break the siege.");
                    Encounter(sally);
                    MarkLost();
                }
                Clock = Systems.Roads.WakeAfterRest(_content, Clock + 12);
                Halts.Add(new Halt(from, Clock, "camp"));
                LongRest();
            }
            Swell += s.Swell;
        }

        /// <summary>The hoard, if the occupants hid it: found on a check at the heart, or only part of the gold.</summary>
        private double HoardShare(SiteDef site)
        {
            if (Map is not { Hidden: true }) return 1;
            var look = BestCheck(SkillKind.Perception);
            int dc = Dungeon.FindDc + 2 + Dungeon.FindPerTier * Math.Max(1, site.Tier);
            if (look.Total >= dc)
            {
                Log($"{look.Name} finds where they hid the hoard (d20 {look.Roll}, {look.Total} vs DC {dc}).");
                Mark("find", look, dc);
                Map.Hidden = false;
                return 1;
            }
            Log($"They never find where the hoard was hidden (d20 {look.Roll}, {look.Total} vs DC {dc}), only what was left lying about.");
            Mark("find", look, dc);
            return Dungeon.Reactions.HiddenShare;
        }
    }
}
