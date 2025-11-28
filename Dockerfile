# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# 1. Copia apenas o(s) projeto(s) necessários para restore
COPY VEA.API.csproj ./
# Se tiver mais projetos que o principal referencia, copie também:
# COPY ../OutroProjeto/OutroProjeto.csproj ../OutroProjeto/

# Restore (aproveita cache)
RUN dotnet restore VEA.API.csproj

# 2. Agora copia apenas o código-fonte necessário (sem .git, bin, obj, etc graças ao .dockerignore)
COPY . ./

# Compila e publica
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Etapa final (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Copia apenas o que foi publicado
COPY --from=build /app/publish ./

# Usuário não-root (boa prática, você já tem)
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S -G appgroup appuser && \
    chown -R appuser:appgroup /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

USER appuser

ENTRYPOINT ["dotnet", "VEA.API.dll"]