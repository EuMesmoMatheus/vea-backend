# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# 1. Restore (cache máximo)
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

# 2. Copia só o necessário (Sonar fica feliz e imagem fica leve)
COPY Program.cs ./
COPY appsettings*.json ./
COPY Controllers ./Controllers/
COPY Models ./Models/
COPY Services ./Services/
COPY wwwroot ./wwwroot/

# 3. Build & Publish
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Etapa final - runtime leve e segura
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Copia só o que foi publicado
COPY --from=build /app/publish ./

# Cria usuário não-root
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S -G appgroup appuser && \
    chown -R appuser:appgroup /app

# Railway usa a variável PORT dinamicamente
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

# Roda sem root
USER appuser

ENTRYPOINT ["dotnet", "VEA.API.dll"]