# =============================================================================
# Stage 1 — Build  (SDK completo para restore/build/publish)
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas os .csproj primeiro — camada de cache invalidada só quando
# dependências mudam, não a cada alteração de código.
COPY ["src/HidrometroApp.Api/HidrometroApp.Api.csproj",                        "src/HidrometroApp.Api/"]
COPY ["src/HidrometroApp.Core/HidrometroApp.Core.csproj",                      "src/HidrometroApp.Core/"]
COPY ["src/HidrometroApp.Infrastructure/HidrometroApp.Infrastructure.csproj",  "src/HidrometroApp.Infrastructure/"]

RUN dotnet restore "src/HidrometroApp.Api/HidrometroApp.Api.csproj"

# Copia todo o código-fonte e compila
COPY src/ src/

RUN dotnet build "src/HidrometroApp.Api/HidrometroApp.Api.csproj" \
        -c Release \
        --no-restore \
        -o /app/build

RUN dotnet publish "src/HidrometroApp.Api/HidrometroApp.Api.csproj" \
        -c Release \
        --no-restore \
        --no-build \
        -o /app/publish

# =============================================================================
# Stage 2 — Runtime  (imagem mínima ASP.NET, sem SDK)
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Railway expõe a porta 8080 por padrão.
# Se a Railway injetar $PORT em runtime, sobrescreva ASPNETCORE_URLS no dashboard:
#   ASPNETCORE_URLS=http://+:$PORT
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

# Diretórios de dados — montados como volume em produção ou usados diretamente
RUN mkdir -p /app/storage/fotos /app/storage/logs

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "HidrometroApp.Api.dll"]
