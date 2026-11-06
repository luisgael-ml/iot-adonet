namespace IotAdoNet.Datos;

public sealed record Resumen(long Lecturas, long UltimoMinuto, long AlertasPendientes,
                             long Criticas, long DispositivosActivos, long Sensores);

public sealed record SensorFila(int Id, string Codigo, string Tipo, string Unidad, decimal Umbral,
                                string Dispositivo, string Estado, decimal? UltimoValor, string? Hora);

public sealed record LecturaFila(long Id, decimal Valor, string Fecha, string? Severidad);

public sealed record AlertaFila(int Id, string Hora, string Sensor, string Dispositivo,
                                string Severidad, decimal Valor, decimal Umbral);

public sealed record DispositivoOpcion(string Codigo, string Nombre);

/// <summary>Error de base de datos traducido a un mensaje para el usuario.</summary>
public sealed class ErrorDatos(string mensaje, string? sqlState) : Exception(mensaje)
{
    public string? SqlState { get; } = sqlState;
}
