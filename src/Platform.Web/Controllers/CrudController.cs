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
    /// Shows one page of records.
    /// </summary>
    /// <param name="request">Page and search text from the query string.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The list page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var result = await Api.GetPagedAsync(request, cancellationToken);
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (!result.IsSuccess)
        {
            FlashError(result.ErrorMessage ?? $"Could not load {PluralName.ToLowerInvariant()}.");
        }

        var page = result.Value ?? new PagedResult<TDto> { Page = request.Page, PageSize = request.PageSize };
        return View(IndexView, new ListViewModel<TDto>(PluralName, page, request.Search));
    }

    /// <summary>
    /// Shows an empty create form.
    /// </summary>
    /// <returns>The form page.</returns>
    [HttpGet]
    public IActionResult Create() => View(FormView, new FormViewModel($"New {SingularName.ToLowerInvariant()}", new TCreate()));

    /// <summary>
    /// Validates and creates a record.
    /// </summary>
    /// <param name="form">Posted fields.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the list, or the form with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(TCreate form, CancellationToken cancellationToken)
    {
        var model = new FormViewModel($"New {SingularName.ToLowerInvariant()}", form);
        if (!await ValidateAsync(_createValidator, form, cancellationToken))
        {
            return View(FormView, model);
        }

        var result = await Api.CreateAsync(form, cancellationToken);
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (!result.IsSuccess)
        {
            AddApiErrors(result);
            return View(FormView, model);
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
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (!result.IsSuccess || result.Value is null)
        {
            return NotFound();
        }

        return View(FormView, new FormViewModel($"Edit {SingularName.ToLowerInvariant()}", ToUpdateRequest(result.Value), id));
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
        var model = new FormViewModel($"Edit {SingularName.ToLowerInvariant()}", form, id);
        if (!await ValidateAsync(_updateValidator, form, cancellationToken))
        {
            return View(FormView, model);
        }

        var result = await Api.UpdateAsync(id, form, cancellationToken);
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (!result.IsSuccess)
        {
            AddApiErrors(result);
            return View(FormView, model);
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
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
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
}
