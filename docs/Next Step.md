# 🎩 MediaButler - NEXT STEP list -

[![Version](https://img.shields.io/badge/version-1.0.3-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20x64-green.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010-purple.svg)]()

## UPCOMING TASKS

### Recently Completed ✅
- **ML Training Pipeline Fixes (v1.0.3)**
  - Fixed API response serialization: TrainingStatus enum → string conversion
  - Fixed ML.NET schema mismatch: Added MapValueToKey transformation for Category → Label
  - Enhanced training data retrieval with batch processing (API pagination compliance)
  - Added fallback logic for insufficient training data from multiple file statuses

- **LastView Page Implementation (v1.0.4)**
  - Created modernized LastView.razor page with contemporary design patterns
  - Implemented expandable file categories with hover effects and animations
  - Added real-time search functionality across filenames and categories
  - Integrated with current FilesApiService and FileManagementDto structure
  - Added SignalR integration for real-time updates when files are moved
  - Updated navigation menu to include "Recent Files" page
  - Modernized from legacy FC_WEB implementation with improved UX

Increase test coverage task 1.7.1

Suggestions to improve Claude Code:
```
sample code or description
```

-AGENTS TO CREATE

- [ ] Documentation agent
- [ ] c# agent specialist


# Next Steps
- [ ] Implement last view page: review all
- [ ] Improve test coverage to 80% (currently at 65%)
- ✅ Review the load records from old db based on import folder - set in config
- [ ] Docker deploy


✅ **COMPLETED**: FileCat Migration Tool Updated
- ✅ Filter only IsActive = true records from FileCat database
- ✅ Migrate filesize as-is to new TrackedFiles table
- ✅ Migrate filecat.name to TrackedFile.FileName with filename refactoring using watch folder method
- ✅ Migrate path to TargetPath field
- ✅ Migrate filecategory to Category (uppercased)
- ✅ Migrate lastupdate date to LastUpdateDate
- ✅ Status mapping: IsNotToMove = 1 → Status = 8 (Ignored)
- ✅ Status mapping: IsToCategorize = 0 → Status = 2 (Classified)
- ✅ Status mapping: All other records → Status = 5 (Moved)
- ✅ Added project file and fixed compilation issues
- ✅ Ready for production migration from FileCat to MediaButler