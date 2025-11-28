# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o .csproj correto (que tá na raiz)
COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

# Copia todo o resto (que tá na raiz)
COPY . ./

# Publica
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "VEA.API.dll"]
