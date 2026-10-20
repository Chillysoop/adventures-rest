using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Command-line options after "--", for sharing a moment or checking a build headlessly:
/// <c>--seed 7 --day 40 --autoplay --tab bounties --site wolfwood --scry wolfwood --screenshot shot.png</c>.
/// Also <c>--scout site</c>, <c>--mirror site</c>, <c>--build pool|spire|tower|palisade</c>, <c>--relic item-id</c> and <c>--buy all|N</c> (companies' reports);
/// for the walls <c>--works palisade|stone|all</c> (raised at once), <c>--call N|tower:N</c>, <c>--flag x,y[,guards]</c> and <c>--wait hours</c>;
/// <c>--spot x,y</c> marks a spot (days east and south) for a company to look at, and <c>--strike</c> opens the quick orders' Strike list.
/// </summary>
public sealed class Options
{
    public int? Seed;
    public int Day;
    public int Hour;
    public int? Speed;
    public string? Tab;
    /// <summary>--tray Bounties opens that drawer of the action tray, for screenshots.</summary>
    public string? Tray;
    /// <summary>--stat N: hover the Nth resource in the top bar, for a shot of its month.</summary>
    public int? Stat;
    /// <summary>--tax 0.05: set the tax rate before the days run, for a shot of a town in the red.</summary>
    public double? Tax;
    public float? Scroll;

    /// <summary>With --autoplay, leave companies' reports unbought for the player (screenshots of the companies window).</summary>
    public bool KeepReports;

    /// <summary>For roster shots (--roster ready|offer): the company picked in the roster, and the answer to the job offered it.</summary>
    public string? RosterPick;
    public (string Company, string Site, JobAnswer Answer)? RosterAnswer;

    /// <summary>With --autoplay, the stand-in leaves the armory to the player: stock and offers wait (--keep-armory).</summary>
    public bool KeepArmory;

    /// <summary>Ingredients put in the alchemy bench's pot for a shot (--brew hearthmint,grave-dust).</summary>
    public List<string> Brew = [];

    /// <summary>Lift the fog everywhere (screenshots of the wilds and the roads through them).</summary>
    public bool Reveal;
    public string? Style;
    public bool Clean;
    public string? Site;
    /// <summary>Aim the camera at the first finished building with this id.</summary>
    public string? At;
    public string? Screenshot;
    public int Frames = 45;

    /// <summary>For clips: the camera pulls slowly back from the town to the whole map, turning as it goes.</summary>
    public bool Flyout;
    public bool Autoplay;

    /// <summary>--load a save file instead of starting a town; --menu opens the game menu (for screenshots).</summary>
    public string? Load;
    public bool Menu;

    /// <summary>--title shows the main menu; --tutorial start|step-id runs the tutorial from there (screenshots).</summary>
    public bool Title;

    /// <summary>--storyteller id: who tells a new town's story; --storytellers opens the main menu's choice of them.</summary>
    public string? Storyteller;
    public bool Storytellers;

    /// <summary>--episode id: strike a townsperson out on the streets with that inspiration or break, look at them and open their inspector.</summary>
    public string? Episode;

    /// <summary>--history [power-id]: open the world's history timeline, with a power's story picked.</summary>
    public string? History;

    /// <summary>--glossary [entry-id]: open the glossary at an entry; --keyword id[,id] pins keyword cards (screenshots).</summary>
    public string? Glossary;
    public List<string> Keywords = [];
    public string? Tutorial;
    public bool Hero;

    /// <summary>--folk: select the most practised townsperson, for a look at their character sheet.</summary>
    public bool Folk;

    /// <summary>--skirmish [gear]: the town's heroes, one of each class, fight a ring of raiders by the square (for shots); "gear" hands them famous magic arms first.</summary>
    public string? Skirmish;

    /// <summary>--rolls: look into the busiest tavern and replay its latest skill checks as dice over people's heads.</summary>
    public bool Rolls;

    /// <summary>--wall N: select a section of the outer wall (0 is the gate) in the inspector, for shots of the call to arms.</summary>
    public int? Wall { get; private set; }

    /// <summary>
    /// --siege H: run on to the next siege and stop H hours after the host reaches the walls (8 by default: it camps through
    /// the night and the assault goes in at dawn, 6 hours in), the camera on the walls.
    /// </summary>
    public float? Siege;

    /// <summary>--house: select the house on the most pleasant street, for screenshots of the inspector.</summary>
    public bool House;

    /// <summary>--crowded [kind] selects the place (of a kind: tavern, holy, bath...) that turned the most townsfolk away yesterday, for a shot of the crowd at its door.</summary>
    public string? Crowded;
    /// <summary>Aim the camera at the building going up with the most hands on it (<c>--rising</c>), for a shot of the builders.</summary>
    public bool Rising;
    /// <summary>--tourney N: put on a tourney of level N (0 modest to 2 legendary), stake on its second event and ride to that day's --tourney-hour, card open over the lists.</summary>
    public int? Tourney;
    public int TourneyHour = 13;
    public int TourneyDay;
    /// <summary>--secrets: put a motion before the council and hand the mayor a secret on each councillor, with the Council window open, for a shot of pressing a vote.</summary>
    public bool Secrets;
    /// <summary>--guilds [strike]: open the Council window on its guilds; with strike, the first guild walks out, for a shot of a strike.</summary>
    public string? Guilds;
    /// <summary>--tourney-plain: the shot of the lists without the card over it.</summary>
    public bool TourneyCard = true;
    /// <summary>With <c>--at</c>: open that building's inspector (<c>--card</c>), for a shot of a monument's parts.</summary>
    public bool Card;
    /// <summary>For screenshots: click the alert at this position in the strip (0 is the first) once it shows.</summary>
    public int? Alert;
    /// <summary>For screenshots: show the hover summary of the alert icon at this position.</summary>
    public int? AlertHover;

    /// <summary>Camera distance in tiles and an optional point to look at (--cam 40 --look 512,480), and the strategic view.</summary>
    public float? Cam;
    public Godot.Vector2? Look;
    public bool StrategicView;

    /// <summary>--shadowed: aim the camera at a settlement under an enemy's shadow (with --cam, for screenshots).</summary>
    public bool Shadowed;

    /// <summary>--lens room|work|growth|appeal|danger: open the info lens on that view (for screenshots).</summary>
    public string? Lens;
    public int Yaw;

    /// <summary>Markers to place for a screenshot (--marker rally,512,470,Muster here) and one to open the card of (--open-marker 0).</summary>
    public readonly List<(Map.MarkerKind Kind, Godot.Vector2 At, string Label)> Markers = [];
    public int? OpenMarker;

    /// <summary>Extra trees per forest tile and a frame-time report, for the late-game performance check.</summary>
    public float Trees = 1;
    public bool Bench;

    /// <summary>--smoke plays a build through from the main menu and quits with its verdict (see <see cref="Main"/>'s Smoke.cs); --smoke-days sets how far the town runs first.</summary>
    public bool Smoke;
    public int SmokeDays = 120;

    /// <summary>Keep time running through war news, for recording clips.</summary>
    public bool NoPause;
    public readonly List<(string Verb, string Arg)> Script = [];

    /// <summary>--plan opens the build bar on a group; --tool picks a zone or building; --hover x,y,x2,y2 previews a stroke over lots.</summary>
    public string? Plan, Tool;
    public int[]? Hover;

    /// <summary>--beautify D: dress the town as a player who cares how it looks, keeping its square open from day D (see <see cref="Showcase"/>).</summary>
    public int? Beautify;

    /// <summary>--decor picks a decoration (or "eraser") to hold; --decor-at x,y,angle shows it at a point in lots.</summary>
    /// <summary>--story ID opens that story event's card (a fresh one, bound to the town as it stands); --story-pick N takes option N; --background ID picks the mayor's past.</summary>
    public string? Story, Background;

    /// <summary>--find: leave explorers' reports to the player and open the first one waiting; --find-pick ACT answers it.</summary>
    public bool Find;

    /// <summary>--tales: companies tell their tales even with the stand-in mayor; --tale N|last opens one; --recap shows the recap card.</summary>
    public bool Tales, Recap;
    public string? Tale;
    public int? TaleAt;

    /// <summary>--strike: open the quick orders' Strike list over the corner map.</summary>
    public bool Strike;
    public string? FindPick;
    public int? StoryPick;

    /// <summary>--mayor-level N raises the mayor to level N (advances left to choose); --advance A spends one (repeatable); --inspiration N sets it. For shots.</summary>
    public int? MayorLevel, Inspiration;
    public List<string> Advances = [];

    public string? DecorTool;
    public double[]? DecorAt;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : "";
            switch (args[i])
            {
                case "--seed": o.Seed = int.Parse(Next()); break;
                case "--day": o.Day = int.Parse(Next()); break;
                case "--hour": o.Hour = int.Parse(Next()); break;
                case "--speed": o.Speed = int.Parse(Next()); break;
                case "--tab": o.Tab = Next(); break;
                case "--tray": o.Tray = Next(); break;
                case "--stat": o.Stat = int.Parse(Next()); break;
                case "--tax": o.Tax = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--scroll": o.Scroll = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--site": o.Site = Next(); break;
                case "--at": o.At = Next(); break;
                case "--screenshot": o.Screenshot = Next(); break;
                case "--frames": o.Frames = int.Parse(Next()); break;
                case "--flyout": o.Flyout = true; break;
                case "--autoplay": o.Autoplay = true; break;
                case "--load": o.Load = Next(); break;
                case "--menu": o.Menu = true; break;
                case "--title": o.Title = true; break;
                case "--storyteller": o.Storyteller = Next(); break;
                case "--storytellers": o.Storytellers = o.Title = true; break;
                case "--episode": o.Episode = Next(); break;
                case "--history": o.History = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--glossary": o.Glossary = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--keyword": o.Keywords.AddRange(Next().Split(',')); break;
                case "--tutorial": o.Tutorial = Next(); break;
                case "--hero": o.Hero = true; break;
                case "--folk": o.Folk = true; break;
                case "--skirmish": o.Skirmish = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--rolls": o.Rolls = true; break;
                case "--wall": o.Wall = int.Parse(Next()); break;
                case "--strike": o.Strike = true; break;
                case "--house": o.House = true; break;
                case "--crowded": o.Crowded = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--rising": o.Rising = true; break;
                case "--siege": o.Siege = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture) : 8; break;
                case "--card": o.Card = true; break;
                case "--tourney": o.Tourney = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? int.Parse(Next()) : 1; break;
                case "--tourney-hour": o.TourneyHour = int.Parse(Next()); break;
                case "--tourney-day": o.TourneyDay = int.Parse(Next()); break;
                case "--tourney-plain": o.TourneyCard = false; break;
                case "--secrets": o.Secrets = true; break;
                case "--guilds": o.Guilds = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--alert": o.Alert = int.Parse(Next()); break;
                case "--alert-hover": o.AlertHover = int.Parse(Next()); break;
                case "--no-pause": o.NoPause = true; break;
                case "--keep-reports": o.KeepReports = true; break;
                case "--keep-armory": o.KeepArmory = true; break;
                case "--brew": o.Brew.AddRange(Next().Split(',')); break;
                case "--reveal": o.Reveal = true; break;
                case "--style": o.Style = Next(); break;
                case "--clean": o.Clean = true; break;
                case "--cam": o.Cam = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--look":
                    var xy = Next().Split(',');
                    o.Look = new Godot.Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "--strategic": o.StrategicView = true; break;
                case "--shadowed": o.Shadowed = true; break;
                case "--lens": o.Lens = Next(); break;
                case "--marker":
                    var mk = Next().Split(',', 4);
                    if (Enum.TryParse<Map.MarkerKind>(mk[0], true, out var kind))
                        o.Markers.Add((kind, new Godot.Vector2(float.Parse(mk[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(mk[2], System.Globalization.CultureInfo.InvariantCulture)), mk.Length > 3 ? mk[3] : ""));
                    break;
                case "--open-marker": o.OpenMarker = int.Parse(Next()); break;
                case "--yaw": o.Yaw = int.Parse(Next()); break;
                case "--trees": o.Trees = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--bench": o.Bench = true; break;
                case "--smoke": o.Smoke = true; break;
                case "--smoke-days": o.SmokeDays = int.Parse(Next()); break;
                case "--plan": o.Plan = Next(); break;
                case "--tool": o.Tool = Next(); break;
                case "--hover": o.Hover = Next().Split(',').Select(int.Parse).ToArray(); break;
                case "--beautify": o.Beautify = int.Parse(Next()); break;
                case "--decor": o.DecorTool = Next(); break;
                case "--story": o.Story = Next(); break;
                case "--find": o.Find = true; break;
                case "--tales": o.Tales = true; break;
                case "--tale": o.Tale = Next(); break;
                case "--tale-at": o.TaleAt = int.Parse(Next()); break;
                case "--recap": o.Recap = true; break;
                case "--find-pick": o.FindPick = Next(); break;
                case "--story-pick": o.StoryPick = int.Parse(Next()); break;
                case "--background": o.Background = Next(); break;
                case "--mayor-level": o.MayorLevel = int.Parse(Next()); break;
                case "--advance": o.Advances.Add(Next()); break;
                case "--inspiration": o.Inspiration = int.Parse(Next()); break;
                case "--decor-at": o.DecorAt = Next().Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); break;
                case "--bounty" or "--scry" or "--send" or "--recall" or "--build" or "--scout" or "--mirror" or "--relic" or "--stock" or "--pact" or "--buy" or "--call" or "--flag" or "--wait" or "--works" or "--roster" or "--spot" or "--draw-wall" or "--gate" or "--raise" or "--skyrun":
                    o.Script.Add((args[i][2..], Next()));
                    break;
            }
        }
        return o;
    }

    /// <summary>Player actions to take once the town is set up, in order.</summary>
    public void ApplyScript(Simulation sim)
    {
        var t = sim.Town;
        foreach (var (verb, arg) in Script)
        {
            switch (verb)
            {
                case "bounty":
                    var site = Quests.Site(t, arg);
                    if (site != null) sim.PostBounty(arg, Quests.Asking(t, site.Def.Tier));
                    break;
                case "scry":
                    sim.CommissionScrying(arg);
                    break;
                case "send" or "recall":
                    var party = t.Parties.LastOrDefault(p => !p.Returned);
                    if (party != null) sim.SendWord(party.Id, verb == "recall");
                    break;
                case "build" when arg is "pool" or "spire":
                    t.Treasury += 2000;
                    sim.BuildSightWork(arg == "pool" ? "scrying_pool" : "sleepless_spire");
                    break;
                case "build":
                    sim.BuildDefense(arg == "tower" ? DefenseWork.Tower : DefenseWork.Palisade);
                    break;
                case "raise":
                    // For shots: a civic building finished at once on the lot the council would give it.
                    var rd = t.Content.Building(arg);
                    if (Planning.CivicLot(t, rd) is { } rlot) t.AddBuilding(rd, rlot, null, rd.Name, true);
                    break;
                case "skyrun":
                    // For shots of the sky dock: send the airship on its best paying run ("best") or to a settlement by name,
                    // with the hold stocked from the market's target if the town has nothing to spare.
                    var ports = Skyship.Ports(t).Where(s => arg == "best" || s.Name == arg).ToList();
                    foreach (var g in Goods.All.Where(g => Skyship.Spare(t, g) < t.Content.Trade.Sky.MinCargo && t.Market.Target(g) > 0).Take(3))
                        t.Market.Seed(g, t.Market.Target(g));
                    t.Treasury += 500;
                    var pick = ports.SelectMany(s => Goods.All.Where(g => Skyship.Refuse(t, s, g) == null).Select(g => (S: s, G: g)))
                        .OrderByDescending(x => Skyship.Profit(t, x.S, x.G)).FirstOrDefault();
                    if (pick.S != null) sim.SendSkyship(pick.S.Id, pick.G);
                    else Godot.GD.Print($"--skyrun {arg}: nowhere to fly");
                    break;
                case "works":
                    // For shots of the walls: raise them at once, the stone and (with "all") every work that goes on it.
                    var d = t.Defenses;
                    d.Palisade = PalisadeState.Built;
                    if (arg == "palisade") d.Towers = Math.Max(d.Towers, 2);
                    else d.StoneWall = PalisadeState.Built;
                    if (arg == "all")
                    {
                        d.Gatehouse = d.Moat = PalisadeState.Built;
                        d.Barracks = t.Content.Garrison.BarracksWork.Max;
                        d.Towers = Garrison.MaxTowers(t);
                    }
                    Garrison.Sync(t);
                    break;
                case "draw-wall" or "gate":
                    // For shots of drawn walls: --draw-wall 0:x,y;x,y;... draws ring 0 through corners given in tiles from the town's centre,
                    // --gate 0:x,y puts a gate on its line nearest a point (the same offsets).
                    var (ringPart, ptsPart) = (arg[..arg.IndexOf(':')], arg[(arg.IndexOf(':') + 1)..]);
                    var pts = ptsPart.Split(';').Select(xy => xy.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray())
                        .Select(v => (Town.Center + v[0], Town.Center + v[1])).ToList();
                    int wr = int.Parse(ringPart);
                    if (verb == "draw-wall") t.Treasury += 400;
                    string? why = verb == "draw-wall" ? sim.DrawWall(wr, pts) : sim.PlaceGate(wr, pts[0].Item1, pts[0].Item2);
                    if (why != null) Godot.GD.Print($"--{verb} {arg}: {why}");
                    break;
                case "call":
                    if (arg.StartsWith("tower:", StringComparison.Ordinal)) sim.CallToArms(PostKind.Tower, int.Parse(arg[6..]));
                    else sim.CallToArms(PostKind.Wall, int.Parse(arg));
                    break;
                case "flag":
                    var f = arg.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    sim.PlantFlag(f[0], f[1], f.Length > 2 ? (int)f[2] : 0);
                    break;
                case "spot":
                    var sp = arg.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    sim.MarkSpot(sp[0], sp[1]);
                    break;
                case "wait":
                    for (int h = 0; h < int.Parse(arg); h++) sim.Step();
                    break;
                case "scout":
                    sim.PostScout(arg);
                    break;
                case "mirror":
                    sim.PlantMirror(arg);
                    break;
                case "buy":
                    // Buy companies' reports ("all", or how many, newest first), topping up the treasury to pay.
                    var sale = Intel.ForSale(t);
                    int n = arg == "all" ? sale.Count : int.Parse(arg);
                    foreach (var p in sale.Take(n))
                    {
                        t.Treasury += Intel.Ask(t, p);
                        sim.BuyIntel(p.Id);
                    }
                    break;
                case "stock":
                    // For armory shots: a find handed in under a pact.
                    if (t.Content.Library.Item(arg) is LibItem find) Armory.Add(t, find, "handed in under a pact");
                    break;
                case "pact":
                    // Swear the friendliest company ("best"), or the one named, to a loot pact.
                    string? company = arg == "best" ? Intel.MembersByCompany(t).Select(x => x.Name).FirstOrDefault() : arg;
                    if (company != null) sim.OfferPact(company);
                    break;
                case "roster":
                    // Wait (up to 40 days) for a company with three heroes fit to ride and pick it (healing those in town if
                    // none is), and with "offer", offer it the job it leans toward most at half again the asking.
                    for (int h = 0; h < 40 * SimTime.HoursPerDay && !Roster.Companies(t).Any(e => e.Ready >= 3 && e.Road == null); h++) sim.Step();
                    var best = Roster.Companies(t).OrderByDescending(e => e.Ready).ThenByDescending(e => e.Members.Count(m => !m.IsAway)).ThenByDescending(e => e.Relation).FirstOrDefault();
                    if (best == null) break;
                    if (best.Ready == 0)
                        foreach (var m in best.Members.Where(m => !m.IsAway)) m.Hero!.Hp = m.Hero.MaxHp;
                    RosterPick = best.Name;
                    if (arg != "offer") break;
                    t.Treasury = Math.Max(t.Treasury, 3000);
                    var job = t.Sites.Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared)
                        .Select(s => Roster.Weigh(t, best.Name, s.Def.Id, Js.Round(Terms.Suggested(t, s, t.Content.Bounties.Term("slay")) * 1.5)))
                        .Where(v => v.Blocked == null).OrderByDescending(v => v.Score).ThenBy(v => v.Site!.Def.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (job != null && sim.OfferJob(best.Name, job.Site!.Def.Id, job.Reward) is { } answer) RosterAnswer = (best.Name, job.Site.Def.Name, answer);
                    break;
                case "relic":
                    // For showing a late-game moment quickly: a party has just brought a relic home.
                    if (t.Content.Library.Item(arg) is LibItem item) Watch.ReceiveRelic(t, item, "Company of the Compass");
                    break;
            }
        }
    }

    /// <summary>--clean: hide everything drawn over the map (HUD, windows, labels) so only the world shows.</summary>
    private static void HideHud(Node root)
    {
        var map = Find(root);
        if (map == null) return;
        foreach (var c in map.GetChildren()) if (c is CanvasItem ci && c is not SubViewportContainer) ci.Visible = false;
        for (Node n = map; n.GetParent() is Node p; n = p)
            foreach (var sib in p.GetChildren()) if (sib != n && sib is CanvasItem ci) ci.Visible = false;
        Bubbles(root);
        static void Bubbles(Node n)
        {
            if (n is Label3D l) l.Layers = 1 << 19;
            foreach (var c in n.GetChildren()) Bubbles(c);
        }
        static Node? Find(Node n)
        {
            if (n is Client.Map.MapView) return n;
            foreach (var c in n.GetChildren()) if (Find(c) is Node f) return f;
            return null;
        }
    }

    /// <summary>Save a screenshot and quit once enough frames have drawn for the camera to settle.</summary>
    public void Capture(Node node, ref int frames)
    {
        // A clip being recorded (--write-movie) with --clean hides the HUD from the start, not just for the last frame.
        if (Screenshot != null && Clean && frames == 5 && Engine.GetWriteMoviePath() != "") HideHud(node.GetTree().Root);
        if (Screenshot == null || ++frames < Frames) return;
        if (Clean && frames == Frames) { HideHud(node.GetTree().Root); return; }
        var img = node.GetViewport().GetTexture().GetImage();
        img.SavePng(Screenshot);
        GD.Print($"Saved {Screenshot}");
        node.GetTree().Quit();
        Screenshot = null;
    }
}
