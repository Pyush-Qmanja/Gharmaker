namespace Platform.Web.Models;

/// <summary>
/// What the shared create/edit page (<c>Views/Shared/CrudForm.cshtml</c>)
/// needs. The fields themselves come from the entity's <c>_Form</c> partial.
/// </summary>
public sealed class FormViewModel
{
    /// <summary>
    /// Creates the model.
    /// </summary>
    /// <param name="title">Page heading, e.g. "New brand".</param>
    /// <param name="form">Create or update request bound to the fields.</param>
    /// <param name="id">Record being edited; null when creating.</param>
    public FormViewModel(string title, object form, Guid? id = null)
    {
        Title = title;
        Form = form;
        Id = id;
    }

    /// <summary>Page heading.</summary>
    public string Title { get; }

    /// <summary>Create or update request; the model of the <c>_Form</c> partial.</summary>
    public object Form { get; }

    /// <summary>Record being edited; null when creating.</summary>
    public Guid? Id { get; }

    /// <summary>True when editing an existing record.</summary>
    public bool IsEdit => Id.HasValue;
}
