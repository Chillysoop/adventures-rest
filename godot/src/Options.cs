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
    /// <summary>--dock Town opens that dock group list, for screenshots.</summary>
    public string? Dock;
    public float? Scroll;

    /// <summary>With --autoplay, leave companies' reports unbought for the player (screenshots of the companies window).</summary>
    public bool KeepReports;

    /// <summary>For roster shots (--roster ready|offer): the company picked in the roster, and the answer to the job offered it.</summary>
    public string? RosterPick;
    public (string Company, string Site, JobAnswer Answer)? RosterAnswer;

    /// <summary>With --autoplay, the stand-in leaves the armory to the player: stock and offers wait (--keep-armory).</summary>
    public bool KeepArmory;

    /// <summary>Lift the fog everywhere (screenshots of the wilds and the roads through them).</summary>
    public bool Reveal;
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

    /// <summary>--history [power-id]: open the world's history timeline, with a power's story picked.</summary>
    public string? History;

    /// <summary>--glossary [entry-id]: open the glossary at an entry; --keyword id[,id] pins keyword cards (screenshots).</summary>
    public string? Glossary;
    public List<string> Keywords = [];
    public string? Tutorial;
    public bool Hero;

    /// <summary>--folk: select the most practised townsperson, for a look at their character sheet.</summary>
    public bool Folk;

    /// <summary>--rolls: look into the busiest tavern and replay its latest skill checks as dice over people's heads.</summary>
    public bool Rolls;

    /// <summary>--wall N: select a section of the outer wall (0 is the gate) in the inspector, for shots of the call to arms.</summary>
    public int? Wall { get; private set; }

    /// <summary>--house: select the house on the most pleasant street, for screenshots of the inspector.</summary>
    public bool House;
    /// <summary>Aim the camera at the building going up with the most hands on it (<c>--rising</c>), for a shot of the builders.</summary>
    public bool Rising;
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
                case "--dock": o.Dock = Next(); break;
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
                case "--history": o.History = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--glossary": o.Glossary = args.Length > i + 1 && !args[i + 1].StartsWith("--") ? Next() : ""; break;
                case "--keyword": o.Keywords.AddRange(Next().Split(',')); break;
                case "--tutorial": o.Tutorial = Next(); break;
                case "--hero": o.Hero = true; break;
                case "--folk": o.Folk = true; break;
                case "--rolls": o.Rolls = true; break;
                case "--wall": o.Wall = int.Parse(Next()); break;
                case "--strike": o.Strike = true; break;
                case "--house": o.House = true; break;
                case "--rising": o.Rising = true; break;
                case "--card": o.Card = true; break;
                case "--alert": o.Alert = int.Parse(Next()); break;
                case "--alert-hover": o.AlertHover = int.Parse(Next()); break;
                case "--no-pause": o.NoPause = true; break;
                case "--keep-reports": o.KeepReports = true; break;
                case "--keep-armory": o.KeepArmory = true; break;
                case "--reveal": o.Reveal = true; break;
                case "--cam": o.Cam = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--look":
                    var xy = Next().Split(',');
                    o.Look = new Godot.Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "--strategic": o.StrategicView = true; break;
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
                case "--find-pick": o.FindPick = Next(); break;
                case "--story-pick": o.StoryPick = int.Parse(Next()); break;
                case "--background": o.Background = Next(); break;
                case "--mayor-level": o.MayorLevel = int.Parse(Next()); break;
                case "--advance": o.Advances.Add(Next()); break;
                case "--inspiration": o.Inspiration = int.Parse(Next()); break;
                case "--decor-at": o.DecorAt = Next().Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); break;
                case "--bounty" or "--scry" or "--send" or "--recall" or "--build" or "--scout" or "--mirror" or "--relic" or "--stock" or "--pact" or "--buy" or "--call" or "--flag" or "--wait" or "--works" or "--roster" or "--spot" or "--draw-wall" or "--gate":
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

    /// <summary>Save a screenshot and quit once enough frames have drawn for the camera to settle.</summary>
    public void Capture(Node node, ref int frames)
    {
        if (Screenshot == null || ++frames < Frames) return;
        var img = node.GetViewport().GetTexture().GetImage();
        img.SavePng(Screenshot);
        GD.Print($"Saved {Screenshot}");
        node.GetTree().Quit();
        Screenshot = null;
    }
}
