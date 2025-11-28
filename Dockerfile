# Build – tag oficial que funciona hoje no Railway
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY VEA.API.csproj ./
RUN dotnet restore VEA.API.csproj

COPY . ./
RUN dotnet publish VEA.API.csproj -c Release -o /app/publish --no-restore

# Runtime – tag oficial que funciona hoje no Railway
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "VEA.API.dll"]
