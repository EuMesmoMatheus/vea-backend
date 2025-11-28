# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# 1. Copia só o csproj primeiro (cache perfeito)
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

#2. Copia tudo que realmente precisa (sem .git, bin, obj, etc)
COPY Controllers/    Controllers/
COPY Data/          Data/
COPY Models/        Models/
COPY Services/      Services/
COPY Properties/    Properties/
COPY wwwroot/       wwwroot/
COPY Program.cs     ./
COPY appsettings*.json ./

#3. Publish
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore --self-contained false

# Etapa final
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Non-root user (você já tinha, tá perfeito)
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S -G appgroup appuser && \
    chown -R appuser:appgroup /app

# Obrigatório na nuvem
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

USER appuser

ENTRYPOINT ["dotnet", "VEA.API.dll"]