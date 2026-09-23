using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;

namespace Platform.Api.Controllers;

/// <summary>
/// The audit log (P10), read only: <c>/api/audit</c>.
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Audit)]
[Produces("application/json")]
[RequiresCapability(Capabilities.AuditView)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="audit">Audit log reads.</param>
    public AuditController(IAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>
    /// Lists audit entries, newest first.
    /// </summary>
    /// <param name="request">Kind of record, one record, paging.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with one page.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> GetPaged([FromQuery] AuditListRequest request, CancellationToken cancellationToken) =>
        Ok(await _audit.GetPagedAsync(request, cancellationToken));

    /// <summary>
    /// Shows one entry with every changed field.
    /// </summary>
    /// <param name="id">Entry id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the entry, or 404.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditEntryDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _audit.GetAsync(id, cancellationToken));
}
