using System.Text.Json;
using System.Text.RegularExpressions;
using MWZSystem.Ui;

namespace MedicApp.Tests;

/// <summary>
/// Pantalla de carga de MWZSystem.Ui (desde 1.6.0): la app sirve un paquete que la trae, cada formulario que guarda dice qué está
/// haciendo, las descargas no dejan la página bloqueada y lo que se carga con fetch marca su parte como ocupada.
/// </summary>
public class LoadingScreenTests
{
    private static readonly string AppFolder = FindAppFolder();
    private static readonly string ViewsFolder = Path.Combine(AppFolder, "Views");

    [Fact]
    public void La_app_sirve_el_mwz_ui_js_con_pantalla_de_carga()
    {
        var manifest = Directory.EnumerateFiles(AppContext.BaseDirectory, "MedicApp.staticwebassets.runtime.json").Single();
        using var json = JsonDocument.Parse(File.ReadAllText(manifest));
        var packageRoot = json.RootElement.GetProperty("ContentRoots").EnumerateArray()
            .Select(r => r.GetString()!)
            .Single(r => r.Contains("mwzsystem.ui", StringComparison.OrdinalIgnoreCase));

        // La pantalla de carga llegó en 1.6.0; la versión exacta en uso la revisa ActionColorTests.
        var version = new Version(Path.GetFileName(Path.GetDirectoryName(packageRoot.TrimEnd(Path.DirectorySeparatorChar))!));
        Assert.True(version >= new Version(1, 6, 0), $"MWZSystem.Ui {version}");
        var script = File.ReadAllText(Path.Combine(packageRoot, "js", "mwz-ui.js"));
        Assert.Contains("mwz-texto-carga", script);
        Assert.Contains("mwz-descarga", script);
        Assert.Contains("ocupado", script);
    }

    [Fact]
    public void Cada_formulario_que_guarda_tiene_mensaje_de_carga()
    {
        var forms = Tags("form").Where(t => Regex.IsMatch(t.Tag, @"method=""post""", RegexOptions.IgnoreCase)).ToList();

        Assert.True(forms.Count >= 21, $"Se esperaban al menos 21 formularios POST y hay {forms.Count}.");
        var missing = forms.Where(t => !t.Tag.Contains("data-mwz-texto-carga=\"")).Select(t => t.File).ToList();
        Assert.True(missing.Count == 0, "Sin data-mwz-texto-carga: " + string.Join(", ", missing));
        Assert.All(forms, t => Assert.DoesNotMatch(@"data-mwz-texto-carga=""\s*""", t.Tag));
    }

    [Fact]
    public void Los_mensajes_de_carga_existen_en_espanol()
    {
        var resources = File.ReadAllText(Path.Combine(AppFolder, "Resources", "UiText.resx"));
        var keys = Tags("form").Concat(Tags("a"))
            .SelectMany(t => Regex.Matches(t.Tag, @"data-mwz-texto-carga=""@T\[""(\w+)""\]""").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.Matches($@"<data name=""{key}"" xml:space=""preserve""><value>[^<]+…</value>", resources));
    }

    [Fact]
    public void Las_descargas_no_bloquean_la_pagina()
    {
        var downloads = Tags("a").Where(t => t.Tag.Contains("asp-action=\"Download\"")).ToList();

        // Documentos del expediente clínico, documentos de incapacidad y la guía de uso.
        Assert.Equal(3, downloads.Count);
        Assert.All(downloads, t => Assert.Contains("data-mwz-descarga", t.Tag));
    }

    [Theory]
    [InlineData("calendar.js", 3)]
    [InlineData("leave-calendar.js", 1)]
    [InlineData("demo-calendar.js", 1)]
    public void Lo_que_se_carga_con_fetch_marca_su_parte_como_ocupada(string file, int expected)
    {
        var script = File.ReadAllText(Path.Combine(AppFolder, "wwwroot", "js", file));

        Assert.Equal(expected, Regex.Count(script, @"mwz\.ocupado\("));
    }

    /// <summary>Etiquetas de apertura de un elemento en todas las vistas (pueden ocupar varias líneas).</summary>
    private static IEnumerable<(string File, string Tag)> Tags(string element) =>
        Directory.EnumerateFiles(ViewsFolder, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), $@"<{element}\b[^>]*>", RegexOptions.Singleline)
                .Select(m => (Path.GetRelativePath(ViewsFolder, file), m.Value)));

    private static string FindAppFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var app = Path.Combine(dir.FullName, "MedicApp", "Views");
            if (Directory.Exists(app))
            {
                return Path.GetDirectoryName(app)!;
            }
        }

        throw new DirectoryNotFoundException("No se encontró MedicApp/Views.");
    }
}
