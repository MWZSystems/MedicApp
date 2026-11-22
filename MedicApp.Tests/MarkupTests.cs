using System.Text.RegularExpressions;

namespace MedicApp.Tests;

/// <summary>Arreglos de la revisión con Lighthouse (MWZSystem.Ui 1.9.0) que viven en las vistas, el CSS y la publicación de MedicApp.</summary>
public class MarkupTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Cada_tabla_tiene_titulo_y_encabezados_de_columna()
    {
        var views = Directory.EnumerateFiles(Path.Combine(Root, "MedicApp", "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Select(file => (File: Path.GetFileName(file), Text: File.ReadAllText(file)))
            .Where(v => v.Text.Contains("<table", StringComparison.Ordinal)).ToList();

        Assert.Equal(11, views.Sum(v => Regex.Count(v.Text, @"<table\b")));
        Assert.All(views, v => Assert.Equal(Regex.Count(v.Text, @"<table\b"),Regex.Count(v.Text, @"<table[^>]*><caption class=""visually-hidden"">@T\[""\w+""\]</caption>")));
        Assert.All(views, v => Assert.DoesNotMatch(@"<th(?![\w-])(?![^>]*scope=)", v.Text));
    }

    [Fact]
    public void El_CSS_ya_no_trae_los_avisos_ni_la_barra_de_version_propios()
    {
        // Los avisos y la etiqueta «PRD · v1.0.0» son de MWZSystem.Ui.
        var css = File.ReadAllText(Path.Combine(Root, "MedicApp", "wwwroot", "css", "medicapp.css"));
        Assert.DoesNotContain("app-toast", css);
        Assert.DoesNotContain("toast-stack", css);
        Assert.DoesNotContain("app-version-bar", css);
        Assert.DoesNotContain("app-version-bar", File.ReadAllText(Path.Combine(Root, "MedicApp", "Views", "Shared", "_Layout.cshtml")));
    }

    [Fact]
    public void La_publicacion_marca_el_ambiente_del_sitio()
    {
        var script = File.ReadAllText(Path.Combine(Root, "scripts", "Publicar-MedicApp.ps1"));
        Assert.Contains("Mwz = @{ Ambiente = $taskAmbiente }", script);
        Assert.Contains("$taskAmbiente -notin @('STG','PRD','DEV')", script);
        // En el repositorio no se fija ningún ambiente: en local la etiqueta dice DEV.
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "MedicApp"), "appsettings*.json"))
        {
            Assert.DoesNotContain("Ambiente", File.ReadAllText(file));
        }
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "MedicApp", "Views")) && Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio de MedicApp.");
    }
}
