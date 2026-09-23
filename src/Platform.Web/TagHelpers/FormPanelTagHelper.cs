using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Platform.Web.TagHelpers;

/// <summary>
/// One titled section of a form — heading and explanation on the left, fields
/// on the right:
/// <c>&lt;form-panel title="Profile" description="..."&gt;fields&lt;/form-panel&gt;</c>.
/// Every form is built from these, so section markup exists once.
/// </summary>
[HtmlTargetElement("form-panel", Attributes = "title")]
public sealed class FormPanelTagHelper : TagHelper
{
    /// <summary>Section heading.</summary>
    [HtmlAttributeName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional explanation under the heading.</summary>
    [HtmlAttributeName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// True to put the heading above the content and give the content the full
    /// width — for wide content such as the access grid.
    /// </summary>
    [HtmlAttributeName("stacked")]
    public bool Stacked { get; set; }

    /// <summary>
    /// Wraps the child content in the section layout.
    /// </summary>
    /// <param name="context">Tag helper context.</param>
    /// <param name="output">Output to write.</param>
    /// <returns>A task that completes once the child content is rendered.</returns>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        TagHelperContent body = await output.GetChildContentAsync();

        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Stacked ? "form-panel form-panel--stacked" : "form-panel");

        var heading = new TagBuilder("h2");
        heading.AddCssClass("form-panel__title");
        heading.InnerHtml.Append(Title);

        var header = new TagBuilder("div");
        header.AddCssClass("form-panel__header");
        header.InnerHtml.AppendHtml(heading);
        if (!string.IsNullOrEmpty(Description))
        {
            var description = new TagBuilder("p");
            description.AddCssClass("form-panel__description");
            description.InnerHtml.Append(Description);
            header.InnerHtml.AppendHtml(description);
        }

        var content = new TagBuilder("div");
        content.AddCssClass("form-panel__body");
        content.InnerHtml.AppendHtml(body);

        output.Content.SetHtmlContent(header);
        output.Content.AppendHtml(content);
    }
}
