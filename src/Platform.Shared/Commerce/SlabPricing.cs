using Platform.Shared.Entities.Pricing;

namespace Platform.Shared.Commerce;

/// <summary>
/// Picks the rate from a price's quantity slabs. The whole quantity takes the
/// rate of the highest slab it reaches (decision 3): 150 bags against slabs at
/// 0 and 100 are all charged at the 100-bag rate.
/// </summary>
public static class SlabPricing
{
    /// <summary>
    /// Returns the unit price for a quantity.
    /// </summary>
    /// <param name="slabs">Slabs of one price, in any order; must include one at 0.</param>
    /// <param name="quantity">Amount bought, in the price's unit.</param>
    /// <returns>Price of one unit at that quantity.</returns>
    /// <exception cref="ArgumentException">No slab applies (there is no slab at 0).</exception>
    public static decimal UnitPriceFor(IEnumerable<PriceSlab> slabs, decimal quantity) =>
        slabs.Where(s => s.MinQuantity <= quantity)
            .OrderByDescending(s => s.MinQuantity)
            .Select(s => (decimal?)s.UnitPrice)
            .FirstOrDefault()
        ?? throw new ArgumentException("A price must have a slab starting at 0.", nameof(slabs));

    /// <summary>
    /// Returns the slabs sorted by quantity, the canonical order they are stored and shown in.
    /// </summary>
    /// <param name="slabs">Slabs in any order.</param>
    /// <returns>Slabs ascending by minimum quantity.</returns>
    public static List<PriceSlab> Sorted(IEnumerable<PriceSlab> slabs) =>
        slabs.OrderBy(s => s.MinQuantity).ToList();
}
