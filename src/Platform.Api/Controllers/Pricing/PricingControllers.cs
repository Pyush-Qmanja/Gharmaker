using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
using Platform.Api.Services.Pricing;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Pricing;

namespace Platform.Api.Controllers.Pricing;

/// <summary>
/// Price lists: retail, tier and contract (list, get, create, update, deactivate).
/// </summary>
[Route(ApiRoutes.PriceLists)]
[CrudCapabilities(Capabilities.PricingView, Capabilities.PricingManage)]
public sealed class PriceListsController : CrudControllerBase<PriceListDto, CreatePriceListRequest, UpdatePriceListRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">Price list service.</param>
    public PriceListsController(ICrudService<PriceListDto, CreatePriceListRequest, UpdatePriceListRequest> service)
        : base(service)
    {
    }
}

/// <summary>
/// Prices of SKUs in a price list. Setting a price inserts a dated row (P8).
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Prices)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class PricesController : ControllerBase
{
    private readonly IPriceService _prices;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="prices">Price service.</param>
    public PricesController(IPriceService prices)
    {
        _prices = prices;
    }

    /// <summary>
    /// One page of products with each SKU's current and upcoming price in a list.
    /// </summary>
    /// <param name="request">List, category, search and page.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [RequiresCapability(Capabilities.PricingView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<PriceGridProductDto>>> GetGrid([FromQuery] PriceGridRequest request, CancellationToken cancellationToken) =>
        Ok(await _prices.GetGridAsync(request, cancellationToken));

    /// <summary>
    /// Every price a SKU has had in a list, newest first.
    /// </summary>
    /// <param name="priceListId">List.</param>
    /// <param name="skuId">SKU.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The rows.</returns>
    [HttpGet("history")]
    [RequiresCapability(Capabilities.PricingView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SkuPriceDto>>> GetHistory([FromQuery] Guid priceListId, [FromQuery] Guid skuId, CancellationToken cancellationToken) =>
        Ok(await _prices.GetHistoryAsync(priceListId, skuId, cancellationToken));

    /// <summary>
    /// Sets a SKU's price in a list from now or a later date.
    /// </summary>
    /// <param name="request">Price.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The new price row.</returns>
    [HttpPost]
    [RequiresCapability(Capabilities.PricingManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SkuPriceDto>> Set([FromBody] SetSkuPriceRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await _prices.SetAsync(request, cancellationToken));
}

/// <summary>
/// GST rates by HSN code. A new rate is a dated row (P8).
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.TaxRates)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class TaxRatesController : ControllerBase
{
    private readonly ITaxRateService _rates;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="rates">Tax rate service.</param>
    public TaxRatesController(ITaxRateService rates)
    {
        _rates = rates;
    }

    /// <summary>
    /// Rates in force now, or every rate of one HSN code.
    /// </summary>
    /// <param name="request">Filter and page.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>One page of rates.</returns>
    [HttpGet]
    [RequiresCapability(Capabilities.PricingView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TaxRateDto>>> List([FromQuery] TaxRateListRequest request, CancellationToken cancellationToken) =>
        Ok(await _rates.ListAsync(request, cancellationToken));

    /// <summary>
    /// HSN codes of active products that have no rate (those products cannot be sold).
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The HSN codes.</returns>
    [HttpGet("missing")]
    [RequiresCapability(Capabilities.PricingView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MissingTaxRateDto>>> Missing(CancellationToken cancellationToken) =>
        Ok(await _rates.GetMissingAsync(cancellationToken));

    /// <summary>
    /// Adds a rate for an HSN code from now or a later date.
    /// </summary>
    /// <param name="request">Rate.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The new rate.</returns>
    [HttpPost]
    [RequiresCapability(Capabilities.PricingManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaxRateDto>> Create([FromBody] CreateTaxRateRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await _rates.CreateAsync(request, cancellationToken));
}
