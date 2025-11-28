# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# -------------------------------------------------
# 1. Restore (máximo cache)
# -------------------------------------------------
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

# -------------------------------------------------
# 2. Copia explicitamente só o que precisa
#     → Sonar entende que é controlado e para de reclamar
#     → .dockerignore continua protegendo contra .git, secrets, bin/obj, etc.
# -------------------------------------------------
COPY Program.cs ./
COPY appsettings.json ./
COPY appsettings.Development.json ./         # pega appsettings.Development.json só se estiver fora do .dockerignore (mas você já bloqueou)

# Pastas do seu projeto (adiciona ou remove conforme precisar)
COPY Controllers ./Controllers/
COPY Models ./Models/
COPY Services ./Services/
COPY wwwroot ./wwwroot/
# Se no futuro criar mais pastas (ex: Data, Helpers, Extensions, Properties, etc.)
# COPY Data ./Data/
# COPY Properties ./Properties/

# -------------------------------------------------
# 3. Publica a aplicação
# -------------------------------------------------
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# -------------------------------------------------
# Etapa final (runtime) – imagem mínima e segura
# -------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Copia apenas o que foi publicado (nunca código-fonte nem arquivos sensíveis)
COPY --from=build /app/publish ./

# Cria usuário não-root (você já tinha, só deixei mais enxuto)
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S -G appgroup appuser && \
    chown -R appuser:appgroup /app

# Porta que o Railway (e outros) espera
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Roda sem privilégios
USER appuser

# Start
ENTRYPOINT ["dotnet", "VEA.API.dll"]