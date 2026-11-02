using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace MedicApp.Services;

public enum FeedbackKind { Success, Info, Warning, Error }
public record FeedbackMessage(FeedbackKind Kind, string Text);

/// <summary>One-use feedback persisted through redirects, compatible with older TempData keys.</summary>
public static class Feedback
{
    private const string Key = "Feedback";
    private static readonly (string Key, FeedbackKind Kind)[] Legacy = [("Success", FeedbackKind.Success), ("Info", FeedbackKind.Info), ("Warning", FeedbackKind.Warning), ("Error", FeedbackKind.Error)];
    public static void Add(ITempDataDictionary tempData, FeedbackKind kind, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return; }
        var list = Read(tempData.Peek(Key) as string); var message = new FeedbackMessage(kind, text.Trim());
        if (!list.Contains(message)) { list.Add(message); }
        tempData[Key] = JsonSerializer.Serialize(list);
    }
    public static void Success(this Controller controller, string text) => Add(controller.TempData, FeedbackKind.Success, text);
    public static void Info(this Controller controller, string text) => Add(controller.TempData, FeedbackKind.Info, text);
    public static void Warning(this Controller controller, string text) => Add(controller.TempData, FeedbackKind.Warning, text);
    public static void Error(this Controller controller, string text) => Add(controller.TempData, FeedbackKind.Error, text);
    public static List<FeedbackMessage> Take(ITempDataDictionary tempData)
    {
        var list = Read(tempData[Key] as string);
        tempData.Remove(Key);
        foreach (var (key, kind) in Legacy)
        {
            if (tempData[key] is string text && !string.IsNullOrWhiteSpace(text)) { list.Add(new(kind, text.Trim())); }
            tempData.Remove(key);
        }
        return list.Distinct().OrderBy(x => x.Kind == FeedbackKind.Error ? 0 : 1).ToList();
    }
    private static List<FeedbackMessage> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return []; }
        try { return (JsonSerializer.Deserialize<List<FeedbackMessage?>>(json) ?? []).OfType<FeedbackMessage>().Where(x => Enum.IsDefined(x.Kind) && !string.IsNullOrWhiteSpace(x.Text)).Select(x => x with { Text = x.Text.Trim() }).Distinct().ToList(); }
        catch (JsonException) { return []; }
    }
}
