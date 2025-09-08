# MediaButler MAUI Android App Requirements
**Version:** 1.0.0  
**Target Framework:** .NET 9  
**Platform:** Android (API 24+)  
**Architecture:** MVVM with API-first approach

## 📋 Project Overview

The MediaButler MAUI Android app provides a mobile companion to the existing NAS-based MediaButler API system. The app enables users to monitor file processing, confirm classifications, and manage their media library from anywhere on their network.

### Key Principles
- **API-First Design**: Lightweight UI shell consuming MediaButler.API
- **"Simple Made Easy"**: Following Rich Hickey's principles with un-braided, composable components
- **Single Responsibility**: Each view/service has one clear purpose
- **Offline-First**: Graceful handling of network disconnections
- **Performance-Conscious**: Optimized for older Android devices

### Future Enhancement Deep Dive

#### **Web Scrub Integration**
**Purpose**: Enhance MediaButler's classification accuracy and metadata richness through web scraping services

**Core Features**:
- **Metadata Enrichment**: Automatic lookup of series information, episode titles, air dates, cast information
- **Series Validation**: Verify ML classification results against online databases (TVDB, TMDB, IMDB)
- **Missing Episode Detection**: Identify gaps in series collections and suggest downloads
- **Quality Recommendations**: Suggest optimal file quality based on series importance and storage constraints
- **Release Monitoring**: Track new episode releases and notify when available

**Technical Implementation**:
```csharp
public interface IWebScrabService
{
    Task<SeriesMetadata> EnrichSeriesAsync(string seriesName, CancellationToken cancellationToken);
    Task<EpisodeMetadata> GetEpisodeDetailsAsync(string seriesName, int season, int episode);
    Task<List<MissingEpisode>> DetectMissingEpisodesAsync(string seriesName);
    Task<List<NewRelease>> GetNewReleasesAsync(List<string> monitoredSeries);
}

public class SeriesMetadata
{
    public string OfficialName { get; set; }
    public string Description { get; set; }
    public List<string> Genres { get; set; }
    public DateTime FirstAired { get; set; }
    public int TotalSeasons { get; set; }
    public string Network { get; set; }
    public SeriesStatus Status { get; set; }
    public List<SeasonInfo> Seasons { get; set; }
}
```

**Mobile App Integration**:
- **Series Detail View**: Rich metadata display with posters, descriptions, cast information
- **Collection Gaps**: Visual indication of missing episodes with download suggestions
- **New Release Notifications**: Push notifications for new episodes of monitored series
- **Metadata Override**: Manual correction of web-scraped data with user feedback

#### **Mule Connection Management**
**Purpose**: Connect and manage multiple MediaButler instances across different locations/devices

**Core Features**:
- **Multi-Instance Discovery**: Automatic discovery of MediaButler instances on network
- **Centralized Dashboard**: Unified view of all connected MediaButler systems
- **Load Balancing**: Distribute processing across multiple instances
- **Failover Support**: Automatic switching when primary instance becomes unavailable
- **Synchronized Configuration**: Propagate settings changes across all instances

**Technical Architecture**:
```csharp
public interface IMuleConnectionService
{
    Task<List<MediaButlerInstance>> DiscoverInstancesAsync();
    Task<bool> ConnectToInstanceAsync(MediaButlerInstance instance);
    Task<AggregatedStats> GetAggregatedStatsAsync();
    Task<bool> SynchronizeConfigurationAsync(ConfigurationUpdate update);
    Task<List<DistributedFile>> GetDistributedFileListAsync();
}

public class MediaButlerInstance
{
    public string InstanceId { get; set; }
    public string Name { get; set; }
    public string Endpoint { get; set; }
    public string Version { get; set; }
    public InstanceHealth Health { get; set; }
    public InstanceCapabilities Capabilities { get; set; }
    public DateTime LastSeen { get; set; }
}

public class AggregatedStats
{
    public int TotalInstancesOnline { get; set; }
    public int TotalFilesTracked { get; set; }
    public int TotalPendingFiles { get; set; }
    public double AverageProcessingTime { get; set; }
    public List<InstanceStats> PerInstanceStats { get; set; }
}
```

**Mobile App Features**:
- **Instance Switcher**: Quick switching between connected MediaButler instances
- **Unified File View**: Combined file list from all instances with source indication
- **Health Monitoring**: Real-time health status of all connected instances
- **Distributed Operations**: Perform bulk operations across multiple instances
- **Connection Management**: Add/remove/configure MediaButler instances

**UI/UX Considerations**:
- **Instance Indicator**: Clear visual indication of which instance is currently active
- **Network Topology View**: Visual representation of connected instances
- **Sync Status**: Show synchronization status between instances
- **Offline Handling**: Graceful handling when some instances are unreachable

---

## 🎯 Business Objectives

### Primary Goals
1. **Remote Monitoring**: Check system status and file processing from mobile devices
2. **Quick Confirmations**: Approve file classifications with minimal friction
3. **Instant Notifications**: Real-time alerts for new files requiring attention
4. **Troubleshooting Support**: Access to logs and system health information

### Success Metrics
- **User Adoption**: 80% of MediaButler users install mobile app within 3 months
- **Task Completion**: 90% of file confirmations completed in <30 seconds
- **Performance**: App startup <3 seconds, API calls <2 seconds
- **Reliability**: <1% crash rate, 99% successful API communications

---

## 🏗️ Technical Architecture

### Project Structure
```
src/MediaButler.Mobile/
├── Platforms/
│   └── Android/                    # Android-specific implementations
├── Views/                          # XAML pages and code-behind
├── ViewModels/                     # MVVM view models with data binding
├── Services/                       # API communication and business logic
├── Models/                         # Data transfer objects and local models
├── Converters/                     # Value converters for data binding
├── Controls/                       # Custom controls and user controls
├── Resources/                      # Images, fonts, styles, translations
└── Utilities/                      # Helper classes and extensions
```

### Architecture Patterns
- **MVVM (Model-View-ViewModel)**: Clean separation of concerns
- **Dependency Injection**: Built-in .NET DI container
- **Repository Pattern**: Abstract API communication layer
- **Observer Pattern**: Real-time updates via SignalR/polling
- **Command Pattern**: User actions as commands with validation

### Technology Stack
- **.NET 9**: Latest performance improvements and features
- **MAUI**: Cross-platform UI framework (Android focus)
- **CommunityToolkit.Mvvm**: MVVM helpers and source generators
- **Refit**: Type-safe HTTP client generation
- **Polly**: Resilience and transient-fault handling
- **Microsoft.Extensions.Http**: HTTP client factory and policies
- **SQLite**: Local caching and offline support
- **Serilog**: Structured logging aligned with API

---

## 📱 Core Features

### 1. Dashboard & Overview
**Purpose**: Quick system status and pending file summary

**Features**:
- System health indicators (API connectivity, memory usage, processing queue)
- Pending files count with priority indicators
- Recent activity timeline (last 10 operations)
- Quick action buttons (scan folder, bulk confirm)
- Network status and API endpoint configuration

**UI Components**:
- Status cards with color-coded indicators
- Pending files badge with tap-to-navigate
- Activity feed with timestamps and icons
- Floating action button for quick scan

### 2. File Management
**Purpose**: Browse, filter, and manage tracked files

**Features**:
- Paginated file list with pull-to-refresh
- Status-based filtering (New, Pending, Confirmed, Moved, Error)
- Search functionality (filename, category, date range)
- File details view with metadata and processing history
- Bulk selection and operations
- Sort options (date, name, size, status)

**UI Components**:
- RecyclerView with virtual scrolling for performance
- Filter chips with multi-selection
- Search bar with debounced input
- SwipeRefreshLayout for manual refresh
- Bottom sheet for file details

### 3. Classification Confirmation
**Purpose**: Review and confirm ML classification results

**Features**:
- ML suggestion display with confidence scoring
- Alternative category suggestions
- Manual category input with autocomplete
- Batch confirmation for similar files
- Preview of target organization path
- Confidence threshold visualization

**UI Components**:
- Card-based layout for each pending file
- Confidence meter with color gradients
- Category selection with search/filter
- Swipe gestures (approve/reject/custom)
- Bulk selection with action bar

### 4. System Monitoring
**Purpose**: Monitor MediaButler system health and performance

**Features**:
- Real-time performance metrics (memory, CPU, processing speed)
- API response time monitoring
- Error log viewer with filtering
- Storage space monitoring (watch folder, target library)
- Background service status
- Network latency and connectivity tests

**UI Components**:
- Live charts for performance metrics
- Log viewer with severity filtering
- Storage usage circular progress indicators
- Network status indicators with retry actions
- Health check results in expandable lists

### 5. Configuration Management
**Purpose**: View and modify MediaButler settings

**Features**:
- Read-only view of current configuration
- Critical settings modification (watch folder, target paths)
- Validation of user inputs before API submission
- Configuration backup/restore
- Setting descriptions and help text
- Restart requirement indicators

**UI Components**:
- Grouped settings in categories
- Input validation with real-time feedback
- Confirmation dialogs for critical changes
- Help tooltips and descriptions
- Progress indicators for configuration updates

### 6. Notifications & Alerts
**Purpose**: Real-time notifications for important events

**Features**:
- Push notifications for new files requiring attention
- Background sync of pending files
- Configurable notification preferences
- Error alerts with actionable information
- Processing completion notifications
- System health alerts

**Implementation**:
- Android notification channels for categorization
- Background services for periodic sync
- SignalR connection for real-time updates
- Local notification scheduling
- Notification action buttons (approve/view/dismiss)

---

## 🔌 API Integration

### HTTP Client Configuration
```csharp
// Refit interface for type-safe API calls
public interface IMediaButlerApi
{
    [Get("/api/files")]
    Task<ApiResponse<PagedResponse<TrackedFileResponse>>> GetFilesAsync(
        [Query] int skip = 0,
        [Query] int take = 20,
        [Query] string status = null,
        [Query] string category = null);

    [Post("/api/files/{hash}/confirm")]
    Task<ApiResponse<string>> ConfirmFileAsync(
        string hash,
        [Body] ConfirmFileRequest request);

    [Get("/api/health/detailed")]
    Task<ApiResponse<SystemHealthResponse>> GetSystemHealthAsync();

    [Get("/api/stats/dashboard")]
    Task<ApiResponse<DashboardStatsResponse>> GetDashboardStatsAsync();
}
```

### Resilience Policies
```csharp
// Polly policies for robust API communication
services.AddHttpClient<IMediaButlerApi>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetTimeoutPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    => Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
                Log.Warning("API retry {RetryCount} after {Delay}ms", 
                    retryCount, timespan.TotalMilliseconds));
```

### Real-time Updates
- **Primary**: SignalR connection for live updates
- **Fallback**: Polling with exponential backoff
- **Offline**: Local cache with sync on reconnection

---

## 💾 Data Management

### Local Storage Strategy
```csharp
// SQLite entities for offline support
public class CachedFile
{
    public string Hash { get; set; }
    public string FileName { get; set; }
    public FileStatus Status { get; set; }
    public string Category { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsSynced { get; set; }
}

public class PendingAction
{
    public int Id { get; set; }
    public string ActionType { get; set; }
    public string TargetHash { get; set; }
    public string ActionData { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
}
```

### Sync Strategy
- **Download First**: Cache essential data on app startup
- **Optimistic Updates**: Apply changes locally, sync with API
- **Conflict Resolution**: API data always wins
- **Background Sync**: Periodic sync when app in background
- **Manual Refresh**: Pull-to-refresh for immediate updates

---

## 🎨 User Experience Design

### Design System
**Colors**:
- Primary: Material Design Blue (#2196F3)
- Secondary: Accent Teal (#009688)
- Success: Green (#4CAF50)
- Warning: Orange (#FF9800)
- Error: Red (#F44336)
- Background: Dynamic (light/dark theme support)

**Typography**:
- Headers: Roboto Medium
- Body: Roboto Regular
- Monospace: Roboto Mono (for logs, technical data)

**Icons**:
- Material Design Icons
- Custom MediaButler logo and branding
- Consistent iconography across all screens

### Navigation Patterns
- **Bottom Navigation**: Primary navigation between main sections
- **Top App Bar**: Screen titles, search, and actions
- **Navigation Drawer**: Secondary navigation and settings
- **Floating Action Button**: Primary actions (scan, confirm)

### Responsive Design
- **Phone Portrait**: Single-column layout with stacked cards
- **Phone Landscape**: Two-column layout where appropriate
- **Tablet**: Master-detail layout with side navigation

---

## 🔒 Security & Privacy

### Network Security
- **TLS/HTTPS**: All API communication encrypted
- **Certificate Pinning**: Validate API server certificates
- **IP Allowlist**: Restrict to local network ranges
- **Timeout Policies**: Prevent hanging connections

### Data Protection
- **No Sensitive Storage**: No passwords or tokens stored locally
- **Cache Encryption**: SQLite database encryption at rest
- **Memory Protection**: Clear sensitive data from memory
- **Audit Logging**: Track user actions for troubleshooting

### Network Discovery
- **mDNS/Bonjour**: Automatic MediaButler server discovery
- **Manual Configuration**: Fallback for manual IP/port entry
- **Health Validation**: Verify API compatibility before use

---

## 📈 Performance Requirements

### App Performance Targets
- **Cold Start**: <3 seconds to main screen
- **Warm Start**: <1 second to main screen
- **Memory Usage**: <200MB resident memory
- **Battery Impact**: Minimal when app in background
- **Storage Usage**: <50MB app + <100MB cache

### Network Performance
- **API Calls**: <2 seconds for standard operations
- **File List Loading**: <1 second for 50 items
- **Real-time Updates**: <500ms latency
- **Offline Mode**: Full functionality for cached data
- **Sync Performance**: <30 seconds for full refresh

### UI Performance
- **List Scrolling**: 60fps on mid-range devices
- **Animation Smoothness**: No frame drops during transitions
- **Touch Responsiveness**: <100ms touch feedback
- **Search Results**: <300ms for local filtering

---

## 🧪 Testing Strategy

### Test Categories
```
    /\     E2E Tests (15+ tests)
   /  \    - Full user workflows
  /____\   - API integration tests
 /      \  - Device compatibility
/________\
Integration Tests (25+ tests)
- API communication
- Database operations
- Background services

Unit Tests (50+ tests)
- ViewModels and business logic
- Data transformations
- Utility functions
```

### Test Tools
- **xUnit**: Core testing framework
- **Moq**: Mocking framework for dependencies
- **FluentAssertions**: Readable test assertions
- **Microsoft.Extensions.DependencyInjection.Testing**: DI testing
- **SQLite Memory**: In-memory database for tests

### Device Testing
- **Target Devices**: Android 7.0+ (API 24+)
- **Form Factors**: Phone, tablet, foldable support
- **Performance Tiers**: Low-end, mid-range, high-end devices
- **Network Conditions**: WiFi, mobile data, offline scenarios

---

## 🚀 Development Phases

### Phase 1: Foundation (Sprint 1)
**Duration**: 1 week
**Goal**: Basic app structure and API connectivity

**Deliverables**:
- Project setup with MAUI and dependencies
- Basic MVVM architecture implementation
- API client configuration with Refit
- Simple dashboard with health check
- Navigation structure and shell setup

### Phase 2: Core Features (Sprint 2-3)
**Duration**: 2 weeks
**Goal**: Essential file management and confirmation features

**Deliverables**:
- File list with pagination and filtering
- File details view with all metadata
- Classification confirmation workflow
- Basic notifications setup
- Local caching implementation

### Phase 3: Advanced Features (Sprint 4)
**Duration**: 1 week
**Goal**: System monitoring and configuration

**Deliverables**:
- System health monitoring screens
- Configuration viewing and basic editing
- Advanced search and filtering
- Bulk operations implementation
- Performance optimization

### Phase 4: Polish & Release (Sprint 5)
**Duration**: 1 week
**Goal**: Production readiness and testing

**Deliverables**:
- Comprehensive testing suite
- Performance optimization and profiling
- UI/UX polish and accessibility
- Documentation and deployment guides
- Beta testing and feedback integration

---

## 📋 Acceptance Criteria

### Functional Requirements
- ✅ **API Communication**: Successfully connect to MediaButler API
- ✅ **File Management**: View, filter, and search tracked files
- ✅ **Classification**: Confirm ML suggestions with 90% success rate
- ✅ **Real-time Updates**: Receive notifications within 30 seconds
- ✅ **Offline Support**: Function with cached data when offline
- ✅ **Configuration**: View and modify critical settings

### Non-Functional Requirements
- ✅ **Performance**: Meet all performance targets outlined above
- ✅ **Reliability**: <1% crash rate in production usage
- ✅ **Usability**: 90% of users complete key tasks without assistance
- ✅ **Compatibility**: Support Android 7.0+ devices
- ✅ **Security**: Pass security audit with no critical vulnerabilities

### User Experience
- ✅ **Intuitive Navigation**: Users understand app structure immediately
- ✅ **Responsive Design**: Smooth operation on all supported devices
- ✅ **Error Handling**: Clear error messages with actionable guidance
- ✅ **Accessibility**: Support for screen readers and accessibility features

---

## 🔮 Future Enhancements

### Version 1.1 Considerations
- **Enhanced Platform Integration**: 
  - **iOS**: Shortcuts app integration, Siri support
  - **Android**: App shortcuts, adaptive icons
- **Advanced Notifications**: Rich notifications with inline actions
- **Widgets**: 
  - **Android**: Home screen widgets for quick status
  - **iOS**: Today View widgets and Lock Screen widgets
- **Dark Theme**: Full dark mode support with automatic switching

### Version 2.0 Vision
- **Local Processing**: Edge ML classification on device
- **Camera Integration**: Scan physical media for digital organization
- **Sharing Features**: Share file lists and library organization
- **Advanced Analytics**: Detailed usage and performance analytics
- **Platform-Specific Features**:
  - **iOS**: Focus modes integration, Live Activities
  - **Android**: Tiles integration, advanced notification channels

---

This requirements document provides a comprehensive foundation for developing the MediaButler MAUI Android app, ensuring alignment with the existing API-first architecture while delivering a native mobile experience that enhances the overall MediaButler ecosystem.