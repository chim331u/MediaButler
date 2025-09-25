# 🎩 MediaButler - NEXT STEP list -

## ✅ COMPLETED TASKS

### Web UI Implementation (Sprint 4 - COMPLETED)
- ✅ **UI Elements & Actions Matrix** - Fully implemented in Files.razor
- ✅ **ML Classification Integration** - Real ML service integrated in ProcessingController.cs
- ✅ **Move Queue Functionality** - ReadyToMove → Move → Revert workflow implemented
- ✅ **Health Check Display** - Fixed Database status mapping issue
- ✅ **Navigation Structure** - Files.razor as home page, updated sidebar
- ✅ **Icon Updates** - Health Status with health_and_safety icon

[![Version](https://img.shields.io/badge/version-1.0.3-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20x64-green.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010-purple.svg)]()

## UPCOMING TASKS

Increase test coverage task 1.7.1

Suggestions to improve Claude Code:
```

Use the phrase “Prepare to discuss.” This lets Claude Code build up context before jumping into code.

Build and reuse context with Double Escape and Resume. When you reach a “smart” state, fork it into new tabs and tasks. This saves time and keeps consistency across PRs.

Plan in PR-sized chunks. Ask for function names, short descriptions, and test names. Then implement chunk by chunk with linting, compiling, and tests to keep the feedback loop tight.

Run a planner vs. developer split. Use one session to plan and critique, another to implement. Have the planner review the developer’s steps and provide concrete feedback. Tip: use the phrase “my developer” so Claude doesn’t assume it is critiquing its own code.

Avoid Compact. If the window gets messy, rewind to a clean earlier state instead of patching bad context. Always restart from a good checkpoint.
```

-AGENTS TO CREATE

[] Documentation agent
[] c# agent specialist


# Next Steps
# MediaButler File Management Component Specification

## Component Overview

The SearchBarComponent displays files with different status-based UI states and available actions.

## File Status Types

- **NEW** - Recently discovered files
- **ML CLASSIFIED** - Files processed by ML classification
- **MOVED** - Successfully organized files
- **ERROR** - Files with processing errors
- **IGNORED** - Files marked to ignore

## UI Elements & Actions Matrix

| RadzenSelectBarItem/Action |       ALL       |  NEW  | ML CLASSIFIED | MOVED | ERROR | IGNORED |
|----------------------------|:---------------:|:---:|:-------------:|:-----:|:-------:|:-------:|
| **UI Components**          |                 |     |               |       |
| Status (Pill)              |     From Db     | NEW | ML CLASSIFIED | MOVED | ERROR | IGNORED |
| Add Select Category        | Based on status |  ❌  |       ✅       |   ❌   |    ❌    |    ✅    |
| RadzenDropDown Category    | Based on status |  ❌  |       ✅       |   ❌   |    ✅    |    ✅    |
| Add To Move Queue          | Based on status |  ❌  |       ✅       |   ❌   |    ✅    |    ✅    |
| NotShowAgain               | Based on status |  ✅  |       ✅       |   ✅   |    ❌    |    ✅    |
| ViewDetail                 | Based on status |  ✅  |       ✅       |   ✅   |    ✅    |    ✅    |
| Copy Path                  | Based on status |  ✅  |       ✅       |   ✅   |    ✅    |    ✅    |
| Open Folder                | Based on status |  ✅  |       ✅       |   ✅   |    ✅    |    ✅    |
| Delete                     | Based on status |  ✅  |       ✅       |   ✅   |    ✅    |    ✅    |
| **File Actions**           |                 |     |               |       |         |
| Move File *                |        ❌        |  ❌ |       ✅       |   ❌   |    ❌    |    ✅    |
| Refresh Button             |        ✅        |  ✅  |       ✅       |  ✅    |    ✅    |    ✅    |
| **System Actions**         |                 |     |               |       |         |
| Force Category             |        ✅        |  ✅  |       ✅       |   ✅   |    ✅    |    ✅    |
| Scan Folder                |        ✅        |  ✅  |       ✅       |   ✅   |    ✅    |    ✅    |
| Train Model                |        ✅        |  ✅  |       ✅       |   ✅   |    ✅    |    ✅    |


**Note:** ✅* = Conditional (Active when move queue > 0)

## ✅ IMPLEMENTATION STATUS - COMPLETED

### Files.razor Button Visibility Implementation
All UI elements and actions from the matrix above have been successfully implemented with status-based visibility logic:

```csharp
// Helper methods implemented for conditional button display
private bool ShowAddSelectCategory(string? status) =>
    status?.ToUpperInvariant() switch
    {
        "ML CLASSIFIED" or "CLASSIFIED" => true,
        "READYTOMOVE" => true,
        "IGNORED" => true,
        _ => false
    };

private bool ShowMoveButton(FileManagementDto file) =>
    file.IsInMoveQueue && (file.Status == "ML CLASSIFIED" || file.Status == "READYTOMOVE");
```

### Key Features Implemented:
- ✅ **Dynamic Button Visibility**: Buttons show/hide based on file status
- ✅ **Move Queue Management**: ReadyToMove → Move → Revert workflow
- ✅ **Status Pills with Colors**: Yellow for ReadyToMove, proper status display
- ✅ **ML Classification Integration**: Real ML service replacing simulation
- ✅ **Health Check Fixes**: Database status properly displays OK icon

### Technical Improvements:
- ✅ **Home Page Navigation**: Files.razor is now the default home route
- ✅ **Sidebar Updates**: Removed Files link, added Health Status with proper icon
- ✅ **API Integration**: ProcessingController uses actual IClassificationService
- ✅ **Status Mapping**: Fixed health check status mapping for proper UI display