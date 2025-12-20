# MediaButler .NET 8 → .NET 10 Migration Plan

## Migration Strategy
- **Approach**: All projects at once (synchronized migration)
- **Focus**: Performance improvements with moderate feature adoption
- **Timeline**: 3-4 weeks (Normal pace with adequate testing)
- **Philosophy**: "Simple Made Easy" - minimal complexity, maximum benefit
- **Scope**: Backend and Web projects only (MAUI excluded - manual migration later)

---

## Projects Scope

| Project | Current TFM | Target TFM | Complexity | Notes |
|---------|-------------|------------|------------|-------|
| MediaButler.Core | net8.0 | net10.0 | Low | Domain models, no dependencies |
| MediaButler.Data | net8.0 | net10.0 | Medium | EF Core compatibility check |
| MediaButler.ML | net8.0 | net10.0 | Medium | ML.NET compatibility check |
| MediaButler.Services | net8.0 | net10.0 | Medium | Business logic, DI changes |
| MediaButler.API | net8.0 | net10.0 | High | ASP.NET Core + Hangfire |
| MediaButler.Web | net10.0 | net10.0 | None | Already on .NET 10 ✅ |
| MediaButler.Tests.Unit | net8.0 | net10.0 | Low | Test framework updates |
| MediaButler.Tests.Integration | net8.0 | net10.0 | Medium | DB/IO test compatibility |
| MediaButler.Tests.Acceptance | net8.0 | net10.0 | Medium | API contract validation |
| **MediaButler.Mobile** | **net9.0** | **EXCLUDED** | **N/A** | **Manual migration later** |

---

## Phase 1: Pre-Migration Analysis (Week 1 - Days 1-2)

| # | Task | Est. Time | Dependencies | Output |
|---|------|-----------|--------------|--------|
| 1.1 | Inventory all NuGet packages across projects | 2 hours | None | Package compatibility matrix |
| 1.2 | Check .NET 10 compatibility for critical packages | 3 hours | 1.1 | Incompatible packages list |
| 1.3 | Review .NET 10 breaking changes documentation | 2 hours | None | Breaking changes checklist |
| 1.4 | Identify deprecated APIs used in codebase | 2 hours | 1.3 | API migration checklist |
| 1.5 | Analyze ML.NET and EF Core .NET 10 support | 2 hours | 1.2 | ML/EF compatibility report |
| 1.6 | Review Hangfire .NET 10 compatibility | 1 hour | 1.2 | Hangfire upgrade path |
| 1.7 | Check Radzen.Blazor .NET 10 support | 1 hour | 1.2 | Blazor components status |
| 1.8 | Create migration branch from main | 15 min | None | `feature/net10-migration` branch |
| 1.9 | Document baseline performance metrics | 2 hours | None | Performance baseline report |

**Deliverables**: Compatibility matrix, breaking changes list, migration branch

---

## Phase 2: Project File Updates (Week 1 - Days 3-4)

| # | Task | Est. Time | Order | Notes |
|---|------|-----------|-------|-------|
| 2.1 | Update MediaButler.Core to net10.0 | 30 min | 1st | No external dependencies |
| 2.2 | Update MediaButler.Data to net10.0 | 1 hour | 2nd | EF Core version check |
| 2.3 | Update MediaButler.ML to net10.0 | 1 hour | 3rd | ML.NET compatibility |
| 2.4 | Update MediaButler.Services to net10.0 | 1 hour | 4th | Service layer updates |
| 2.5 | Update MediaButler.API to net10.0 | 2 hours | 5th | ASP.NET Core changes |
| 2.6 | Update all test projects to net10.0 | 1 hour | 6th | xUnit compatibility |
| 2.7 | Update global.json SDK version | 15 min | After all | Lock .NET 10 SDK |
| 2.8 | Update Docker base images to .NET 10 | 1 hour | After 2.5 | ARM32 SDK images |

**Deliverables**: Updated .csproj files, updated Dockerfiles

---

## Phase 3: NuGet Package Updates (Week 1 - Day 5)

| # | Package Category | Est. Time | Priority | Notes |
|---|-----------------|-----------|----------|-------|
| 3.1 | Microsoft.AspNetCore.* packages | 1 hour | Critical | API framework |
| 3.2 | Microsoft.EntityFrameworkCore.* | 1 hour | Critical | Data layer |
| 3.3 | Microsoft.ML.* packages | 1 hour | Critical | Classification engine |
| 3.4 | Hangfire.* packages | 1 hour | Critical | Background jobs |
| 3.5 | Radzen.Blazor package | 30 min | High | Web UI components |
| 3.6 | Serilog.* packages | 30 min | Medium | Logging framework |
| 3.7 | FluentValidation.* packages | 30 min | Medium | Validation library |
| 3.8 | xUnit and testing packages | 30 min | High | Test infrastructure |
| 3.9 | Resolve package version conflicts | 1 hour | Critical | Dependency resolution |

**Deliverables**: Updated packages, resolved conflicts

---

## Phase 4: Code Updates & Breaking Changes (Week 2 - Days 1-3)

| # | Area | Task | Est. Time | Impact |
|---|------|------|-----------|--------|
| 4.1 | API Controllers | Update minimal API syntax if needed | 2 hours | Low |
| 4.2 | Dependency Injection | Review DI lifetime changes | 2 hours | Medium |
| 4.3 | Authentication | Check auth middleware changes | 1 hour | Low |
| 4.4 | EF Core | Update LINQ queries for performance | 3 hours | Medium |
| 4.5 | ML.NET | Verify model loading/training APIs | 2 hours | Medium |
| 4.6 | SignalR | Review hub connection changes | 1 hour | Low |
| 4.7 | Blazor WASM | Check WebAssembly runtime changes | 2 hours | Medium |
| 4.8 | Hangfire | Verify job serialization | 1 hour | Low |
| 4.9 | ARM32 Optimizations | Review GC and memory settings | 2 hours | High |

**Deliverables**: Updated code, documented changes

---

## Phase 5: Performance Optimization (Week 2 - Days 4-5)

| # | Optimization Area | Task | Est. Time | Expected Gain |
|---|-------------------|------|-----------|---------------|
| 5.1 | HTTP/3 Support | Enable HTTP/3 in Kestrel | 1 hour | Better latency |
| 5.2 | ARM64 Intrinsics | Leverage improved ARM intrinsics | 2 hours | 10-15% speedup |
| 5.3 | GC Improvements | Tune DATAS (Dynamic Adaptive To Application Size) GC | 2 hours | Better memory |
| 5.4 | LINQ Optimizations | Use new LINQ performance improvements | 2 hours | 5-10% speedup |
| 5.5 | Regex Source Generators | Already using - verify compatibility | 1 hour | Maintained perf |
| 5.6 | Async Stream Improvements | Adopt IAsyncEnumerable enhancements | 2 hours | Better throughput |
| 5.7 | JSON Serialization | Use System.Text.Json improvements | 1 hour | Faster API responses |
| 5.8 | ML Model Loading | Optimize FastText loading with new APIs | 2 hours | Faster startup |

**Deliverables**: Performance-optimized code, benchmark results

---

## Phase 6: Testing & Validation (Week 3 - Full Week)

| # | Test Category | Task | Est. Time | Pass Criteria |
|---|--------------|------|-----------|---------------|
| 6.1 | Unit Tests | Run all 250+ unit tests | 2 hours | 100% pass rate |
| 6.2 | Integration Tests | Run all 300+ integration tests | 4 hours | 100% pass rate |
| 6.3 | Acceptance Tests | Run all 240+ acceptance tests | 4 hours | 100% pass rate |
| 6.4 | ML Model Tests | Verify classification accuracy maintained | 2 hours | ≥82% accuracy |
| 6.5 | Database Tests | EF Core migration compatibility | 2 hours | All migrations work |
| 6.6 | API Contract Tests | Swagger/OpenAPI validation | 1 hour | No breaking changes |
| 6.7 | Blazor Web UI Tests | Component rendering tests | 3 hours | All components work |
| 6.8 | Performance Tests | Benchmark .NET 8 vs .NET 10 | 4 hours | ≥5% improvement |
| 6.9 | ARM32 Device Tests | Deploy to QNAP NAS test environment | 4 hours | Stable operation |
| 6.10 | Regression Testing | Full system integration test | 4 hours | No regressions |

**Deliverables**: Test reports, performance comparison

---

## Phase 7: Docker & Deployment Updates (Week 3 - End)

| # | Task | Est. Time | Output |
|---|------|-----------|--------|
| 7.1 | Update Dockerfile base images to .NET 10 | 1 hour | Updated Dockerfiles |
| 7.2 | Build ARM32 Docker image for QNAP | 2 hours | ARM32 image |
| 7.3 | Build ARM64 Docker image for Mac dev | 1 hour | ARM64 image |
| 7.4 | Test Docker builds on both platforms | 2 hours | Build validation |
| 7.5 | Update deployment scripts for .NET 10 SDK | 1 hour | Updated scripts |
| 7.6 | Update CI/CD pipelines (if any) | 2 hours | Pipeline configs |
| 7.7 | Create rollback procedure documentation | 1 hour | Rollback guide |
| 7.8 | Update ARM32 memory settings for .NET 10 | 1 hour | Optimized config |

**Deliverables**: Docker images, deployment scripts, rollback plan

---

## Phase 8: Documentation & Finalization (Week 4)

| # | Documentation Task | Est. Time | Location |
|---|-------------------|-----------|----------|
| 8.1 | Update CLAUDE.md with .NET 10 details | 1 hour | Root |
| 8.2 | Update README.md technology stack | 30 min | Root |
| 8.3 | Update deployment-guide.md | 1 hour | docs/ |
| 8.4 | Document breaking changes encountered | 2 hours | docs/net10-migration.md |
| 8.5 | Update performance benchmarks | 1 hour | docs/ |
| 8.6 | Update API documentation | 1 hour | docs/api-documentation.md |
| 8.7 | Create migration retrospective | 1 hour | docs/net10-retrospective.md |
| 8.8 | Update developer setup instructions | 1 hour | docs/ |

**Deliverables**: Updated documentation

---

## Risk Assessment & Mitigation

| Risk | Probability | Impact | Mitigation Strategy |
|------|------------|--------|---------------------|
| Breaking API changes in ASP.NET Core | Medium | High | Thorough testing, version compatibility checks |
| EF Core query translation issues | Low | Medium | Integration tests, SQL profiling |
| ML.NET model incompatibility | Low | High | Test training/prediction before migration |
| Hangfire serialization issues | Low | Medium | Test job execution, backup database |
| ARM32 performance regression | Low | High | Performance benchmarks, memory profiling |
| Third-party package incompatibility | Medium | Medium | Check compatibility matrix first |
| Docker build failures | Low | Low | Multi-stage fallback Dockerfiles |
| Test failures in .NET 10 runtime | Medium | Medium | Fix tests incrementally |
| Production deployment issues | Low | Critical | Staged rollout, rollback plan |

---

## Success Criteria

| Metric | Target | Measurement |
|--------|--------|-------------|
| Test Pass Rate | 100% (790+ tests) | All test suites green |
| Performance Improvement | ≥5% faster | Benchmark comparison |
| Memory Footprint | <300MB on ARM32 | Docker stats monitoring |
| Build Success | 100% clean builds | No warnings/errors |
| Zero Breaking Changes | API compatibility maintained | Acceptance tests pass |
| Code Coverage | ≥82% maintained | Coverage reports |
| Classification Accuracy | ≥82% maintained | ML model validation |
| Deployment Success | Clean QNAP deployment | Production validation |

---

## Rollback Plan

| Step | Action | Time | Notes |
|------|--------|------|-------|
| 1 | Keep .NET 8 Docker images tagged | N/A | Always maintain previous version |
| 2 | Maintain migration branch separate from main | N/A | Don't merge until validated |
| 3 | If critical issue: revert to .NET 8 image | 5 min | Docker container restart |
| 4 | If deployment fails: rollback database migrations | 10 min | EF migration scripts |
| 5 | Document rollback reason and blockers | 30 min | For future reference |

---

## Post-Migration Tasks

| # | Task | Timeline | Owner |
|---|------|----------|-------|
| 9.1 | Monitor production performance for 1 week | Week 5 | DevOps |
| 9.2 | Collect user feedback on Web app | Week 5 | Product |
| 9.3 | Archive .NET 8 Docker images | Week 6 | DevOps |
| 9.4 | Update team knowledge base | Week 5 | Tech Lead |
| 9.5 | Plan MAUI .NET 10 migration | Week 6 | Mobile Team |
| 9.6 | Plan .NET 11 migration strategy | Future | Architecture |

---

## Key .NET 10 Features to Adopt (Moderate Approach)

| Feature | Benefit | Complexity | Recommendation |
|---------|---------|------------|----------------|
| Improved ARM64 intrinsics | 10-15% performance gain | Low | ✅ Adopt |
| HTTP/3 support | Better network performance | Low | ✅ Adopt |
| DATAS GC improvements | Better memory management | Low | ✅ Adopt |
| New LINQ operators | Cleaner code | Low | ✅ Adopt |
| Enhanced System.Text.Json | Faster serialization | Low | ✅ Adopt |
| Improved async streams | Better throughput | Medium | ✅ Adopt |
| New minimal API features | Cleaner endpoints | Medium | ⚠️ Optional |
| C# 13 language features | Modern syntax | Medium | ⚠️ Optional |
| Native AOT support | Faster startup | High | ❌ Skip (ARM32 complexity) |

---

## Timeline Summary

```
Week 1: Pre-Migration + Project Updates + Packages
├─ Days 1-2: Analysis & compatibility checks
├─ Days 3-4: Update all .csproj files (excluding MAUI)
└─ Day 5:    Update NuGet packages

Week 2: Code Changes + Performance Optimization
├─ Days 1-3: Address breaking changes
└─ Days 4-5: Performance tuning

Week 3: Testing & Docker
├─ Days 1-5: Comprehensive testing (790+ tests)
└─ Days 6-7: Docker builds & deployment prep

Week 4: Documentation & Production Deployment
├─ Days 1-3: Update all documentation
├─ Days 4-5: Staged production deployment
└─ Days 6-7: Monitoring & validation
```

---

## Critical Dependencies

1. **.NET 10 SDK** installed on all development machines
2. **Docker** with .NET 10 base images available
3. **QNAP NAS** test environment for ARM32 validation
4. **Backup** of current production database
5. **Test data** for ML model validation

---

## Communication Plan

| Milestone | Stakeholder Update | Format |
|-----------|-------------------|--------|
| Pre-migration analysis complete | Status report | Email |
| All projects migrated | Demo/walkthrough | Meeting |
| Testing complete | Test results | Report |
| Production deployment | Go/No-go decision | Meeting |
| Post-deployment validation | Success metrics | Dashboard |
| MAUI migration planning | Separate planning session | Meeting |

---

## Notes

- **MAUI (MediaButler.Mobile)** is explicitly excluded from this migration
- MAUI will remain on .NET 9 until manual migration is planned and executed separately
- All solution references to MAUI project will remain unchanged
- MAUI team will coordinate separate migration timeline after backend stabilizes on .NET 10

---

**Migration Start Date**: TBD
**Expected Completion**: 3-4 weeks from start
**Migration Lead**: TBD
**Status**: Planning Phase
