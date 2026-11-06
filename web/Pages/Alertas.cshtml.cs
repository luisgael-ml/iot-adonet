using IotAdoNet.Datos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IotAdoNet.Pages;

public class AlertasModel(RepositorioIot repo) : PageModel
{
    public List<AlertaFila> Alertas { get; private set; } = [];
    public long Total { get; private set; }

    public async Task OnGetAsync()
    {
        Alertas = await repo.AlertasPendientesAsync();                 // muestra hasta 50
        Total = (await repo.ResumenAsync()).AlertasPendientes;         // total real con COUNT(*)
    }

    public async Task<IActionResult> OnPostAtenderAsync(int id)
    {
        var n = await repo.AtenderAlertaAsync(id);
        TempData["Ok"] = n == 1 ? $"Alerta {id} atendida." : $"La alerta {id} ya estaba atendida.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTodasAsync()
    {
        var n = await repo.AtenderTodasAsync();
        TempData["Ok"] = $"{n} alertas marcadas como atendidas (ExecuteNonQuery devolvio {n}).";
        return RedirectToPage();
    }
}
