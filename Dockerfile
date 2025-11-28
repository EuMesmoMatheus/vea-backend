# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copia só o csproj e restaura dependências (cache eficiente)
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

# Copia o resto do código e publica
COPY . ./
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Etapa final (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Cria usuário não-root (resolve o alerta do SonarQube)
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S -G appgroup appuser && \
    chown -R appuser:appgroup /app

# Expõe a porta que o Railway e o ASP.NET Core esperam
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Roda como usuário não privilegiado ← isso mata o hotspot do Sonar
USER appuser

# Comando de inicialização (exatamente igual ao que você já usava)
ENTRYPOINT ["dotnet", "VEA.API.dll"]