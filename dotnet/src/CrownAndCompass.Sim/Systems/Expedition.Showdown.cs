namespace CrownAndCompass.Sim.Systems;

/// <summary>A creature on the showdown's grid as the screen draws it.</summary>
public sealed record GridUnit(int Id, string Name, bool Hero, int AgentId, string Kind, int X, int Y, int Hp, int MaxHp, int Ac, bool Down, bool Dead, bool Boss, int Speed, IReadOnlyList<string> Marks);

/// <summary>Something the hero whose turn it is can do: what it is, how far it reaches, what it needs aimed at, and whether it can now.</summary>
public sealed record GridAction(string Id, string Name, string Icon, string Kind, string Aim, int Range, int Area, bool Bonus, bool Ready, string Tip);

/// <summary>A way to talk the master round, as the screen offers it.</summary>
public sealed record TalkOption(string Id, string Name, string Icon, int HeroId, int Bonus, int Dc);

public static partial class Expedition
{
    public sealed partial class Venture
    {
        /// <summary>The boss fight of this delve is a showdown the mayor may take command of (<see cref="Showdowns"/>).</summary>
        public bool Duel;

        /// <summary>The delve stopped at the master's door for the showdown; <see cref="AfterDoor"/> finishes it.</summary>
        public bool AtDoor;
        public DungeonRoom? DoorRoom;
        private int _doorLine;
        private double _doorStart;
        private (List<Combatant> Who, int Attack, int Ac, bool First) _doorFeat = ([], 0, 0, false);

        /// <summary>The showdown is over: the boss room as it ended, the hoard, and the delve's outcome.</summary>
        internal ExpeditionOutcome AfterDoor(SiteDef site, Showdown fight)
        {
            AtDoor = false;
            Clock += 1;
            Unfeature(_doorFeat);
            MarkLost();
            bool won = fight.Won && !Wiped;
            if (fight.Talked && !fight.Yielded) Talked = true;
            if (DoorRoom is { } room)
            {
                if (won)
                {
                    room.Cleared = true;
                    room.Retaken = false;
                    if (fight.Talked && Map != null) foreach (var r in Map.Rooms) r.Cleared = true;
                }
                Visits.Add(new RoomVisit(room.Id, _doorLine, Lines.Count - 1, !won ? "lost" : fight.Talked ? "talked" : "cleared"));
            }
            Halts.Insert(0, new Halt(_doorStart, Clock, "delve"));
            var outcome = Wiped ? ExpeditionOutcome.Wiped : won ? ExpeditionOutcome.Cleared : ExpeditionOutcome.Retreated;
            if (outcome == ExpeditionOutcome.Cleared && !Driven)
            {
                SiteCleared = true;
                if (Talked) Gold += (int)(_rng.Range(site.Loot[0], site.Loot[1] + 1) * _content.Showdown.Talk.Loot);
                else Hoard(site);
            }
            return outcome;
        }

        /// <summary>
        /// A showdown: the master of a meaningful place met on a grid. Talk first, then a fight on SRD rules, turn by
        /// turn in initiative order, every hero either left to their own judgment or commanded. It drives the venture's
        /// own fight code (every spell, power, item and potion goes through the same calls as any fight), only limited
        /// to what each one can reach from where it stands.
        /// </summary>
        public sealed class Showdown
        {
            private sealed class Unit
            {
                public int Id;
                public Combatant? C;
                public Foe? F;
                public int X, Y;
                public int Init;
                public int Move;
                public bool Acted, Bonus, Reaction = true;
                public int Dodge;
                public bool Hero => C != null;
                public bool Out => C != null ? C.Down || C.Dead : F!.Hp <= 0;
            }

            private readonly Venture _v;
            private readonly SiteDef _site;
            private readonly EncounterDef _e;
            private readonly ShowdownRules _r;
            private readonly List<Unit> _units = [];
            private readonly List<Foe> _foes = [];
            private readonly Rng _dice;
            private int _turn = -1;
            private int _powers;
            private int _wins, _losses;
            private int _logFrom;

            public int W { get; }
            public int H { get; }

            /// <summary>The ground of each cell: "" open, wall, pillar, chasm, water, rubble, web.</summary>
            public string[] Ground { get; }

            /// <summary>talk, fight or over.</summary>
            public string Phase { get; private set; } = "talk";
            public int Round { get; private set; }
            public bool Won { get; private set; }
            public bool Talked { get; private set; }
            public bool Yielded { get; private set; }
            public string Boss { get; }

            /// <summary>Points won and lost in the talk, against <see cref="TalkRules.Wins"/> and <see cref="TalkRules.Losses"/>.</summary>
            public int Wins => _wins;
            public int Losses => _losses;

            /// <summary>The master's monster id, for its face.</summary>
            public string BossId => _e.Monster;
            public string SiteName => _site.Name;

            /// <summary>Heroes the mayor commands; the rest act on their own.</summary>
            public HashSet<int> Commanded { get; } = [];

            /// <summary>The fight's lines so far, from the venture's tale.</summary>
            public IReadOnlyList<string> Lines => _v.Lines.Skip(_logFrom).ToList();

            internal Showdown(Venture v, SiteDef site, EncounterDef e, ShowdownRules rules, string feature, int seed)
            {
                _v = v;
                _site = site;
                _e = e;
                _r = rules;
                _dice = new Rng(seed);
                _logFrom = v.Lines.Count;
                W = Math.Max(6, rules.Grid.W);
                H = Math.Max(5, rules.Grid.H);
                Ground = new string[W * H];
                for (int i = 0; i < Ground.Length; i++) Ground[i] = "";
                Lay(feature);
                var def = v._content.Monster(e.Monster);
                Boss = The(def);
                foreach (var c in v.Party) Commanded.Add(c.Agent.Id);
                Muster();
                v.Log(Fill(_r.Talk.Lines.GetValueOrDefault("open", ""), "", 0, 0, 0));
                v.Mark("boss");
                // A master too dim to bargain: straight to blades.
                if ((Prowess.Lib(v._content, def)?.Int ?? def.Int) < _r.Talk.MinInt) Begin(false, false);
            }

            // The room ------------------------------------------------------------------------------

            private void Lay(string feature)
            {
                int pillars = _dice.Range(_r.Pillars[0], _r.Pillars[^1] + 1);
                for (int i = 0; i < pillars; i++)
                {
                    int x = _dice.Range(3, W - 3), y = _dice.Range(1, H - 1);
                    Ground[y * W + x] = "pillar";
                }
                if (!_r.Ground.TryGetValue(feature, out var g) || g.Kind == "" || g.Cells <= 0) return;
                if (g.Kind == "chasm")
                {
                    // A rift across the room with a ledge to cross at.
                    int x = W / 2;
                    int gap = _dice.Range(1, H - 1);
                    for (int y = 0; y < H; y++) if (y != gap) Ground[y * W + x] = "chasm";
                    return;
                }
                for (int i = 0; i < g.Cells; i++)
                {
                    int x = _dice.Range(2, W - 2), y = _dice.Range(0, H);
                    if (Ground[y * W + x] == "") Ground[y * W + x] = g.Kind;
                }
            }

            private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
            private string At(int x, int y) => Inside(x, y) ? Ground[y * W + x] : "wall";
            private bool Blocks(int x, int y, bool flies) => At(x, y) is "wall" or "pillar" || (!flies && At(x, y) == "chasm");
            private static int Cost(string g, bool flies) => !flies && g is "water" or "rubble" or "web" ? 2 : 1;
            private Unit? Standing(int x, int y) => _units.FirstOrDefault(u => !u.Out && u.X == x && u.Y == y);
            private static int Dist(Unit a, int x, int y) => Math.Max(Math.Abs(a.X - x), Math.Abs(a.Y - y));
            private static int Dist(Unit a, Unit b) => Dist(a, b.X, b.Y);

            private bool Flies(Unit u) => u.F?.Lib is { Fly: > 0 } || u.C?.Form is { Fly: > 0 };

            /// <summary>Squares a unit moves in a turn: 30 feet for most heroes, a monk or barbarian faster, the small slower; a foe's from its stat block.</summary>
            private int Speed(Unit u)
            {
                if (u.F != null) return u.F.Lib is { Fly: > 0 } ? _r.Fly : _r.Speed;
                var c = u.C!;
                return Math.Max(3, _r.Speed + _r.FastClasses.GetValueOrDefault(c.Sheet.Cls.Id, 0) + _r.SmallRaces.GetValueOrDefault(c.Agent.Race, 0));
            }

            /// <summary>Every square a unit can reach this turn with the movement it has left, and the cost to each.</summary>
            private Dictionary<(int, int), int> Reach(Unit u, int budget)
            {
                bool flies = Flies(u);
                var best = new Dictionary<(int, int), int> { [(u.X, u.Y)] = 0 };
                var open = new List<(int X, int Y)> { (u.X, u.Y) };
                while (open.Count > 0)
                {
                    var next = new List<(int X, int Y)>();
                    foreach (var (x, y) in open)
                    {
                        int have = best[(x, y)];
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx, ny = y + dy;
                                if (!Inside(nx, ny) || Blocks(nx, ny, flies)) continue;
                                var other = Standing(nx, ny);
                                // Through friends, never through foes.
                                if (other != null && other.Hero != u.Hero) continue;
                                int cost = have + Cost(At(nx, ny), flies);
                                if (cost > budget || (best.TryGetValue((nx, ny), out int was) && was <= cost)) continue;
                                best[(nx, ny)] = cost;
                                next.Add((nx, ny));
                            }
                    }
                    open = next;
                }
                // Nobody ends a move on someone else.
                foreach (var k in best.Keys.ToList()) if (k != (u.X, u.Y) && Standing(k.Item1, k.Item2) != null) best.Remove(k);
                return best;
            }

            // Talk -----------------------------------------------------------------------------------

            /// <summary>The ways to talk the master round, each by the hero best at it, with the DC it is rolled against.</summary>
            public List<TalkOption> TalkOptions()
            {
                if (Phase != "talk") return [];
                var list = new List<TalkOption>();
                foreach (var w in _r.Talk.Ways)
                {
                    var best = _v.Standing.OrderByDescending(c => TalkBonus(c, w)).ThenBy(c => c.Agent.Id).FirstOrDefault();
                    if (best != null) list.Add(new TalkOption(w.Id, w.Name, w.Icon, best.Agent.Id, TalkBonus(best, w), TalkDc(w)));
                }
                return list;
            }

            private int TalkBonus(Combatant c, TalkWayDef w)
            {
                int ab = Rng.Modifier(c.Agent.Abilities[w.Ability]);
                if (w.Ability != "cha") ab = Math.Max(ab, Rng.Modifier(c.Agent.Abilities.Cha));
                int cls = c.Sheet.Cls.Skills.GetValueOrDefault(SkillKind.Parley, 0);
                return ab + cls + (cls > 0 ? Proficiency(c.Sheet.Level) : 0) + c.Skill;
            }

            private int TalkDc(TalkWayDef w)
            {
                var def = _v._content.Monster(_e.Monster);
                var lib = Prowess.Lib(_v._content, def);
                int score = w.Against switch { "cha" => lib?.Cha ?? 10, "int" => lib?.Int ?? def.Int, _ => lib?.Wis ?? 10 };
                return 10 + Rng.Modifier(score) + Math.Max(1, _site.Tier) / 2;
            }

            /// <summary>Try a way of talking, by a hero (or the party's best at it). Returns false if there is no talking now.</summary>
            public bool Talk(string way, int heroId = 0)
            {
                if (Phase != "talk") return false;
                var w = _r.Talk.Ways.FirstOrDefault(x => x.Id == way);
                if (w == null) return false;
                var who = _v.Standing.FirstOrDefault(c => c.Agent.Id == heroId) ?? _v.Standing.OrderByDescending(c => TalkBonus(c, w)).ThenBy(c => c.Agent.Id).First();
                int dc = TalkDc(w);
                int roll = _dice.D20();
                int total = roll + TalkBonus(who, w);
                _v.Log(Fill(w.Text, who.Agent.GivenName, 0, 0, 0) + ".");
                bool ok = roll == 20 || (roll != 1 && total >= dc);
                _v.Log(Fill(_r.Talk.Lines.GetValueOrDefault(ok ? "won" : "lost", ""), who.Agent.GivenName, roll, total, dc));
                _v.Mark("parley", who, roll, total, dc);
                if (ok) _wins++;
                else _losses++;
                if (_wins >= _r.Talk.Wins)
                {
                    _v.Log(Fill(_r.Talk.Lines.GetValueOrDefault(way, ""), who.Agent.GivenName, 0, 0, 0));
                    _v.Mark("goal", who);
                    if (way == "deceive") Begin(true, true);
                    else
                    {
                        Talked = true;
                        Yielded = way == "intimidate";
                        Won = true;
                        Phase = "over";
                    }
                }
                else if (_losses >= _r.Talk.Losses)
                {
                    _v.Log(Fill(_r.Talk.Lines.GetValueOrDefault("fail", ""), who.Agent.GivenName, 0, 0, 0));
                    Begin(false, false, true);
                }
                return true;
            }

            /// <summary>No more talk: draw weapons, with the first blow if nobody has spoken yet.</summary>
            public void Attack()
            {
                if (Phase != "talk") return;
                bool first = _wins + _losses == 0;
                if (first) _v.Log(Fill(_r.Talk.Lines.GetValueOrDefault("attack", ""), "", 0, 0, 0));
                Begin(first, false);
            }

            private string Fill(string text, string hero, int roll, int total, int dc) => Cap(text
                .Replace("{boss}", Boss, StringComparison.Ordinal)
                .Replace("{hero}", hero, StringComparison.Ordinal)
                .Replace("{roll}", roll.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{total}", total.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{dc}", dc.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));

            // The fight ------------------------------------------------------------------------------

            /// <summary>Blades out: the master and its guards take the far side, the party the near; initiative is rolled.</summary>
            /// <summary>Everyone takes their place: the party at the near wall, the master and its guards at the far one.</summary>
            private void Muster()
            {
                var v = _v;
                var def = v._content.Monster(_e.Monster);
                _foes.Add(v.NewFoe(def));
                if (!string.IsNullOrEmpty(_e.Escort) && _e.EscortCount != null)
                {
                    var ed = v._content.Monster(_e.Escort);
                    int en = _dice.Range(_e.EscortCount[0], _e.EscortCount[1] + 1);
                    for (int i = 0; i < en; i++) _foes.Add(v.NewFoe(ed));
                }
                int id = 0;
                var heroes = v.Party.Where(c => !c.Dead).ToList();
                for (int i = 0; i < heroes.Count; i++)
                    _units.Add(new Unit { Id = ++id, C = heroes[i], X = 1 + i % 2, Y = Math.Clamp(H / 2 - heroes.Count / 2 + i, 0, H - 1) });
                for (int i = 0; i < _foes.Count; i++)
                    _units.Add(new Unit { Id = ++id, F = _foes[i], X = i == 0 ? W - 2 : W - 3 - i % 2, Y = Math.Clamp(H / 2 + (i % 2 == 0 ? i / 2 : -(i + 1) / 2), 0, H - 1) });
                foreach (var u in _units)
                    while (Blocks(u.X, u.Y, false) || _units.Any(o => o != u && o.X == u.X && o.Y == u.Y)) u.Y = (u.Y + 1) % H;
            }

            /// <summary>Blades out: initiative is rolled, the first blow to whoever earned it. Guards sent away by a lie leave the room.</summary>
            private void Begin(bool partyFirst, bool noGuards, bool foesFirst = false)
            {
                var v = _v;
                if (noGuards)
                {
                    _foes.RemoveRange(1, _foes.Count - 1);
                    _units.RemoveAll(u => u.F != null && u.F != _foes[0]);
                }
                foreach (var f in _foes)
                    if (!v.Fought.Contains(f.Def.Id)) v.Fought.Add(f.Def.Id);
                v.Log($"{Cap(_e.Text)}: {Boss}{(_foes.Count > 1 ? $", with {_foes.Count - 1} more at its side" : "")}.");
                v.Speak("boss");
                v._held.Clear();
                v._snared.Clear();
                v._dazed.Clear();
                v.StartFight();
                foreach (var c in v.Party) c.FightUsed.Clear();
                foreach (var u in _units)
                {
                    int dex = u.C != null ? u.C.Agent.Abilities.Dex : u.F!.Lib?.Dex ?? 10;
                    u.Init = _dice.D20() + Rng.Modifier(dex) + (u.Hero && partyFirst ? 20 : 0) + (!u.Hero && (foesFirst || (!partyFirst && v._doorFeat.First)) ? 20 : 0);
                }
                _units.Sort((a, b) => b.Init != a.Init ? b.Init.CompareTo(a.Init) : a.Id.CompareTo(b.Id));
                Phase = "fight";
                Round = 1;
                v._round = 1;
                v.FoesRoundStart(_foes, 1);
                Next();
            }

            /// <summary>The unit whose turn it is, or null when the fight is over.</summary>
            private Unit? Current => Phase == "fight" && _turn >= 0 && _turn < _units.Count ? _units[_turn] : null;

            /// <summary>The hero the mayor is to move now, or 0 when the fight runs itself (or is over).</summary>
            public int Waiting => Current is { C: { } c } && Commanded.Contains(c.Agent.Id) ? c.Agent.Id : 0;

            /// <summary>Move on to the next turn, playing every turn nobody commands, until a commanded hero is up or it is over.</summary>
            private void Next()
            {
                for (int guard = 0; guard < 2000 && Phase == "fight"; guard++)
                {
                    if (Over()) return;
                    _turn++;
                    if (_turn >= _units.Count)
                    {
                        _turn = 0;
                        Round++;
                        _v._round = Round;
                        _powers = 0;
                        _v.FoesRoundStart(_foes, Round);
                        if (Over()) return;
                        // Left to themselves, the party knows when it is beaten, as in any fight.
                        if (Commanded.Count == 0 && _foes.Any(f => f.Hp > 0) && _v.BreakOff(true))
                        {
                            Flee();
                            return;
                        }
                        // Off the grid a fight is called at twenty rounds; here too.
                        if (Round > 20)
                        {
                            Finish(false);
                            return;
                        }
                    }
                    var u = _units[_turn];
                    if (u.Out) continue;
                    u.Move = Speed(u);
                    u.Acted = u.Bonus = false;
                    u.Reaction = true;
                    if (u.Dodge > 0)
                    {
                        u.C!.AcBuff -= u.Dodge;
                        u.Dodge = 0;
                    }
                    if (u.Hero && Commanded.Contains(u.C!.Agent.Id))
                    {
                        // A commanded hero frozen or swallowed loses the turn like anyone.
                        if (_v._held.ContainsKey(u.C) || (_v._dazed.TryGetValue(u.C, out int dz) && Round <= dz)) continue;
                        if (u.C.Form != null && u.C.Shape <= 0) _v.EndShape(u.C);
                        u.C.Sneaked = u.C.Smote = u.C.Struck = false;
                        _v.Regen(u.C);
                        return;
                    }
                    if (u.Hero) AutoHero(u);
                    else FoeTurn(u);
                }
            }

            /// <summary>The party breaks off and runs, carrying the fallen.</summary>
            public void Flee()
            {
                if (Phase != "fight") return;
                var fallen = _v.Party.Where(c => c.Down && !c.Dead).Select(c => c.Agent.GivenName).ToList();
                _v.Log(fallen.Count == 0
                    ? "Bloodied and outmatched, the party breaks off and runs."
                    : $"Bloodied and outmatched, the party breaks off and runs, dragging {ListNames(fallen)} with them.");
                _v.Mark("retreat");
                _v.Speak("retreat");
                Finish(false, true);
            }

            private bool Over()
            {
                if (Phase != "fight") return true;
                if (_foes.All(f => f.Hp <= 0 && !_v.Rising(f, Round)))
                {
                    Finish(true);
                    return true;
                }
                if (_v.Standing.Count == 0)
                {
                    Finish(false);
                    return true;
                }
                return false;
            }

            private void Finish(bool won, bool fled = false)
            {
                var v = _v;
                Won = won;
                Phase = "over";
                foreach (var u in _units.Where(u => u.Dodge > 0)) u.C!.AcBuff -= u.Dodge;
                v.EndFight();
                int max = _foes.Sum(f => f.Def.Hp);
                v.FoeShare = max == 0 ? 0 : _foes.Sum(f => Math.Max(0, f.Hp)) / (double)max;
                if (won)
                {
                    v.Xp += _foes.Sum(f => f.Def.Xp);
                    v.Log(Voices.Variant(v._content, "over", "The fight is over in {rounds}.", v.Seed, ("rounds", $"{Round} round{(Round == 1 ? "" : "s")}")));
                    v.Mark("won");
                    v.Speak("victory");
                }
                else if (v.Standing.Count > 0 && !fled)
                {
                    v.Log("Bloodied and outmatched, the party breaks off and runs.");
                    v.Mark("retreat");
                }
                v.AfterCombat();
            }

            // A hero's turn ----------------------------------------------------------------------------

            private Unit Of(int heroId) => _units.First(u => u.C?.Agent.Id == heroId);

            private bool Ranged(Combatant c) => c.Form == null && c.Sheet.Weapon?.Properties.Any(p => p.StartsWith("Ammunition", StringComparison.Ordinal)) == true;

            /// <summary>How far a hero's weapon reaches: a bow across the room, a thrown blade a few squares, a sword the next square.</summary>
            private int WeaponReach(Combatant c) =>
                Ranged(c) ? _r.Ranged : c.Form == null && c.Sheet.Weapon?.Properties.Any(p => p.StartsWith("Thrown", StringComparison.Ordinal)) == true ? _r.Thrown : 1;

            private List<Foe> Within(Unit u, int range) => _units.Where(o => o.F != null && !o.Out && Dist(u, o) <= range).Select(o => o.F!).ToList();

            /// <summary>What the hero whose turn it is can do now, as the hotbar shows it.</summary>
            public List<GridAction> Actions()
            {
                if (Current is not { C: { } c } u || !Commanded.Contains(c.Agent.Id)) return [];
                var v = _v;
                var s = c.Sheet;
                var list = new List<GridAction>();
                bool act = !u.Acted;
                int reach = WeaponReach(c);
                list.Add(new GridAction("attack", c.Form != null ? $"{Library.Prose(c.Form.Name)} attacks" : s.Weapon != null ? Library.Prose(s.Weapon.Name) : "Attack", "act-attack", "action", "foe", reach, 0, false, act && Within(u, reach).Count > 0, $"{Prowess.Attacks(v._content, s)} attack(s), reach {reach}"));
                if (s.Cls.Spell && c.Form == null && !v._hushed && Prowess.Castable(v._content, s).Any(k => k.Play.Kind == "cantrip"))
                    list.Add(new GridAction("cantrip", "Cantrip", "act-cantrip", "action", "foe", _r.SpellRange, 0, false, act && Within(u, _r.SpellRange).Count > 0, "The hero's best cantrip at the foe"));
                if (c.Form == null && !v._hushed)
                {
                    int top = Prowess.SlotTop(s.Cls, s.Level);
                    foreach (var (p, sp) in Prowess.Castable(v._content, s).Where(k => k.Spell.Level > 0))
                    {
                        string aim = p.Kind switch { "bolt" => "foe", "hold" => p.Targets > 1 ? "cell" : "foe", "blast" => "cell", "heal" => "ally", _ => "self" };
                        int area = p.Kind == "blast" || (p.Kind == "hold" && p.Targets > 1) ? _r.BlastRadius : 0;
                        bool ready = act && c.Slots > 0 && (aim != "foe" || Within(u, _r.SpellRange).Count > 0) && (p.Kind != "buff" || !v._buffs.Contains(p.Id));
                        list.Add(new GridAction($"spell:{p.Id}", Library.Prose(sp.Name), $"spell-{p.Kind}", "action", aim, aim == "self" ? 0 : _r.SpellRange, area, false, ready, $"Level {sp.Level} · {c.Slots} slot(s) left · up to {Prowess.CastAt(sp, top)}"));
                    }
                }
                if (s.Cls.Healer && c.Slots > 0 && c.Form == null && !v._hushed && !Prowess.Castable(v._content, s).Any(k => k.Play.Kind == "heal") && s.Cls.HealSpell != null)
                    list.Add(new GridAction("heal", Library.Prose(s.Cls.HealSpell.Name), "spell-heal", "action", "ally", 1, 0, false, act, "Heal a friend beside you"));
                if (c.Powers.Count > 0) list.Add(new GridAction("item", "Item power", "act-item", "action", "foe", _r.SpellRange, 0, false, act, "Use the best power of what the hero carries on the foe"));
                if (Prowess.Power(v._content, s, "turn-undead") != null || Prowess.Powers(v._content, s).Any(p => p.Id.Contains("rite", StringComparison.Ordinal) || p.Id.Contains("channel", StringComparison.Ordinal)))
                    list.Add(new GridAction("rite", "Channel", "act-rite", "action", "self", 0, 0, false, act, "Turn the dead or work a rite"));
                if (c.Scrolls > 0 && Scrolls.Reads(s.Cls) && c.Form == null && !v._hushed)
                    list.Add(new GridAction("scroll", "Spell scroll", "act-scroll", "action", "cell", _r.SpellRange, _r.BlastRadius, false, act, $"{c.Scrolls} left"));
                list.Add(new GridAction("dash", "Dash", "act-dash", "action", "self", 0, 0, false, act, "Move twice as far this turn"));
                list.Add(new GridAction("dodge", "Dodge", "act-dodge", "action", "self", 0, 0, false, act, $"+{_r.DodgeAc} AC until your next turn"));
                list.Add(new GridAction("potion", "Potion", "act-potion", "bonus", "self", 0, 0, true, !u.Bonus && c.Potions > 0 && c.Form == null, $"{c.Potions} left"));
                list.Add(new GridAction("bonus", "Bonus action", "act-bonus", "bonus", "self", 0, 0, true, !u.Bonus, "Rage, second wind, a mark, a step: whatever the hero's class does with a bonus action"));
                list.Add(new GridAction("flee", "Retreat", "act-flee", "turn", "self", 0, 0, false, true, "The whole party breaks off and runs, carrying the fallen"));
                list.Add(new GridAction("auto", "Auto", "act-auto", "turn", "self", 0, 0, false, true, "Let them play this turn"));
                list.Add(new GridAction("end", "End turn", "act-end", "turn", "self", 0, 0, false, true, ""));
                return list;
            }

            /// <summary>Squares the commanded hero can move to now.</summary>
            public List<(int X, int Y)> Moves() => Current is { C: { } c } u && Commanded.Contains(c.Agent.Id) ? Reach(u, u.Move).Keys.Where(k => k != (u.X, u.Y)).ToList() : [];

            /// <summary>Move the commanded hero to a square, drawing opportunity attacks from foes it leaves.</summary>
            public bool Move(int x, int y)
            {
                if (Current is not { C: { } c } u || !Commanded.Contains(c.Agent.Id)) return false;
                var reach = Reach(u, u.Move);
                if (!reach.TryGetValue((x, y), out int cost) || cost == 0) return false;
                Step(u, x, y);
                u.Move -= cost;
                if (Over()) return true;
                if (u.Out) Next();
                return true;
            }

            /// <summary>Do something with the commanded hero, aimed at a square (a foe, a friend, the middle of a blast).</summary>
            public bool Act(string id, int x = -1, int y = -1)
            {
                if (Current is not { C: { } c } u || !Commanded.Contains(c.Agent.Id)) return false;
                var v = _v;
                var s = c.Sheet;
                var target = Standing(x, y);
                List<Foe> Area(int r) => _units.Where(o => o.F != null && !o.Out && Math.Max(Math.Abs(o.X - x), Math.Abs(o.Y - y)) <= r).Select(o => o.F!).ToList();
                bool aimedFoe(int range) => target is { F: not null } && Dist(u, target) <= range;
                switch (id)
                {
                    case "end":
                        Next();
                        return true;
                    case "flee":
                        Flee();
                        return true;
                    case "auto":
                        AutoHero(u);
                        Next();
                        return true;
                    case "potion":
                        if (u.Bonus || c.Potions <= 0) return false;
                        u.Bonus = true;
                        c.Potions--;
                        int drunk = v.Potion();
                        c.Hp = Math.Min(s.MaxHp, c.Hp + drunk);
                        v.Log(Voices.Variant(v._content, "potion", "{name} gulps a {potion} (+{hp} hp).", v.Seed, ("name", c.Agent.GivenName), ("potion", v.PotionName()), ("hp", drunk.ToString())));
                        return true;
                    case "bonus":
                        if (u.Bonus) return false;
                        u.Bonus = true;
                        v.BonusAction(c, _foes, Within(u, _r.SpellRange), Round);
                        return Done(u, false);
                }
                if (u.Acted) return false;
                switch (id)
                {
                    case "attack":
                        if (!aimedFoe(WeaponReach(c))) return false;
                        v.StrikeTurn(c, [target!.F!], Round);
                        break;
                    case "cantrip":
                        if (!aimedFoe(_r.SpellRange) || !v.CantripAttack(c, [target!.F!], Round)) return false;
                        break;
                    case "item":
                        var aim = target is { F: not null } ? [target.F] : Within(u, _r.SpellRange);
                        if (aim.Count == 0 || !v.ItemAction(c, _foes, aim, Round)) return false;
                        break;
                    case "rite":
                        if (!v.TurnUndead(c, Within(u, _r.SpellRange), Round) && !v.Rite(c, Within(u, _r.SpellRange), Round)) return false;
                        break;
                    case "scroll":
                        if (c.Scrolls <= 0) return false;
                        var hit = Area(_r.BlastRadius);
                        if (hit.Count == 0) return false;
                        v.ReadScroll(c, _foes, hit, Round);
                        break;
                    case "dash":
                        u.Move += Speed(u);
                        break;
                    case "dodge":
                        u.Dodge = _r.DodgeAc;
                        c.AcBuff += u.Dodge;
                        v.Log($"{c.Agent.GivenName} keeps their guard up.");
                        break;
                    case "heal":
                        if (target is not { C: { } ally } || Dist(u, target) > 1 || c.Slots <= 0) return false;
                        v.HealOne(c, ally, null, null);
                        break;
                    default:
                        if (!id.StartsWith("spell:", StringComparison.Ordinal) || c.Slots <= 0) return false;
                        string pid = id[6..];
                        var k = Prowess.Castable(v._content, s).FirstOrDefault(q => q.Play.Id == pid);
                        if (k.Spell == null) return false;
                        int top = Prowess.SlotTop(s.Cls, s.Level);
                        switch (k.Play.Kind)
                        {
                            case "bolt":
                                if (!aimedFoe(_r.SpellRange)) return false;
                                v.CastBolt(c, k.Play, k.Spell, top, target!.F!, _foes, Round);
                                break;
                            case "blast":
                                var caught = Area(_r.BlastRadius);
                                if (caught.Count == 0) return false;
                                v.CastBlast(c, k.Play, k.Spell, top, caught, _foes, Round);
                                break;
                            case "hold":
                                var held = k.Play.Targets > 1 ? Area(_r.BlastRadius) : target is { F: not null } ? [target.F] : [];
                                held = v.Holdable(k.Play, k.Spell, top, held, Round);
                                if (held.Count == 0) return false;
                                v.CastHold(c, k.Play, k.Spell, top, held, Round);
                                break;
                            case "buff":
                                if (v._buffs.Contains(k.Play.Id)) return false;
                                v.CastBuff(c, k.Play, k.Spell, top);
                                break;
                            case "aura":
                                v.CastAura(c, k.Play, k.Spell, top, Round);
                                break;
                            case "heal":
                                if (target is not { C: { } patient } || Dist(u, target) > _r.SpellRange) return false;
                                v.HealOne(c, patient, k.Play, k.Spell);
                                break;
                            default:
                                return false;
                        }
                        break;
                }
                u.Acted = true;
                return Done(u, true);
            }

            /// <summary>After an action: the fight may be over, and a hero with nothing left to do ends the turn.</summary>
            private bool Done(Unit u, bool acted)
            {
                if (Over()) return true;
                if (u.Out || (u.Acted && u.Bonus && u.Move == 0)) Next();
                return true;
            }

            /// <summary>Step a unit along to a square; foes it leaves the reach of take their swing (a hero's too).</summary>
            private void Step(Unit u, int x, int y)
            {
                foreach (var o in _units.Where(o => !o.Out && o.Hero != u.Hero && o.Reaction && Dist(o, u) <= 1 && Math.Max(Math.Abs(o.X - x), Math.Abs(o.Y - y)) > 1).ToList())
                {
                    o.Reaction = false;
                    if (u.Out) break;
                    if (o.F != null) Opportunity(o.F, u.C!);
                    else if (u.F != null && o.C != null && !o.C.Down)
                    {
                        _v.Log($"{o.C.Agent.GivenName} swings as {The(u.F.Def)} pulls away.");
                        _v.WeaponAttack(o.C, [u.F], Round);
                    }
                }
                if (!u.Out)
                {
                    u.X = x;
                    u.Y = y;
                }
            }

            /// <summary>A foe's one swing as a hero runs past it.</summary>
            private void Opportunity(Foe f, Combatant t)
            {
                var v = _v;
                int roll = _dice.D20();
                if (roll == 1 || (roll != 20 && roll + f.Def.Attack < t.ArmorClass))
                {
                    v.Log($"{Cap(The(f.Def))} swings at {t.Agent.GivenName} as they go by, and misses.");
                    return;
                }
                int dmg = v.Guard(t, f, v.Taken(t, f, Math.Max(1, f.Def.Dice.Roll(_dice, roll == 20)), Round));
                Hurt(t, dmg);
                v.Log($"{Cap(The(f.Def))} catches {t.Agent.GivenName} going by for {dmg}{(t.Down ? $". {t.Agent.GivenName} goes down" : "")}.");
                if (t.Down) v.Mark("down", t);
            }

            /// <summary>
            /// A hero left to their own judgment: up to the nearest foe their blows can find (a caster keeps its distance),
            /// then the turn they would take in any fight, at what they can reach.
            /// </summary>
            private void AutoHero(Unit u)
            {
                var c = u.C!;
                if (_v._held.ContainsKey(c) || (_v._dazed.TryGetValue(c, out int dz) && Round <= dz)) return;
                bool caster = c.Form == null && (c.Sheet.Cls.Spell || c.Sheet.Cls.Healer) && c.Sheet.Cls.Role != "frontline";
                int reach = caster ? _r.SpellRange : WeaponReach(c);
                var foe = _units.Where(o => o.F != null && !o.Out).OrderBy(o => Dist(u, o)).ThenByDescending(o => o.F!.Def.Boss).ThenBy(o => o.Id).FirstOrDefault();
                if (foe == null) return;
                if (Dist(u, foe) > reach || (!caster && reach == 1 && Dist(u, foe) > 1))
                {
                    var spots = Reach(u, u.Move);
                    var best = spots.Keys.OrderBy(k => Math.Max(Math.Abs(k.Item1 - foe.X), Math.Abs(k.Item2 - foe.Y)) <= reach ? 0 : 1)
                        .ThenBy(k => Math.Max(Math.Abs(k.Item1 - foe.X), Math.Abs(k.Item2 - foe.Y)))
                        .ThenBy(k => spots[k]).ThenBy(k => k.Item2).ThenBy(k => k.Item1).First();
                    if (best != (u.X, u.Y)) Step(u, best.Item1, best.Item2);
                    if (u.Out || Over()) return;
                }
                var near = Within(u, reach);
                if (near.Count == 0)
                {
                    // Nothing in reach after a full move: dash on.
                    var spots = Reach(u, Speed(u));
                    var best = spots.Keys.OrderBy(k => Math.Max(Math.Abs(k.Item1 - foe.X), Math.Abs(k.Item2 - foe.Y))).ThenBy(k => k.Item2).ThenBy(k => k.Item1).First();
                    if (best != (u.X, u.Y)) Step(u, best.Item1, best.Item2);
                    return;
                }
                _v.HeroTurn(c, near, Round);
            }

            // A foe's turn ---------------------------------------------------------------------------

            /// <summary>A foe readies (regeneration, its power), goes for whoever it would pick, and swings at whoever it can reach.</summary>
            private void FoeTurn(Unit u)
            {
                var f = u.F!;
                var v = _v;
                v.FoeReady(f, _foes, Round, ref _powers);
                if (f.Def.Boss) v.BossMoves(_foes, Round);
                if (u.Out || Over() || f.Held(Round) || f.Spent == Round) return;
                var heroes = _units.Where(o => o.C != null && !o.Out).ToList();
                if (heroes.Count == 0) return;
                if (!heroes.Any(h => Dist(u, h) <= 1))
                {
                    var picked = v.Victim(f, heroes.Select(x => x.C!).ToList());
                    var prey = heroes.FirstOrDefault(h => h.C == picked) ?? heroes.OrderBy(h => Dist(u, h)).ThenBy(h => h.Id).First();
                    var spots = Reach(u, Speed(u));
                    var best = spots.Keys.OrderBy(k => Math.Max(Math.Abs(k.Item1 - prey.X), Math.Abs(k.Item2 - prey.Y))).ThenBy(k => spots[k]).ThenBy(k => k.Item2).ThenBy(k => k.Item1).First();
                    if (best != (u.X, u.Y)) Step(u, best.Item1, best.Item2);
                }
                if (u.Out) return;
                v.FoeAttacks(f, _foes, Round, t => _units.Any(o => o.C == t && Dist(u, o) <= 1));
            }

            // Run it all ------------------------------------------------------------------------------

            /// <summary>Let it play out with nobody commanded: the party's best talker tries their best way once, then the fight.</summary>
            public void Auto()
            {
                Commanded.Clear();
                if (Phase == "talk")
                {
                    var best = TalkOptions().OrderByDescending(o => o.Bonus - o.Dc).ThenBy(o => o.Id, StringComparer.Ordinal).FirstOrDefault();
                    while (Phase == "talk" && best != null && best.Bonus - best.Dc >= -8) Talk(best.Id, best.HeroId);
                    if (Phase == "talk") Attack();
                }
                if (Phase == "fight") Next();
            }

            /// <summary>Command a hero, or leave them to it.</summary>
            public void Command(int heroId, bool on)
            {
                if (on) Commanded.Add(heroId);
                else Commanded.Remove(heroId);
                if (Phase == "fight" && !on && Current is { C: { } c } && c.Agent.Id == heroId)
                {
                    AutoHero(_units[_turn]);
                    Next();
                }
            }

            /// <summary>Everything on the grid, as the screen draws it.</summary>
            public List<GridUnit> Units() => _units.Select(u => u.C != null
                ? new GridUnit(u.Id, u.C.Agent.GivenName, true, u.C.Agent.Id, u.C.Sheet.Cls.Id, u.X, u.Y, Math.Max(0, u.C.Hp), u.C.Sheet.MaxHp, u.C.ArmorClass, u.C.Down, u.C.Dead, false, Speed(u), Marks(u))
                : new GridUnit(u.Id, Cap(u.F!.Def.Name), false, 0, u.F.Def.Id, u.X, u.Y, Math.Max(0, u.F.Hp), u.F.Def.Hp, u.F.Def.Ac, u.F.Hp <= 0, u.F.Hp <= 0, u.F.Def.Boss, Speed(u), Marks(u))).ToList();

            private List<string> Marks(Unit u)
            {
                var m = new List<string>();
                if (Phase != "fight") return m;
                if (u.F != null)
                {
                    if (u.F.Held(Round)) m.Add(u.F.HeldBy == "" ? "held" : u.F.HeldBy);
                    if (u.F.HinderUntil >= Round) m.Add("hindered");
                }
                else if (u.C != null)
                {
                    if (u.C.Raging) m.Add("raging");
                    if (u.Dodge > 0) m.Add("dodging");
                    if (_v._snared.ContainsKey(u.C)) m.Add("grappled");
                    if (_v._held.ContainsKey(u.C)) m.Add("swallowed");
                }
                if (_turn >= 0 && _turn < _units.Count && _units[_turn] == u && Phase == "fight") m.Add("turn");
                return m;
            }

            /// <summary>The order of turns, as unit ids, from the one acting now.</summary>
            public List<int> Order() => _turn < 0 ? _units.Select(u => u.Id).ToList() : _units.Skip(_turn).Concat(_units.Take(_turn)).Where(u => !u.Out).Select(u => u.Id).ToList();

            /// <summary>The unit whose turn it is (0 for none), and what it has left this turn.</summary>
            public (int Unit, int Move, bool Acted, bool Bonus) Turn => Current is { } u ? (u.Id, u.Move, u.Acted, u.Bonus) : (0, 0, true, true);
        }

        /// <summary>A hero heals one friend: with a healing spell of theirs, or their class's own.</summary>
        private void HealOne(Combatant c, Combatant patient, SpellPlay? play, LibSpell? spell)
        {
            var s = c.Sheet;
            int top = Prowess.SlotTop(s.Cls, s.Level);
            var sp = spell ?? s.Cls.HealSpell!;
            var dice = play != null && spell != null ? Prowess.DiceAt(play, spell, Prowess.CastAt(spell, top)) ?? new Dice(1, 8, 0) : sp.Dice ?? new Dice(1, 8, 0);
            int healed = Math.Max(1, dice.Roll(_rng) + Rng.Modifier(c.Agent.Abilities[s.Cls.Casting]));
            bool raised = patient.Down;
            patient.Hp = Math.Min(patient.Sheet.MaxHp, Math.Max(0, patient.Hp) + healed);
            patient.Down = false;
            c.Slots--;
            Log(raised
                ? $"{c.Agent.GivenName} drags {patient.Agent.GivenName} back from the brink with {Library.Prose(sp.Name)} (+{healed} hp)."
                : $"{c.Agent.GivenName} casts {Library.Prose(sp.Name)} on {patient.Agent.GivenName} (+{healed} hp).");
            Mark("heal", c);
        }
    }
}
