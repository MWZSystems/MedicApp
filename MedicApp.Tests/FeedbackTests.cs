using System.Text.Json;
using MedicApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace MedicApp.Tests;

public class FeedbackTests
{
    private sealed class Provider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private static TempDataDictionary Data() => new(new DefaultHttpContext(), new Provider());
    [Fact]
    public void ErrorsFirstWithoutReorderingOtherMessagesAndTakenOnlyOnce()
    {
        var data = Data(); Feedback.Add(data, FeedbackKind.Info, "Información"); Feedback.Add(data, FeedbackKind.Success, "Guardado"); Feedback.Add(data, FeedbackKind.Error, "Error"); Feedback.Add(data, FeedbackKind.Warning, "Atención");
        Assert.Equal(new[] { FeedbackKind.Error, FeedbackKind.Info, FeedbackKind.Success, FeedbackKind.Warning }, Feedback.Take(data).Select(x => x.Kind)); Assert.Empty(Feedback.Take(data));
    }
    [Fact]
    public void EmptyAndRepeatedMessagesAreIgnoredButDifferentKindsAreKept()
    {
        var data = Data(); Feedback.Add(data, FeedbackKind.Success, ""); Feedback.Add(data, FeedbackKind.Success, "  "); Feedback.Add(data, FeedbackKind.Success, " Guardado "); Feedback.Add(data, FeedbackKind.Success, "Guardado"); Feedback.Add(data, FeedbackKind.Info, "Guardado");
        var messages = Feedback.Take(data); Assert.Equal(2, messages.Count); Assert.All(messages, x => Assert.Equal("Guardado", x.Text));
    }
    [Fact]
    public void LegacyKeysAreConsumedAndDeduplicatedTogetherWithTheNewList()
    {
        var data = Data(); data["Success"] = "Guardado"; data["Info"] = "Sin cambios"; data["Warning"] = "Atención"; data["Error"] = "No se pudo guardar"; Feedback.Add(data, FeedbackKind.Success, "Guardado");
        var messages = Feedback.Take(data); Assert.Equal(4, messages.Count); Assert.Equal(FeedbackKind.Error, messages[0].Kind); Assert.Empty(data.Keys);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[null,{\"Kind\":123,\"Text\":\"invalid\"},{\"Kind\":0,\"Text\":\" \"}]")]
    public void InvalidStoredContentDoesNotBreakThePage(string json)
    {
        var data = Data(); data["Feedback"] = json; data["Success"] = "Guardado"; Assert.Single(Feedback.Take(data));
    }
    [Fact]
    public void PendingMessagesSurviveTempDataSaveUntilTheDestinationPageReadsThem()
    {
        var data = Data(); Feedback.Add(data, FeedbackKind.Success, "Guardado"); Feedback.Add(data, FeedbackKind.Warning, "Falta evidencia");
        var json = Assert.IsType<string>(data.Peek("Feedback")); Assert.Equal(2, JsonSerializer.Deserialize<List<FeedbackMessage>>(json)!.Count);
    }
}
