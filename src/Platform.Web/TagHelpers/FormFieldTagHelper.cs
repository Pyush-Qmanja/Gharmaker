using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Platform.Web.TagHelpers;

/// <summary>
/// Renders a complete form field — label, input and validation message — from
/// one tag: <c>&lt;form-field asp-for="Name" /&gt;</c>. Every form in the UI
/// uses this, so field markup and CSS classes are defined exactly once.
/// </summary>
/// <remarks>
/// The input type is inferred from the property type (text, number,
/// checkbox) and can be overridden with <c>type="url"</c>, <c>type="email"</c>, etc.
/// The label defaults to the property name split into words ("LogoUrl" becomes
/// "Logo Url") and can be overridden with <c>label="Logo URL"</c>.
/// </remarks>
[HtmlTargetElement("form-field", Attributes = ForAttributeName, TagStructure = TagStructure.WithoutEndTag)]
public sealed partial class FormFieldTagHelper : TagHelper
{
    private const string ForAttributeName = "asp-for";
    private readonly IHtmlGenerator _generator;

    /// <summary>
    /// Creates the tag helper.
    /// </summary>
    /// <param name="generator">MVC's HTML generator, so names, values and validation attributes match standard tag helpers.</param>
    public FormFieldTagHelper(IHtmlGenerator generator)
    {
        _generator = generator;
    }

    /// <summary>Property the field is bound to.</summary>
    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    /// <summary>Optional input type override, e.g. "url" or "email".</summary>
    [HtmlAttributeName("type")]
    public string? InputType { get; set; }

    /// <summary>Optional label text override.</summary>
    [HtmlAttributeName("label")]
    public string? Label { get; set; }

    /// <summary>Optional help text shown under the input.</summary>
    [HtmlAttributeName("hint")]
    public string? Hint { get; set; }

    /// <summary>Current view context, injected by Razor.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>
    /// Replaces the <c>form-field</c> tag with the wrapper, label, input,
    /// optional hint and validation message.
    /// </summary>
    /// <param name="context">Tag helper context.</param>
    /// <param name="output">Output to write.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        bool isCheckbox = For.ModelExplorer.ModelType == typeof(bool);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", isCheckbox ? "form-field form-field--check" : "form-field");

        TagBuilder label = _generator.GenerateLabel(
            ViewContext, For.ModelExplorer, For.Name, Label ?? DefaultLabel(), new { @class = "form-field__label" });

        TagBuilder input = isCheckbox
            ? _generator.GenerateCheckBox(ViewContext, For.ModelExplorer, For.Name, (bool?)For.Model, new { @class = "form-check-input" })
            : _generator.GenerateTextBox(ViewContext, For.ModelExplorer, For.Name, For.Model, format: null,
                new { @class = "form-control", type = InputType ?? DefaultInputType() });

        TagBuilder validation = _generator.GenerateValidationMessage(
            ViewContext, For.ModelExplorer, For.Name, message: null, tag: "span", new { @class = "form-field__error" });

        if (isCheckbox)
        {
            // Unchecked boxes post nothing, so a hidden "false" follows the box;
            // the model binder takes the first value, giving true or false.
            output.Content.AppendHtml(input)
                .AppendHtml(_generator.GenerateHiddenForCheckbox(ViewContext, For.ModelExplorer, For.Name))
                .AppendHtml(label);
        }
        else
        {
            output.Content.AppendHtml(label).AppendHtml(input);
        }

        if (!string.IsNullOrEmpty(Hint))
        {
            var hint = new TagBuilder("small");
            hint.AddCssClass("form-field__hint");
            hint.InnerHtml.Append(Hint);
            output.Content.AppendHtml(hint);
        }

        output.Content.AppendHtml(validation);
    }

    /// <summary>
    /// Picks the HTML input type from the property's CLR type.
    /// </summary>
    /// <returns>"number" for numeric types, otherwise "text".</returns>
    private string DefaultInputType()
    {
        Type type = Nullable.GetUnderlyingType(For.ModelExplorer.ModelType) ?? For.ModelExplorer.ModelType;
        return type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double)
            ? "number"
            : "text";
    }

    /// <summary>
    /// Uses the display name from metadata if set, otherwise splits the
    /// PascalCase property name into words.
    /// </summary>
    /// <returns>The label text.</returns>
    private string DefaultLabel() =>
        For.Metadata.DisplayName ?? PascalCaseBoundary().Replace(For.Metadata.PropertyName ?? For.Name, " $1");

    /// <summary>Matches an upper-case letter that starts a new word.</summary>
    /// <returns>The compiled regex.</returns>
    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
