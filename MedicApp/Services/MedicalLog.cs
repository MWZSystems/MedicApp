namespace MedicApp.Services;

public static partial class MedicalLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Login rejected. TraceId {TraceId}")]
    public static partial void LoginRejected(ILogger logger, string traceId);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Request failed with {ErrorType}, status {StatusCode}, trace {TraceId}")]
    public static partial void RequestFailed(ILogger logger, string errorType, int statusCode, string traceId);
}
