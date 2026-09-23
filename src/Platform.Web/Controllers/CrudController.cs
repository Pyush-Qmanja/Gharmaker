using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Common;
using Platform.Web.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// List, create, edit and deactivate screens for one resource. A resource
/// screen derives from this, names itself, and supplies two partials
/// (<c>_Table</c> and <c>_Form</c>) in its view folder. The page layout comes
/// from the shared <c>CrudIndex</c> and <c>CrudForm</c> views.
/// </summary>
/// <remarks>
/// Permissions are enforced by the API. When it answers 403 this controller
/// shows the shared "not allowed" page; when it answers 401 it sends the user
/// to sign in again.
/// </remarks>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request.</typeparam>
/// <typeparam name="TUpdate">Update request.</typeparam>
public abstract class CrudController<TDto, TCreate, TUpdate> : PlatformControllerBase
    where TDto : EntityDto
    where TCreate : class, new()
    where TUpdate : class
{
    /// <summary>Shared list view; an entity may override it with its own <c>CrudIndex.cshtml</c>.</summary>
    protected const string IndexView = "CrudIndex";

    /// <summary>Shared create/edit view; an entity may override it with its own <c>CrudForm.cshtml</c>.</summary>
    protected const string FormView = "CrudForm";

    /// <summary>API client for this resource.</summary>
    protected readonly ICrudApiClient<TDto, TCreate, TUpdate> Api;

    private readonly IValidator<TCreate> _createValidator;
    private readonly IValidator<TUpdate> _updateValidator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">API client for this resource.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    protected CrudController(
        ICrudApiClient<TDto, TCreate, TUpdate> api,
        IValidator<TCreate> createValidator,
        IValidator<TUpdate> updateValidator)
    {
        Api = api;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Singular display name, e.g. "Brand".</summary>
    protected abstract string SingularName { get; }

    /// <summary>Plural display name, e.g. "Brands".</summary>
    protected abstract string PluralName { get; }

    /// <summary>
    /// Builds the edit form model from a loaded record.
    /// </summary>
    /// <param name="dto">Record from the API.</param>
    /// <returns>An update request pre-filled with the current values.</returns>
    protected abstract TUpdate ToUpdateRequest(TDto dto);

    /// <summary>
    /// Loads lookup data (e.g. role options) into ViewData before a list or
    /// form is rendered. Default: nothing to load.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>A task that completes when ViewData is ready.</returns>
    protected virtual Task PrepareViewAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Shows one page of records.
    /// </summary>
    /// <param name="request">Page and search text from the query string.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The list page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var result = await Api.GetPagedAsync(request, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (!result.IsSuccess)
        {
            FlashError(result.ErrorMessage ?? $"Could not load {PluralName.ToLowerInvariant()}.");
        }

        var page = result.Value ?? new PagedResult<TDto> { Page = request.Page, PageSize = request.PageSize };
        await PrepareViewAsync(cancellationToken);
        return View(IndexView, new ListViewModel<TDto>(PluralName, page, request.Search, itemName: SingularName));
    }

    /// <summary>
    /// Shows an empty create form.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form page.</returns>
    [HttpGet]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        FormAsync(new FormViewModel($"New {SingularName.ToLowerInvariant()}", new TCreate()), cancellationToken);

    /// <summary>
    /// Validates and creates a record.
    /// </summary>
    /// <param name="form">Posted fields.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the list, or the form with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(TCreate form, CancellationToken cancellationToken)
    {
        (form as INormalisable)?.Normalise();
        var model = new FormViewModel($"New {SingularName.ToLowerInvariant()}", form);
        if (!await ValidateAsync(_createValidator, form, cancellationToken))
        {
            return await FormAsync(model, cancellationToken);
        }

        var result = await Api.CreateAsync(form, cancellationToken);
        if (HandleWriteAccess(result) is { } denied)
        {
            return denied;
        }

        if (!result.IsSuccess)
        {
            AddApiErrors(result);
            return await FormAsync(model, cancellationToken);
        }

        FlashSuccess($"{SingularName} created.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the edit form for a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form page, or 404.</returns>
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await Api.GetByIdAsync(id, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (!result.IsSuccess || result.Value is null)
        {
            return NotFound();
        }

        return await FormAsync(new FormViewModel($"Edit {SingularName.ToLowerInvariant()}", ToUpdateRequest(result.Value), id), cancellationToken);
    }

    /// <summary>
    /// Validates and saves changes to a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="form">Posted fields.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the list, the form with errors, or 404.</returns>
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, TUpdate form, CancellationToken cancellationToken)
    {
        (form as INormalisable)?.Normalise();
        var model = new FormViewModel($"Edit {SingularName.ToLowerInvariant()}", form, id);
        if (!await ValidateAsync(_updateValidator, form, cancellationToken))
        {
            return await FormAsync(model, cancellationToken);
        }

        var result = await Api.UpdateAsync(id, form, cancellationToken);
        if (HandleWriteAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (!result.IsSuccess)
        {
            AddApiErrors(result);
            return await FormAsync(model, cancellationToken);
        }

        FlashSuccess($"{SingularName} saved.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deactivates a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the list with a message.</returns>
    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Api.DeleteAsync(id, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.IsSuccess)
        {
            FlashSuccess($"{SingularName} deactivated.");
        }
        else
        {
            FlashError(result.ErrorMessage ?? $"Could not deactivate the {SingularName.ToLowerInvariant()}.");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Like <see cref="PlatformControllerBase.HandleAccess"/>, but for a save the
    /// user was allowed to start: a 403 whose reason the API explained (e.g.
    /// "you can only give access you hold") goes back onto the form instead of
    /// replacing it with the "not allowed" page.
    /// </summary>
    /// <param name="result">Result of a create or update.</param>
    /// <returns>A redirect to sign-in, the "not allowed" page, or null to carry on (the caller shows the API's errors on the form).</returns>
    private IActionResult? HandleWriteAccess(ApiResult result) =>
        result.IsForbidden && !string.IsNullOrEmpty(result.ErrorMessage) ? null : HandleAccess(result);

    /// <summary>
    /// Loads lookups and renders the shared form page.
    /// </summary>
    /// <param name="model">Form page model.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form view.</returns>
    private async Task<IActionResult> FormAsync(FormViewModel model, CancellationToken cancellationToken)
    {
        await PrepareViewAsync(cancellationToken);
        return View(FormView, model);
    }
}
