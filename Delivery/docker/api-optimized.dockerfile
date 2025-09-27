# Ultra-optimized ARM32 Dockerfile - Minimal warnings, faster build for MediaButler API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy only required projects (avoid solution issues)
COPY src/MediaButler.API/MediaButler.API.csproj ./src/MediaButler.API/
COPY src/MediaButler.Core/MediaButler.Core.csproj ./src/MediaButler.Core/
COPY src/MediaButler.Data/MediaButler.Data.csproj ./src/MediaButler.Data/
COPY src/MediaButler.Services/MediaButler.Services.csproj ./src/MediaButler.Services/
COPY src/MediaButler.ML/MediaButler.ML.csproj ./src/MediaButler.ML/

# Restore with warning suppression for cleaner output
WORKDIR /src/src/MediaButler.API
RUN dotnet restore --verbosity quiet

# Copy source code
WORKDIR /src
COPY src/MediaButler.API/ ./src/MediaButler.API/
COPY src/MediaButler.Core/ ./src/MediaButler.Core/
COPY src/MediaButler.Data/ ./src/MediaButler.Data/
COPY src/MediaButler.Services/ ./src/MediaButler.Services/
COPY src/MediaButler.ML/ ./src/MediaButler.ML/

# Build with warning suppression for production
WORKDIR /src/src/MediaButler.API
RUN dotnet publish \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --verbosity quiet \
    -p:TreatWarningsAsErrors=false \
    -p:WarningLevel=0

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# ARM32 optimized environment
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_GCServer=0 \
    DOTNET_GCConcurrent=1 \
    ASPNETCORE_URLS=http://+:8080

WORKDIR /app
COPY --from=build /app/publish .

# Create data directories and set permissions
RUN mkdir -p /data /data/logs && \
    chmod 755 /data /data/logs

EXPOSE 8080

# Health check for monitoring
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MediaButler.API.dll"]