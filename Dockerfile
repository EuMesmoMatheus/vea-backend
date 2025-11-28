# Build - versão específica do .NET 8.0 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0.100 AS build
WORKDIR /src

# Copia o .csproj correto
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

# Copia todo o resto
COPY . ./

# Publica
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Runtime - versão específica do .NET 8.0 ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:8.0.100 AS final
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "VEA.API.dll"]
