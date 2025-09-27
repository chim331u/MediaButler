# Minimal ARM32 Dockerfile - Single project build for MediaButler API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy all source files first (simplest approach)
COPY . .

# Build directly the API project without restore step
WORKDIR /src/src/MediaButler.API
RUN dotnet publish \
    --configuration Release \
    --output /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY --from=build /app/publish .

# Create data directory
RUN mkdir -p /data

# Set environment
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "MediaButler.API.dll"]