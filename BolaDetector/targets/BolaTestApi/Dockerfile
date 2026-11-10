FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish targets/BolaTestApi/BolaTestApi.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "BolaTestApi.dll"]
