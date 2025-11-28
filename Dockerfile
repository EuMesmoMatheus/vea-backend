# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Só copia o que realmente precisa para o restore (cache)
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

# Agora copia o resto do código-fonte
COPY . ./

# Publica a aplicação
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Etapa final (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Copia APENAS a pasta já publicada (nunca copia código-fonte, .git, secrets, etc)
COPY --from=build /app/publish ./

# Cria usuário não-root e ajusta permissões
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S -G appgroup appuser && \
    chown -R appuser:appgroup /app

# Porta que o Railway espera
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Roda como usuário sem privilégios
USER appuser

# Start
ENTRYPOINT ["dotnet", "VEA.API.dll"]