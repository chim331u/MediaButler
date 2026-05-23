# MediaButler API - Minimal ARM32 Build (.NET 10)
# Optimized for QNAP NAS deployment (1GB RAM constraint)
# Target memory footprint: <150MB

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

# Set memory-conscious environment variables for build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_GCServer=0 \
    DOTNET_gcConcurrent=false \
    DOTNET_GCHeapHardLimit=200000000

# Copy all source files
COPY . .

# Build with extreme memory conservation and size optimizations
WORKDIR /source/src/MediaButler.API
RUN dotnet publish \
    --configuration Release \
    --output /app \
    --self-contained false \
    --verbosity quiet \
    /p:InvariantGlobalization=true \
    /p:MaxCpuCount=1 \
    /p:BuildInParallel=false \
    /p:UseSharedCompilation=false \
    /p:PublishReadyToRun=false \
    /p:PublishSingleFile=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

# =============================================================================
# RUNTIME STAGE - .NET 10 Runtime for ARM32
# =============================================================================

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
LABEL maintainer="MediaButler Team"
LABEL description="MediaButler API - QNAP NAS Optimized (Minimal Build)"
LABEL version="1.0.0"

# Install additional dependencies
RUN apk add --no-cache \
    ca-certificates \
    tzdata

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app ./

# Create required directories with proper permissions
RUN mkdir -p \
    /data/library \
    /data/watch \
    /data/temp \
    /data/processing \
    /logs \
    /models \
    /configs \
    && chown -R $APP_UID:$APP_UID \
        /data \
        /logs \
        /models \
        /configs \
    && chmod -R 755 \
        /data \
        /logs \
        /models \
        /configs

# Switch to non-root user for security
USER $APP_UID

# Expose API port
EXPOSE 8080

# Environment variables for 1GB RAM optimization
ENV \
    # .NET 10 Runtime optimizations (DATAS GC enabled by default)
    DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=0 \
    DOTNET_gcConcurrent=false \
    DOTNET_GCHeapHardLimit=140000000 \
    DOTNET_GCHighMemPercent=75 \
    DOTNET_GCConserveMemory=9 \
    DOTNET_ThreadPool_ForceMinWorkerThreads=4 \
    DOTNET_ThreadPool_ForceMaxWorkerThreads=8 \
    # ASP.NET Core optimizations
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HOSTINGSTARTUPASSEMBLIES= \
    ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS=true \
    # MediaButler ARM32 optimizations
    MediaButler__Performance__MaxConcurrentOperations=2 \
    MediaButler__Performance__MaxMemoryUsageMB=140 \
    MediaButler__ML__MaxBatchSize=10 \
    MediaButler__FileDiscovery__MaxConcurrentScans=1 \
    # Timezone
    TZ=UTC

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:8080/health || exit 1

# Signal handling for graceful shutdown
STOPSIGNAL SIGTERM

# Start the application
ENTRYPOINT ["dotnet", "MediaButler.API.dll"]

# Add OpenContainers metadata labels
LABEL org.opencontainers.image.title="MediaButler API (Minimal)"
LABEL org.opencontainers.image.description="Intelligent TV series file organization API optimized for QNAP NAS (ARM32)"
LABEL org.opencontainers.image.version="1.0.0"
LABEL org.opencontainers.image.vendor="MediaButler"
LABEL org.opencontainers.image.licenses="MIT"
LABEL org.opencontainers.image.source="https://github.com/chim331u/MediaButler"
