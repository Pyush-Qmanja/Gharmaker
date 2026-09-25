using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Entities.Pricing;
using Platform.Web.Common;
using Platform.Web.Extensions;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// Price lists (inherited list, create, edit, deactivate), and each list's
/// price grid with the page that sets one SKU's price. A new price is always
/// a new dated row (P8); the history stays visible under the form.
/// </summary>
public sealed class PriceListsController : CrudController<PriceListDto, CreatePriceListRequest, UpdatePriceListRequest>
{
    private readonly IPricingApiClient _pricing;
    private readonly ICatalogApiClient _catalog;
    private readonly ISalesApiClient _sales;
    private readonly IUserAccess _access;
    private readonly IValidator<SetSkuPriceRequest> _priceValidator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">Price list API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    /// <param name="pricing">Price and GST API client.</param>
    /// <param name="catalog">Catalogue API client, for the SKU being priced.</param>
    /// <param name="sales">Sales API client, for contract customers.</param>
    /// <param name="access">Current user's capabilities.</param>
    /// <param name="priceValidator">Shared price validator.</param>
    public PriceListsController(
        ICrudApiClient<PriceListDto, CreatePriceListRequest, UpdatePriceListRequest> api,
        IValidator<CreatePriceListRequest> createValidator,
        IValidator<UpdatePriceListRequest> updateValidator,
        IPricingApiClient pricing,
        ICatalogApiClient catalog,
        ISalesApiClient sales,
        IUserAccess access,
        IValidator<SetSkuPriceRequest> priceValidator)
        : base(api, createValidator, updateValidator)
    {
        _pricing = pricing;
        _catalog = catalog;
        _sales = sales;
        _access = access;
        _priceValidator = priceValidator;
    }

    /// <inheritdoc />
    protected override string SingularName => "Price list";

    /// <inheritdoc />
    protected override string PluralName => "Price lists";

    /// <inheritdoc />
    protected override UpdatePriceListRequest ToUpdateRequest(PriceListDto dto) => new()
    {
        Name = dto.Name,
        Code = dto.Code,
        Type = dto.Type,
        CustomerId = dto.CustomerId,
        Remarks = dto.Remarks,
        IsActive = dto.IsActive,
    };

    /// <summary>
    /// Loads customers for the contract-list drop-down.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>A task that completes when ViewData is ready.</returns>
    protected override async Task PrepareViewAsync(CancellationToken cancellationToken)
    {
        var customers = await _sales.GetCustomersAsync(new PagedRequest { PageSize = Paging.MaxPageSize }, cancellationToken);
        ViewData[ViewDataKeys.UserOptions] = (customers.Value?.Items ?? new List<Platform.Shared.Dtos.Sales.CustomerDto>())
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem($"{c.Name} ({c.Email})", c.Id.ToString()))
            .ToList();
    }

    /// <summary>
    /// Shows a list's prices, product by product.
    /// </summary>
    /// <param name="id">Price list.</param>
    /// <param name="search">Product search.</param>
    /// <param name="page">Page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The price grid.</returns>
    [HttpGet]
    public async Task<IActionResult> Prices(Guid id, string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var list = await Api.GetByIdAsync(id, cancellationToken);
        if (HandleAccess(list) is { } denied)
        {
            return denied;
        }

        if (list.Value is null)
        {
            return NotFound();
        }

        var request = new PriceGridRequest { PriceListId = id, Search = search, Page = Math.Max(page, 1), PageSize = 10 };
        var grid = await _pricing.GetGridAsync(request, cancellationToken);
        if (!grid.IsSuccess)
        {
            FlashError(grid.ErrorMessage ?? "Could not load prices.");
        }

        return View(new PriceGridViewModel(
            list.Value,
            new ListViewModel<PriceGridProductDto>(
                "Products",
                grid.Value ?? new PagedResult<PriceGridProductDto> { Page = request.Page, PageSize = request.PageSize },
                search,
                new Dictionary<string, string?> { ["id"] = id.ToString() },
                itemName: "product",
                pageAction: nameof(Prices)),
            await _access.CanAsync(Capabilities.PricingManage)));
    }

    /// <summary>
    /// Shows the form to set a SKU's price, with its history.
    /// </summary>
    /// <param name="id">Price list.</param>
    /// <param name="productId">Product of the SKU.</param>
    /// <param name="skuId">SKU.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form.</returns>
    [HttpGet]
    public async Task<IActionResult> SetPrice(Guid id, Guid productId, Guid skuId, CancellationToken cancellationToken)
    {
        var history = await _pricing.GetHistoryAsync(id, skuId, cancellationToken);
        var current = history.Value?.FirstOrDefault(p => p.IsCurrent) ?? history.Value?.FirstOrDefault();
        var form = new SetPriceForm
        {
            Uom = current?.Uom ?? string.Empty,
            Slabs = current?.Slabs.Select(s => new PriceSlabDto { MinQuantity = s.MinQuantity, UnitPrice = s.UnitPrice }).ToList() ?? new(),
        };
        return await SetPriceViewAsync(id, productId, skuId, form.WithRows(), cancellationToken);
    }

    /// <summary>
    /// Saves a new price from now or a later date.
    /// </summary>
    /// <param name="id">Price list.</param>
    /// <param name="productId">Product of the SKU.</param>
    /// <param name="skuId">SKU.</param>
    /// <param name="form">Posted form.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the grid, or the form with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> SetPrice(Guid id, Guid productId, Guid skuId, SetPriceForm form, CancellationToken cancellationToken)
    {
        var request = new SetSkuPriceRequest
        {
            PriceListId = id,
            SkuId = skuId,
            Uom = form.Uom,
            Slabs = form.Slabs,
            ValidFrom = form.ValidFromIst?.FromIstToUtc(),
            Remarks = form.Remarks,
        };
        request.Normalise();
        ModelState.Clear();
        if (await ValidateAsync(_priceValidator, request, cancellationToken))
        {
            var result = await _pricing.SetPriceAsync(request, cancellationToken);
            if (HandleAccess(result) is { } denied)
            {
                return denied;
            }

            if (result.IsSuccess)
            {
                FlashSuccess(result.Value!.IsCurrent ? "Price saved. It applies from now." : $"Price saved. It applies from {result.Value.ValidFrom.ToIstString()}.");
                return RedirectToAction(nameof(Prices), new { id });
            }

            AddApiErrors(result);
        }

        form.Slabs = request.Slabs;
        return await SetPriceViewAsync(id, productId, skuId, form.WithRows(), cancellationToken);
    }

    /// <summary>
    /// Builds the set-price page.
    /// </summary>
    /// <param name="id">Price list.</param>
    /// <param name="productId">Product.</param>
    /// <param name="skuId">SKU.</param>
    /// <param name="form">Form values.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page, or 404.</returns>
    private async Task<IActionResult> SetPriceViewAsync(Guid id, Guid productId, Guid skuId, SetPriceForm form, CancellationToken cancellationToken)
    {
        var list = await Api.GetByIdAsync(id, cancellationToken);
        if (HandleAccess(list) is { } denied)
        {
            return denied;
        }

        var product = await _catalog.GetProductAsync(productId, cancellationToken);
        var sku = product.Value?.Skus.FirstOrDefault(s => s.Id == skuId);
        if (list.Value is null || sku is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(form.Uom))
        {
            form.Uom = sku.BaseUom;
        }

        var history = await _pricing.GetHistoryAsync(id, skuId, cancellationToken);
        return View(nameof(SetPrice), new SetPriceViewModel(list.Value, product.Value!, sku, form, history.Value ?? new List<SkuPriceDto>()));
    }
}
