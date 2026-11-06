using IotAdoNet.Datos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IotAdoNet.Pages;

public class IndexModel(RepositorioIot repo) : PageModel
{
    public Resumen Resumen { get; private set; } = default!;
    public List<SensorFila> Sensores { get; private set; } = [];
    public List<DispositivoOpcion> Dispositivos { get; private set; } = [];
    public List<(string Hora, string Tabla, string Operacion, string Registro, string Cambios, string App)> Auditoria { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Resumen = await repo.ResumenAsync();
        Sensores = await repo.SensoresAsync();
        Dispositivos = await repo.DispositivosOpcionesAsync();
        Auditoria = await repo.AuditoriaAsync();
    }

    // C · Crear un sensor
    public async Task<IActionResult> OnPostCrearAsync(string codigo, string dispositivo, string tipo, decimal umbral)
    {
        try
        {
            var id = await repo.CrearSensorAsync(codigo, dispositivo, tipo, umbral);
            TempData["Ok"] = $"Sensor {codigo.ToUpperInvariant()} creado con id {id} (tipo {tipo}, unidad {RepositorioIot.UnidadPorTipo[tipo]}).";
        }
        catch (ErrorDatos e) { TempData["Error"] = e.Message; }
        return RedirectToPage();
    }
}
