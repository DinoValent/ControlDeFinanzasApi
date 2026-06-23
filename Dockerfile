# Etapa 1: compilar el proyecto
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app

# Etapa 2: imagen liviana solo para ejecutar (no necesita el SDK completo)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

# Render asigna el puerto dinámicamente vía la variable PORT
ENV ASPNETCORE_URLS=http://+:$PORT
ENTRYPOINT ["dotnet", "GestorGastos.Api.dll"]