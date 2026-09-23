using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Common;
using Platform.Web.Extensions;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// Catalogue screens: browse by category and brand, product page with a unit
/// converter, and bulk import (upload → preview → confirm).
/// </summary>
public sealed class CatalogController : PlatformControllerBase
{
    /// <summary>Largest upload accepted by the screen (matches the API limit).</summary>
    private const long MaxImportBytes = 10 * 1024 * 1024;

    private readonly ICatalogApiClient _catalog;
    private readonly ICrudApiClient<BrandDto, CreateBrandRequest, UpdateBrandRequest> _brands;
    private readonly IUserAccess _access;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="catalog">Catalogue API client.</param>
    /// <param name="brands">Brand API client, for the brand filter.</param>
    /// <param name="access">Current user's capabilities, to show the Import button.</param>
    public CatalogController(
        ICatalogApiClient catalog,
        ICrudApiClient<BrandDto, CreateBrandRequest, UpdateBrandRequest> brands,
        IUserAccess access)
    {
        _catalog = catalog;
        _brands = brands;
        _access = access;
    }

    /// <summary>
    /// Shows the category tree and a page of products for the chosen filters.
    /// </summary>
    /// <param name="request">Category, brand, search and page from the query string.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The browse page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CatalogBrowseRequest request, CancellationToken cancellationToken)
    {
        var tree = await _catalog.GetCategoryTreeAsync(cancellationToken);
        if (HandleAccess(tree) is { } denied)
        {
            return denied;
        }

        var products = await _catalog.GetProductsAsync(request, cancellationToken);
        if (!products.IsSuccess)
        {
            FlashError(products.ErrorMessage ?? "Could not load products.");
        }

        IReadOnlyList<BrandDto> brands = await _brands.ListForLookupAsync(cancellationToken);
        IReadOnlyList<CategoryNodeDto> categories = tree.Value ?? new List<CategoryNodeDto>();

        return View(new CatalogIndexViewModel
        {
            Categories = categories,
            CategoryId = request.CategoryId,
            CategoryName = request.CategoryId is { } id ? FindName(categories, id) : null,
            BrandId = request.BrandId,
            BrandOptions = brands
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem(b.Name, b.Id.ToString(), b.Id == request.BrandId))
                .ToList(),
            Products = new ListViewModel<ProductListItemDto>(
                "Products",
                products.Value ?? new PagedResult<ProductListItemDto> { Page = request.Page, PageSize = request.PageSize },
                request.Search,
                new Dictionary<string, string?>
                {
                    ["categoryId"] = request.CategoryId?.ToString(),
                    ["brandId"] = request.BrandId?.ToString(),
                },
                itemName: "product"),
            CanImport = await _access.CanAsync(Capabilities.CatalogManage),
        });
    }

    /// <summary>
    /// Shows a product with its SKUs, and converts an amount when the converter was used.
    /// </summary>
    /// <param name="id">Product id.</param>
    /// <param name="skuId">SKU to convert, if the converter was submitted.</param>
    /// <param name="value">Amount to convert.</param>
    /// <param name="from">Unit to convert from.</param>
    /// <param name="to">Unit to convert to.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The product page, or 404.</returns>
    [HttpGet]
    public async Task<IActionResult> Product(Guid id, Guid? skuId, decimal? value, string? from, string? to, CancellationToken cancellationToken)
    {
        var product = await _catalog.GetProductAsync(id, cancellationToken);
        if (HandleAccess(product) is { } denied)
        {
            return denied;
        }

        if (!product.IsSuccess || product.Value is null)
        {
            return NotFound();
        }

        ConversionResultDto? conversion = null;
        string? conversionError = null;
        if (skuId is { } sku && value is { } amount && !string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
        {
            var result = await _catalog.ConvertAsync(sku, new SkuConversionRequest { Value = amount, From = from, To = to }, cancellationToken);
            conversion = result.Value;
            conversionError = result.IsSuccess
                ? null
                : result.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? result.ErrorMessage;
        }

        return View(new ProductPageViewModel
        {
            Product = product.Value,
            ConvertSkuId = skuId,
            ConvertValue = value,
            ConvertFrom = from,
            ConvertTo = to,
            Conversion = conversion,
            ConversionError = conversionError,
        });
    }

    /// <summary>
    /// Shows the import page: how the file works, template download and upload.
    /// </summary>
    /// <returns>The import page.</returns>
    [HttpGet]
    public IActionResult Import() => View();

    /// <summary>
    /// Downloads the Excel template from the API.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The .xlsx file.</returns>
    [HttpGet]
    public async Task<IActionResult> Template(CancellationToken cancellationToken)
    {
        var file = await _catalog.GetImportTemplateAsync(cancellationToken);
        if (HandleAccess(file) is { } denied)
        {
            return denied;
        }

        if (!file.IsSuccess || file.Value is null)
        {
            FlashError(file.ErrorMessage ?? "Could not download the template.");
            return RedirectToAction(nameof(Import));
        }

        return File(file.Value.Content, file.Value.ContentType, file.Value.FileName);
    }

    /// <summary>
    /// Uploads a file to the API for checking and shows the row-by-row preview.
    /// Nothing is saved yet.
    /// </summary>
    /// <param name="file">Uploaded Excel or CSV file.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The preview page, or the import page with an error.</returns>
    [HttpPost]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Choose an Excel (.xlsx) or CSV (.csv) file.");
            return View();
        }

        await using Stream content = file.OpenReadStream();
        var preview = await _catalog.PreviewImportAsync(content, file.FileName, cancellationToken);
        if (HandleAccess(preview) is { } denied)
        {
            return denied;
        }

        if (!preview.IsSuccess || preview.Value is null)
        {
            ModelState.AddModelError("file", preview.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? preview.ErrorMessage ?? "The file could not be checked.");
            return View();
        }

        return View("ImportPreview", preview.Value);
    }

    /// <summary>
    /// Saves a previewed import and returns to the catalogue with a summary.
    /// </summary>
    /// <param name="importId">Preview id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the catalogue, or back to the import page on failure.</returns>
    [HttpPost]
    public async Task<IActionResult> Commit(Guid importId, CancellationToken cancellationToken)
    {
        var result = await _catalog.CommitImportAsync(importId, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (!result.IsSuccess || result.Value is null)
        {
            FlashError(result.IsNotFound
                ? "That preview has expired. Upload the file again."
                : result.ErrorMessage ?? "The import could not be saved.");
            return RedirectToAction(nameof(Import));
        }

        ImportResultDto done = result.Value;
        var parts = new List<string>();
        if (done.SkusCreated > 0) parts.Add(done.SkusCreated.Counted("SKU") + " added");
        if (done.SkusUpdated > 0) parts.Add(done.SkusUpdated.Counted("SKU") + " updated");
        if (done.ProductsCreated > 0) parts.Add(done.ProductsCreated.Counted("new product"));
        if (done.CategoriesCreated > 0) parts.Add(done.CategoriesCreated.Counted("new category", "new categories"));
        if (done.BrandsCreated > 0) parts.Add(done.BrandsCreated.Counted("new brand"));
        FlashSuccess($"Import saved: {string.Join(", ", parts)}.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Finds a category's name anywhere in the tree.
    /// </summary>
    /// <param name="nodes">Nodes to search.</param>
    /// <param name="id">Category id.</param>
    /// <returns>The name, or null.</returns>
    private static string? FindName(IEnumerable<CategoryNodeDto> nodes, Guid id)
    {
        foreach (CategoryNodeDto node in nodes)
        {
            if (node.Id == id)
            {
                return node.Name;
            }

            if (FindName(node.Children, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
