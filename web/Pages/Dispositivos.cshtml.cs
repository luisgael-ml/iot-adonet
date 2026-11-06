using System.Data;
using IotAdoNet.Datos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IotAdoNet.Pages;

/// <summary>Modelo desconectado: DataTable llenado por un DataAdapter y guardado con Update.</summary>
public class DispositivosModel(RepositorioIot repo) : PageModel
{
    public DataTable Tabla { get; private set; } = new();

    public async Task OnGetAsync() => Tabla = await repo.DispositivosTablaAsync();

    public async Task<IActionResult> OnPostAsync(Dictionary<string, string> estado)
    {
        try
        {
            var (n, sql) = await repo.GuardarEstadosAsync(estado);
            TempData["Ok"] = $"DataAdapter.Update envio {n} fila(s) modificada(s). UPDATE generado por el CommandBuilder: {sql}";
        }
        catch (ErrorDatos e) { TempData["Error"] = e.Message; }
        return RedirectToPage();
    }
}
