using MWZSystem.Ui;

namespace MedicApp.Tests;

/// <summary>
/// Colores por acción de MWZSystem.Ui 1.7.0 (criterio de Trainify). Las pantallas renderizadas se revisan en
/// HttpTests.Pages_FollowTheActionColors, que usa LocalDB; aquí, lo que no necesita base de datos.
/// </summary>
public class ActionColorTests
{
    private static readonly string AppFolder = FindAppFolder();

    [Fact]
    public void Usa_MWZSystem_Ui_1_7_0()
    {
        Assert.Equal(new Version(1, 7, 0, 0), typeof(MwzShell).Assembly.GetName().Version);
    }

    [Fact]
    public void Plantilla_y_vistas_activan_los_colores()
    {
        Assert.Contains("@addTagHelper *, MWZSystem.Ui", File.ReadAllText(Path.Combine(AppFolder, "Views", "_ViewImports.cshtml")));
        Assert.Contains("ActionColors = true", File.ReadAllText(Path.Combine(AppFolder, "Views", "Shared", "_Layout.cshtml")));
    }

    [Fact]
    public void Un_eliminar_o_desactivar_sale_en_rojo_y_aceptar_nunca()
    {
        Assert.Equal("btn btn-danger", MwzActionColors.Apply("btn btn-outline-secondary", "Eliminar"));
        Assert.Equal("btn btn-danger", MwzActionColors.Apply("btn btn-outline-danger", "Desactivar"));
        Assert.Equal("btn btn-primary", MwzActionColors.Apply("btn btn-danger", "Aceptar"));
        Assert.Equal("btn btn-success", MwzActionColors.Apply("btn btn-primary", "Aprobar"));
    }

    [Fact]
    public void Los_botones_solidos_no_conservan_el_turquesa_de_la_marca()
    {
        // El turquesa encima de .btn-primary taparía el azul de «Guardar»; la marca sigue en el menú y los contornos.
        foreach (var file in new[] { "medicapp.css", "medicapp-base.css" })
        {
            var css = File.ReadAllText(Path.Combine(AppFolder, "wwwroot", "css", file));
            Assert.DoesNotMatch(@"\.btn-primary\s*[{:,]", css);
        }
    }

    [Theory]
    // Contorno de los botones neutros (descargar, ir a otra pantalla): texto oscuro de la marca sobre blanco y al pasar el cursor.
    [InlineData("#173f49", "#ffffff")]
    [InlineData("#173f49", "#e7f3f4")]
    // Textos de la marca sobre el fondo de la página.
    [InlineData("#173f49", "#f4f8f9")]
    [InlineData("#5b737a", "#ffffff")]
    // Texto de la paleta común sobre cada color de botón.
    [InlineData("#ffffff", "#0d6efd")]
    [InlineData("#ffffff", "#198754")]
    [InlineData("#212529", "#0dcaf0")]
    [InlineData("#212529", "#ffc107")]
    [InlineData("#ffffff", "#dc3545")]
    [InlineData("#ffffff", "#6c757d")]
    public void Los_colores_cumplen_contraste_AA(string text, string background)
    {
        Assert.True(MwzActionColors.Contrast(text, background) >= 4.5, $"{text} sobre {background}: {MwzActionColors.Contrast(text, background):0.00}");
    }

    [Fact]
    public void Los_colores_de_la_marca_son_los_que_se_revisan()
    {
        var css = File.ReadAllText(Path.Combine(AppFolder, "wwwroot", "css", "medicapp.css"))
            + File.ReadAllText(Path.Combine(AppFolder, "wwwroot", "css", "medicapp-base.css"));
        Assert.Contains("--dark-color:#173f49", css);
        Assert.Contains("--gray-color:#5b737a", css);
        Assert.Contains("--primary-soft:#e7f3f4", css);
        Assert.Contains("background:#f4f8f9", css);
    }

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
