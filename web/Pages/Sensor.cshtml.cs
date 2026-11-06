using IotAdoNet.Datos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IotAdoNet.Pages;

public class SensorModel(RepositorioIot repo) : PageModel
{
    public SensorFila? Sensor { get; private set; }
    public List<LecturaFila> Lecturas { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string codigo)
    {
        Sensor = await repo.SensorAsync(codigo);
        if (Sensor is null)
        {
            TempData["Error"] = $"El sensor {codigo} no existe.";
            return RedirectToPage("/Index");
        }
        Lecturas = await repo.LecturasAsync(codigo);
        return Page();
    }

    // C · Registrar una lectura
    public async Task<IActionResult> OnPostRegistrarAsync(string codigo, decimal valor)
    {
        try
        {
            var (id, situacion) = await repo.RegistrarLecturaAsync(codigo, valor);
            TempData["Ok"] = $"Lectura {id} registrada: {valor:0.00} -> {situacion}.";
        }
        catch (ErrorDatos e) { TempData["Error"] = e.Message; }
        return RedirectToPage(new { codigo });
    }

    // U · Actualizar el umbral
    public async Task<IActionResult> OnPostUmbralAsync(string codigo, decimal umbral)
    {
        try
        {
            var anterior = await repo.ActualizarUmbralAsync(codigo, umbral);
            TempData["Ok"] = $"Umbral de {codigo} actualizado: {anterior:0.00} -> {umbral:0.00}.";
        }
        catch (ErrorDatos e) { TempData["Error"] = e.Message; }
        return RedirectToPage(new { codigo });
    }

    // D · Eliminar una lectura
    public async Task<IActionResult> OnPostEliminarLecturaAsync(string codigo, long id)
    {
        try
        {
            var n = await repo.EliminarLecturaAsync(id);
            TempData["Ok"] = n == 1 ? $"Lectura {id} eliminada." : $"La lectura {id} ya no existe.";
        }
        catch (ErrorDatos e) { TempData["Error"] = e.Message; }
        return RedirectToPage(new { codigo });
    }

    // D · Eliminar el sensor (con o sin sus lecturas)
    public async Task<IActionResult> OnPostEliminarSensorAsync(string codigo, bool conLecturas)
    {
        try
        {
            var (lecturas, alertas) = await repo.EliminarSensorAsync(codigo, conLecturas);
            TempData["Ok"] = $"Sensor {codigo} eliminado junto con {lecturas} lecturas y {alertas} alertas (una transaccion).";
            return RedirectToPage("/Index");
        }
        catch (ErrorDatos e)
        {
            TempData["Error"] = e.Message + (conLecturas ? "" : " Usa \"Eliminar con lecturas\".");
            return RedirectToPage(new { codigo });
        }
    }
}
