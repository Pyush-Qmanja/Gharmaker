using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Platform.Web.Common;

namespace Platform.Web.TagHelpers;

/// <summary>
/// A phone number field with its country calling code in a separate drop-down
/// (India preselected), so people type only their number: <c>&lt;phone-field asp-for="Phone" /&gt;</c>.
/// The two parts are joined into one international number (+919876543210) by
/// <see cref="PhoneValueProviderFactory"/> before the model is bound.
/// </summary>
[HtmlTargetElement("phone-field", Attributes = ForAttributeName, TagStructure = TagStructure.WithoutEndTag)]
public sealed class PhoneFieldTagHelper : TagHelper
{
    private const string ForAttributeName = "asp-for";

    private readonly IHtmlGenerator _generator;

    /// <summary>
    /// Creates the tag helper.
    /// </summary>
    /// <param name="generator">Generates the label and validation message.</param>
    public PhoneFieldTagHelper(IHtmlGenerator generator)
    {
        _generator = generator;
    }

    /// <summary>The phone property.</summary>
    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    /// <summary>Label text; defaults to "Mobile".</summary>
    public string? Label { get; set; }

    /// <summary>Help text under the field.</summary>
    public string? Hint { get; set; }

    /// <summary>The view, for field names and model state.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>
    /// Renders label, calling-code drop-down, number box, hint and error.
    /// </summary>
    /// <param name="context">Tag context.</param>
    /// <param name="output">Tag output.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string name = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        string id = TagBuilder.CreateSanitizedId(name, "_");

        // After a failed post, show what was typed; otherwise split the stored number.
        (string code, string local) = DialCodes.Split(For.Model as string);
        if (ViewContext.ViewData.ModelState.TryGetValue(name + DialCodes.FieldSuffix, out ModelStateEntry? posted) && posted.AttemptedValue is { } postedCode)
        {
            code = postedCode;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "form-field");

        TagBuilder label = _generator.GenerateLabel(ViewContext, For.ModelExplorer, For.Name, Label ?? "Mobile", new { @class = "form-field__label" });

        var group = new TagBuilder("div");
        group.AddCssClass("phone-input");

        var select = new TagBuilder("select");
        select.AddCssClass("form-select phone-input__code");
        select.Attributes["name"] = name + DialCodes.FieldSuffix;
        select.Attributes["aria-label"] = "Country code";
        foreach (DialCode dial in DialCodes.All)
        {
            var option = new TagBuilder("option");
            option.Attributes["value"] = dial.Code;
            if (dial.Code == code)
            {
                option.Attributes["selected"] = "selected";
            }

            option.InnerHtml.Append($"{dial.Code} {dial.Country}");
            select.InnerHtml.AppendHtml(option);
        }

        var input = new TagBuilder("input");
        input.TagRenderMode = TagRenderMode.SelfClosing;
        input.AddCssClass("form-control phone-input__number");
        input.Attributes["type"] = "tel";
        input.Attributes["id"] = id;
        input.Attributes["name"] = name;
        input.Attributes["value"] = local;
        input.Attributes["inputmode"] = "tel";
        input.Attributes["autocomplete"] = "tel-national";
        input.Attributes["placeholder"] = "98765 43210";
        if (ViewContext.ViewData.ModelState.TryGetValue(name, out ModelStateEntry? entry) && entry.Errors.Count > 0)
        {
            input.AddCssClass("input-validation-error");
        }

        group.InnerHtml.AppendHtml(select).AppendHtml(input);
        output.Content.AppendHtml(label).AppendHtml(group);

        if (!string.IsNullOrEmpty(Hint))
        {
            var hint = new TagBuilder("small");
            hint.AddCssClass("form-field__hint");
            hint.InnerHtml.Append(Hint);
            output.Content.AppendHtml(hint);
        }

        output.Content.AppendHtml(_generator.GenerateValidationMessage(
            ViewContext, For.ModelExplorer, For.Name, message: null, tag: "span", new { @class = "form-field__error" }));
    }
}

/// <summary>
/// Joins each posted phone number with its calling code (<c>Phone</c> +
/// <c>Phone__dial</c>) so models bind one international number. Registered
/// first, so it answers for those fields and leaves every other field to the
/// normal form value provider.
/// </summary>
public sealed class PhoneValueProviderFactory : IValueProviderFactory
{
    /// <summary>
    /// Adds the provider when the request is a form post with phone fields.
    /// </summary>
    /// <param name="context">Value provider context.</param>
    /// <returns>A completed task.</returns>
    public async Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        HttpRequest request = context.ActionContext.HttpContext.Request;
        if (!request.HasFormContentType)
        {
            return;
        }

        IFormCollection form = await request.ReadFormAsync();
        var phones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in form.Keys.Where(k => k.EndsWith(DialCodes.FieldSuffix, StringComparison.Ordinal)))
        {
            string field = key[..^DialCodes.FieldSuffix.Length];
            phones[field] = DialCodes.Join(form[key], form[field]);
        }

        if (phones.Count > 0)
        {
            context.ValueProviders.Insert(0, new PhoneValueProvider(phones));
        }
    }

    /// <summary>
    /// Serves the joined numbers; knows nothing else.
    /// </summary>
    private sealed class PhoneValueProvider : IValueProvider
    {
        private readonly IReadOnlyDictionary<string, string> _phones;

        /// <summary>
        /// Creates the provider.
        /// </summary>
        /// <param name="phones">Joined number by field name.</param>
        public PhoneValueProvider(IReadOnlyDictionary<string, string> phones)
        {
            _phones = phones;
        }

        /// <summary>
        /// Never claims a prefix, so complex models still bind their other fields from the form.
        /// </summary>
        /// <param name="prefix">Prefix asked about.</param>
        /// <returns>False.</returns>
        public bool ContainsPrefix(string prefix) => false;

        /// <summary>
        /// Returns the joined number for a phone field.
        /// </summary>
        /// <param name="key">Field name.</param>
        /// <returns>The number, or nothing for other fields.</returns>
        public ValueProviderResult GetValue(string key) =>
            _phones.TryGetValue(key, out string? phone) ? new ValueProviderResult(phone) : ValueProviderResult.None;
    }
}
