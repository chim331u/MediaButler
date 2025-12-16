# MediaButler Migration Plan Review & Improvements

## Executive Summary

The proposed migration plan (`eager-purring-puzzle.md`) is **solid and well-structured** for a Proof of Concept (PoC). The choice of Go (Chi, SQLC, Asynq) and the parallel deployment strategy with Nginx are excellent for minimizing risk.

However, verified analysis of the codebase reveals two critical areas requiring adjustment to avoid significant rework or regression:

1.  **ML Integration (High Risk)**: The plan to "wrap existing FastText model in Python" is not feasible without complete retraining and code porting, as the current model is a native ML.NET artifact.
2.  **SignalR Notifications**: The strategy to replace SignalR with SSE/WebSockets would break the existing frontend. A better, non-breaking alternative exists within the current architecture.

---

## 🔍 Critical Findings & Recommendations

### 1. ML Integration: The "Python Wrapper" Fallacy

**Observation**:
The current `FastTextClassificationService` relies on `Microsoft.ML` and loads a `.zip` model (`classification-simplified-model.zip`). This is a .NET-serialized format, **incompatible with Python libraries** (like `fasttext` or `scikit-learn`).

Additionally, the classification logic involves significant C# feature engineering (`ExtractSeriesName`, tokenization) that would need to be manually ported to Python.

**Risk**:
-   Cannot simply "load" the existing model in Python.
-   Requires retraining a new model from scratch in Python.
-   Requires rewriting complex regex/tokenization logic in Python.
-   Increases PoC scope significantly.

**✅ Recommended Improvement**: **Keep ML in .NET (Internal Service)**
Instead of creating a Python service, expose the classification logic as an internal endpoint on the .NET API.
-   **Implementation**: Add a simple internal endpoint in .NET (e.g., `POST /internal/classify`).
-   **Workflow**: The Go Service calls this .NET endpoint via HTTP when it needs to classify a file.
-   **Benefit**: Zero ML migration effort, 100% fidelity to existing classification logic, no new Python dependency.

### 2. SignalR & Notifications

**Observation**:
The plan suggests replacing SignalR with "SSE + WebSocket fallback". Since the goal is only migrating the API (not the Frontend), removing SignalR **breaks the existing Web UI** which listens to SignalR Hubs.

**Analysis**:
The codebase contains `SignalRNotificationClient.cs` and `NotificationsController.cs` which implement a **Bridge Pattern**:
-   The API exposes `POST /api/notifications/batch`.
-   This endpoint broadcasts received messages to SignalR Hubs.

**✅ Recommended Improvement**: **Use the Existing Bridge**
Do not implement SSE or WebSockets in Go.
-   **Implementation**: The Go API should send notifications by making HTTP POST requests to the .NET API's `/api/notifications/batch` endpoint.
-   **benefit**: The Frontend remains completely untouched. The Go backend effectively "sends" SignalR messages via the .NET proxy.

### 3. Database & Concurrency

**Observation**:
Sharing a SQLite database between two processes is viable but risky if both write heavily.
-   **Go**: Handles File Operations (Writes).
-   **.NET**: Handles Legacy endpoints (Reads/Writes?).

**✅ Recommendation**:
Ensure strictly **Single Writer Principle** where possible.
-   Verify that .NET "Background Jobs" that write to the DB are disabled/paused if Go takes over those responsibilities.
-   Enable `WAL` mode (Write-Ahead Logging) on the SQLite database if not already enabled.

### 4. JSON Serialization

**Observation**:
The plan states "Match .NET casing (PascalCase)" but the .NET `Program.cs` explicitly configures `camelCase`.

```csharp
options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
```

**✅ Correction**:
The Go API must use `camelCase` for JSON properties to match the *actual* behavior of the .NET API (e.g., `json:"fileName"` not `FileName`).

---

## 🛠 Revised Phase 2: Tech Stack

| Component | Original Plan | **Revised Proposal** | Rationale |
| :--- | :--- | :--- | :--- |
| **ML** | Python + FastAPI | **.NET Internal API** | Avoids model retraining & code porting. |
| **Real-time** | SSE / WebSockets | **HTTP → .NET Bridge** | Preserves Frontend compatibility (SignalR). |
| **JSON** | PascalCase | **camelCase** | Matches actual .NET configuration. |

## 🚀 Revised Phase 7: Implementation Roadmap

**Week 2 Change (Services)**:
-   [DELETE] ~~Wrap ML.NET model in Python~~
-   [NEW] Create `POST /internal/classify` endpoint in .NET (if not exists) or use `TrainingController`.
-   [NEW] Implement `MLClient` in Go that calls .NET URL.

**Week 3 Change (HTTP API)**:
-   [DELETE] ~~Implement SSE~~
-   [NEW] Implement `NotificationService` in Go that POSTs to `.NET/api/notifications/batch`.

---

## Summary of Action

1.  **Approve** the plan with the above modifications.
2.  **Proceed** with Go setup (Phase 7 - Week 1).
