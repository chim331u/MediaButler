# MediaButler Mobile - API Access Module (Phase 1 Complete)

## Overview
Phase 1 of the API access module has been successfully implemented for the MediaButler.Mobile Android app. This provides core infrastructure for flexible, user-configurable API connections with automatic failover and live configuration updates.

## Implementation Status: ✅ COMPLETE

### Completed Components

#### 1. Data Models (Immutable Value Objects)
- ✅ `ApiEndpoint.cs` - Represents a single API endpoint configuration
  - Protocol, host, port, priority, enabled status
  - Validation method `IsValid()`
  - URL builder `GetBaseUrl()`
- ✅ `ConnectionSettings.cs` - Connection behavior settings
  - Health check path, timeout, retry attempts, retry delay
- ✅ `ApiConfiguration.cs` - Complete configuration state
  - List of endpoints, connection settings
  - Factory method `CreateDefault()` for first run
  - Helper method `GetEnabledEndpointsByPriority()`
- ✅ `ConnectionResult.cs` - Connection test result
  - Success/failure status, latency, error message
  - Factory methods `Success()` and `Failure()`

#### 2. Service Interfaces
- ✅ `IApiConfigurationService` - Configuration CRUD operations
  - Load, save, reload, add, remove, update endpoints
  - `ConfigurationChanged` event for live reload
  - First-run detection via `HasConfigurationAsync()`
- ✅ `IApiConnectionService` - Endpoint testing and failover
  - Discover active endpoint with priority-based failover
  - Test individual endpoints via health check
  - Test all endpoints for diagnostics
  - `ActiveEndpointChanged` event
- ✅ `IApiClient` - HTTP request wrapper
  - GET, POST, PUT, DELETE operations
  - Automatic retry and failover
  - JSON serialization/deserialization

#### 3. Service Implementations
- ✅ `ApiConfigurationService.cs` - JSON file persistence
  - Storage location: `FileSystem.AppDataDirectory/api_config.json`
  - Thread-safe file operations with `SemaphoreSlim`
  - In-memory caching for performance
  - Automatic default config creation on first run
- ✅ `ApiConnectionService.cs` - Health check implementation
  - Tests `/api/health` endpoint on each server
  - Network connectivity detection via `Connectivity.Current`
  - Priority-based failover (tries endpoints in order)
  - Validates JSON health check response
  - Caches active endpoint for performance
- ✅ `ApiClient.cs` - HTTP client wrapper
  - Uses `IHttpClientFactory` for efficient connection pooling
  - Automatic failover on 5xx errors or timeouts
  - Configurable retry logic (3 attempts, 1s delay)
  - Invalidates active endpoint on failure

#### 4. Dependency Injection Setup
- ✅ `MauiProgram.cs` updated with service registrations:
  ```csharp
  builder.Services.AddHttpClient();
  builder.Services.AddSingleton<IApiConfigurationService, ApiConfigurationService>();
  builder.Services.AddSingleton<IApiConnectionService, ApiConnectionService>();
  builder.Services.AddSingleton<IApiClient, ApiClient>();
  ```
- ✅ `App.xaml.cs` updated to initialize configuration on startup

#### 5. NuGet Packages Added
- ✅ `Microsoft.Extensions.Http` (v9.0.0) - IHttpClientFactory support

## Configuration File Structure

**Location**: `/data/user/0/com.mediabutler.mobile/files/api_config.json`

**Default Configuration** (created on first run):
```json
{
  "apiEndpoints": [
    {
      "name": "Local",
      "protocol": "http",
      "host": "192.168.1.100",
      "port": 5271,
      "priority": 1,
      "enabled": true
    }
  ],
  "connectionSettings": {
    "healthCheckPath": "/api/health",
    "timeoutSeconds": 5,
    "retryAttempts": 3,
    "retryDelayMs": 1000
  }
}
```

## Key Features Implemented

### 1. FileSystem.AppDataDirectory Storage ✅
- Configuration stored as JSON in app's private data directory
- Automatic creation of default config on first run
- Thread-safe read/write operations

### 2. Manual Reload Button Support ✅
- `IApiConfigurationService.ReloadConfigurationAsync()` method
- Raises `ConfigurationChanged` event on reload
- `ApiConnectionService` subscribes to event and invalidates active endpoint

### 3. Live Configuration Revalidation ✅
- All HTTP clients revalidate connections on config change
- Active endpoint cache is invalidated
- Next API request triggers automatic endpoint discovery
- No app restart required

### 4. Priority-Based Failover ✅
- Endpoints sorted by `Priority` field (1 = highest)
- Tests endpoints in order until one succeeds
- Automatic failover on connection loss during requests

### 5. Health Check Validation ✅
- Calls `/api/health` endpoint (MediaButler.API)
- Validates 200 OK response
- Parses JSON response for `"status": "healthy"`
- Measures latency for diagnostics

### 6. Network Connectivity Detection ✅
- Uses `Connectivity.Current.NetworkAccess` (MAUI)
- Returns appropriate error: "No internet connection" vs "API unreachable"

## Usage Examples

### Example 1: Load Configuration on Startup
```csharp
// In App.xaml.cs
public App(IApiConfigurationService configService)
{
    InitializeComponent();
    _configService = configService;
    await _configService.LoadConfigurationAsync();
}
```

### Example 2: Discover Active Endpoint
```csharp
// In a ViewModel or Page
var connectionService = ServiceProvider.GetService<IApiConnectionService>();
var result = await connectionService.DiscoverActiveEndpointAsync();

if (result.IsSuccess)
{
    Console.WriteLine($"Connected to {result.Endpoint.Name} ({result.LatencyMs}ms)");
}
else
{
    Console.WriteLine($"Connection failed: {result.ErrorMessage}");
}
```

### Example 3: Make API Request with Automatic Failover
```csharp
// In a Service class
public class FileService
{
    private readonly IApiClient _apiClient;

    public FileService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<FileDto>> GetFilesAsync()
    {
        // Automatic endpoint discovery, retry, and failover
        var files = await _apiClient.GetAsync<List<FileDto>>("/api/files");
        return files;
    }
}
```

### Example 4: Add New Endpoint via Settings UI
```csharp
// In Settings page code-behind
private async Task AddEndpointAsync()
{
    var newEndpoint = new ApiEndpoint
    {
        Name = "Remote NAS",
        Protocol = "https",
        Host = "mediabutler.mynas.com",
        Port = 443,
        Priority = 2,
        Enabled = true
    };

    await _configService.AddEndpointAsync(newEndpoint);
}
```

### Example 5: Manual Reload Configuration
```csharp
// In Settings page with "Reload" button
private async Task ReloadConfigurationAsync()
{
    await _configService.ReloadConfigurationAsync();
    // ConfigurationChanged event is raised automatically
    // ApiConnectionService invalidates active endpoint
    // Next API request discovers new active endpoint
}
```

## Architecture Alignment with "Simple Made Easy"

✅ **Values over State**: All models are immutable records
✅ **Declarative**: Configuration defined in JSON, not code
✅ **Compose, Don't Complect**: Services have single responsibilities
✅ **Simple Dependencies**: Clear service hierarchy (Config → Connection → Client → Features)
✅ **No Hidden State**: Active endpoint explicitly cached and observable
✅ **Event-Driven Reload**: `ConfigurationChanged` event for loose coupling

## Build Status
- ✅ Project builds successfully
- ✅ No compilation errors
- ✅ All services registered in DI container
- ✅ Configuration initialized on app startup

## Next Steps (Phase 2 - First-Run Experience)

The following components are planned for Phase 2:
1. Create `FirstRunSetupPage.xaml` (manual endpoint entry form)
2. Implement setup wizard logic (test → save → navigate)
3. Add first-run detection in `App.xaml.cs` (route to setup or main)
4. Test endpoint button with visual feedback

## Next Steps (Phase 3 - Settings UI)

The following components are planned for Phase 3:
1. Create `SettingsPage.xaml` (endpoint list + current status)
2. Create `AddEditEndpointModal.xaml` (CRUD for endpoints)
3. Implement manual reload button
4. Add endpoint status indicators (green/red dot, latency display)
5. Test all endpoints button

## Files Created (Phase 1)

```
src/MediaButler.Mobile/
├── Models/
│   ├── ApiConfiguration.cs
│   ├── ApiEndpoint.cs
│   ├── ConnectionResult.cs
│   └── ConnectionSettings.cs
├── Services/
│   ├── IApiConfigurationService.cs
│   ├── ApiConfigurationService.cs
│   ├── IApiConnectionService.cs
│   ├── ApiConnectionService.cs
│   ├── IApiClient.cs
│   └── ApiClient.cs
├── MauiProgram.cs (modified)
├── App.xaml.cs (modified)
└── MediaButler.Mobile.csproj (modified)
```

## Testing Recommendations

### Manual Testing Checklist
- [ ] First run creates default `api_config.json`
- [ ] Configuration loads successfully on app startup
- [ ] Health check connects to MediaButler.API
- [ ] Failover works when primary endpoint is down
- [ ] Reload configuration updates active endpoint
- [ ] API requests succeed with automatic endpoint discovery
- [ ] Network connectivity detection works (airplane mode test)
- [ ] All endpoints unreachable shows appropriate error

### Unit Testing (Future)
- Test `ApiEndpoint.IsValid()` with various inputs
- Test `ApiConfiguration.GetEnabledEndpointsByPriority()` sorting
- Test `ApiConfigurationService` file read/write operations
- Test `ApiConnectionService` failover logic
- Test `ApiClient` retry behavior

---

**Status**: Phase 1 complete and ready for Phase 2 (First-Run Experience)
**Build**: ✅ Successful
**Date**: 2025-10-06
