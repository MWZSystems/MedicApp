using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Workforce.Shared;

/// <summary>Página de un listado: número (desde 1), tamaño y total de registros que cumplen los filtros.</summary>
public record PageInfo(string Name, int Number, int Size, int Total)
{
    public int Pages => Math.Max(1, (Total + Size - 1) / Size);
    public int From => Total == 0 ? 0 : (Number - 1) * Size + 1;
    public int To => Math.Min(Total, Number * Size);
    public string PageKey => Name + Paging.PageKey;
    public string SizeKey => Name + Paging.SizeKey;
}

/// <summary>
/// Paginación de listados en el servidor. Con <see cref="ToPageAsync{T}"/> la base devuelve solo la página pedida (OFFSET/FETCH)
/// y su conteo, con los filtros ya aplicados; <see cref="ToPage{T}"/> pagina listas que se calculan en memoria (estados de
/// cumplimiento). La página y su tamaño se leen de la URL (pagina, tamano; con prefijo cuando hay varios listados en una pantalla)
/// y quedan en ViewData para el paginador, así el modelo de cada vista no cambia.
/// </summary>
public static class Paging
{
    public const string PageKey = "pagina";
    public const string SizeKey = "tamano";
    public const string SearchKey = "buscar";
    public const int DefaultSize = 25;
    public static readonly int[] Sizes = [25, 50, 100];

    public static string ViewDataKey(string name = "") => "Paging:" + name;

    /// <summary>Texto de búsqueda del listado (parámetro buscar), recortado; null si viene vacío.</summary>
    public static string? Search(this Controller controller, string name = "")
    {
        var value = controller.Request.Query[name + SearchKey].ToString().Trim();
        return value.Length == 0 ? null : value[..Math.Min(value.Length, 100)];
    }

    public static async Task<List<T>> ToPageAsync<T>(this IQueryable<T> ordered, Controller controller, string name = "")
    {
        var (number, size) = Read(controller.Request, name);
        var total = await ordered.CountAsync();
        var info = Clamp(name, number, size, total);
        controller.ViewData[ViewDataKey(name)] = info;
        return await ordered.Skip((info.Number - 1) * info.Size).Take(info.Size).ToListAsync();
    }

    public static List<T> ToPage<T>(this IEnumerable<T> ordered, Controller controller, string name = "") =>
        ordered.ToPage(controller.Request, controller.ViewData, name);

    /// <summary>Paginación en memoria desde una vista, para tablas que salen de un modelo ya calculado (tableros de reportes).</summary>
    public static List<T> ToPage<T>(this IEnumerable<T> ordered, HttpRequest request, Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary viewData, string name = "")
    {
        var list = ordered as IReadOnlyCollection<T> ?? ordered.ToList();
        var (number, size) = Read(request, name);
        var info = Clamp(name, number, size, list.Count);
        viewData[ViewDataKey(name)] = info;
        return list.Skip((info.Number - 1) * info.Size).Take(info.Size).ToList();
    }

    /// <summary>
    /// Después de una acción en una fila, vuelve al mismo listado con su página y filtros (Referer de este sitio que apunta a esa
    /// acción); si la acción vino de otra pantalla, va al listado sin filtros.
    /// </summary>
    public static IActionResult RedirectBack(this Controller controller, string action)
    {
        var referer = controller.Request.GetTypedHeaders().Referer;
        var target = controller.Url.Action(action);
        if (referer is { IsAbsoluteUri: true } && target != null && referer.Authority == controller.Request.Host.Value
            && string.Equals(referer.AbsolutePath.TrimEnd('/'), target.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            && controller.Url.IsLocalUrl(referer.PathAndQuery))
            return controller.LocalRedirect(referer.PathAndQuery);
        return controller.RedirectToAction(action);
    }

    private static (int Number, int Size) Read(HttpRequest request, string name)
    {
        var number = int.TryParse(request.Query[name + PageKey], out var n) ? n : 1;
        var size = int.TryParse(request.Query[name + SizeKey], out var s) && Sizes.Contains(s) ? s : DefaultSize;
        return (number, size);
    }

    // Una página fuera de rango (por ejemplo, tras filtrar) muestra la última que existe.
    private static PageInfo Clamp(string name, int number, int size, int total)
    {
        var info = new PageInfo(name, 1, size, total);
        return info with { Number = Math.Clamp(number, 1, info.Pages) };
    }
}

/// <summary>Caja de búsqueda de un listado paginado (parámetro buscar, con el mismo prefijo que su paginador).</summary>
public record SearchBox(string Placeholder, string Name = "");
