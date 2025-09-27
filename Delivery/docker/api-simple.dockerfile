# Simple ARM32 Dockerfile - Balanced approach for MediaButler API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy solution and project files
COPY MediaButler.sln ./
COPY src/MediaButler.API/MediaButler.API.csproj ./src/MediaButler.API/
COPY src/MediaButler.Core/MediaButler.Core.csproj ./src/MediaButler.Core/
COPY src/MediaButler.Data/MediaButler.Data.csproj ./src/MediaButler.Data/
COPY src/MediaButler.Services/MediaButler.Services.csproj ./src/MediaButler.Services/
COPY src/MediaButler.ML/MediaButler.ML.csproj ./src/MediaButler.ML/

# Restore dependencies
RUN dotnet restore src/MediaButler.API/MediaButler.API.csproj

# Copy source code
COPY src/ ./src/

# Build and publish
WORKDIR /src/src/MediaButler.API
RUN dotnet publish \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Basic ARM32 optimizations
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    ASPNETCORE_URLS=http://+:8080

WORKDIR /app
COPY --from=build /app/publish .

# Create required directories
RUN mkdir -p /data /data/logs

EXPOSE 8080

ENTRYPOINT ["dotnet", "MediaButler.API.dll"]