# Build from the repo root: docker build -f deploy/docker/ApiHost.Dockerfile -t k9crush-api-host .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/Host/K9Crush.Api.Host/K9Crush.Api.Host.csproj
RUN dotnet publish src/Host/K9Crush.Api.Host/K9Crush.Api.Host.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# NOTE on WolverineFx.RuntimeCompilation: this package is referenced
# unconditionally (not Debug-only - see the csproj comment for why), so
# its Roslyn compiler dependency is a normal published DLL alongside
# everything else `dotnet publish` copies in. Nothing special needed here
# for it to work in this lean runtime image - the earlier decision to
# avoid a Debug-only conditional is exactly what makes that true. If this
# ever moves to static codegen (the tracked production follow-up in
# GETTING_STARTED.md), this package - and this note - go away.
EXPOSE 8080
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "K9Crush.Api.Host.dll"]
