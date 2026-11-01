using Microsoft.AspNetCore.Mvc;
namespace MedicApp.Controllers;

public class HelpController(IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Download()
    {
        var path = Path.Combine(environment.ContentRootPath, "Guides", "MedicApp-Guia-de-Uso.pptx");
        if (!System.IO.File.Exists(path)) { return NotFound(); }
        return PhysicalFile(path, "application/vnd.openxmlformats-officedocument.presentationml.presentation", "MedicApp-Guia-de-Uso.pptx");
    }
}
