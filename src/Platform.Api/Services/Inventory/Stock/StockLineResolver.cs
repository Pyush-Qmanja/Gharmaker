using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Api.Services.Catalog;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Units;

namespace Platform.Api.Services.Inventory.Stock;

/// <summary>
/// Turns requested lines (SKU code, amount, any unit) into document lines:
/// finds each SKU and converts the amount into the SKU's base unit through the
/// one conversion service (P7). Every problem is reported against its line.
/// </summary>
public interface IStockLineResolver
{
    /// <summary>
    /// Resolves and converts lines.
    /// </summary>
    /// <param name="lines">Normalised lines (codes upper-case, no blanks).</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One document line per request line, in order.</returns>
    /// <exception cref="FieldValidationException">A SKU is unknown or inactive, or a unit does not apply to it.</exception>
    Task<List<StockDocumentLine>> ResolveAsync(IReadOnlyList<StockLineRequest> lines, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IStockLineResolver"/>.
/// </summary>
public sealed class StockLineResolver : IStockLineResolver
{
    /// <summary>Firestore's limit on values in one "in" filter.</summary>
    private const int InFilterLimit = 30;

    /// <summary>Stored name of <see cref="Sku.Code"/>.</summary>
    private static readonly string SkuCodeField = FirestoreNaming.Field(nameof(Sku.Code));

    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Product> _products;
    private readonly IUomConversionProvider _conversions;

    /// <summary>
    /// Creates the resolver.
    /// </summary>
    /// <param name="skus">SKU data access.</param>
    /// <param name="products">Product data access, for line names.</param>
    /// <param name="conversions">The organisation's unit conversion service.</param>
    public StockLineResolver(IRepository<Sku> skus, IRepository<Product> products, IUomConversionProvider conversions)
    {
        _skus = skus;
        _products = products;
        _conversions = conversions;
    }

    /// <inheritdoc />
    public async Task<List<StockDocumentLine>> ResolveAsync(IReadOnlyList<StockLineRequest> lines, CancellationToken cancellationToken = default)
    {
        var skusByCode = new Dictionary<string, Sku>(StringComparer.OrdinalIgnoreCase);
        foreach (string[] chunk in lines.Select(l => l.SkuCode).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(InFilterLimit))
        {
            foreach (Sku sku in await _skus.ListAsync(_skus.Query().WhereIn(SkuCodeField, chunk), cancellationToken))
            {
                skusByCode[sku.Code] = sku;
            }
        }

        IReadOnlyDictionary<Guid, Product> products = (await _products.GetByIdsAsync(skusByCode.Values.Select(s => s.ProductId), cancellationToken))
            .ToDictionary(p => p.Id);
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);

        var errors = new Dictionary<string, string[]>();
        var resolved = new List<StockDocumentLine>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            StockLineRequest line = lines[i];
            if (!skusByCode.TryGetValue(line.SkuCode, out Sku? sku))
            {
                errors[$"Lines[{i}].SkuCode"] = new[] { $"No SKU with code {line.SkuCode}." };
                continue;
            }

            if (!sku.IsActive)
            {
                errors[$"Lines[{i}].SkuCode"] = new[] { $"SKU {sku.Code} is inactive." };
                continue;
            }

            var units = new SkuUnits(sku.BaseUom, sku.Conversions);
            if (!converter.TryConvert(new Quantity(line.Quantity, line.Uom), sku.BaseUom, units, out Quantity inBase))
            {
                string allowed = string.Join(", ", converter.ReachableUnits(units).Select(u => u.Uom));
                errors[$"Lines[{i}].Uom"] = new[] { $"{sku.Code} cannot be counted in {line.Uom}. Use one of: {allowed}." };
                continue;
            }

            if (inBase.Value == 0)
            {
                errors[$"Lines[{i}].Quantity"] = new[] { $"{line.Quantity} {line.Uom} is less than 0.0001 {sku.BaseUom}." };
                continue;
            }

            string productName = products.TryGetValue(sku.ProductId, out Product? product) ? product.Name : sku.Code;
            resolved.Add(new StockDocumentLine
            {
                SkuId = sku.Id,
                SkuCode = sku.Code,
                Name = $"{productName} · {sku.VariantLabel}",
                Quantity = Quantity.Normalise(line.Quantity),
                Uom = line.Uom,
                BaseQuantity = inBase.Value,
                BaseUom = sku.BaseUom,
            });
        }

        return errors.Count > 0 ? throw new FieldValidationException(errors) : resolved;
    }
}
