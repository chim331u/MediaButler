# MediaButler Mobile - Updated Development Plan
**Last Updated**: 2025-12-23
**Current Version**: .NET 10 MAUI Blazor
**Status**: Phase 1 Complete, Phase 2 In Progress

---

## 📊 CURRENT STATUS ANALYSIS

### ✅ **COMPLETED IMPLEMENTATION**

#### **M1: Foundation Layer** ✅ **100% COMPLETE**
**Commit**: `0189c96` (Mobile SSL/TLS Fix) + `518ee19` (M1 Foundation)

**Infrastructure**:
- ✅ .NET 10 MAUI Blazor project structure
- ✅ Radzen.Blazor UI component library integration
- ✅ Android platform configuration (API 24+)
- ✅ SSL/TLS network security configuration
- ✅ Android network security config for local development
- ✅ HTTPS handler with local network trust for NAS devices

**Services Implemented**:
1. ✅ **ConfigurationService** (145 lines)
   - API base URL validation and security checks
   - HTTPS enforcement for remote hosts
   - HTTP allowed for local network (10.x, 192.168.x, 172.16-31.x)
   - Caching and configuration validation

2. ✅ **HttpClientService** (6,277 bytes)
   - HTTP client factory integration
   - Configured with HTTPS handler
   - Timeout management
   - Base address configuration

3. ✅ **HttpsClientHandlerService** (108 lines)
   - Platform-specific message handler (AndroidMessageHandler)
   - Certificate validation callback
   - Local network IP trust strategy
   - Self-signed certificate support for NAS

4. ✅ **UtilityServices** (293 lines)
   - Network settings management (JSON-based)
   - API URL construction
   - Formatting utilities (currency, file size, dates)
   - Settings persistence

5. ✅ **ServiceApi** (OLD - 363 lines)
   - Legacy API client (still in use)
   - Methods: GetFiles, RefreshCategory, GetCategories, MoveFile, TrainModel
   - Direct HTTP client usage

6. ✅ **TrainingApiService** (NEW - 4,655 bytes)
   - ML model training API integration
   - Uses new HttpClientService pattern

**Pages Implemented**:
1. ✅ **Index.razor** (341 lines) - Home/Dashboard
   - File list with RadzenDataGrid
   - Category dropdown selection per file
   - Bulk file operations (refresh, move, train model)
   - Radzen components (buttons, dropdowns, data grid)
   - File size formatting
   - Loading states and busy indicators

2. ✅ **Settings.razor** (448 lines) - Configuration
   - Tabbed interface (Global Settings, Network Settings)
   - Global settings CRUD (key-value pairs)
   - Network settings management (endpoints, schema, port)
   - Form validation with Radzen forms
   - Add/Edit/Delete operations

3. ✅ **LastViewPage.razor** (168 lines) - Recent Files
   - Recent files display
   - File history tracking

4. ✅ **Logger.razor** (210 lines) - Log Viewer
   - Log file viewing
   - Filtering and search

**Navigation & Layout**:
- ✅ **MainLayout.razor** - Radzen layout with footer navigation
- ✅ **Bottom Navigation Bar**: Home, Settings, Logger, LastView icons
- ✅ **Routes.razor** - Router configuration with NotFound handling
- ✅ **NavMenu.razor** - Navigation menu component

**Data Models**:
- ✅ FilesDetailDto, FileMovedDto, MoveFilesResults
- ✅ Categories, ConfigSettings, Configs
- ✅ GlobalSetting, NetworkSetting
- ✅ BaseEntity pattern

---

## 🚧 **IN PROGRESS / PARTIAL IMPLEMENTATION**

### **M2-M4: API Migration** (PARTIALLY COMPLETE)

**Status**: Infrastructure ready, migration in progress

#### ✅ **Completed**:
- New service architecture (IHttpClientService, IConfigurationService)
- HTTPS handler with certificate validation
- Configuration-based API URL management

#### 🔄 **In Progress**:
- **FilesApiService.cs.m4_todo** exists but commented out (line 51 MauiProgram.cs)
- Needs alignment with API DTOs
- Migration from old ServiceApi to new pattern

#### ❌ **Not Started**:
- SignalR real-time notifications
- Local SQLite caching
- Offline support
- Advanced file filtering
- Batch operations UI

---

## 🎯 **UPDATED DEVELOPMENT PHASES**

### **Phase 1: Foundation** ✅ **COMPLETE**
**Duration**: Completed
**Goal**: Basic app structure and API connectivity

**Deliverables**:
- ✅ Project setup with MAUI and Radzen dependencies
- ✅ Basic Blazor page structure with navigation
- ✅ API client configuration with SSL/TLS support
- ✅ Settings management (network endpoints, global settings)
- ✅ File list display with category management
- ✅ Navigation structure with bottom footer
- ✅ Legacy API integration (ServiceApi)

---

### **Phase 2: API Service Migration** 🔄 **IN PROGRESS**
**Duration**: 1-2 weeks
**Goal**: Migrate from legacy ServiceApi to new typed API services

**Priority Tasks**:

#### **M2: Files API Service** (HIGH PRIORITY)
**File**: `FilesApiService.cs.m4_todo` → `FilesApiService.cs`

**Required Changes**:
1. ✅ Create IFilesApiService interface (already exists in Interfaces/)
2. 🔄 Implement FilesApiService (code exists, needs DTO alignment)
3. ❌ Update Index.razor to use IFilesApiService instead of ServiceApi
4. ❌ Register service in MauiProgram.cs (uncomment line 51)
5. ❌ Test file list, category update, move operations

**API Endpoints to Integrate**:
```csharp
// Current API structure from MediaButler.API
GET  /api/files/by-statuses?statuses={status}      // Multi-status file query
POST /api/v1/file-actions/ignore/{hash}            // Ignore file
POST /api/files/{hash}/confirm                     // Confirm category
GET  /api/files/{hash}                             // File details
```

**DTO Alignment Needed**:
- TrackedFileResponse (from API) vs FilesDetailDto (mobile)
- FileStatus enum synchronization
- Category naming conventions

#### **M3: Training API Service** ✅ **COMPLETE**
- ✅ TrainingApiService implemented
- ✅ Integrated in Index.razor (Train Model button)
- ✅ Uses new HttpClientService pattern

#### **M4: SignalR Notifications** ❌ **NOT STARTED**
**File**: Create `SignalRNotificationService.cs`

**Features**:
- Real-time file discovery notifications
- Processing status updates
- ML classification results
- Error notifications

**Implementation**:
```csharp
public interface ISignalRNotificationService
{
    Task ConnectAsync();
    Task DisconnectAsync();
    event EventHandler<FileDiscoveredEventArgs> FileDiscovered;
    event EventHandler<FileClassifiedEventArgs> FileClassified;
    event EventHandler<FileMoveCompletedEventArgs> FileMoveCompleted;
}
```

---

### **Phase 3: Enhanced File Management** ❌ **NOT STARTED**
**Duration**: 1-2 weeks
**Goal**: Advanced file operations and filtering

**Features to Implement**:

#### **3.1: Advanced File Filtering**
- Status-based filtering (New, Classified, Moved, Error, Ignored)
- Date range filtering
- Category filtering
- File size range filtering
- Search functionality with debounce

**UI Component**:
```razor
<RadzenStack Orientation="Horizontal" Gap="1rem">
    <RadzenDropDown @bind-Value="selectedStatus"
                    Data="fileStatuses"
                    Placeholder="All Statuses" />
    <RadzenTextBox @bind-Value="searchTerm"
                   Placeholder="Search files..."
                   @oninput="OnSearchInput" />
    <RadzenDatePicker @bind-Value="dateFrom" Placeholder="From Date" />
    <RadzenDatePicker @bind-Value="dateTo" Placeholder="To Date" />
</RadzenStack>
```

#### **3.2: File Details Modal**
- Full file metadata display
- ML confidence visualization
- Processing history timeline
- Alternative category suggestions
- Manual category override

#### **3.3: Bulk Operations**
- Multi-select file grid
- Bulk category assignment
- Bulk ignore operation
- Bulk move operation
- Progress tracking

#### **3.4: Pull-to-Refresh**
- SwipeRefreshLayout integration
- Manual data refresh
- Background sync status

---

### **Phase 4: Offline Support & Caching** ❌ **NOT STARTED**
**Duration**: 1 week
**Goal**: Local data caching and offline functionality

**Features**:

#### **4.1: SQLite Local Database**
```csharp
public class LocalDatabase
{
    public DbSet<CachedFile> CachedFiles { get; set; }
    public DbSet<PendingAction> PendingActions { get; set; }
    public DbSet<CategoryCache> CategoryCache { get; set; }
}

public class CachedFile
{
    public string Hash { get; set; }
    public string FileName { get; set; }
    public FileStatus Status { get; set; }
    public string Category { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsSynced { get; set; }
}
```

#### **4.2: Sync Strategy**
- Download essential data on startup
- Optimistic UI updates
- Background sync when online
- Conflict resolution (API wins)
- Manual sync trigger

#### **4.3: Offline Indicators**
- Connection status badge
- Last sync timestamp
- Pending operations count
- Offline mode banner

---

### **Phase 5: System Monitoring** ❌ **NOT STARTED**
**Duration**: 3-5 days
**Goal**: Health monitoring and diagnostics

**Features**:

#### **5.1: Dashboard Enhancement**
- System health metrics
- API response time
- Processing queue depth
- Error rate tracking
- Recent activity feed

#### **5.2: Enhanced Logger**
- Severity filtering (Info, Warning, Error)
- Log search
- Auto-refresh
- Export functionality

#### **5.3: Statistics Page**
- Files processed (daily, weekly, monthly)
- ML accuracy metrics
- Category distribution
- Storage usage

---

### **Phase 6: Polish & Performance** ❌ **NOT STARTED**
**Duration**: 1 week
**Goal**: Production readiness

**Tasks**:
- Performance optimization (lazy loading, virtual scrolling)
- Error handling improvements
- Accessibility compliance
- Dark theme support
- Loading state refinement
- Animation polish
- Memory optimization
- Battery impact testing

---

## 📋 **IMMEDIATE NEXT STEPS**

### **Week 1: Complete M2 (Files API Migration)**

**Day 1-2: DTO Alignment**
1. Compare API TrackedFileResponse with mobile FilesDetailDto
2. Create DTO mapping extensions
3. Align FileStatus enum values
4. Test API responses

**Day 3-4: FilesApiService Integration**
1. Rename FilesApiService.cs.m4_todo → FilesApiService.cs
2. Uncomment service registration in MauiProgram.cs
3. Update Index.razor to inject IFilesApiService
4. Replace ServiceApi calls with FilesApiService methods
5. Test file list loading, category updates, move operations

**Day 5: Testing & Validation**
1. Test all file operations end-to-end
2. Verify SSL/TLS connections work
3. Test error handling
4. Clear app data and test fresh install
5. Document any issues

### **Week 2: SignalR & Real-time Updates**

**Day 1-2: SignalR Infrastructure**
1. Add Microsoft.AspNetCore.SignalR.Client package
2. Create ISignalRNotificationService interface
3. Implement SignalRNotificationService
4. Register service in MauiProgram.cs

**Day 3-4: Event Handlers**
1. Subscribe to FileDiscovered events
2. Subscribe to FileClassified events
3. Subscribe to FileMoveCompleted events
4. Update UI on events (auto-refresh file list)

**Day 5: Connection Management**
1. Auto-reconnect on disconnect
2. Connection status indicator
3. Graceful degradation when offline
4. Test reconnection scenarios

---

## 🎯 **SUCCESS CRITERIA**

### **Phase 2 Complete When**:
- ✅ All legacy ServiceApi calls replaced with typed services
- ✅ FilesApiService integrated and working
- ✅ File list loads correctly from new API endpoints
- ✅ Category updates work via new API
- ✅ Move operations functional
- ✅ SSL/TLS connections stable
- ✅ No regression in existing functionality

### **Phase 3 Complete When**:
- ✅ Advanced filtering implemented and tested
- ✅ File details modal shows all metadata
- ✅ Bulk operations work for multiple files
- ✅ Pull-to-refresh updates data correctly

### **Phase 4 Complete When**:
- ✅ App works offline with cached data
- ✅ Pending actions queue when offline
- ✅ Background sync completes successfully
- ✅ Conflict resolution tested

### **Phase 5 Complete When**:
- ✅ Dashboard shows real-time metrics
- ✅ Logger has advanced filtering
- ✅ Statistics page displays accurate data

### **Phase 6 Complete When**:
- ✅ App meets performance targets (<3s cold start)
- ✅ All critical bugs fixed
- ✅ Dark theme implemented
- ✅ Accessibility features validated
- ✅ Beta testing completed

---

## 📝 **TECHNICAL DEBT & IMPROVEMENTS**

### **Current Issues**:
1. **Dual API Clients**: Both ServiceApi (old) and new services exist
2. **DTO Mismatch**: Mobile DTOs don't match API response models
3. **No Error Boundaries**: App crashes propagate to user
4. **No Loading States**: Some operations lack progress indicators
5. **Hard-coded Strings**: URLs and messages not localized

### **Recommended Refactoring**:
1. **Remove ServiceApi** after M2 migration complete
2. **Centralize DTOs** in shared MediaButler.Core project
3. **Add Error Boundaries** around major components
4. **Implement Loading Service** for consistent loading states
5. **Add Localization** for Italian/English support

---

## 🚀 **DEPLOYMENT PLAN**

### **Beta Release (v1.0-beta)**
**Target**: After Phase 2 complete
**Features**:
- Basic file management
- Category confirmation
- Settings management
- Real-time updates

### **Production Release (v1.0)**
**Target**: After Phase 6 complete
**Requirements**:
- All phases complete
- <1% crash rate
- Performance targets met
- Beta feedback incorporated

---

## 📊 **PROGRESS TRACKING**

| Phase | Status | Progress | Estimated Days | Actual Days |
|-------|--------|----------|----------------|-------------|
| Phase 1: Foundation | ✅ Complete | 100% | 7 | 7 |
| Phase 2: API Migration | 🔄 In Progress | 40% | 10 | TBD |
| Phase 3: File Management | ❌ Not Started | 0% | 10 | - |
| Phase 4: Offline Support | ❌ Not Started | 0% | 7 | - |
| Phase 5: Monitoring | ❌ Not Started | 0% | 5 | - |
| Phase 6: Polish | ❌ Not Started | 0% | 7 | - |
| **Total** | 🔄 **In Progress** | **17%** | **46 days** | **7** |

---

**This plan reflects the actual current state of the mobile codebase and provides a clear roadmap for completing the MediaButler Mobile app following "Simple Made Easy" principles.**
