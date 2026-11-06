# ---------- Etapa 1: restaurar Npgsql y compilar con el SDK de .NET ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion
WORKDIR /src
COPY IotAdoNet.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ---------- Etapa 2: solo el runtime de ASP.NET Core ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=compilacion /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "IotAdoNet.dll"]
