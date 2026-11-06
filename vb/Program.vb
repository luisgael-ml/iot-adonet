' =====================================================================
' TAREA 11 · ADO.NET con Visual Basic .NET
' IoT Analytics Dashboard (NexoDev Solutions)
'
' Cliente de consola que usa el MODELO DESCONECTADO de ADO.NET:
'   reporte              DataSet con dos DataTable y un DataRelation
'   ajustar <tipo> <%>   cambia umbrales en memoria y los guarda con DataAdapter.Update
'   xml                  exporta el DataSet a XML (WriteXml)
'   lector               la misma consulta con DataReader (modelo conectado)
' =====================================================================
Imports System.Data
Imports System.Data.Common
Imports Npgsql

Module Program

    Private Fabrica As DbProviderFactory
    Private Cadena As String

    Function Main(args As String()) As Integer
        ' Mismo modelo de proveedores que el cliente en C#
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance)
        Fabrica = DbProviderFactories.GetFactory("Npgsql")
        Cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Iot")
        If String.IsNullOrEmpty(Cadena) Then
            Console.Error.WriteLine("Falta la variable ConnectionStrings__Iot")
            Return 2
        End If

        Dim comando = If(args.Length > 0, args(0), "reporte")
        Try
            Select Case comando
                Case "reporte" : Reporte()
                Case "ajustar"
                    If args.Length < 3 Then
                        Console.WriteLine("Uso: reporte-vb ajustar <tipo> <porcentaje>")
                        Return 1
                    End If
                    Ajustar(args(1), Decimal.Parse(args(2), Globalization.CultureInfo.InvariantCulture))
                Case "xml" : ExportarXml()
                Case "lector" : Lector()
                Case Else
                    Console.WriteLine("Comandos: reporte | ajustar <tipo> <porcentaje> | xml | lector")
                    Return 1
            End Select
            Return 0
        Catch ex As DbException
            Console.Error.WriteLine($"Error de base de datos [SQLSTATE {ex.SqlState}]: {ex.Message}")
            Return 1
        End Try
    End Function

    Private Function Conectar() As DbConnection
        Dim cn = Fabrica.CreateConnection()
        cn.ConnectionString = Cadena
        cn.Open()
        Return cn
    End Function

    Private Function Adaptador(cn As DbConnection, sql As String) As DbDataAdapter
        Dim cmd = cn.CreateCommand()
        cmd.CommandText = sql
        Dim da = Fabrica.CreateDataAdapter()
        da.SelectCommand = cmd
        Return da
    End Function

    ''' <summary>Carga dos tablas en un DataSet, las relaciona y las recorre sin conexion.</summary>
    Private Function CargarDataSet() As DataSet
        Dim ds As New DataSet("IotAnalytics")
        Using cn = Conectar()
            Adaptador(cn, "SELECT id, codigo, nombre, ubicacion, estado FROM dispositivos ORDER BY codigo") _
                .Fill(ds, "dispositivos")
            Adaptador(cn, "SELECT s.id, s.dispositivo_id, s.codigo, s.tipo, s.unidad, s.umbral_max, " &
                          "(SELECT ROUND(AVG(l.valor), 2) FROM lecturas l " &
                          " WHERE l.sensor_id = s.id AND l.medido_en > NOW() - INTERVAL '1 hour') AS promedio_hora " &
                          "FROM sensores s ORDER BY s.codigo") _
                .Fill(ds, "sensores")
        End Using                                  ' la conexion se cierra aqui

        ' DataRelation: une las tablas en memoria, como una llave foranea
        ds.Relations.Add("dispositivo_sensores",
                         ds.Tables("dispositivos").Columns("id"),
                         ds.Tables("sensores").Columns("dispositivo_id"))
        Return ds
    End Function

    Private Sub Reporte()
        Dim ds = CargarDataSet()
        Console.WriteLine("REPORTE DE DISPOSITIVOS Y SENSORES (DataSet desconectado)")
        Console.WriteLine(New String("="c, 64))
        For Each d As DataRow In ds.Tables("dispositivos").Rows
            Console.WriteLine($"{d("codigo")} - {d("nombre")} ({d("ubicacion")}) [{d("estado")}]")
            For Each s As DataRow In d.GetChildRows("dispositivo_sensores")
                Dim promedio = If(s.IsNull("promedio_hora"), "sin datos",
                                  CDec(s("promedio_hora")).ToString("0.00"))
                Console.WriteLine($"   {s("codigo"),-11} {s("tipo"),-12} umbral {CDec(s("umbral_max")),8:0.00} {s("unidad"),-5}" &
                                  $" promedio 1 h: {promedio}")
            Next
        Next
        ' Calculo sobre los datos en memoria, sin volver a consultar la base
        Dim total = ds.Tables("sensores").Compute("COUNT(id)", "")
        Console.WriteLine(New String("-"c, 64))
        Console.WriteLine($"{ds.Tables("dispositivos").Rows.Count} dispositivos, {total} sensores")
    End Sub

    ''' <summary>
    ''' Ajusta en memoria el umbral de todos los sensores de un tipo y guarda los cambios
    ''' con DataAdapter.Update. El CommandBuilder genera el UPDATE y solo se envian las
    ''' filas con RowState = Modified, todo dentro de una transaccion.
    ''' </summary>
    Private Sub Ajustar(tipo As String, porcentaje As Decimal)
        Using cn = Conectar()
            Using tx = cn.BeginTransaction()
                Dim da = Adaptador(cn, "SELECT id, codigo, tipo, umbral_max FROM sensores ORDER BY codigo")
                da.SelectCommand.Transaction = tx
                Dim cb = Fabrica.CreateCommandBuilder()
                cb.DataAdapter = da

                Dim tabla As New DataTable("sensores")
                da.Fill(tabla)

                For Each fila As DataRow In tabla.Select($"tipo = '{tipo.Replace("'", "''")}'")
                    Dim antes = CDec(fila("umbral_max"))
                    fila("umbral_max") = Math.Round(antes * (1D + porcentaje / 100D), 2)
                    Console.WriteLine($"  {fila("codigo"),-11} {antes,8:0.00} -> {CDec(fila("umbral_max")),8:0.00}  RowState = {fila.RowState}")
                Next

                Dim modificadas = tabla.GetChanges(DataRowState.Modified)
                Console.WriteLine($"Filas modificadas en memoria: {If(modificadas Is Nothing, 0, modificadas.Rows.Count)}")

                Dim upd = cb.GetUpdateCommand()
                upd.Transaction = tx
                da.UpdateCommand = upd
                Dim enviadas = da.Update(tabla)
                tx.Commit()
                Console.WriteLine($"DataAdapter.Update envio {enviadas} UPDATE al servidor y se confirmo la transaccion.")
                Console.WriteLine($"UPDATE generado por el CommandBuilder:{Environment.NewLine}  {upd.CommandText}")
            End Using
        End Using
    End Sub

    ''' <summary>ADO.NET y XML: el DataSet se puede guardar o enviar como XML.</summary>
    Private Sub ExportarXml()
        Dim ds = CargarDataSet()
        ds.Relations.Clear()
        ds.Tables("sensores").Columns.Remove("promedio_hora")
        ds.WriteXml(Console.Out, XmlWriteMode.IgnoreSchema)
        Console.WriteLine()
    End Sub

    ''' <summary>Modelo conectado: DataReader recorre el resultado mientras la conexion esta abierta.</summary>
    Private Sub Lector()
        Using cn = Conectar()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "SELECT s.codigo, COUNT(l.id), MAX(l.valor) FROM sensores s " &
                                  "LEFT JOIN lecturas l ON l.sensor_id = s.id AND l.medido_en > NOW() - INTERVAL '10 minutes' " &
                                  "WHERE s.tipo = @tipo GROUP BY s.codigo ORDER BY s.codigo"
                Dim p = cmd.CreateParameter()
                p.ParameterName = "@tipo"
                p.Value = "temperatura"
                cmd.Parameters.Add(p)
                Using dr = cmd.ExecuteReader()
                    Console.WriteLine("Sensores de temperatura, ultimos 10 minutos (DataReader):")
                    While dr.Read()
                        Dim maximo = If(dr.IsDBNull(2), "-", dr.GetDecimal(2).ToString("0.00"))
                        Console.WriteLine($"  {dr.GetString(0),-11} {dr.GetInt64(1),5} lecturas   maximo {maximo}")
                    End While
                End Using
            End Using
        End Using
    End Sub

End Module
