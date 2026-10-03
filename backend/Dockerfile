# Builds the Rhythm & Flow API into a small container that runs on any host (Render, Railway, Azure, AWS...).
# Build context is the "backend" folder:  docker build -t rhythmflow-api -f backend/Dockerfile backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RhythmFlow.Api/RhythmFlow.Api.csproj RhythmFlow.Api/
RUN dotnet restore RhythmFlow.Api/RhythmFlow.Api.csproj
COPY RhythmFlow.Api/ RhythmFlow.Api/
RUN dotnet publish RhythmFlow.Api/RhythmFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# Hosts such as Render tell the app which port to listen on through the PORT variable.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet RhythmFlow.Api.dll"]
