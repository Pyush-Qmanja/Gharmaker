using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Platform.Web.Extensions;

namespace Platform.Web.TagHelpers;

/// <summary>
/// Renders a complete form field — label, input and validation message — from
/// one tag: <c>&lt;form-field asp-for="Name" /&gt;</c>. Every form in the UI
/// uses this, so field markup and CSS classes are defined exactly once.
/// </summary>
/// <remarks>
/// The control is inferred from the property type (text, number, checkbox,
/// drop-down for enums) and can be overridden with <c>type="url"</c>, <c>type="email"</c>,
/// or turned into a drop-down with <c>items="..."</c>.
/// The label defaults to the property name split into words ("LogoUrl" becomes
/// "Logo Url") and can be overridden with <c>label="Logo URL"</c>.
/// </remarks>
[HtmlTargetElement("form-field", Attributes = ForAttributeName, TagStructure = TagStructure.WithoutEndTag)]
public sealed class FormFieldTagHelper : TagHelper
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

    /// <summary>
    /// Options for a drop-down. When set (or when the property is an enum) the
    /// field renders as a <c>select</c> instead of an input.
    /// </summary>
    [HtmlAttributeName("items")]
    public IEnumerable<SelectListItem>? Items { get; set; }

    /// <summary>Text of a first, empty option for optional drop-downs, e.g. "None".</summary>
    [HtmlAttributeName("empty-option")]
    public string? EmptyOption { get; set; }

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

        TagBuilder input = BuildInput(isCheckbox);

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
    /// Builds the control: checkbox for <c>bool</c>, select for enums or when
    /// <see cref="Items"/> is given, otherwise a typed input.
    /// </summary>
    /// <param name="isCheckbox">True for a <c>bool</c> property.</param>
    /// <returns>The control element.</returns>
    private TagBuilder BuildInput(bool isCheckbox)
    {
        if (isCheckbox)
        {
            return _generator.GenerateCheckBox(ViewContext, For.ModelExplorer, For.Name, (bool?)For.Model, new { @class = "form-check-input" });
        }

        IEnumerable<SelectListItem>? options = Items ?? EnumOptions();
        if (options is not null)
        {
            return _generator.GenerateSelect(
                ViewContext, For.ModelExplorer, EmptyOption, For.Name, options, allowMultiple: false, new { @class = "form-select" });
        }

        var attributes = new Dictionary<string, object>
        {
            ["class"] = "form-control",
            ["type"] = InputType ?? DefaultInputType(),
        };
        if (IsFractional())
        {
            // Without step="any" browsers treat number inputs as whole numbers only.
            attributes["step"] = "any";
        }

        return _generator.GenerateTextBox(ViewContext, For.ModelExplorer, For.Name, For.Model, format: null, attributes);
    }

    /// <summary>
    /// Builds options from an enum property's names, split into words for display
    /// ("ThirdParty" shows as "Third Party"). Null when the property is not an enum.
    /// </summary>
    /// <returns>The options, or null.</returns>
    private IEnumerable<SelectListItem>? EnumOptions()
    {
        Type type = Nullable.GetUnderlyingType(For.ModelExplorer.ModelType) ?? For.ModelExplorer.ModelType;
        return type.IsEnum
            ? Enum.GetNames(type).Select(name => new SelectListItem(name.ToWords(), name)).ToList()
            : null;
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
    /// Checks whether the property holds fractional numbers (latitude, amounts).
    /// </summary>
    /// <returns>True for <c>double</c> and <c>decimal</c>.</returns>
    private bool IsFractional()
    {
        Type type = Nullable.GetUnderlyingType(For.ModelExplorer.ModelType) ?? For.ModelExplorer.ModelType;
        return type == typeof(double) || type == typeof(decimal);
    }

    /// <summary>
    /// Uses the display name from metadata if set, otherwise splits the
    /// PascalCase property name into words.
    /// </summary>
    /// <returns>The label text.</returns>
    private string DefaultLabel() =>
        For.Metadata.DisplayName ?? (For.Metadata.PropertyName ?? For.Name).ToWords();
}
