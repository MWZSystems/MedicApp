using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
namespace MedicApp.Services;

public class MedicalExceptionHandler(ILogger<MedicalExceptionHandler> logger, IStringLocalizer<UiText> text) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var key = exception switch
        {
            MedicalRuleException rule => rule.Message,
            DbUpdateConcurrencyException => "Concurrency",
            DbUpdateException update when update.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } => "Duplicate",
            Microsoft.Data.SqlClient.SqlException { Number: 1205 or 50010 } => "Concurrency",
            _ => "UnexpectedError"
        };
        if (text[key].ResourceNotFound) { key = "UnexpectedError"; }
        var status = key switch { "Forbidden" => 403, "NotFound" => 404, "Concurrency" or "Duplicate" or "Overlap" => 409, "UnexpectedError" => 500, _ => 422 };
        var trace = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        // SQL errors can contain personal values. Log only type and correlation identifier.
        MedicalLog.RequestFailed(logger, exception.GetType().Name, status, trace);
        httpContext.Response.StatusCode = status;
        httpContext.Response.Headers.CacheControl = "no-store";
        if (httpContext.Request.Headers.Accept.Any(x => x != null && x.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            var state = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            state.AddModelError("", text[key]);
            var result = new ViewResult { ViewName = "~/Views/Shared/RequestError.cshtml", StatusCode = status, ViewData = new(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), state) };
            // Exception middleware clears the endpoint. Restore it while rendering so tag helpers use endpoint routing.
            var endpoint = httpContext.GetEndpoint();
            var failed = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
            httpContext.SetEndpoint(failed ?? endpoint ?? new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "Feedback rendering"));
            try { await result.ExecuteResultAsync(new ActionContext(httpContext, httpContext.GetRouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor())); }
            finally { httpContext.SetEndpoint(endpoint); }
        }
        else
        {
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = text[key], Extensions = { ["traceId"] = trace } }, cancellationToken);
        }
        return true;
    }
}
