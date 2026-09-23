using System.Globalization;
using FluentValidation;
using Platform.Shared.Common;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Validation.Common;

namespace Platform.Api.Services.Catalog.Import;

/// <summary>
/// Existing catalogue data an import is checked against, loaded by the service.
/// </summary>
/// <param name="Categories">Every category of the organisation.</param>
/// <param name="Brands">Every brand of the organisation.</param>
/// <param name="UnitCodes">Codes of active units.</param>
/// <param name="ProductsBySlug">Existing products whose slug appears in the file.</param>
/// <param name="SkusByCode">Existing SKUs whose code appears in the file.</param>
public sealed record ImportState(
    IReadOnlyList<Category> Categories,
    IReadOnlyList<Brand> Brands,
    IReadOnlySet<string> UnitCodes,
    IReadOnlyDictionary<string, Product> ProductsBySlug,
    IReadOnlyDictionary<string, Sku> SkusByCode);

/// <summary>
/// Everything an import would do: per-row outcomes plus the entities to create
/// or update. Built without touching the database.
/// </summary>
public sealed class ImportPlan
{
    /// <summary>Outcome of every data row.</summary>
    public List<ImportRowResultDto> Rows { get; } = new();

    /// <summary>Categories to create, parents before children.</summary>
    public List<Category> NewCategories { get; } = new();

    /// <summary>Display paths of <see cref="NewCategories"/>, e.g. "Steel &gt; TMT Bars".</summary>
    public List<string> NewCategoryPaths { get; } = new();

    /// <summary>Brands to create.</summary>
    public List<Brand> NewBrands { get; } = new();

    /// <summary>Products to create.</summary>
    public List<Product> NewProducts { get; } = new();

    /// <summary>Existing products to update (category, HSN or search terms).</summary>
    public List<Product> UpdatedProducts { get; } = new();

    /// <summary>SKUs to create.</summary>
    public List<Sku> NewSkus { get; } = new();

    /// <summary>Existing SKUs to update.</summary>
    public List<Sku> UpdatedSkus { get; } = new();

    /// <summary>True when any row has an error; such a plan must not be applied.</summary>
    public bool HasErrors => Rows.Any(r => r.Status == ImportRowStatus.Error);
}

/// <summary>
/// Turns import rows into an <see cref="ImportPlan"/>: validates each row,
/// resolves categories, brands, products and SKUs against existing data, and
/// decides what to create or update. Pure — no I/O — so preview and commit
/// always agree.
/// </summary>
public static class CatalogImportPlanner
{
    /// <summary>Field-level rules for one row, built from the shared validation rules.</summary>
    private static readonly RowValidator Validator = new();

    /// <summary>
    /// Builds the plan for a file.
    /// </summary>
    /// <param name="rows">Rows read from the file.</param>
    /// <param name="state">Existing catalogue data.</param>
    /// <returns>The plan.</returns>
    public static ImportPlan Plan(IReadOnlyList<ImportFileRow> rows, ImportState state)
    {
        var plan = new ImportPlan();
        var context = new PlanContext(state, plan);

        foreach (ImportFileRow row in rows)
        {
            var result = new ImportRowResultDto
            {
                RowNumber = row.RowNumber,
                CategoryPath = row[CatalogImportColumns.Category],
                Brand = row[CatalogImportColumns.Brand],
                Product = row[CatalogImportColumns.Product],
                SkuCode = row[CatalogImportColumns.SkuCode],
                Variant = row[CatalogImportColumns.Variant],
                BaseUnit = row[CatalogImportColumns.BaseUnit],
            };
            plan.Rows.Add(result);
            context.PlanRow(row, result);
        }

        context.RefreshSearchTerms();
        return plan;
    }

    /// <summary>
    /// Mutable bookkeeping for one planning run.
    /// </summary>
    private sealed class PlanContext
    {
        private readonly ImportState _state;
        private readonly ImportPlan _plan;
        private readonly Dictionary<string, Category> _categoriesBySlug;
        private readonly Dictionary<Guid, Category> _categoriesById;
        private readonly Dictionary<string, Brand> _brandsByKey;
        private readonly Dictionary<string, Product> _plannedProducts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string CategorySlug, string Hsn, int Row)> _productDefinitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _skuRows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, (Product Product, string BrandName, HashSet<string> Extra)> _touched = new();
        private int _nextCategoryOrder;
        private int _nextBrandOrder;

        /// <summary>
        /// Indexes the existing data.
        /// </summary>
        /// <param name="state">Existing catalogue data.</param>
        /// <param name="plan">Plan being built.</param>
        public PlanContext(ImportState state, ImportPlan plan)
        {
            _state = state;
            _plan = plan;
            _categoriesBySlug = state.Categories.GroupBy(c => c.Slug).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            _categoriesById = state.Categories.ToDictionary(c => c.Id);
            _brandsByKey = new Dictionary<string, Brand>(StringComparer.Ordinal);
            foreach (Brand brand in state.Brands)
            {
                _brandsByKey.TryAdd(brand.Slug, brand);
                _brandsByKey.TryAdd(Slug.From(brand.Name), brand);
            }

            _nextCategoryOrder = state.Categories.Count == 0 ? 1 : state.Categories.Max(c => c.DisplayOrder) + 1;
            _nextBrandOrder = state.Brands.Count == 0 ? 1 : state.Brands.Max(b => b.DisplayOrder) + 1;
        }

        /// <summary>
        /// Validates one row and, when valid, adds its changes to the plan.
        /// </summary>
        /// <param name="row">File row.</param>
        /// <param name="result">Outcome to fill in.</param>
        public void PlanRow(ImportFileRow row, ImportRowResultDto result)
        {
            List<string> errors = Validator.Validate(row).Errors.Select(e => e.ErrorMessage).ToList();

            string[] segments = row[CatalogImportColumns.Category]
                .Split(CatalogImportColumns.CategorySeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(s => Slug.From(s).Length == 0 || s.Length > FieldLengths.Name))
            {
                errors.Add($"Each category level needs letters or digits and at most {FieldLengths.Name} characters.");
            }

            string baseUom = row[CatalogImportColumns.BaseUnit].ToUpperInvariant();
            if (baseUom.Length > 0 && !_state.UnitCodes.Contains(baseUom))
            {
                errors.Add($"Unknown base unit '{baseUom}'. See the Units sheet of the template.");
            }

            List<SkuConversion> conversions = ParseConversions(row[CatalogImportColumns.Conversions], baseUom, _state.UnitCodes, errors);
            bool? isActive = ParseActive(row[CatalogImportColumns.Active], errors);

            string skuCode = row[CatalogImportColumns.SkuCode].ToUpperInvariant();
            if (skuCode.Length > 0 && !_skuRows.TryAdd(skuCode, row.RowNumber))
            {
                errors.Add($"SKU code {skuCode} also appears on row {_skuRows[skuCode]}.");
            }

            string brandName = row[CatalogImportColumns.Brand];
            string productName = row[CatalogImportColumns.Product];
            string hsn = row[CatalogImportColumns.Hsn];
            string categorySlug = CategorySlug(segments, segments.Length);
            string productSlug = ProductSlug(Slug.From(brandName), productName);

            // The first row naming a product sets its category and HSN, even if that
            // row has other errors, so every conflict is reported in one upload.
            if (productSlug.Length > 0
                && !_productDefinitions.TryAdd(productSlug, (categorySlug, hsn, row.RowNumber))
                && _productDefinitions[productSlug] is var first
                && (first.CategorySlug != categorySlug || first.Hsn != hsn))
            {
                errors.Add($"'{productName}' has a different category or HSN on row {first.Row}; all rows of one product must match.");
            }

            if (errors.Count > 0)
            {
                Fail(result, errors);
                return;
            }

            _state.ProductsBySlug.TryGetValue(productSlug, out Product? existingProduct);
            _state.SkusByCode.TryGetValue(skuCode, out Sku? existingSku);
            if (existingSku is not null && existingSku.ProductId != existingProduct?.Id)
            {
                Fail(result, new() { $"SKU code {skuCode} already belongs to another product." });
                return;
            }

            Category category = EnsureCategoryPath(segments);
            Brand brand = EnsureBrand(brandName);
            Product product = ResolveProduct(existingProduct, productSlug, productName, brand, category, hsn, result);

            if (existingSku is null)
            {
                _plan.NewSkus.Add(new Sku
                {
                    ProductId = product.Id,
                    Code = skuCode,
                    VariantLabel = row[CatalogImportColumns.Variant],
                    BaseUom = baseUom,
                    Conversions = conversions,
                    IsActive = isActive ?? true,
                });
                result.Status = ImportRowStatus.New;
            }
            else
            {
                if (!existingSku.BaseUom.Equals(baseUom, StringComparison.OrdinalIgnoreCase))
                {
                    result.Messages.Add($"Base unit changes from {existingSku.BaseUom} to {baseUom}.");
                }

                existingSku.VariantLabel = row[CatalogImportColumns.Variant];
                existingSku.BaseUom = baseUom;
                existingSku.Conversions = conversions;
                existingSku.IsActive = isActive ?? true;
                _plan.UpdatedSkus.Add(existingSku);
                result.Status = ImportRowStatus.Update;
            }

            _touched[product.Id].Extra.UnionWith(new[] { skuCode, row[CatalogImportColumns.Variant] });
        }

        /// <summary>
        /// Recomputes search terms for every product the file touched (name, brand,
        /// category names, SKU codes and variants, plus terms it already had).
        /// </summary>
        public void RefreshSearchTerms()
        {
            foreach (var (product, brandName, extra) in _touched.Values)
            {
                IEnumerable<string> categoryNames = product.CategoryPath
                    .Where(_categoriesById.ContainsKey)
                    .Select(id => _categoriesById[id].Name);
                product.SearchTerms = SearchTerms.Build(
                        new[] { product.Name, brandName }.Concat(categoryNames).Concat(extra))
                    .Union(product.SearchTerms)
                    .Order(StringComparer.Ordinal)
                    .ToList();

                if (!_plan.NewProducts.Contains(product) && !_plan.UpdatedProducts.Contains(product))
                {
                    _plan.UpdatedProducts.Add(product);
                }
            }
        }

        /// <summary>
        /// Finds the row's product, creating it (once) or updating its category and HSN.
        /// </summary>
        /// <param name="existing">Product already in the database, if any.</param>
        /// <param name="slug">Product slug.</param>
        /// <param name="name">Product name as written.</param>
        /// <param name="brand">Resolved brand.</param>
        /// <param name="category">Resolved leaf category.</param>
        /// <param name="hsn">HSN code.</param>
        /// <param name="result">Row outcome, for notes.</param>
        /// <returns>The product the SKU belongs to.</returns>
        private Product ResolveProduct(Product? existing, string slug, string name, Brand brand, Category category, string hsn, ImportRowResultDto result)
        {
            if (_plannedProducts.TryGetValue(slug, out Product? planned))
            {
                return planned;
            }

            Product product;
            if (existing is null)
            {
                product = new Product { BrandId = brand.Id, Name = name, Slug = slug };
                _plan.NewProducts.Add(product);
            }
            else
            {
                product = existing;
                if (existing.CategoryId != category.Id)
                {
                    result.Messages.Add($"Product moves to category {category.Name}.");
                }

                if (existing.HsnCode != hsn)
                {
                    result.Messages.Add($"HSN changes from {existing.HsnCode} to {hsn}.");
                }
            }

            product.CategoryId = category.Id;
            product.CategoryPath = PathOf(category);
            product.HsnCode = hsn;
            _plannedProducts[slug] = product;
            _touched[product.Id] = (product, brand.Name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return product;
        }

        /// <summary>
        /// Returns the leaf category for a path, planning any missing levels.
        /// </summary>
        /// <param name="segments">Category names, top level first.</param>
        /// <returns>The leaf category.</returns>
        private Category EnsureCategoryPath(string[] segments)
        {
            Category? parent = null;
            for (int depth = 1; depth <= segments.Length; depth++)
            {
                string slug = CategorySlug(segments, depth);
                if (!_categoriesBySlug.TryGetValue(slug, out Category? category))
                {
                    category = new Category
                    {
                        ParentId = parent?.Id,
                        Name = segments[depth - 1],
                        Slug = slug,
                        DisplayOrder = _nextCategoryOrder++,
                    };
                    _categoriesBySlug[slug] = category;
                    _categoriesById[category.Id] = category;
                    _plan.NewCategories.Add(category);
                    _plan.NewCategoryPaths.Add(string.Join(" > ", segments.Take(depth)));
                }

                parent = category;
            }

            return parent!;
        }

        /// <summary>
        /// Finds a brand by name or slug, planning it when new.
        /// </summary>
        /// <param name="name">Brand name as written.</param>
        /// <returns>The brand.</returns>
        private Brand EnsureBrand(string name)
        {
            string key = Slug.From(name);
            if (!_brandsByKey.TryGetValue(key, out Brand? brand))
            {
                brand = new Brand { Name = name, Slug = key, DisplayOrder = _nextBrandOrder++ };
                _brandsByKey[key] = brand;
                _plan.NewBrands.Add(brand);
            }

            return brand;
        }

        /// <summary>
        /// Returns a category's id and all its ancestors' ids, top level first.
        /// </summary>
        /// <param name="category">Leaf category.</param>
        /// <returns>The path.</returns>
        private List<Guid> PathOf(Category category)
        {
            var path = new List<Guid>();
            for (Category? current = category; current is not null;
                 current = current.ParentId is { } parentId && _categoriesById.TryGetValue(parentId, out var parent) ? parent : null)
            {
                path.Insert(0, current.Id);
            }

            return path;
        }
    }

    /// <summary>
    /// Slug of a category path's first <paramref name="depth"/> levels, e.g. <c>steel/tmt-bars</c>.
    /// </summary>
    /// <param name="segments">Category names.</param>
    /// <param name="depth">Levels to include.</param>
    /// <returns>The path slug.</returns>
    private static string CategorySlug(string[] segments, int depth) =>
        string.Join('/', segments.Take(depth).Select(Slug.From));

    /// <summary>
    /// Product slug: the name's slug, prefixed with the brand's unless the name already starts with it.
    /// </summary>
    /// <param name="brandSlug">Brand slug.</param>
    /// <param name="productName">Product name.</param>
    /// <returns>The slug, e.g. <c>tata-tiscon-550sd</c>.</returns>
    public static string ProductSlug(string brandSlug, string productName)
    {
        string nameSlug = Slug.From(productName);
        return nameSlug.StartsWith(brandSlug + "-", StringComparison.Ordinal) || nameSlug == brandSlug
            ? nameSlug
            : $"{brandSlug}-{nameSlug}";
    }

    /// <summary>
    /// Parses <c>TONNE=20; TRUCK=400</c> into conversions, adding errors for bad parts.
    /// </summary>
    /// <param name="text">Cell text.</param>
    /// <param name="baseUom">Row's base unit.</param>
    /// <param name="unitCodes">Known unit codes.</param>
    /// <param name="errors">Error list to add to.</param>
    /// <returns>The valid conversions.</returns>
    private static List<SkuConversion> ParseConversions(string text, string baseUom, IReadOnlySet<string> unitCodes, List<string> errors)
    {
        var conversions = new List<SkuConversion>();
        foreach (string part in text.Split(CatalogImportColumns.ConversionSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pieces = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length != 2
                || !decimal.TryParse(pieces[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal factor)
                || factor <= 0)
            {
                errors.Add($"Conversion '{part}' must look like TONNE=20 with a number greater than zero.");
                continue;
            }

            string unit = pieces[0].ToUpperInvariant();
            if (!unitCodes.Contains(unit))
            {
                errors.Add($"Unknown unit '{unit}' in conversion '{part}'. See the Units sheet of the template.");
            }
            else if (unit == baseUom)
            {
                errors.Add($"Conversion '{part}' repeats the base unit.");
            }
            else if (conversions.Any(c => c.Uom == unit))
            {
                errors.Add($"Unit {unit} is converted twice.");
            }
            else
            {
                conversions.Add(new SkuConversion { Uom = unit, Factor = factor });
            }
        }

        return conversions;
    }

    /// <summary>
    /// Parses the Active cell: blank/yes/y/true/1 or no/n/false/0.
    /// </summary>
    /// <param name="text">Cell text.</param>
    /// <param name="errors">Error list to add to.</param>
    /// <returns>The value, or null when invalid.</returns>
    private static bool? ParseActive(string text, List<string> errors)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "" or "yes" or "y" or "true" or "1":
                return true;
            case "no" or "n" or "false" or "0":
                return false;
            default:
                errors.Add($"Active must be Yes or No, not '{text}'.");
                return null;
        }
    }

    /// <summary>
    /// Marks a row as an error with its messages.
    /// </summary>
    /// <param name="result">Row outcome.</param>
    /// <param name="errors">Messages.</param>
    private static void Fail(ImportRowResultDto result, List<string> errors)
    {
        result.Status = ImportRowStatus.Error;
        result.Messages.AddRange(errors);
    }

    /// <summary>
    /// Field rules for one row, reusing the platform's shared validation rules
    /// so a SKU code or name is judged the same everywhere.
    /// </summary>
    private sealed class RowValidator : AbstractValidator<ImportFileRow>
    {
        /// <summary>
        /// Defines the row rules.
        /// </summary>
        public RowValidator()
        {
            RuleFor(r => r[CatalogImportColumns.Category]).NotEmpty().OverridePropertyName(CatalogImportColumns.Category);
            RuleFor(r => r[CatalogImportColumns.Brand]).ValidName().OverridePropertyName(CatalogImportColumns.Brand);
            RuleFor(r => r[CatalogImportColumns.Product]).ValidName().OverridePropertyName(CatalogImportColumns.Product);
            RuleFor(r => r[CatalogImportColumns.Hsn])
                .Matches(@"^\d{4}(\d{2}){0,2}$").WithMessage("'HSN' must be 4, 6 or 8 digits.")
                .OverridePropertyName(CatalogImportColumns.Hsn);
            RuleFor(r => r[CatalogImportColumns.SkuCode]).ValidCode().OverridePropertyName(CatalogImportColumns.SkuCode);
            RuleFor(r => r[CatalogImportColumns.Variant]).NotEmpty().MaximumLength(FieldLengths.VariantLabel)
                .OverridePropertyName(CatalogImportColumns.Variant);
            RuleFor(r => r[CatalogImportColumns.BaseUnit]).NotEmpty().OverridePropertyName(CatalogImportColumns.BaseUnit);
        }
    }
}
