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
        var status = key switch { "Forbidden" => 403, "NotFound" => 404, "Concurrency" or "Duplicate" or "Overlap" => 409, "UnexpectedError" => 500, _ => 422 };
        var trace = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        // SQL errors can contain personal values. Log only type and correlation identifier.
        MedicalLog.RequestFailed(logger, exception.GetType().Name, status, trace);
        httpContext.Response.StatusCode = status;
        httpContext.Response.Headers.CacheControl = "no-store";
        if (httpContext.Request.Headers.Accept.Any(x => x != null && x.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            httpContext.Response.ContentType = "text/html; charset=utf-8";
            var message = System.Net.WebUtility.HtmlEncode(text[key].Value);
            await httpContext.Response.WriteAsync($"<!doctype html><html lang='es'><meta charset='utf-8'><title>MedicApp</title><body><h1>{message}</h1><p>{System.Net.WebUtility.HtmlEncode(text["Trace"].Value)}: {System.Net.WebUtility.HtmlEncode(trace)}</p><a href='{httpContext.Request.PathBase}/'>{System.Net.WebUtility.HtmlEncode(text["Home"].Value)}</a></body></html>", cancellationToken);
        }
        else
        {
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = text[key], Extensions = { ["traceId"] = trace } }, cancellationToken);
        }
        return true;
    }
}
