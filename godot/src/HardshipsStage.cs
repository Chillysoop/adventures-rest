using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// For screenshots (--hardships): sets a town up with what it would take months of play to see at once: a few sick
/// houses, traps before the south wall, a wound on the most storied hero, and the big bad's company out on the road.
/// </summary>
public partial class Main
{
    private void StageHardships()
    {
        var t = _sim.Town;
        foreach (var h in t.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0).OrderBy(b => b.Id).Take(5))
            Sickness.Seed(t, h, "a caravan home from the coast brought a fever with it");
        if (!Garrison.Walled(t)) t.Defenses.Palisade = PalisadeState.Built;
        t.Treasury += 600;
        foreach (string kind in new[] { "pitch-ditch", "caltrops", "wolf-pits" }) _sim.LayTrap(kind, 0);
        _sim.LayTrap("caltrops", 2);
        if (t.Heroes.Where(a => !a.IsAway).OrderByDescending(a => a.Hero!.Epithet != "").ThenByDescending(a => a.Hero!.Items.Count).ThenByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault() is { } hero
            && hero.Hero!.Wounds.Count == 0)
            hero.Hero.Wounds.Add(new Wound { Id = t.Content.Wounds.Wounds[0].Id, Site = t.Sites.OrderBy(s => s.Def.Id, StringComparer.Ordinal).First().Def.Name, Day = Math.Max(1, t.Day - 6) });
        if (Saga.BigBad(t) is not { Alive: true })
        {
            var top = t.Saga.Menaces.Where(m => m.Alive && Menaces.CanCrown(t, m)).OrderByDescending(m => m.Power).ThenBy(m => m.Id).FirstOrDefault()
                ?? (t.Content.Saga.Finale.Fallback.Count > 0 ? Menaces.Awaken(t, t.Content.Saga.Finale.Fallback[0]) : null);
            if (top == null) return;
            t.Saga.BigBadId = top.Id;
            t.Saga.CrownDay = t.Day;
            t.Saga.NextRideDay = t.Day + 1000;
        }
        t.DarkCompany.NextTick = t.Tick;
        for (int i = 0; i < 12 && !DarkCompany.Out(t); i++) _sim.RunDays(1);
    }
}
