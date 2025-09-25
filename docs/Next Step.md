# 🎩 MediaButler - NEXT STEP list -


[![Version](https://img.shields.io/badge/version-1.0.2-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20x64-green.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

merge web_analysis_panning_complete.md/9. Implementation Plan & Roadmap in dev_planning.md/SPRINT 4: Web Interface & User Experience (Days 13-16)

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

## Status-Specific Behavior

### NEW Status

Category Display: Hidden (no ML classification yet)
Move File: Disabled (requires classification first)
Refresh Button: Disabled (no ML data to refresh)
NotShowAgain: Hidden (no confirmation needed)

### ML CLASSIFIED Status
Category Display: Shows ML-suggested category
Move File: Enabled (ready for organization)
Refresh Button: Enabled (can re-run ML classification)
NotShowAgain: Conditional (visible when move queue > 0)

### MOVED Status
Category Display: Hidden (file already organized)
Move File: Disabled (already moved)
Refresh Button: Disabled (no classification needed)
NotShowAgain: Hidden (operation complete)

### ERROR Status
Category Display: Hidden (classification failed)
Move File: Disabled (needs error resolution)
Refresh Button: Disabled (error state)
NotShowAgain: Hidden (requires manual intervention)

### IGNORED Status
Category Display: Shows assigned category
Move File: Enabled (can still be moved if desired)
Refresh Button: Enabled (can re-classify)
NotShowAgain: Conditional (visible when move queue > 0)

## Action Categories

### Always Available Actions
These actions are available regardless of file status:
- Force Category
- Scan Folder
- Train Model
- Add Select Category
- Add To Move Queue
- View Detail
- Copy Path
- Open Folder
- Delete Action Button

### Status-Dependent Actions
| Action | Available For | Condition |
|---|---|---|
| **Move File** | ML CLASSIFIED, IGNORED | File has valid category |
| **Refresh Button** | ML CLASSIFIED, IGNORED | Can re-run classification |
| **Category Display** | ML CLASSIFIED, IGNORED | Shows current/suggested category |
| **NotShowAgain** | ML CLASSIFIED, IGNORED | Move queue count > 0 |

## Implementation Notes

- **Status Pills**: Visual indicators showing current file state
- **Conditional Visibility**: UI elements show/hide based on status and queue state
- **Queue Integration**: Some actions depend on move queue status
- **Category Management**: Different category display logic per status
- **Error Handling**: Disabled actions for error states to prevent issues