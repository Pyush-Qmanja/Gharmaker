using Platform.Api.Services.Pricing;
using Platform.Shared.Commerce;
using Platform.Shared.Common;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Pricing;
using Xunit;

namespace Platform.Tests.Unit;

/// <summary>
/// GST as a tax invoice shows it: split within a state, integrated across states,
/// rounded to paise per line, totals that add up.
/// </summary>
public sealed class GstCalculatorTests
{
    /// <summary>Within the seller's state, 28% is 14% CGST plus 14% SGST.</summary>
    [Fact]
    public void IntraStateSplitsIntoCgstAndSgst()
    {
        var tax = GstCalculator.Compute(45_600m, 28m, 0m, isInterState: false);

        Assert.Equal(6_384m, tax.CgstAmount);
        Assert.Equal(6_384m, tax.SgstAmount);
        Assert.Equal(0m, tax.IgstAmount);
        Assert.Equal(58_368m, tax.TotalAmount);
    }

    /// <summary>To another state, the whole rate is IGST.</summary>
    [Fact]
    public void InterStateIsIgst()
    {
        var tax = GstCalculator.Compute(12_000m, 28m, 0m, isInterState: true);

        Assert.Equal(3_360m, tax.IgstAmount);
        Assert.Equal(0m, tax.CgstAmount + tax.SgstAmount);
        Assert.Equal(15_360m, tax.TotalAmount);
    }

    /// <summary>Each half is rounded to paise on its own, half away from zero, as printed.</summary>
    [Fact]
    public void HalvesAreRoundedToPaise()
    {
        var tax = GstCalculator.Compute(0.25m, 18m, 0m, isInterState: false);

        Assert.Equal(0.02m, tax.CgstAmount);
        Assert.Equal(0.02m, tax.SgstAmount);
        Assert.Equal(0.29m, tax.TotalAmount);
    }

    /// <summary>Cess is added on top of GST.</summary>
    [Fact]
    public void CessIsAddedOnTop()
    {
        var tax = GstCalculator.Compute(1_000m, 28m, 12m, isInterState: true);

        Assert.Equal(120m, tax.CessAmount);
        Assert.Equal(1_400m, tax.TotalAmount);
    }

    /// <summary>Order totals are the sums of the rounded lines, so the invoice always adds up.</summary>
    [Fact]
    public void TotalsAreSumsOfLines()
    {
        var lines = new[]
        {
            GstCalculator.Compute(10_000m, 28m, 0m, false),
            GstCalculator.Compute(32_500m, 18m, 0m, false),
        };

        var total = GstCalculator.Sum(lines);

        Assert.Equal(42_500m, total.TaxableAmount);
        Assert.Equal(4_325m, total.CgstAmount);
        Assert.Equal(51_150m, total.TotalAmount);
        Assert.Equal(total.TaxableAmount + total.CgstAmount + total.SgstAmount + total.IgstAmount + total.CessAmount, total.TotalAmount);
    }
}

/// <summary>
/// Quantity slabs apply to the whole quantity (decision 3).
/// </summary>
public sealed class SlabPricingTests
{
    private static readonly PriceSlab[] Cement =
    {
        new() { MinQuantity = 100, UnitPrice = 380 },
        new() { MinQuantity = 0, UnitPrice = 400 },
        new() { MinQuantity = 500, UnitPrice = 365 },
    };

    /// <summary>Below the first break the base rate applies; at or above, the whole quantity takes the better rate.</summary>
    /// <param name="quantity">Bags bought.</param>
    /// <param name="expected">Rate per bag.</param>
    [Theory]
    [InlineData(1, 400)]
    [InlineData(99.5, 400)]
    [InlineData(100, 380)]
    [InlineData(150, 380)]
    [InlineData(500, 365)]
    public void WholeQuantityTakesTheHighestSlabReached(decimal quantity, decimal expected) =>
        Assert.Equal(expected, SlabPricing.UnitPriceFor(Cement, quantity));

    /// <summary>A price without a slab at 0 is refused rather than priced at zero.</summary>
    [Fact]
    public void NoSlabAtZeroIsRefused() =>
        Assert.Throws<ArgumentException>(() => SlabPricing.UnitPriceFor(new[] { new PriceSlab { MinQuantity = 10, UnitPrice = 1 } }, 5));
}

/// <summary>
/// Where an order ships from (decision #2: never split): one serving warehouse
/// must hold enough of every line; the fastest wins, then the fullest.
/// </summary>
public sealed class AllocationPlannerTests
{
    private static readonly Guid Fast = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Slow = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Cement = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Steel = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    /// <summary>Stock of cement and steel in the fast and slow warehouses.</summary>
    /// <param name="fastCement">Cement free in the fast warehouse.</param>
    /// <param name="slowCement">Cement free in the slow warehouse.</param>
    /// <param name="fastSteel">Steel free in the fast warehouse.</param>
    /// <param name="slowSteel">Steel free in the slow warehouse.</param>
    /// <returns>Stock by SKU.</returns>
    private static Dictionary<Guid, List<WarehouseStock>> Stock(decimal fastCement, decimal slowCement, decimal fastSteel, decimal slowSteel) => new()
    {
        [Cement] = new() { new WarehouseStock(Fast, 1, fastCement), new WarehouseStock(Slow, 3, slowCement) },
        [Steel] = new() { new WarehouseStock(Fast, 1, fastSteel), new WarehouseStock(Slow, 3, slowSteel) },
    };

    /// <summary>The fastest warehouse ships when it has enough of everything.</summary>
    [Fact]
    public void FastestWarehouseThatHasEverythingShips() =>
        Assert.Equal(new OrderSource(Fast, 1), AllocationPlanner.PlanOrder(
            new[] { new LineNeed(Cement, 50), new LineNeed(Steel, 10) }, Stock(60, 500, 20, 500)));

    /// <summary>A faster warehouse missing one line loses to a slower one that has it all.</summary>
    [Fact]
    public void WholeOrderMovesToTheWarehouseThatHasItAll() =>
        Assert.Equal(new OrderSource(Slow, 3), AllocationPlanner.PlanOrder(
            new[] { new LineNeed(Cement, 50), new LineNeed(Steel, 10) }, Stock(60, 500, 5, 500)));

    /// <summary>A quantity is never split: 120 bags with 100 + 50 in two places cannot be promised.</summary>
    [Fact]
    public void QuantityIsNeverSplit() =>
        Assert.Null(AllocationPlanner.PlanOrder(new[] { new LineNeed(Cement, 120) }, Stock(50, 100, 0, 0)));

    /// <summary>Lines that each fit somewhere, but not together anywhere, cannot be promised.</summary>
    [Fact]
    public void LinesMustFitTogether() =>
        Assert.Null(AllocationPlanner.PlanOrder(
            new[] { new LineNeed(Cement, 50), new LineNeed(Steel, 10) }, Stock(60, 0, 0, 20)));

    /// <summary>Between equally fast warehouses, the one left fuller ships.</summary>
    [Fact]
    public void TieGoesToTheFullest()
    {
        var stock = new Dictionary<Guid, List<WarehouseStock>>
        {
            [Cement] = new() { new WarehouseStock(Fast, 2, 60), new WarehouseStock(Slow, 2, 90) },
        };

        Assert.Equal(Slow, AllocationPlanner.PlanOrder(new[] { new LineNeed(Cement, 50) }, stock)!.WarehouseId);
    }

    /// <summary>What one order can get is the best single place, not the sum.</summary>
    [Fact]
    public void MostFromOnePlaceIsNotTheSum() =>
        Assert.Equal(100m, AllocationPlanner.MostFromOnePlace(Stock(50, 100, 0, 0)[Cement]));

    /// <summary>Delivery is one date from one warehouse; empty warehouses do not count.</summary>
    [Fact]
    public void OneWarehouseOneDate()
    {
        var today = new DateOnly(2026, 9, 24);
        int? lead = AllocationPlanner.FastestLeadTime(Stock(0, 100, 0, 0)[Cement]);

        Assert.Equal(3, lead);
        Assert.Equal((today.AddDays(3), today.AddDays(3)), AllocationPlanner.Window(today, lead!.Value));
    }
}

/// <summary>
/// Financial year codes, states and money rounding.
/// </summary>
public sealed class ReferenceDataTests
{
    /// <summary>The financial year turns over on 1 April in India, not in UTC.</summary>
    /// <param name="utc">UTC instant.</param>
    /// <param name="expected">FY code.</param>
    [Theory]
    [InlineData("2026-03-31T18:29:59Z", "2526")]
    [InlineData("2026-03-31T18:30:00Z", "2627")]
    [InlineData("2027-03-31T12:00:00Z", "2627")]
    [InlineData("2099-12-31T00:00:00Z", "9900")]
    public void FinancialYearFollowsIst(string utc, string expected) =>
        Assert.Equal(expected, FinancialYear.Code(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));

    /// <summary>State names match whatever the spelling, and are told apart by GST code.</summary>
    [Fact]
    public void StatesMatchBySpellingVariants()
    {
        Assert.True(IndianStates.AreSame("maharashtra", " Maharashtra "));
        Assert.True(IndianStates.AreSame("Andaman & Nicobar Islands", "Andaman and Nicobar Islands"));
        Assert.False(IndianStates.AreSame("Maharashtra", "Karnataka"));
        Assert.False(IndianStates.AreSame("Atlantis", "Atlantis"));
    }

    /// <summary>Money rounds half away from zero and drops trailing zeros.</summary>
    [Fact]
    public void MoneyRounding()
    {
        Assert.Equal(2.35m, Money.Round(2.345m));
        Assert.Equal("97500", Money.Round(97500.00m).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// GST rates are dated (P8) and found by the longest matching HSN prefix.
/// </summary>
public sealed class TaxRateTableTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>An 8-digit rate beats the 4-digit chapter rate.</summary>
    [Fact]
    public void LongestPrefixWins()
    {
        var table = new TaxRateTable(new[]
        {
            Rate("6810", 18, Now.AddDays(-10)),
            Rate("68101110", 12, Now.AddDays(-10)),
        });

        Assert.Equal(12m, table.Find("68101110", Now)!.RatePercent);
        Assert.Equal(18m, table.Find("68109990", Now)!.RatePercent);
        Assert.Null(table.Find("2523", Now));
    }

    /// <summary>A rate applies from its start; the one before stays until then.</summary>
    [Fact]
    public void RatesAreDated()
    {
        var table = new TaxRateTable(new[]
        {
            Rate("2523", 28, Now.AddDays(-30)),
            Rate("2523", 18, Now.AddDays(5)),
        });

        Assert.Equal(28m, table.Find("2523", Now)!.RatePercent);
        Assert.Equal(18m, table.Find("2523", Now.AddDays(6))!.RatePercent);
        Assert.Null(table.Find("2523", Now.AddDays(-31)));
    }

    /// <summary>
    /// Builds a rate row.
    /// </summary>
    /// <param name="hsn">HSN code.</param>
    /// <param name="percent">Rate.</param>
    /// <param name="from">Start.</param>
    /// <returns>The row.</returns>
    private static TaxRate Rate(string hsn, decimal percent, DateTime from) =>
        new() { HsnCode = hsn, RatePercent = percent, ValidFrom = from };
}
