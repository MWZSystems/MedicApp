using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
namespace MedicApp.Services;

/// <summary>Preserves invalid POST status codes while giving browser forms a readable error page.</summary>
public sealed class FeedbackResultFilter(IStringLocalizer<UiText> text) : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method) || !context.HttpContext.Request.Headers.Accept.Any(x => x != null && x.Contains("text/html", StringComparison.OrdinalIgnoreCase))) { return; }
        var status = context.Result is StatusCodeResult code ? code.StatusCode : context.Result is ObjectResult result ? result.StatusCode : null;
        if (status != StatusCodes.Status400BadRequest) { return; }
        var state = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        state.AddModelError("", text["FeedbackInvalidForm"]);
        context.Result = new ViewResult { ViewName = "~/Views/Shared/RequestError.cshtml", StatusCode = status, ViewData = new(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), state) };
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
