# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o .csproj da pasta correta
COPY vea-backend/VeaBackend.csproj ./
RUN dotnet restore VeaBackend.csproj

# Copia o resto do código
COPY vea-backend/ ./

RUN dotnet publish VeaBackend.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "VeaBackend.dll"]
