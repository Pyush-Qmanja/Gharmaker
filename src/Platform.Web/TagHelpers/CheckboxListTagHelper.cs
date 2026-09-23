using System.Collections;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Platform.Web.TagHelpers;

/// <summary>
/// Renders a list property (<c>List&lt;string&gt;</c>, <c>List&lt;Guid&gt;</c>...) as
/// a set of checkboxes, grouped by <see cref="SelectListItem.Group"/>, with its
/// label and validation message:
/// <c>&lt;checkbox-list asp-for="Capabilities" items="options" /&gt;</c>.
/// Every multi-select in the UI uses this, so its markup exists once.
/// Selected values that are not among the options are preserved as hidden
/// fields, so an editor who cannot see an option never removes it by saving.
/// </summary>
[HtmlTargetElement("checkbox-list", Attributes = "asp-for,items", TagStructure = TagStructure.WithoutEndTag)]
public sealed class CheckboxListTagHelper : TagHelper
{
    private readonly IHtmlGenerator _generator;

    /// <summary>
    /// Creates the tag helper.
    /// </summary>
    /// <param name="generator">MVC's HTML generator, for the label and validation message.</param>
    public CheckboxListTagHelper(IHtmlGenerator generator)
    {
        _generator = generator;
    }

    /// <summary>List property the checked values bind to.</summary>
    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = default!;

    /// <summary>Options to render; <see cref="SelectListItem.Value"/> is posted when checked.</summary>
    [HtmlAttributeName("items")]
    public IEnumerable<SelectListItem> Items { get; set; } = Array.Empty<SelectListItem>();

    /// <summary>Optional label text override.</summary>
    [HtmlAttributeName("label")]
    public string? Label { get; set; }

    /// <summary>Text shown when there are no options.</summary>
    [HtmlAttributeName("empty-text")]
    public string EmptyText { get; set; } = "Nothing to choose from.";

    /// <summary>Current view context, injected by Razor.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>
    /// Writes a fieldset with a legend, one checkbox per option (grouped), and the validation message.
    /// </summary>
    /// <param name="context">Tag helper context.</param>
    /// <param name="output">Output to write.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        HashSet<string> selected = SelectedValues(name);

        output.TagName = "fieldset";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "form-field checkbox-list");

        var legend = new TagBuilder("legend");
        legend.AddCssClass("form-field__label");
        legend.InnerHtml.Append(Label ?? For.Metadata.DisplayName ?? For.Metadata.PropertyName ?? For.Name);
        output.Content.AppendHtml(legend);

        List<SelectListItem> items = Items.ToList();
        if (items.Count == 0)
        {
            var empty = new TagBuilder("p");
            empty.AddCssClass("form-field__hint");
            empty.InnerHtml.Append(EmptyText);
            output.Content.AppendHtml(empty);
        }

        int index = 0;
        foreach (var group in items.GroupBy(i => i.Group?.Name))
        {
            var groupBox = new TagBuilder("div");
            groupBox.AddCssClass("checkbox-list__group");
            if (group.Key is not null)
            {
                var heading = new TagBuilder("span");
                heading.AddCssClass("checkbox-list__group-title");
                heading.InnerHtml.Append(group.Key);
                groupBox.InnerHtml.AppendHtml(heading);
            }

            foreach (SelectListItem item in group)
            {
                groupBox.InnerHtml.AppendHtml(Option(name, $"{TagBuilder.CreateSanitizedId(name, "_")}_{index++}", item, selected.Contains(item.Value)));
            }

            output.Content.AppendHtml(groupBox);
        }

        // Values already selected but not offered (e.g. a scope the editor cannot
        // see) are kept as hidden fields, so saving never silently removes them.
        var offered = items.Select(i => i.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string kept in selected.Where(v => !offered.Contains(v)))
        {
            var hidden = new TagBuilder("input") { TagRenderMode = TagRenderMode.SelfClosing };
            hidden.Attributes["type"] = "hidden";
            hidden.Attributes["name"] = name;
            hidden.Attributes["value"] = kept;
            output.Content.AppendHtml(hidden);
        }

        output.Content.AppendHtml(_generator.GenerateValidationMessage(
            ViewContext, For.ModelExplorer, For.Name, message: null, tag: "span", new { @class = "form-field__error" }));
    }

    /// <summary>
    /// Builds one labelled checkbox.
    /// </summary>
    /// <param name="name">Posted field name.</param>
    /// <param name="id">Unique element id.</param>
    /// <param name="item">Option.</param>
    /// <param name="isChecked">Whether it starts checked.</param>
    /// <returns>The option markup.</returns>
    private static TagBuilder Option(string name, string id, SelectListItem item, bool isChecked)
    {
        var input = new TagBuilder("input") { TagRenderMode = TagRenderMode.SelfClosing };
        input.AddCssClass("form-check-input");
        input.Attributes["type"] = "checkbox";
        input.Attributes["id"] = id;
        input.Attributes["name"] = name;
        input.Attributes["value"] = item.Value;
        if (isChecked)
        {
            input.Attributes["checked"] = "checked";
        }

        var label = new TagBuilder("label");
        label.AddCssClass("checkbox-list__label");
        label.Attributes["for"] = id;
        label.InnerHtml.Append(item.Text);

        var row = new TagBuilder("div");
        row.AddCssClass("checkbox-list__option");
        row.InnerHtml.AppendHtml(input);
        row.InnerHtml.AppendHtml(label);
        return row;
    }

    /// <summary>
    /// Values to show as checked: what was posted (after a failed save) or else the model's list.
    /// </summary>
    /// <param name="name">Posted field name.</param>
    /// <returns>Checked values as strings.</returns>
    private HashSet<string> SelectedValues(string name)
    {
        if (ViewContext.ViewData.ModelState.TryGetValue(name, out var entry) && entry.AttemptedValue is not null)
        {
            return entry.AttemptedValue.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return For.Model is IEnumerable values and not string
            ? values.Cast<object?>().Select(v => v?.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();
    }
}
