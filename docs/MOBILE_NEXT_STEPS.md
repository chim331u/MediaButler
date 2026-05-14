# MediaButler Mobile - Next Steps & Priorities

**Date**: 2024-12-24
**Current Status**: Phase 2 API Migration 85% Complete
**Overall Progress**: ~50% Complete

---

## 🎯 IMMEDIATE PRIORITIES (This Week)

### 1. Complete Phase 2 API Migration - **CRITICAL**
**Estimated Time**: 1-2 hours
**Priority**: P0 (Blocking)

**Task**: Migrate `LastViewPage.razor` from legacy `IServiceApi` to `IFilesApiService`

**Steps**:
1. Read `LastViewPage.razor` current implementation
2. Replace `IServiceApi _service` with `IFilesApiService _filesApiService`
3. Update `GetLastFilesList()` call:
   ```csharp
   // OLD:
   filesFromApi = await _service.GetLastFilesList();

   // NEW:
   var statuses = new FileStatus[] { FileStatus.Moved };
   var result = await _filesApiService.GetFilesByStatusesAsync(
       skip: 0,
       take: 100,
       statuses: statuses,
       cancellationToken: CancellationToken.None
   );

   if (result.IsSuccess && result.Value != null) {
       filesFromApi = DtoMapper.ToFilesDetailDtoList(result.Value.Items);
   } else {
       // Handle error
   }
   ```
4. Add `using MediaButler.Core.Enums;` for `FileStatus`
5. Add `using MediaButler.Mobile.Data.MigrationHelpers;` for `DtoMapper`
6. Build and verify compilation
7. **Remove** `ServiceApi.cs` and `IServiceApi.cs` (legacy code)
8. Update `MauiProgram.cs` - remove `IServiceApi` registration

**Success Criteria**:
- [x] LastViewPage.razor uses IFilesApiService
- [x] Build successful (0 errors)
- [x] Legacy ServiceApi.cs removed
- [x] All pages migrated to new API

---

### 2. Runtime Testing - **HIGH PRIORITY**
**Estimated Time**: 2-3 hours
**Priority**: P1 (High)

**Prerequisites**:
- API server running at configured URL (http://10.0.2.2:5271 or update in appsettings.json)
- Android emulator or physical device

**Test Scenarios**:

#### **Test 1: Configuration Loading**
- [ ] App starts without "API URL not configured" error
- [ ] Logs show "✅ API configuration validated successfully"
- [ ] Base URL matches appsettings.json value

#### **Test 2: Index.razor - File List**
- [ ] Files load on page open
- [ ] Files displayed in RadzenDataGrid
- [ ] Categories dropdown populated
- [ ] File selection toggles work

#### **Test 3: Index.razor - Refresh**
- [ ] Click "autorenew" button
- [ ] API call to `/api/files/by-statuses` successful
- [ ] File list updates
- [ ] Log shows "Data updated"

#### **Test 4: Index.razor - Train Model**
- [ ] Click "psychology" button
- [ ] API call to `/api/training/trainModel` successful
- [ ] Training status message displayed
- [ ] Accuracy percentage shown

#### **Test 5: Index.razor - Move Files**
- [ ] Select files to move (toggle switch)
- [ ] Click "drive_file_move" button
- [ ] Batch organize API call successful
- [ ] Job ID returned and displayed

#### **Test 6: SignalR Real-time Updates**
- [ ] SignalR connection established on page load
- [ ] Connection ID logged
- [ ] File move notifications received
- [ ] Job completion notifications received

#### **Test 7: LastViewPage.razor**
- [ ] Navigate to LastView page
- [ ] Files with "Moved" status displayed
- [ ] File list renders correctly

#### **Test 8: Error Handling**
- [ ] Stop API server
- [ ] Trigger file refresh
- [ ] Error message displayed (not crash)
- [ ] Result.Error shown to user

#### **Test 9: HTTPS Certificate (Optional)**
- [ ] Change appsettings.json BaseUrl to HTTPS NAS address
- [ ] Self-signed certificate accepted for local network
- [ ] Log shows certificate validation decision

**Success Criteria**:
- All 9 test scenarios pass
- No crashes or unhandled exceptions
- User-friendly error messages
- SignalR reconnection works after network interruption

---

## 📅 SHORT-TERM GOALS (Next 1-2 Weeks)

### 3. M4: Feature Flags System - **NOT STARTED**
**Estimated Time**: 3-4 days
**Priority**: P2 (Medium)

**Purpose**: Enable incremental rollout and A/B testing

**Tasks**:
1. Create `HybridFilesService` adapter
   - Routes to legacy or new API based on feature flags
   - Implements `IServiceApi` for backward compatibility
2. Implement feature flag routing logic
3. Test with flags ON/OFF
4. Document flag behavior

**Feature Flags**:
- `UseNewApi` - Master switch for new API
- `EnableBatchOperations` - Batch file operations
- `EnablePagination` - Server-side pagination
- `EnableSignalRService` - Centralized SignalR service
- `EnableCaching` - Response caching

**Deliverables**:
- [ ] HybridFilesService.cs (~200 lines)
- [ ] Flag routing tested
- [ ] Documentation updated

---

### 4. M5: Performance Optimization - **NOT STARTED**
**Estimated Time**: 4-5 days
**Priority**: P2 (Medium)

**Tasks**:

#### **Pagination Implementation**
- [ ] Update Index.razor to load 20 files at a time
- [ ] Add "Load More" button
- [ ] Test with 100+ files
- [ ] Verify memory usage <100MB

#### **Response Caching**
- [ ] Create `CachedFilesApiService` decorator
- [ ] Cache categories (5-minute TTL)
- [ ] Cache invalidation on file operations
- [ ] Test cache hit/miss rates

#### **Batch Operations**
- [ ] Use `OrganizeBatchAsync()` for multi-file moves
- [ ] Job tracking via SignalR
- [ ] Progress reporting

**Performance Targets**:
- Initial file load: <2 seconds on ARM32 NAS
- Memory usage: <100MB typical
- Categories API: <100ms (cached)

---

## 🎯 MEDIUM-TERM GOALS (2-4 Weeks)

### 5. M6: Architecture Improvements - **NOT STARTED**
**Estimated Time**: 1-2 weeks
**Priority**: P3 (Low)

**Tasks**:
- [ ] Create ViewModels (MVVM pattern)
  - FilesViewModel
  - SettingsViewModel
  - ViewModelBase
- [ ] Extract business logic from Razor pages
- [ ] Implement navigation service
- [ ] Add unit tests (30+ tests)

**Benefits**:
- Cleaner separation of concerns
- Testable business logic
- Better code reusability

---

### 6. Testing & Quality - **NOT STARTED**
**Estimated Time**: 1-2 weeks
**Priority**: P3 (Low)

**Tasks**:

#### **Unit Tests**
- [ ] HttpClientService tests (10 tests)
- [ ] FilesApiService tests (15 tests)
- [ ] TrainingApiService tests (5 tests)
- [ ] ConfigurationService tests (8 tests)
- [ ] DtoMapper tests (10 tests)

**Target**: 70%+ code coverage

#### **Integration Tests**
- [ ] API service integration tests
- [ ] SignalR connection tests
- [ ] Configuration loading tests

#### **Performance Tests**
- [ ] Memory profiling (target: <100MB)
- [ ] Load time benchmarks (target: <2s)
- [ ] Cache effectiveness tests

---

## 🚫 OUT OF SCOPE (Deferred)

The following items are explicitly deferred to post-v1.0:

1. **Offline Support**
   - Local caching of files
   - Offline queue for operations
   - Sync when online

2. **Advanced UI Features**
   - Search/filter enhancements
   - Sorting customization
   - Bulk selection UI

3. **Comprehensive Integration Testing**
   - End-to-end automated tests
   - UI testing framework
   - Continuous integration

4. **Input Validation & Sanitization**
   - Client-side validation
   - Input sanitization
   - Security hardening beyond HTTPS

---

## 📊 PROGRESS TRACKING

### Completed Milestones ✅
- [x] M1: Foundation Layer (100%)
- [x] M2: Security Hardening (100%)
- [x] M3: API Service Layer (100%)

### In Progress ⏳
- [x] Phase 2: API Migration (85% - LastViewPage pending)

### Not Started ❌
- [ ] M4: Feature Flags System
- [ ] M5: Performance Optimization
- [ ] M6: Architecture Improvements
- [ ] M7: Gradual Rollout
- [ ] M8: Cleanup & Final Release

### Overall Completion: **~50%**

---

## 🎯 DEFINITION OF DONE

### Phase 2 API Migration
- [x] All pages use new API services
- [x] Build successful (0 errors)
- [x] Legacy code removed
- [ ] Runtime testing passed (9/9 scenarios)

### v1.0 Release Criteria
- [ ] All milestones M1-M6 complete
- [ ] 9/9 runtime test scenarios passing
- [ ] Performance targets met (<2s load, <100MB RAM)
- [ ] Unit test coverage >70%
- [ ] No critical bugs
- [ ] Documentation complete

---

**Document Version**: 1.0
**Last Updated**: 2024-12-24
**Next Review**: After LastViewPage migration
