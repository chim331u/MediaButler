# MediaButler - AI Agent Developer Guidelines

This document defines the strict operational rules, architectural principles, and collaboration guidelines that any AI coding assistant or agent MUST follow when working on the MediaButler repository.

---

## 🤝 1. Collaboration & Language Rules

### 🇮🇹 User Communication Language
* **Rule**: The AI agent MUST always communicate and respond to the USER in **Italian** (if possible), maintaining a professional, concise, and humble tone.

### 🛑 Commit and Push Consent (Mandatory)
* **Rule**: You are strictly prohibited from performing `git commit` or `git push` automatically.
* **Action**: You MUST implement and test changes locally, verify that they compile/run, and present the diff/summary to the user. You MUST explicitly ask for user consent *before* staging, committing, or pushing code.

### 🤖 Subagent Delegation Policy
To prevent context bloat and ensure clean modular development, delegate specialized tasks to the appropriate background subagents:
* **`backend_agent`**: Use for Go backend API, SQLite integration, filesystem watchers, and statistical Naive Bayes calculations.
* **`frontend_agent`**: Use for Svelte/Vite UI development, HSL dark-mode styling, and real-time SSE listener configurations.
* **`android_agent`**: Use for Kotlin/Jetpack Compose Android app tasks, OkHttp-SSE networks, and Android SDK compilations.
* **`research`**: Use for read-only codebase analysis, ontology searching, or literature reviews.

---

## 🏛️ 2. Architectural Principles: "Simple Made Easy"

Every design decision MUST adhere to Rich Hickey's *Simple Made Easy* philosophy:
1. **Decoupled Concerns**: Keep code orthogonal. Do not mix database transactions with business logic, and do not let UI styling dictate data processing structures.
2. **Values Over State**: Prefer immutable data structures. Only use mutable state when absolutely necessary to represent an identity changing over time.
3. **Composition Over Inheritance**: Compose small, single-purpose, highly cohesive units rather than inheriting from complex base classes.
4. **Declarative Style**: Describe *what* needs to be achieved (e.g. Go functional iterations, CSS specifications) rather than imperatively writing step-by-step loops.

---

## ⚙️ 3. QNAP NAS & ARM32 v7 Technical Constraints

Since the production environment is a highly resource-constrained QNAP NAS (ARM32v7, 1GB RAM), all implementations MUST enforce:

### 💾 Memory & Disk I/O Protection
* **Copy Throttling**: When performing buffered copy-then-delete file movements, read in a maximum of `1MB` chunks (to save RAM) and introduce a `5ms` sleep (`time.Sleep(5 * time.Millisecond)`) between writes to prevent 100% disk I/O queue saturation.
* **SQLite WAL Concurrency**: Always initialize SQLite in **WAL (Write-Ahead Logging)** mode with `busy_timeout=5000`. Limit the connection pool (`SetMaxOpenConns(5)`) to allow parallel WAL reads without locking, while SQLite automatically serializes write transactions.

### 🌐 Network & Timeout Optimizations
* **Persistent Streaming (SSE)**: The HTTP Server MUST NOT enforce tight `ReadTimeout` or `WriteTimeout` limits (configure them to `0` or disable them). This keeps Server-Sent Events (SSE) connections open indefinitely without triggering client disconnections (`ERR_INCOMPLETE_CHUNKED_ENCODING`).
* **Double Volume Mounts**: Maintain dual-path mapping in `docker-compose.qnap.yml` to support legacy paths (`/watch` and `/library`) and remake paths (`/app/watch` and `/app/dest`) pointing to the same host folders.
* **Local Testing Isolation**: In `docker-compose.yml` (Mac Local Testing), always map the parent directory `./temp` to `/app/data` (rather than the single database file) to prevent macOS from failing to create SQLite WAL companion journals.

---

## 🛠️ 4. Code Quality & Cleanliness Rules

* **No Dead Code**: Immediately remove unused imports or dead variables to keep compiled Go binary sizes minimal (using striping flags `-s -w`).
* **Changelog Integrity**: When structural changes or major fixes are made to Go or Svelte, update the `scripts/CHANGELOG.md` (or similar) file to maintain clean repository traceability.
* **Self-Verification Before Success**: Before presenting a solution or completing a task, the AI agent MUST *automatically* run unit tests and verify the frontend build. Never declare success if tests fail or compilation breaks.

---

## ✍️ 5. AI Response Style Guide

* **Conciseness**: Keep replies focused and professional. Avoid excessive politeness or redundant compliments.
* **Humility**: Avoid superlatives (e.g., "perfectly", "flawlessly", "100% correct", "Summary of Achievements"). Ground responses in the concrete work performed.
* **No Placeholders**: Never use placeholders or mock files. Build fully operational code.

---

## 🧪 6. Verification Checklist

Before ending your turn, you MUST:
1. Run backend unit and integration tests: `go test -v ./...` in `src/backend`.
2. Ensure that Svelte frontend compiles correctly: `npm run build` in `src/frontend`.
3. Reactively update the `docs/task.md` file by inserting the completed action at the top, following the bottom-up specification.
