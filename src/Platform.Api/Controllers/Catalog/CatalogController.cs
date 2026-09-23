using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Catalog;
using Platform.Api.Services.Catalog.Import;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Common;

namespace Platform.Api.Controllers.Catalog;

/// <summary>
/// Catalogue browsing, unit conversion and bulk import: <c>/api/catalog</c>.
/// Internal catalogue — the customer storefront gets its own endpoints (P1).
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Catalog)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CatalogController : ControllerBase
{
    /// <summary>Content type of the Excel template.</summary>
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Largest import file accepted (10 MB).</summary>
    private const long MaxImportBytes = 10 * 1024 * 1024;

    private readonly ICatalogBrowseService _browse;
    private readonly ICatalogImportService _import;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="browse">Catalogue read operations.</param>
    /// <param name="import">Catalogue import.</param>
    public CatalogController(ICatalogBrowseService browse, ICatalogImportService import)
    {
        _browse = browse;
        _import = import;
    }

    /// <summary>
    /// Returns the full category tree.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with top-level categories and their descendants.</returns>
    [HttpGet("categories")]
    [RequiresCapability(Capabilities.CatalogView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryNodeDto>>> GetCategoryTree(CancellationToken cancellationToken) =>
        Ok(await _browse.GetCategoryTreeAsync(cancellationToken));

    /// <summary>
    /// Lists products under a category (including sub-categories), of a brand, and/or matching search words.
    /// </summary>
    /// <param name="request">Filters and paging.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with one page of products.</returns>
    [HttpGet("products")]
    [RequiresCapability(Capabilities.CatalogView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetProducts([FromQuery] CatalogBrowseRequest request, CancellationToken cancellationToken) =>
        Ok(await _browse.GetProductsAsync(request, cancellationToken));

    /// <summary>
    /// Returns a product with its breadcrumb and SKUs, each SKU with every unit it can be expressed in.
    /// </summary>
    /// <param name="id">Product id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the product, or 404.</returns>
    [HttpGet("products/{id:guid}")]
    [RequiresCapability(Capabilities.CatalogView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> GetProduct(Guid id, CancellationToken cancellationToken) =>
        Ok(await _browse.GetProductAsync(id, cancellationToken));

    /// <summary>
    /// Converts an amount of a SKU between units, e.g. 3 TONNE of cement to BAG (P7).
    /// </summary>
    /// <param name="id">SKU id.</param>
    /// <param name="request">Amount, from-unit and to-unit.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with both quantities, 400 when the units cannot be converted for this SKU, or 404.</returns>
    [HttpGet("skus/{id:guid}/convert")]
    [RequiresCapability(Capabilities.CatalogView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversionResultDto>> Convert(Guid id, [FromQuery] SkuConversionRequest request, CancellationToken cancellationToken) =>
        Ok(await _browse.ConvertAsync(id, new QuantityDto { Value = request.Value, Uom = request.From }, request.To, cancellationToken));

    /// <summary>
    /// Downloads the Excel import template: example rows, valid units and instructions.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the .xlsx file.</returns>
    [HttpGet("import/template")]
    [RequiresCapability(Capabilities.CatalogManage)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, ExcelContentType)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken) =>
        File(await _import.BuildTemplateAsync(cancellationToken), ExcelContentType, "catalogue-import-template.xlsx");

    /// <summary>
    /// Checks an Excel (.xlsx) or CSV file and reports what importing it would do. Saves nothing.
    /// </summary>
    /// <param name="file">The file to check.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the per-row preview and an import id, or 400 when the file cannot be read.</returns>
    [HttpPost("import/preview")]
    [RequiresCapability(Capabilities.CatalogManage)]
    [RequestSizeLimit(MaxImportBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportPreviewDto>> PreviewImport(IFormFile file, CancellationToken cancellationToken)
    {
        await using Stream content = file.OpenReadStream();
        return Ok(await _import.PreviewAsync(content, file.FileName, cancellationToken));
    }

    /// <summary>
    /// Saves a previewed import. Rows are re-checked against current data first.
    /// </summary>
    /// <param name="importId">Id returned by the preview.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with what changed, 404 when the preview expired, or 422 when rows no longer pass.</returns>
    [HttpPost("import/{importId:guid}/commit")]
    [RequiresCapability(Capabilities.CatalogManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ImportResultDto>> CommitImport(Guid importId, CancellationToken cancellationToken) =>
        Ok(await _import.CommitAsync(importId, cancellationToken));
}
