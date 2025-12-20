# .NET 10 Migration - Package Inventory & Compatibility Matrix

**Generated**: 2025-12-20
**Migration Plan**: Week 1, Phase 1.1
**Status**: Analysis Complete

---

## Current Target Frameworks

| Project | Current TFM | Target TFM | Status |
|---------|-------------|------------|--------|
| MediaButler.Core | net8.0 | net10.0 | ✅ Ready |
| MediaButler.Data | net8.0 | net10.0 | ✅ Ready |
| MediaButler.ML | net8.0 | net10.0 | ✅ Ready |
| MediaButler.Services | net8.0 | net10.0 | ✅ Ready |
| MediaButler.API | net8.0 | net10.0 | ✅ Ready |
| MediaButler.Web | **net9.0** | net10.0 | ⚠️ Currently .NET 9 |
| MediaButler.Tests.Unit | net8.0 | net10.0 | ✅ Ready |
| MediaButler.Tests.Integration | net8.0 | net10.0 | ✅ Ready |
| MediaButler.Tests.Acceptance | net8.0 | net10.0 | ✅ Ready |

---

## Complete Package Inventory

### MediaButler.Core (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| MediatR | 13.0.0 | Messaging | High |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | Logging | Medium |

**Dependencies**: None
**Complexity**: Low
**Notes**: Minimal dependencies, clean migration path

---

### MediaButler.Data (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| Microsoft.EntityFrameworkCore.Design | 8.0.0 | ORM | Critical |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.0 | ORM | Critical |

**Dependencies**: MediaButler.Core
**Complexity**: Medium
**Notes**: EF Core migration compatibility is critical

---

### MediaButler.ML (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| Microsoft.ML | 3.0.1 | Machine Learning | Critical |
| Microsoft.ML.FastTree | 3.0.1 | Machine Learning | Critical |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | Logging | Medium |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0 | Configuration | Medium |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.0 | DI | Medium |
| Microsoft.Extensions.Options.ConfigurationExtensions | 8.0.0 | Configuration | Medium |
| Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions | 8.0.0 | Health Checks | Low |
| Microsoft.Extensions.Diagnostics.HealthChecks | 8.0.0 | Health Checks | Low |

**Dependencies**: MediaButler.Core
**Complexity**: Medium
**Notes**: ML.NET 3.0.1 compatibility with .NET 10 needs verification

---

### MediaButler.Services (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| Hangfire.Core | 1.8.14 | Background Jobs | Critical |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.0 | DI | Medium |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.0 | Hosting | Medium |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | Logging | Medium |
| Microsoft.Extensions.Options | 8.0.0 | Configuration | Medium |
| Microsoft.AspNetCore.SignalR.Core | 1.1.0 | Real-time | High |

**Dependencies**: MediaButler.Core, MediaButler.Data, MediaButler.ML
**Complexity**: Medium
**Notes**: Hangfire and SignalR compatibility needs verification

---

### MediaButler.API (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| Hangfire.AspNetCore | 1.8.14 | Background Jobs | Critical |
| Hangfire.Core | 1.8.14 | Background Jobs | Critical |
| Hangfire.InMemory | 1.0.0 | Background Jobs | Critical |
| Hangfire.Storage.SQLite | 0.4.2 | Background Jobs | Critical |
| Microsoft.AspNetCore.OpenApi | 8.0.0 | API Docs | High |
| Microsoft.AspNetCore.SignalR | 1.2.0 | Real-time | High |
| Microsoft.EntityFrameworkCore.Tools | 8.0.0 | ORM Tools | Medium |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.0 | ORM | Critical |
| Serilog.AspNetCore | 8.0.0 | Logging | High |
| Serilog.Settings.Configuration | 9.0.0 | Logging | Medium |
| Swashbuckle.AspNetCore | 6.5.0 | API Docs | High |
| FluentValidation.AspNetCore | 11.3.0 | Validation | High |

**Dependencies**: MediaButler.Services, MediaButler.ML, MediaButler.Data
**Complexity**: High
**Notes**: Most complex project, multiple framework dependencies

---

### MediaButler.Web (net9.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| Microsoft.AspNetCore.Components.WebAssembly | 9.0.9 | Blazor | Critical |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 9.0.9 | Blazor Dev | Medium |
| Microsoft.AspNetCore.SignalR.Client | 9.0.9 | Real-time | High |
| Microsoft.Extensions.Http | 9.0.9 | HTTP Client | Medium |
| Radzen.Blazor | 7.3.5 | UI Components | High |

**Dependencies**: MediaButler.Core
**Complexity**: Medium
**Notes**: Already on .NET 9, need to verify Radzen compatibility with .NET 10

---

### MediaButler.Tests.Unit (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| coverlet.collector | 6.0.4 | Coverage | Medium |
| FluentAssertions | 6.12.0 | Testing | High |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.0 | Testing | High |
| Microsoft.NET.Test.Sdk | 17.8.0 | Testing | Critical |
| Moq | 4.20.69 | Testing | High |
| xunit | 2.5.3 | Testing | Critical |
| xunit.runner.visualstudio | 2.5.3 | Testing | Critical |

**Dependencies**: MediaButler.API, MediaButler.Core, MediaButler.Services, MediaButler.ML
**Complexity**: Low
**Notes**: Standard test framework, good compatibility expected

---

### MediaButler.Tests.Integration (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| coverlet.collector | 6.0.4 | Coverage | Medium |
| FluentAssertions | 6.12.0 | Testing | High |
| Microsoft.Data.Sqlite | 8.0.0 | Database | High |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.0 | Database | High |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.0 | Database | High |
| Microsoft.Extensions.Configuration | 8.0.0 | Configuration | Medium |
| Microsoft.NET.Test.Sdk | 17.8.0 | Testing | Critical |
| Moq | 4.20.70 | Testing | High |
| Testcontainers | 3.6.0 | Testing | Medium |
| xunit | 2.5.3 | Testing | Critical |
| xunit.runner.visualstudio | 2.5.3 | Testing | Critical |

**Dependencies**: MediaButler.Data, MediaButler.Services, MediaButler.Tests.Unit
**Complexity**: Medium
**Notes**: Database integration testing, EF Core compatibility critical

---

### MediaButler.Tests.Acceptance (net8.0 → net10.0)

| Package | Current Version | Category | Critical |
|---------|----------------|----------|----------|
| coverlet.collector | 6.0.4 | Coverage | Medium |
| FluentAssertions | 6.12.0 | Testing | High |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.0 | Testing | High |
| Microsoft.NET.Test.Sdk | 17.8.0 | Testing | Critical |
| xunit | 2.5.3 | Testing | Critical |
| xunit.runner.visualstudio | 2.5.3 | Testing | Critical |

**Dependencies**: MediaButler.API
**Complexity**: Low
**Notes**: API contract testing, minimal dependencies

---

## Critical Package Categories

### 🔴 CRITICAL - Must Verify Before Migration

| Package | Projects Using | Current | .NET 10 Compatible | Notes |
|---------|---------------|---------|-------------------|-------|
| Microsoft.ML | MediaButler.ML | 3.0.1 | ⚠️ TO VERIFY | Core ML functionality |
| Microsoft.ML.FastTree | MediaButler.ML | 3.0.1 | ⚠️ TO VERIFY | Classification engine |
| Hangfire.AspNetCore | MediaButler.API | 1.8.14 | ⚠️ TO VERIFY | Background processing |
| Hangfire.Core | MediaButler.API, Services | 1.8.14 | ⚠️ TO VERIFY | Job persistence |
| Hangfire.Storage.SQLite | MediaButler.API | 0.4.2 | ⚠️ TO VERIFY | SQLite storage |
| Microsoft.EntityFrameworkCore.Sqlite | Data, API, Tests | 8.0.0 | ✅ YES | EF Core 10 available |

### 🟡 HIGH PRIORITY - Likely Compatible

| Package | Projects Using | Current | Expected .NET 10 Version |
|---------|---------------|---------|-------------------------|
| Microsoft.AspNetCore.* | API, Web, Tests | 8.0.0 / 9.0.9 | 10.0.0 |
| Serilog.* | API | 8.0.0 / 9.0.0 | Compatible |
| FluentValidation.AspNetCore | API | 11.3.0 | Compatible |
| Radzen.Blazor | Web | 7.3.5 | ⚠️ CHECK RELEASE NOTES |
| Swashbuckle.AspNetCore | API | 6.5.0 | Compatible |

### 🟢 LOW RISK - Standard Framework Packages

| Package Category | Auto-Upgrade | Notes |
|-----------------|-------------|-------|
| Microsoft.Extensions.* | ✅ Yes | Framework packages, update to 10.0.0 |
| xunit / xunit.runner | ✅ Yes | Testing framework, well maintained |
| FluentAssertions | ✅ Yes | Third-party, good .NET support |
| Moq | ✅ Yes | Mocking library, compatible |
| coverlet.collector | ✅ Yes | Code coverage, compatible |

---

## Version Conflicts to Resolve

### Microsoft.AspNetCore.SignalR Versions

- **MediaButler.Services**: 1.1.0 (SignalR.Core)
- **MediaButler.API**: 1.2.0 (SignalR)
- **MediaButler.Web**: 9.0.9 (SignalR.Client)

**Action**: Standardize to .NET 10 SignalR packages

### Moq Versions

- **MediaButler.Tests.Unit**: 4.20.69
- **MediaButler.Tests.Integration**: 4.20.70

**Action**: Standardize to latest stable version

---

## Third-Party Package Research Required

### 1. Hangfire Ecosystem
- **Current**: 1.8.14 (Core, AspNetCore)
- **Storage**: SQLite 0.4.2, InMemory 1.0.0
- **Research**: Check Hangfire 1.8.x compatibility with .NET 10
- **Fallback**: Hangfire 1.9.x if needed

### 2. Radzen.Blazor
- **Current**: 7.3.5
- **Research**: Check .NET 10 compatibility
- **Risk**: UI library, breaking changes possible

### 3. Microsoft.ML
- **Current**: 3.0.1
- **Research**: ML.NET roadmap for .NET 10
- **Risk**: Core functionality, critical to verify

---

## Next Steps (Phase 1.2)

1. ✅ Package inventory complete
2. ⏭️ Research .NET 10 compatibility for:
   - Hangfire 1.8.14
   - Microsoft.ML 3.0.1
   - Radzen.Blazor 7.3.5
   - Hangfire.Storage.SQLite 0.4.2
3. ⏭️ Check Microsoft docs for breaking changes
4. ⏭️ Create compatibility test plan

---

## Summary

- **Total Projects**: 9 (excluding Mobile)
- **Total Unique Packages**: ~30
- **Critical Dependencies**: 6
- **High Priority**: 7
- **Low Risk**: 17
- **Known Issues**: MediaButler.Web currently on .NET 9 (not .NET 10)

**Risk Level**: ⚠️ **MEDIUM** - Critical packages need verification before proceeding
