using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;
using Platform.Web.Common;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// PIN codes each warehouse delivers to: list and filter, add many at once
/// (a pasted list), change the lead time, remove. Warehouse-scoped by the API.
/// </summary>
public sealed class DeliveryAreasController : PlatformControllerBase
{
    private readonly IDeliveryAreaApiClient _areas;
    private readonly IStockApiClient _stock;
    private readonly IUserAccess _access;
    private readonly IValidator<AddDeliveryAreasRequest> _addValidator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="areas">Delivery area API client.</param>
    /// <param name="stock">Stock API client, for the warehouse list.</param>
    /// <param name="access">Current user's capabilities.</param>
    /// <param name="addValidator">Shared validator for adding PIN codes.</param>
    public DeliveryAreasController(
        IDeliveryAreaApiClient areas, IStockApiClient stock, IUserAccess access, IValidator<AddDeliveryAreasRequest> addValidator)
    {
        _areas = areas;
        _stock = stock;
        _access = access;
        _addValidator = addValidator;
    }

    /// <summary>
    /// Lists delivery areas.
    /// </summary>
    /// <param name="request">Warehouse, PIN code prefix and page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DeliveryAreaListRequest request, CancellationToken cancellationToken)
    {
        var areas = await _areas.ListAsync(request, cancellationToken);
        if (HandleAccess(areas) is { } denied)
        {
            return denied;
        }

        return View(new DeliveryAreasViewModel(
            new ListViewModel<DeliveryAreaDto>(
                "Delivery areas",
                areas.Value ?? new PagedResult<DeliveryAreaDto> { Page = request.Page, PageSize = request.PageSize },
                request.Search,
                new Dictionary<string, string?> { ["warehouseId"] = request.WarehouseId?.ToString() },
                itemName: "PIN code"),
            await WarehouseOptionsAsync(request.WarehouseId, cancellationToken),
            request.WarehouseId,
            await _access.CanAsync(Capabilities.DeliveryManage)));
    }

    /// <summary>
    /// Shows the form to add PIN codes.
    /// </summary>
    /// <param name="warehouseId">Warehouse to preselect.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form.</returns>
    [HttpGet]
    public async Task<IActionResult> Add(Guid? warehouseId, CancellationToken cancellationToken)
    {
        IReadOnlyList<SelectListItem> options = await WarehouseOptionsAsync(warehouseId, cancellationToken);
        if (options.Count == 0)
        {
            // No warehouse to deliver from yet: a setup step, not a lack of access.
            return View("NoWarehouse");
        }

        ViewData[ViewDataKeys.WarehouseOptions] = options;
        return View(new AddDeliveryAreasForm { WarehouseId = warehouseId ?? Guid.Empty });
    }

    /// <summary>
    /// Adds PIN codes to a warehouse.
    /// </summary>
    /// <param name="form">Posted form.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the list, or the form with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Add(AddDeliveryAreasForm form, CancellationToken cancellationToken)
    {
        var request = new AddDeliveryAreasRequest { WarehouseId = form.WarehouseId, Pincodes = new List<string> { form.Pincodes }, LeadTimeDays = form.LeadTimeDays };
        request.Normalise();
        if (await ValidateAsync(_addValidator, request, cancellationToken))
        {
            var result = await _areas.AddAsync(request, cancellationToken);
            if (HandleAccess(result) is { } denied)
            {
                return denied;
            }

            if (result.IsSuccess)
            {
                FlashSuccess($"{result.Value!.Added} PIN codes added, {result.Value.Updated} updated.");
                return RedirectToAction(nameof(Index), new { warehouseId = form.WarehouseId });
            }

            AddApiErrors(result);
        }

        // Errors on single PIN codes (Pincodes[3]) are shown against the text box.
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Pincodes[", StringComparison.Ordinal)).ToList())
        {
            foreach (var error in ModelState[key]!.Errors)
            {
                ModelState.AddModelError(nameof(AddDeliveryAreasForm.Pincodes), error.ErrorMessage);
            }

            ModelState.Remove(key);
        }

        ViewData[ViewDataKeys.WarehouseOptions] = await WarehouseOptionsAsync(form.WarehouseId, cancellationToken);
        return View(form);
    }

    /// <summary>
    /// Shows one area to change its lead time or status.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="warehouseId">Warehouse the area belongs to (for the lookup).</param>
    /// <param name="pincode">Its PIN code (for the lookup).</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form.</returns>
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, Guid warehouseId, string pincode, CancellationToken cancellationToken)
    {
        var found = await _areas.ListAsync(new DeliveryAreaListRequest { WarehouseId = warehouseId, Search = pincode }, cancellationToken);
        if (HandleAccess(found) is { } denied)
        {
            return denied;
        }

        DeliveryAreaDto? area = found.Value?.Items.FirstOrDefault(a => a.Id == id);
        return area is null
            ? NotFound()
            : View(new EditDeliveryAreaViewModel(area, new UpdateDeliveryAreaRequest { LeadTimeDays = area.LeadTimeDays, IsActive = area.IsActive }));
    }

    /// <summary>
    /// Saves an area's lead time and status.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="form">Posted values.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the list.</returns>
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, UpdateDeliveryAreaRequest form, CancellationToken cancellationToken)
    {
        var result = await _areas.UpdateAsync(id, form, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.IsSuccess)
        {
            FlashSuccess($"PIN code {result.Value!.Pincode} saved.");
            return RedirectToAction(nameof(Index), new { warehouseId = result.Value.WarehouseId });
        }

        FlashError(result.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? result.ErrorMessage ?? "Could not save.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Stops delivering to a PIN code from a warehouse.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="warehouseId">Warehouse, to return to its list.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the list.</returns>
    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, Guid? warehouseId, CancellationToken cancellationToken)
    {
        var result = await _areas.DeleteAsync(id, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.IsSuccess)
        {
            FlashSuccess("PIN code removed.");
        }
        else
        {
            FlashError(result.ErrorMessage ?? "Could not remove the PIN code.");
        }

        return RedirectToAction(nameof(Index), new { warehouseId });
    }

    /// <summary>
    /// Lists the warehouses the user can see, for filters and forms.
    /// </summary>
    /// <param name="selected">Warehouse to mark selected.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The options.</returns>
    private async Task<IReadOnlyList<SelectListItem>> WarehouseOptionsAsync(Guid? selected, CancellationToken cancellationToken) =>
        ((await _stock.GetWarehousesAsync(cancellationToken)).Value ?? new List<StockWarehouseDto>())
            .Select(w => new SelectListItem($"{w.Code} — {w.Name}", w.Id.ToString(), w.Id == selected))
            .ToList();
}
