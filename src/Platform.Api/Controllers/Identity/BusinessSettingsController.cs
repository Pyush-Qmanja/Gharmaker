using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Identity;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;

namespace Platform.Api.Controllers.Identity;

/// <summary>
/// Business details used for GST (legal name, GSTIN, registered address).
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.BusinessSettings)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class BusinessSettingsController : ControllerBase
{
    private readonly IBusinessSettingsService _settings;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="settings">Business settings service.</param>
    public BusinessSettingsController(IBusinessSettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Reads the business details.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The details.</returns>
    [HttpGet]
    [RequiresCapability(Capabilities.SettingsView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<BusinessSettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _settings.GetAsync(cancellationToken));

    /// <summary>
    /// Saves the business details.
    /// </summary>
    /// <param name="request">New details.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The saved details.</returns>
    [HttpPut]
    [RequiresCapability(Capabilities.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessSettingsDto>> Update([FromBody] UpdateBusinessSettingsRequest request, CancellationToken cancellationToken) =>
        Ok(await _settings.UpdateAsync(request, cancellationToken));
}
