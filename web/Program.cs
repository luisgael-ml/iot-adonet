// =====================================================================
// TAREA 11 · Programacion del lado del cliente: ADO.NET con C#
// IoT Analytics Dashboard (NexoDev Solutions)
// Aplicacion web (interfaz grafica basica) que realiza operaciones CRUD
// sobre PostgreSQL usando ADO.NET puro: DbConnection, DbCommand,
// DbDataReader, DbDataAdapter y DbTransaction. Sin Entity Framework.
// =====================================================================
using System.Data.Common;
using IotAdoNet.Datos;
using IotAdoNet.Servicios;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Modelo de proveedores de ADO.NET: se registra el proveedor de PostgreSQL una sola vez
// y el resto del programa trabaja con las clases base de System.Data.Common.
DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
var fabrica = DbProviderFactories.GetFactory("Npgsql");

var cadena = builder.Configuration.GetConnectionString("Iot")
             ?? throw new InvalidOperationException("Falta la cadena de conexion ConnectionStrings__Iot");

builder.Services.AddSingleton(new ConexionIot(fabrica, cadena));
builder.Services.AddScoped<RepositorioIot>();
builder.Services.AddHostedService<SimuladorLecturas>();
builder.Services.AddRazorPages();

var app = builder.Build();
app.UseStaticFiles();
app.MapRazorPages();
app.Run();
