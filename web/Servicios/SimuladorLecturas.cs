using System.Data.Common;
using IotAdoNet.Datos;

namespace IotAdoNet.Servicios;

/// <summary>
/// Servicio en segundo plano que envia una lectura por sensor cada segundo, como lo
/// harian los dispositivos IoT. Usa un comando preparado dentro de una transaccion
/// por ronda (la forma mas eficiente segun la prueba de rendimiento).
/// </summary>
public sealed class SimuladorLecturas(ConexionIot bd, IConfiguration config, ILogger<SimuladorLecturas> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue("SIMULADOR", true)) return;
        var azar = new Random();
        var rondas = 0;
        long total = 0;
        var sensores = new List<(string Codigo, decimal Umbral)>();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var cn = await bd.AbrirAsync(ct);
                if (rondas % 30 == 0) sensores = await CargarSensoresAsync(cn, ct);   // incluye sensores nuevos

                await using var tx = await cn.BeginTransactionAsync(ct);
                await using var cmd = ConexionIot.Comando(cn,
                    "INSERT INTO lecturas (sensor_id, valor) SELECT id, @valor FROM sensores WHERE codigo = @codigo",
                    tx, ("@valor", 0m), ("@codigo", ""));
                await cmd.PrepareAsync(ct);
                foreach (var (codigo, umbral) in sensores)
                {
                    var r = azar.NextDouble();
                    var valor = umbral * (decimal)(0.76 + 0.08 * azar.NextDouble());
                    if (r < 0.02) valor = umbral * 1.10m;          // pico: advertencia
                    else if (r < 0.03) valor = umbral * 1.30m;     // pico: critica
                    cmd.Parameters[0].Value = Math.Round(valor, 2);
                    cmd.Parameters[1].Value = codigo;
                    total += await cmd.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                if (++rondas % 60 == 0)
                    log.LogInformation("Simulador: {Rondas} rondas, {Total} lecturas registradas", rondas, total);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException)
            {
                log.LogWarning("Simulador: {Mensaje}. Reintentando...", ex.Message);
            }
            await Task.Delay(1000, ct);
        }
    }

    private static async Task<List<(string, decimal)>> CargarSensoresAsync(DbConnection cn, CancellationToken ct)
    {
        var lista = new List<(string, decimal)>();
        await using var cmd = ConexionIot.Comando(cn, """
            SELECT s.codigo, s.umbral_max FROM sensores s
            JOIN dispositivos d ON d.id = s.dispositivo_id
            WHERE d.estado = 'activo' ORDER BY s.id
            """);
        await using var lector = await cmd.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            lista.Add((lector.GetString(0), lector.GetDecimal(1)));
        return lista;
    }
}
