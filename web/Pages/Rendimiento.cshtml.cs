using IotAdoNet.Datos;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IotAdoNet.Pages;

public class RendimientoModel(RepositorioIot repo) : PageModel
{
    public int Cantidad { get; private set; } = 1000;
    public List<(string Forma, double Ms)> Resultados { get; private set; } = [];

    public void OnGet() { }

    public async Task OnPostAsync(int cantidad)
    {
        Cantidad = Math.Clamp(cantidad, 10, 5000);
        Resultados = await repo.BenchmarkAsync(Cantidad);
    }
}
