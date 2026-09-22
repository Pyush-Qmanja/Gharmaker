using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Shared.Dtos.Common;

namespace Platform.Api.Controllers;

/// <summary>
/// Standard REST endpoints for one resource. A resource controller derives
/// from this, sets its <c>[Route]</c>, and gets list, get, create, update and
/// delete with consistent status codes and no repeated code.
/// </summary>
/// <remarks>
/// Authorisation: requires an authenticated caller. Capability + scope checks
/// (P6) are added in Phase 1 via <c>[RequiresCapability]</c> — never by role name.
/// </remarks>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public abstract class CrudControllerBase<TDto, TCreate, TUpdate> : ControllerBase
    where TDto : EntityDto
{
    /// <summary>Business operations for this resource.</summary>
    protected readonly ICrudService<TDto, TCreate, TUpdate> Service;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">Business operations for this resource.</param>
    protected CrudControllerBase(ICrudService<TDto, TCreate, TUpdate> service)
    {
        Service = service;
    }

    /// <summary>
    /// Lists one page of records.
    /// </summary>
    /// <param name="request">Page, page size and optional search text.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the page.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken) =>
        Ok(await Service.GetPagedAsync(request, cancellationToken));

    /// <summary>
    /// Gets one record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the record, or 404.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await Service.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Creates a record.
    /// </summary>
    /// <param name="request">Create request body.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>201 with the record and its location, 400 if invalid, or 409 on a duplicate.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TDto>> Create([FromBody] TCreate request, CancellationToken cancellationToken)
    {
        TDto created = await Service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="request">Update request body.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the record, 400 if invalid, 404, or 409 on a duplicate.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TDto>> Update(Guid id, [FromBody] TUpdate request, CancellationToken cancellationToken) =>
        Ok(await Service.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Deactivates a master record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>204, 404, or 422 when the resource cannot be deleted.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
