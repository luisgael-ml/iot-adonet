using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace IotAdoNet.Datos;

/// <summary>
/// Acceso a datos con ADO.NET. Usa los dos modelos de ADO.NET:
///  - Conectado: DbCommand + DbDataReader (lectura rapida, hacia adelante).
///  - Desconectado: DbDataAdapter + DataTable + DbCommandBuilder (ver dispositivos).
/// Todas las consultas usan parametros (@nombre): el valor viaja aparte del SQL.
/// </summary>
public sealed class RepositorioIot(ConexionIot bd)
{
    // ============================ LEER ============================

    public async Task<Resumen> ResumenAsync()
    {
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, """
            SELECT (SELECT COUNT(*) FROM lecturas),
                   (SELECT COUNT(*) FROM lecturas WHERE medido_en > NOW() - INTERVAL '1 minute'),
                   (SELECT COUNT(*) FROM alertas WHERE NOT atendida),
                   (SELECT COUNT(*) FROM alertas WHERE NOT atendida AND severidad = 'critica'),
                   (SELECT COUNT(*) FROM dispositivos WHERE estado = 'activo'),
                   (SELECT COUNT(*) FROM sensores)
            """);
        await using var lector = await cmd.ExecuteReaderAsync();
        await lector.ReadAsync();
        return new Resumen(lector.GetInt64(0), lector.GetInt64(1), lector.GetInt64(2),
                           lector.GetInt64(3), lector.GetInt64(4), lector.GetInt64(5));
    }

    /// <summary>Sensores con su ultima lectura (DataReader: modelo conectado).</summary>
    public async Task<List<SensorFila>> SensoresAsync()
    {
        var lista = new List<SensorFila>();
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, """
            SELECT s.id, s.codigo, s.tipo, s.unidad, s.umbral_max, d.codigo, d.estado,
                   u.valor, to_char(u.medido_en, 'HH24:MI:SS')
            FROM sensores s
            JOIN dispositivos d ON d.id = s.dispositivo_id
            LEFT JOIN LATERAL (SELECT l.valor, l.medido_en FROM lecturas l
                               WHERE l.sensor_id = s.id
                               ORDER BY l.medido_en DESC LIMIT 1) u ON TRUE
            ORDER BY s.codigo
            """);
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            lista.Add(new SensorFila(
                lector.GetInt32(0), lector.GetString(1), lector.GetString(2), lector.GetString(3),
                lector.GetDecimal(4), lector.GetString(5), lector.GetString(6),
                lector.IsDBNull(7) ? null : lector.GetDecimal(7),     // NULL si no tiene lecturas
                lector.IsDBNull(8) ? null : lector.GetString(8)));
        }
        return lista;
    }

    public async Task<SensorFila?> SensorAsync(string codigo)
        => (await SensoresAsync()).FirstOrDefault(s => s.Codigo == codigo);

    public async Task<List<LecturaFila>> LecturasAsync(string codigo, int cuantas = 20)
    {
        var lista = new List<LecturaFila>();
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, """
            SELECT l.id, l.valor, to_char(l.medido_en, 'YYYY-MM-DD HH24:MI:SS'), a.severidad
            FROM lecturas l
            JOIN sensores s ON s.id = l.sensor_id
            LEFT JOIN alertas a ON a.lectura_id = l.id
            WHERE s.codigo = @codigo
            ORDER BY l.medido_en DESC
            LIMIT @cuantas
            """, null, ("@codigo", codigo), ("@cuantas", cuantas));
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            lista.Add(new LecturaFila(lector.GetInt64(0), lector.GetDecimal(1), lector.GetString(2),
                                      lector.IsDBNull(3) ? null : lector.GetString(3)));
        return lista;
    }

    public async Task<List<DispositivoOpcion>> DispositivosOpcionesAsync()
    {
        var lista = new List<DispositivoOpcion>();
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, "SELECT codigo, nombre FROM dispositivos ORDER BY codigo");
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            lista.Add(new DispositivoOpcion(lector.GetString(0), lector.GetString(1)));
        return lista;
    }

    public async Task<List<AlertaFila>> AlertasPendientesAsync()
    {
        var lista = new List<AlertaFila>();
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, """
            SELECT a.id, to_char(a.creada_en, 'HH24:MI:SS'), s.codigo, d.codigo, a.severidad, a.valor, a.umbral
            FROM alertas a
            JOIN sensores s ON s.id = a.sensor_id
            JOIN dispositivos d ON d.id = s.dispositivo_id
            WHERE NOT a.atendida
            ORDER BY (a.severidad = 'critica') DESC, a.creada_en DESC
            LIMIT 50
            """);
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            lista.Add(new AlertaFila(lector.GetInt32(0), lector.GetString(1), lector.GetString(2),
                                     lector.GetString(3), lector.GetString(4), lector.GetDecimal(5),
                                     lector.GetDecimal(6)));
        return lista;
    }

    /// <summary>Ultimos cambios registrados por el disparador de auditoria.</summary>
    public async Task<List<(string Hora, string Tabla, string Operacion, string Registro, string Cambios, string App)>> AuditoriaAsync()
    {
        var lista = new List<(string, string, string, string, string, string)>();
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, """
            SELECT to_char(ocurrido_en, 'HH24:MI:SS'), tabla, operacion, COALESCE(registro, ''),
                   CASE WHEN operacion = 'UPDATE' THEN cambios::text ELSE '' END, aplicacion
            FROM auditoria ORDER BY id DESC LIMIT 8
            """);
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            lista.Add((lector.GetString(0), lector.GetString(1), lector.GetString(2),
                       lector.GetString(3), lector.GetString(4), lector.GetString(5)));
        return lista;
    }

    // ============================ CREAR ============================

    /// <summary>Cada tipo de sensor tiene una sola unidad valida: no se captura a mano.</summary>
    public static readonly IReadOnlyDictionary<string, string> UnidadPorTipo = new Dictionary<string, string>
    {
        ["temperatura"] = "°C", ["vibracion"] = "mm/s", ["energia"] = "kWh", ["humedad"] = "%"
    };

    /// <summary>INSERT ... RETURNING con ExecuteScalar: devuelve el id generado.</summary>
    public async Task<int> CrearSensorAsync(string codigo, string dispositivo, string tipo, decimal umbral)
    {
        if (!UnidadPorTipo.TryGetValue(tipo, out var unidad))
            throw new ErrorDatos($"El tipo {tipo} no es valido.", null);
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, """
            INSERT INTO sensores (dispositivo_id, codigo, tipo, unidad, umbral_max)
            SELECT d.id, @codigo, @tipo, @unidad, @umbral FROM dispositivos d WHERE d.codigo = @dispositivo
            RETURNING id
            """, null, ("@codigo", codigo.Trim().ToUpperInvariant()), ("@tipo", tipo),
                       ("@unidad", unidad), ("@umbral", umbral), ("@dispositivo", dispositivo));
        var id = await Ejecutar(() => cmd.ExecuteScalarAsync());
        return id is null ? throw new ErrorDatos($"El dispositivo {dispositivo} no existe.", null) : Convert.ToInt32(id);
    }

    /// <summary>Inserta la lectura y consulta si el disparador genero una alerta, en una transaccion.</summary>
    public async Task<(long Id, string Situacion)> RegistrarLecturaAsync(string codigo, decimal valor)
    {
        await using var cn = await bd.AbrirAsync();
        await using var tx = await cn.BeginTransactionAsync();
        await using var ins = ConexionIot.Comando(cn, """
            INSERT INTO lecturas (sensor_id, valor)
            SELECT id, @valor FROM sensores WHERE codigo = @codigo
            RETURNING id
            """, tx, ("@valor", valor), ("@codigo", codigo));
        var id = await Ejecutar(() => ins.ExecuteScalarAsync());
        if (id is null)
        {
            await tx.RollbackAsync();
            throw new ErrorDatos($"El sensor {codigo} no existe.", null);
        }
        await using var sev = ConexionIot.Comando(cn, "SELECT severidad FROM alertas WHERE lectura_id = @id",
                                                  tx, ("@id", Convert.ToInt64(id)));
        var severidad = await sev.ExecuteScalarAsync();
        await tx.CommitAsync();
        return (Convert.ToInt64(id), severidad as string ?? "normal");
    }

    // ============================ ACTUALIZAR ============================

    /// <summary>UPDATE con ExecuteNonQuery: devuelve el numero de filas afectadas.</summary>
    public async Task<decimal> ActualizarUmbralAsync(string codigo, decimal umbral)
    {
        await using var cn = await bd.AbrirAsync();
        await using var tx = await cn.BeginTransactionAsync();
        await using var leer = ConexionIot.Comando(cn,
            "SELECT umbral_max FROM sensores WHERE codigo = @codigo FOR UPDATE", tx, ("@codigo", codigo));
        var anterior = await leer.ExecuteScalarAsync();
        if (anterior is null)
            throw new ErrorDatos($"El sensor {codigo} no existe.", null);

        await using var upd = ConexionIot.Comando(cn,
            "UPDATE sensores SET umbral_max = @umbral WHERE codigo = @codigo", tx,
            ("@umbral", umbral), ("@codigo", codigo));
        await Ejecutar(() => upd.ExecuteNonQueryAsync());
        await tx.CommitAsync();
        return Convert.ToDecimal(anterior);
    }

    public async Task<int> AtenderAlertaAsync(int id)
    {
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn,
            "UPDATE alertas SET atendida = TRUE WHERE id = @id AND NOT atendida", null, ("@id", id));
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> AtenderTodasAsync()
    {
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, "UPDATE alertas SET atendida = TRUE WHERE NOT atendida");
        return await cmd.ExecuteNonQueryAsync();
    }

    // ---------- Modelo desconectado: DataAdapter + DataTable + CommandBuilder ----------

    /// <summary>Llena un DataTable con los dispositivos; la conexion se cierra al terminar Fill.</summary>
    public async Task<DataTable> DispositivosTablaAsync()
    {
        await using var cn = await bd.AbrirAsync();
        using var adaptador = bd.Fabrica.CreateDataAdapter()!;
        adaptador.SelectCommand = ConexionIot.Comando(cn,
            "SELECT id, codigo, nombre, ubicacion, estado FROM dispositivos ORDER BY codigo");
        var tabla = new DataTable("dispositivos");
        adaptador.Fill(tabla);
        return tabla;
    }

    /// <summary>
    /// Modifica las filas en memoria y las guarda con DataAdapter.Update. El CommandBuilder
    /// genera el UPDATE a partir del SELECT; solo se envian las filas con RowState = Modified.
    /// </summary>
    public async Task<(int Modificadas, string SqlGenerado)> GuardarEstadosAsync(IDictionary<string, string> nuevos)
    {
        await using var cn = await bd.AbrirAsync();
        await using var tx = await cn.BeginTransactionAsync();
        using var adaptador = bd.Fabrica.CreateDataAdapter()!;
        adaptador.SelectCommand = ConexionIot.Comando(cn,
            "SELECT id, codigo, nombre, ubicacion, estado FROM dispositivos ORDER BY codigo", tx);
        using var constructor = bd.Fabrica.CreateCommandBuilder()!;
        constructor.DataAdapter = adaptador;

        var tabla = new DataTable("dispositivos");
        adaptador.Fill(tabla);
        foreach (DataRow fila in tabla.Rows)
        {
            var codigo = (string)fila["codigo"];
            if (nuevos.TryGetValue(codigo, out var estado) && estado != (string)fila["estado"])
                fila["estado"] = estado;                       // RowState pasa a Modified
        }

        var actualizar = constructor.GetUpdateCommand();
        actualizar.Transaction = tx;
        adaptador.UpdateCommand = actualizar;
        try
        {
            var n = await Ejecutar(() => Task.FromResult(adaptador.Update(tabla)));
            await tx.CommitAsync();
            return (n, actualizar.CommandText);
        }
        catch (DBConcurrencyException)
        {
            // el UPDATE generado compara los valores originales: si otro usuario cambio la fila, falla
            await tx.RollbackAsync();
            throw new ErrorDatos("Otro usuario modifico el dispositivo mientras lo editabas. Recarga la pagina.", null);
        }
    }

    // ============================ ELIMINAR ============================

    public async Task<int> EliminarLecturaAsync(long id)
    {
        await using var cn = await bd.AbrirAsync();
        await using var cmd = ConexionIot.Comando(cn, "DELETE FROM lecturas WHERE id = @id", null, ("@id", id));
        return await Ejecutar(() => cmd.ExecuteNonQueryAsync());
    }

    /// <summary>
    /// Sin conLecturas, la llave foranea rechaza el borrado si hay lecturas (23503).
    /// Con conLecturas, borra alertas, lecturas y sensor en UNA transaccion: todo o nada.
    /// </summary>
    public async Task<(int Lecturas, int Alertas)> EliminarSensorAsync(string codigo, bool conLecturas)
    {
        await using var cn = await bd.AbrirAsync();
        await using var tx = await cn.BeginTransactionAsync();
        int lecturas = 0, alertas = 0;
        try
        {
            if (conLecturas)
            {
                await using var a = ConexionIot.Comando(cn,
                    "DELETE FROM alertas WHERE sensor_id = (SELECT id FROM sensores WHERE codigo = @c)", tx, ("@c", codigo));
                alertas = await a.ExecuteNonQueryAsync();
                await using var l = ConexionIot.Comando(cn,
                    "DELETE FROM lecturas WHERE sensor_id = (SELECT id FROM sensores WHERE codigo = @c)", tx, ("@c", codigo));
                lecturas = await l.ExecuteNonQueryAsync();
            }
            await using var s = ConexionIot.Comando(cn, "DELETE FROM sensores WHERE codigo = @c", tx, ("@c", codigo));
            var n = await Ejecutar(() => s.ExecuteNonQueryAsync());
            if (n == 0) throw new ErrorDatos($"El sensor {codigo} no existe.", null);
            await tx.CommitAsync();
            return (lecturas, alertas);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ============================ RENDIMIENTO ============================

    /// <summary>
    /// Inserta n lecturas de cuatro formas y las elimina al final:
    ///  A. un comando por lectura, cada uno con su propia transaccion (autocommit)
    ///  B. un comando por lectura dentro de una transaccion
    ///  C. comando preparado (Prepare) reutilizado dentro de una transaccion
    ///  D. DbBatch: todas las sentencias en un solo viaje al servidor
    /// </summary>
    public async Task<List<(string Forma, double Ms)>> BenchmarkAsync(int n)
    {
        const string sql = "INSERT INTO lecturas (sensor_id, valor) SELECT id, @valor FROM sensores WHERE codigo = 'PRE03-VIB'";
        var resultados = new List<(string, double)>();
        await using var cn = await bd.AbrirAsync();
        await using var max = ConexionIot.Comando(cn, "SELECT COALESCE(MAX(id), 0) FROM lecturas");
        var desde = Convert.ToInt64(await max.ExecuteScalarAsync());
        var reloj = Stopwatch.StartNew();

        for (var i = 0; i < n; i++)
        {
            await using var cmd = ConexionIot.Comando(cn, sql, null, ("@valor", 5m + i % 10 / 10m));
            await cmd.ExecuteNonQueryAsync();
        }
        resultados.Add(("A. Un comando por lectura (autocommit)", reloj.Elapsed.TotalMilliseconds));

        reloj.Restart();
        await using (var tx = await cn.BeginTransactionAsync())
        {
            for (var i = 0; i < n; i++)
            {
                await using var cmd = ConexionIot.Comando(cn, sql, tx, ("@valor", 5m + i % 10 / 10m));
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        resultados.Add(("B. Un comando por lectura, una transaccion", reloj.Elapsed.TotalMilliseconds));

        reloj.Restart();
        await using (var tx = await cn.BeginTransactionAsync())
        {
            await using var cmd = ConexionIot.Comando(cn, sql, tx, ("@valor", 5m));
            await cmd.PrepareAsync();                   // el servidor analiza el SQL una sola vez
            for (var i = 0; i < n; i++)
            {
                cmd.Parameters[0].Value = 5m + i % 10 / 10m;
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        resultados.Add(("C. Comando preparado, una transaccion", reloj.Elapsed.TotalMilliseconds));

        reloj.Restart();
        if (bd.Fabrica.CanCreateBatch)
        {
            await using var lote = cn.CreateBatch();
            for (var i = 0; i < n; i++)
            {
                var bc = lote.CreateBatchCommand();
                bc.CommandText = sql;
                var p = bc.CreateParameter();
                p.ParameterName = "@valor";
                p.Value = 5m + i % 10 / 10m;
                bc.Parameters.Add(p);
                lote.BatchCommands.Add(bc);
            }
            await lote.ExecuteNonQueryAsync();          // un solo viaje de red, una transaccion implicita
            resultados.Add(("D. DbBatch (un solo viaje al servidor)", reloj.Elapsed.TotalMilliseconds));
        }

        await using var limpiar = ConexionIot.Comando(cn,
            "DELETE FROM lecturas WHERE id > @desde AND sensor_id = (SELECT id FROM sensores WHERE codigo = 'PRE03-VIB')",
            null, ("@desde", desde));
        await limpiar.ExecuteNonQueryAsync();
        return resultados;
    }

    // ============================ ERRORES ============================

    /// <summary>Ejecuta una operacion y traduce los errores de PostgreSQL por su SQLSTATE.</summary>
    private static async Task<T> Ejecutar<T>(Func<Task<T>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (DbException ex)
        {
            var mensaje = ex.SqlState switch
            {
                "23505" => "Ya existe un registro con ese codigo.",
                "23503" => "El registro tiene datos que dependen de el (lecturas o alertas).",
                "23514" => "El valor no cumple una regla de la tabla (CHECK): revisa el tipo, el umbral o el estado.",
                "22P02" => "El formato del dato no es valido.",
                _ => "Error de base de datos."
            };
            throw new ErrorDatos($"{mensaje} [SQLSTATE {ex.SqlState}]", ex.SqlState);
        }
    }
}
