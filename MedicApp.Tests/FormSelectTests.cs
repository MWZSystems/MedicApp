using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MedicApp.Models.ViewModels;

namespace MedicApp.Tests;

/// <summary>
/// Regla común (Trainify, FormSelectTests): en un formulario, una lista sin dato guardado empieza vacía y es obligatoria,
/// para que nadie guarde la primera opción sin elegirla; el servidor tampoco la supone. Los filtros (GET) no cambian.
/// </summary>
public class FormSelectTests
{
    private static readonly string AppFolder = FindAppFolder();

    // Listas que muestran un valor ya guardado: lo conservan.
    private static readonly string[] SavedValue =
    [
        "id=\"active-@row.Id\"",            // Médicos: disponible o no, el valor guardado del doctor
        "asp-for=\"General.TimeZoneId\"",   // Configuración: la zona horaria guardada
    ];

    [Fact]
    public void Cada_lista_de_formulario_sin_valor_guardado_empieza_vacia_y_es_obligatoria()
    {
        var selects = Directory.EnumerateFiles(Path.Combine(AppFolder, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"<form\b([^>]*)>(.*?)</form>", RegexOptions.Singleline)
                .Where(form => form.Groups[1].Value.Contains("method=\"post\"", StringComparison.Ordinal))
                .SelectMany(form => Regex.Matches(form.Groups[2].Value, @"<select\b([^>]*)>(.*?)</select>", RegexOptions.Singleline))
                .Select(select => (File: Path.GetFileName(file), Tag: select.Groups[1].Value, Body: select.Groups[2].Value)))
            .ToList();

        Assert.True(selects.Count >= 12, $"Se esperaban al menos 12 listas en formularios y hay {selects.Count}.");
        var missing = selects
            .Where(s => !SavedValue.Any(s.Tag.Contains))
            .Where(s => !Regex.IsMatch(s.Tag, @"\brequired\b") || !Regex.IsMatch(s.Body.TrimStart(), @"^(?:@[^<]*)?<option value="""">"))
            // La acción del detalle de cita se llena desde calendar.js, que pone primero la opción vacía.
            .Where(s => !(s.Tag.Contains("id=\"detail-action\"", StringComparison.Ordinal) && s.Body.Trim().Length == 0))
            .Select(s => $"{s.File}: {s.Tag.Trim()}")
            .ToList();
        Assert.True(missing.Count == 0, "Sin opción vacía u obligatoria: " + string.Join(" | ", missing));
    }

    [Fact]
    public void La_accion_del_calendario_empieza_vacia()
    {
        var script = File.ReadAllText(Path.Combine(AppFolder, "wwwroot", "js", "calendar.js"));
        Assert.Contains("action.replaceChildren(new Option(labels.chooseOption, \"\"));", script);
        Assert.Contains("id=\"detail-action\" name=\"Status\" class=\"form-select\" data-select2=\"off\" required", File.ReadAllText(Path.Combine(AppFolder, "Views", "Appointments", "Index.cshtml")));
    }

    [Fact]
    public void El_servidor_no_supone_el_tipo_si_llega_vacio()
    {
        // Los servicios validan el formulario con estas reglas antes de guardar (InvalidStockInput / InvalidClinicalInput).
        Assert.False(Valid(new StockMovementForm { StockItemId = 1, Quantity = 1, Reason = "Prueba", Kind = null }));
        Assert.True(Valid(new StockMovementForm { StockItemId = 1, Quantity = 1, Reason = "Prueba", Kind = Models.Entities.StockMovementKind.Exit }));
        Assert.False(Valid(new PatientUploadForm { PersonId = 1, Kind = null }));
        Assert.True(Valid(new PatientUploadForm { PersonId = 1, Kind = Models.Entities.PatientDocumentKind.Study }));
    }

    private static bool Valid(object form) => Validator.TryValidateObject(form, new ValidationContext(form), [], true);

    private static string FindAppFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var views = Path.Combine(dir.FullName, "MedicApp", "Views");
            if (Directory.Exists(views))
            {
                return Path.GetDirectoryName(views)!;
            }
        }

        throw new DirectoryNotFoundException("No se encontró MedicApp/Views.");
    }
}
