using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Platform.Web.TagHelpers;

/// <summary>
/// Renders one icon from the sprite: <c>&lt;icon name="package" /&gt;</c>
/// becomes an <c>svg</c> that references <c>wwwroot/img/icons.svg#package</c>.
/// Icons are decorative (hidden from screen readers); the text next to them,
/// or the button's <c>aria-label</c>, carries the meaning.
/// </summary>
[HtmlTargetElement("icon", Attributes = "name", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconTagHelper : TagHelper
{
    /// <summary>Path of the sprite under wwwroot.</summary>
    private const string SpritePath = "/img/icons.svg";

    /// <summary>Icon name from <c>Common.Icons</c>.</summary>
    [HtmlAttributeName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Extra CSS classes, e.g. a size modifier.</summary>
    [HtmlAttributeName("class")]
    public string? CssClass { get; set; }

    /// <summary>Current view context, injected by Razor.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>
    /// Writes the <c>svg</c> with a <c>use</c> reference into the sprite.
    /// </summary>
    /// <param name="context">Tag helper context.</param>
    /// <param name="output">Output to write.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string pathBase = ViewContext.HttpContext.Request.PathBase;

        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", string.IsNullOrEmpty(CssClass) ? "icon" : $"icon {CssClass}");
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Attributes.SetAttribute("focusable", "false");

        var use = new TagBuilder("use");
        use.Attributes["href"] = $"{pathBase}{SpritePath}#{Name}";
        output.Content.SetHtmlContent(use);
    }
}
