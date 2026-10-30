using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
namespace MedicApp.TagHelpers;

[HtmlTargetElement("medic-icon")]
public class MedicIconTagHelper(IHttpContextAccessor accessor) : TagHelper
{
    private static readonly HashSet<string> Names = ["login", "lock", "id-card", "stethoscope", "clipboard", "paperclip", "download", "save", "filter", "chevron-left", "chevron-right", "file-check", "alert", "document", "upload", "folder", "settings", "home", "calendar", "clock", "heart", "user", "users", "shield", "history", "key", "logout", "plus", "check", "close", "list", "arrow", "search", "info", "refresh"];
    public string Name { get; set; } = "calendar";
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var name = Names.Contains(Name) ? Name : "calendar";
        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.RemoveAll("name");
        output.Attributes.SetAttribute("class", "medic-icon");
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Attributes.SetAttribute("focusable", "false");
        var use = new TagBuilder("use");
        use.Attributes["href"] = $"{accessor.HttpContext?.Request.PathBase}/branding/icons.svg#{name}";
        output.Content.SetHtmlContent(use);
    }
}
