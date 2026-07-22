# Build from the repo root: docker build -f deploy/docker/Gateway.Dockerfile -t k9crush-gateway .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the whole solution rather than hand-picking individual .csproj files -
# simpler and less error-prone for a 12-project modular monolith with
# cross-project references than trying to optimize layer caching here.
# .dockerignore already strips bin/obj/tests/deploy/docs from the context.
COPY . .

RUN dotnet restore src/Gateway/K9Crush.Gateway/K9Crush.Gateway.csproj
RUN dotnet publish src/Gateway/K9Crush.Gateway/K9Crush.Gateway.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Official .NET 8+/10 ASP.NET Core images default ASPNETCORE_HTTP_PORTS to
# 8080 - no extra config needed for the app to listen there.
EXPOSE 8080

# Run as non-root - the aspnet base image ships an unprivileged "app" user
# for exactly this purpose since .NET 8's container image updates.
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "K9Crush.Gateway.dll"]
