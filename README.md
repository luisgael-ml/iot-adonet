# IoT Analytics Dashboard · ADO.NET con C# y Visual Basic (Tarea 11)

NexoDev Solutions · Programación de Base de Datos (TESE) · Luis Gael Monterrubio López

## Documentación

📄 [Abrir la documentación de la Tarea 11 (PDF)](docs/Documentacion_Tarea11.pdf)

Clientes **ADO.NET** (proveedor Npgsql) que realizan operaciones CRUD sobre PostgreSQL 16, sin Entity Framework:

- **web/** · C# · ASP.NET Core Razor Pages: interfaz gráfica básica, modelo **conectado** (`DbCommand`, `DbDataReader`, `ExecuteScalar`, `ExecuteNonQuery`, transacciones), modelo **desconectado** (`DbDataAdapter` + `DataTable` + `DbCommandBuilder`), `DbBatch` y un simulador en segundo plano.
- **vb/** · Visual Basic .NET: `DataSet` con `DataRelation`, cambios en memoria guardados con `DataAdapter.Update`, exportación a XML y `DataReader`.
- **grafana/** · paneles de monitoreo, incluida la auditoría de cada operación hecha desde los clientes.

```
Navegador ─► web (C#, ADO.NET) ─┐
reporte-vb (VB.NET, ADO.NET) ───┼─SSL─► PostgreSQL ◄── Grafana
                                │
            simulador (C#) ─────┘
```

## Levantar

```bash
docker compose up -d --build
```

- Aplicación web: http://localhost:8091
- Grafana: http://localhost:3011 (admin / admin123)
- Cliente VB: `docker exec adonet-vb reporte-vb reporte | ajustar <tipo> <porcentaje> | xml | lector`
