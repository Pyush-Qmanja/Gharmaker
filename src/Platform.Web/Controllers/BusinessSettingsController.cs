using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// The business details GST needs: legal name, GSTIN and the one registered
/// address the business invoices from. The store takes no orders until they are complete.
/// </summary>
public sealed class BusinessSettingsController : PlatformControllerBase
{
    private readonly ISalesApiClient _sales;
    private readonly IUserAccess _access;
    private readonly IValidator<UpdateBusinessSettingsRequest> _validator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="sales">Sales API client.</param>
    /// <param name="access">Current user's capabilities.</param>
    /// <param name="validator">Shared validator.</param>
    public BusinessSettingsController(ISalesApiClient sales, IUserAccess access, IValidator<UpdateBusinessSettingsRequest> validator)
    {
        _sales = sales;
        _access = access;
        _validator = validator;
    }

    /// <summary>
    /// Shows the business details.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _sales.GetBusinessSettingsAsync(cancellationToken);
        if (HandleAccess(settings) is { } denied)
        {
            return denied;
        }

        BusinessSettingsDto value = settings.Value ?? new BusinessSettingsDto();
        var form = new UpdateBusinessSettingsRequest
        {
            LegalName = value.LegalName ?? value.Name,
            Gstin = value.Gstin ?? string.Empty,
            Address = value.Address ?? new AddressDto(),
        };
        return View(new BusinessSettingsViewModel(form, value.IsComplete, await _access.CanAsync(Capabilities.SettingsManage)));
    }

    /// <summary>
    /// Saves the business details.
    /// </summary>
    /// <param name="form">Posted details.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page, saved or with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Index(UpdateBusinessSettingsRequest form, CancellationToken cancellationToken)
    {
        form.Gstin = (form.Gstin ?? string.Empty).Trim().ToUpperInvariant();
        if (await ValidateAsync(_validator, form, cancellationToken, prefix: "Form"))
        {
            var result = await _sales.UpdateBusinessSettingsAsync(form, cancellationToken);
            if (HandleAccess(result) is { } denied)
            {
                return denied;
            }

            if (result.IsSuccess)
            {
                FlashSuccess("Business details saved. GST is now worked out from this address.");
                return RedirectToAction(nameof(Index));
            }

            AddApiErrors(result, prefix: "Form");
        }

        return View(new BusinessSettingsViewModel(form, IsComplete: false, CanManage: true));
    }
}
