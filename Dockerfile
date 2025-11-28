# Build – versão mais nova do SDK 8.0 (funciona 100%)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o .csproj correto
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

# Copia o resto
COPY . ./

# Publica
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Runtime – versão mais nova do ASP.NET 8.0
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "VEA.API.dll"]
