# EMS.API container for Render (or any Docker host).
# Build context = repository root.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so dependency layers cache between builds.
COPY EMS.API/EMS.API.csproj EMS.API/
COPY EMS.Shared/EMS.Shared.csproj EMS.Shared/
RUN dotnet restore EMS.API/EMS.API.csproj

COPY EMS.API/ EMS.API/
COPY EMS.Shared/ EMS.Shared/
RUN dotnet publish EMS.API/EMS.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Render injects PORT; bind to it (8080 fallback for local docker run).
CMD ["/bin/sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet EMS.API.dll"]
