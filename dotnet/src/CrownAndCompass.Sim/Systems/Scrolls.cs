namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Spell scrolls. The scriptorium (and the mage tower) sells single-use scrolls of one area spell that any hero who
/// casts can read in a fight; hoards turn them up too, and a wizard copies a found scroll into the spellbook, where
/// it makes their own area spell hit harder for good. Numbers in shrines.json <c>scroll</c>.
/// </summary>
public static class Scrolls
{
    /// <summary>Only heroes who cast can make sense of a scroll.</summary>
    public static bool Reads(ClassDef cls) => cls.Magic != "";

    /// <summary>Casters with gold to spare buy scrolls before they march.</summary>
    public static void Outfit(Town town, IReadOnlyList<Agent> heroes)
    {
        var sr = town.Content.Shrines.Scroll;
        var shops = town.Buildings.Where(b => sr.Shops.Contains(b.Def.Id) && b.Complete && !b.Closed && !b.ForSale).ToList();
        if (shops.Count == 0) return;
        foreach (var h in heroes)
        {
            var s = h.Hero!;
            if (!Reads(s.Cls)) continue;
            while (s.Scrolls < sr.Carry && h.Gold >= sr.Price * 2)
            {
                var shop = shops[(h.Id + s.Scrolls) % shops.Count];
                double tax = sr.Price * town.TaxRate;
                h.Gold -= sr.Price;
                town.Book(TreasuryAccount.SalesTax, tax);
                shop.Cash += sr.Price - tax - sr.Wholesale;
                shop.RevenueToday += sr.Price;
                s.Scrolls++;
            }
        }
    }
}
