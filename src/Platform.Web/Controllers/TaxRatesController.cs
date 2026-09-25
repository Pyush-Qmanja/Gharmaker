using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Pricing;
using Platform.Web.Extensions;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// GST rates by HSN code: the rates in force, the HSN codes still missing a
/// rate (their products cannot be sold), each code's history, and adding a rate.
/// </summary>
public sealed class TaxRatesController : PlatformControllerBase
{
    private readonly IPricingApiClient _pricing;
    private readonly IUserAccess _access;
    private readonly IValidator<CreateTaxRateRequest> _validator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="pricing">Price and GST API client.</param>
    /// <param name="access">Current user's capabilities.</param>
    /// <param name="validator">Shared rate validator.</param>
    public TaxRatesController(IPricingApiClient pricing, IUserAccess access, IValidator<CreateTaxRateRequest> validator)
    {
        _pricing = pricing;
        _access = access;
        _validator = validator;
    }

    /// <summary>
    /// Shows the rates in force, or one HSN code's history.
    /// </summary>
    /// <param name="request">HSN code, search and page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TaxRateListRequest request, CancellationToken cancellationToken)
    {
        var rates = await _pricing.GetTaxRatesAsync(request, cancellationToken);
        if (HandleAccess(rates) is { } denied)
        {
            return denied;
        }

        var missing = string.IsNullOrEmpty(request.HsnCode)
            ? (await _pricing.GetMissingTaxRatesAsync(cancellationToken)).Value ?? new List<MissingTaxRateDto>()
            : new List<MissingTaxRateDto>();
        return View(new TaxRatesViewModel(
            new ListViewModel<TaxRateDto>(
                "GST rates",
                rates.Value ?? new PagedResult<TaxRateDto> { Page = request.Page, PageSize = request.PageSize },
                request.Search,
                new Dictionary<string, string?> { ["hsnCode"] = request.HsnCode },
                itemName: "rate"),
            missing,
            request.HsnCode,
            await _access.CanAsync(Capabilities.PricingManage)));
    }

    /// <summary>
    /// Shows the form for a new rate.
    /// </summary>
    /// <param name="hsnCode">HSN code to fill in, if any.</param>
    /// <returns>The form.</returns>
    [HttpGet]
    public IActionResult Create(string? hsnCode) => View(new TaxRateForm { HsnCode = hsnCode ?? string.Empty });

    /// <summary>
    /// Saves a new rate.
    /// </summary>
    /// <param name="form">Posted form.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the list, or the form with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(TaxRateForm form, CancellationToken cancellationToken)
    {
        var request = new CreateTaxRateRequest
        {
            HsnCode = (form.HsnCode ?? string.Empty).Trim(),
            RatePercent = form.RatePercent,
            CessPercent = form.CessPercent,
            ValidFrom = form.ValidFromIst?.FromIstToUtc(),
            Remarks = form.Remarks,
        };
        if (!await ValidateAsync(_validator, request, cancellationToken))
        {
            return View(form);
        }

        var result = await _pricing.CreateTaxRateAsync(request, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (!result.IsSuccess)
        {
            AddApiErrors(result);
            return View(form);
        }

        FlashSuccess($"GST {result.Value!.RatePercent.ToPercent()} set for HSN {result.Value.HsnCode}.");
        return RedirectToAction(nameof(Index));
    }
}
