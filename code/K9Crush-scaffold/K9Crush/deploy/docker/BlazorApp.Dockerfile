# Build from the repo root: docker build -f deploy/docker/BlazorApp.Dockerfile -t k9crush-blazor-app .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/Web/K9Crush.Blazor.App/K9Crush.Blazor.App.csproj
RUN dotnet publish src/Web/K9Crush.Blazor.App/K9Crush.Blazor.App.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "K9Crush.Blazor.App.dll"]
