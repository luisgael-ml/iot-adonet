using System.Data.Common;

namespace IotAdoNet.Datos;

/// <summary>
/// Crea y abre conexiones con la fabrica del proveedor. El pool de conexiones
/// de ADO.NET reutiliza las conexiones fisicas: abrir y cerrar es barato.
/// </summary>
public sealed class ConexionIot(DbProviderFactory fabrica, string cadena)
{
    public DbProviderFactory Fabrica { get; } = fabrica;

    public async Task<DbConnection> AbrirAsync(CancellationToken ct = default)
    {
        var conexion = Fabrica.CreateConnection()!;
        conexion.ConnectionString = cadena;
        await conexion.OpenAsync(ct);
        return conexion;
    }

    /// <summary>Crea un comando con parametros nombrados (@nombre): nunca se concatena texto.</summary>
    public static DbCommand Comando(DbConnection conexion, string sql, DbTransaction? tx = null,
                                    params (string Nombre, object? Valor)[] parametros)
    {
        var cmd = conexion.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        foreach (var (nombre, valor) in parametros)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = nombre;
            p.Value = valor ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
}
