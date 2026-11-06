# ---------- Etapa 1: compilar el programa de Visual Basic .NET ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion
WORKDIR /src
COPY ReporteVB.vbproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ---------- Etapa 2: solo el runtime de .NET ----------
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=compilacion /app .
# comando corto: docker exec adonet-vb reporte-vb <comando>
RUN printf '#!/bin/sh\nexec dotnet /app/ReporteVB.dll "$@"\n' > /usr/local/bin/reporte-vb \
 && chmod +x /usr/local/bin/reporte-vb
CMD ["sleep", "infinity"]
