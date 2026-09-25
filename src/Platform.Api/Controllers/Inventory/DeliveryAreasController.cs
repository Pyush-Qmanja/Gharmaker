using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Inventory;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;

namespace Platform.Api.Controllers.Inventory;

/// <summary>
/// PIN codes each warehouse delivers to (warehouse-scoped).
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.DeliveryAreas)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class DeliveryAreasController : ControllerBase
{
    private readonly IDeliveryAreaService _areas;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="areas">Delivery area service.</param>
    public DeliveryAreasController(IDeliveryAreaService areas)
    {
        _areas = areas;
    }

    /// <summary>
    /// Lists active delivery areas.
    /// </summary>
    /// <param name="request">Warehouse, PIN code prefix and page.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>One page.</returns>
    [HttpGet]
    [RequiresCapability(Capabilities.DeliveryView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<DeliveryAreaDto>>> List([FromQuery] DeliveryAreaListRequest request, CancellationToken cancellationToken) =>
        Ok(await _areas.ListAsync(request, cancellationToken));

    /// <summary>
    /// Adds (or updates) PIN codes for one warehouse.
    /// </summary>
    /// <param name="request">Warehouse, PIN codes and lead time.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>How many were added and updated.</returns>
    [HttpPost]
    [RequiresCapability(Capabilities.DeliveryManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddDeliveryAreasResult>> Add([FromBody] AddDeliveryAreasRequest request, CancellationToken cancellationToken) =>
        Ok(await _areas.AddAsync(request, cancellationToken));

    /// <summary>
    /// Changes one area's lead time, or removes / restores it.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The area.</returns>
    [HttpPut("{id:guid}")]
    [RequiresCapability(Capabilities.DeliveryManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryAreaDto>> Update(Guid id, [FromBody] UpdateDeliveryAreaRequest request, CancellationToken cancellationToken) =>
        Ok(await _areas.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Stops delivering to a PIN code from a warehouse.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id:guid}")]
    [RequiresCapability(Capabilities.DeliveryManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _areas.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
