using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Common;
using Platform.Web.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// The audit log (P10): who changed what, when and from where. Read only.
/// </summary>
public sealed class AuditController : PlatformControllerBase
{
    /// <summary>Kinds of record that can be filtered on, as stored in the log.</summary>
    public static readonly IReadOnlyList<(string Entity, string Label)> Entities = new[]
    {
        ("StockDocument", "Stock documents"),
        ("Warehouse", "Warehouses"),
        ("User", "Users"),
        ("Role", "Roles"),
        ("Brand", "Brands"),
        ("Uom", "Units"),
        ("Product", "Products"),
        ("Sku", "SKUs"),
        ("Category", "Categories"),
    };

    private readonly IAuditApiClient _audit;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="audit">Audit API client.</param>
    public AuditController(IAuditApiClient audit)
    {
        _audit = audit;
    }

    /// <summary>
    /// Shows one page of the log, newest first, optionally for one kind of record or one record.
    /// </summary>
    /// <param name="request">Filters and paging.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The log page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AuditListRequest request, CancellationToken cancellationToken)
    {
        var result = await _audit.GetPagedAsync(request, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (!result.IsSuccess)
        {
            FlashError(result.ErrorMessage ?? "Could not load the audit log.");
        }

        ViewData[nameof(AuditListRequest.Entity)] = request.Entity;
        return View(new ListViewModel<AuditEntryDto>(
            "Audit log",
            result.Value ?? new PagedResult<AuditEntryDto> { Page = request.Page, PageSize = request.PageSize },
            search: null,
            new Dictionary<string, string?>
            {
                ["entity"] = request.Entity,
                ["entityId"] = request.EntityId?.ToString(),
            },
            itemName: "entry"));
    }

    /// <summary>
    /// Shows one entry with each field's old and new value.
    /// </summary>
    /// <param name="id">Entry id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The entry page, or 404.</returns>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _audit.GetAsync(id, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        return result.Value is { } entry ? View(entry) : NotFound();
    }
}
