# API Endpoints Reference 📚

Generated from the codebase: consolidated list of available API endpoints with HTTP method and a short note about purpose.

> Versioning: routes include explicit version where present (e.g., `/api/v1/...`).

| Name (route) | Version | Type | Note |
|---|---:|---|---|
| `/api/v1/file-actions/organize-batch` | v1 | POST | Queue a batch organize job (submit files and configuration) |
| `/api/v1/file-actions/batch-status/{jobId}` | v1 | GET | Get status and progress of a batch job |
| `/api/v1/file-actions/batch-cancel/{jobId}` | v1 | POST | Request cancellation of a batch job |
| `/api/v1/file-actions/batch-jobs` | v1 | GET | List recent/filtered batch jobs (pagination supported) |
| `/api/v1/file-actions/validate-batch` | v1 | POST | Validate a batch organize request without executing it |
| `/api/v1/file-actions/ignore/{hash}` | v1 | POST | Mark a file (by hash) as ignored |
| `/api/files` | - | GET | List tracked files (paging & filtering) |
| `/api/files/by-statuses` | - | GET | List files filtered by multiple statuses (paged) |
| `/api/files/{hash}` | - | GET | Get details for a tracked file by hash |
| `/api/files` | - | POST | Register a new file for tracking |
| `/api/files/pending` | - | GET | Get files awaiting user confirmation |
| `/api/files/ready-for-classification` | - | GET | Get files ready for ML classification |
| `/api/files/{hash}/confirm` | - | POST | Confirm a file's category (mark ready) |
| `/api/files/{hash}/moved` | - | POST | Mark a file as moved (target path provided) |
| `/api/files/{hash}` | - | DELETE | Soft-delete a tracked file |
| `/api/files/categories` | - | GET | Get distinct categories used by files |
| `/api/files/scan` | - | POST | Trigger a scan of configured watch folders |
| `/api/files/scan/folder` | - | POST | Trigger a scan of a specific folder (custom path) |
| `/api/system/storage` | - | GET | Get storage/disk usage information |
| `/api/system/memory` | - | GET | Get current memory usage info |
| `/api/stats/processing` | - | GET | Processing statistics (file counts, perf) |
| `/api/stats/ml-performance` | - | GET | ML classification performance & accuracy |
| `/api/stats/system-health` | - | GET | System health metrics (errors, perf) |
| `/api/stats/activity` | - | GET | File processing activity for a date range |
| `/api/stats/categories` | - | GET | Category distribution (counts & percentages) |
| `/api/stats/throughput` | - | GET | Throughput metrics (files processed over time) |
| `/api/stats/errors` | - | GET | Error analysis & failure patterns |
| `/api/stats/file-sizes` | - | GET | File size distribution statistics |
| `/api/stats/trends` | - | GET | Historical trends for metrics |
| `/api/stats/dashboard` | - | GET | Dashboard summary with key metrics |
| `/api/stats/performance` | - | GET | System performance metrics |
| `/api/processing/queue/status` | - | GET | Processing queue status (size, active jobs) |
| `/api/processing/ml-evaluation/queue` | - | POST | Queue files for ML re-evaluation |
| `/api/notificationtest/job-progress` | - | POST | Test notification: job progress |
| `/api/notificationtest/file-move` | - | POST | Test notification: file move |
| `/api/notificationtest/system-status` | - | POST | Test notification: system status |
| `/api/notificationtest/error` | - | POST | Test notification: error |
| `/api/health` | - | GET | Basic health status (version, timestamp) |
| `/api/health/detailed` | - | GET | Detailed health including DB & ML status |
| `/api/health/ready` | - | GET | Readiness probe (returns 503 if not ready) |
| `/api/health/live` | - | GET | Liveness probe |
| `/api/health/ml` | - | GET | ML service health |
| `/api/metrics/health` | - | GET | System health summary (metrics & alerts) |
| `/api/metrics/queue` | - | GET | Queue and throughput metrics |
| `/api/metrics/classification` | - | GET | ML classification metrics |
| `/api/metrics/errors` | - | GET | Error rate metrics |
| `/api/metrics/performance` | - | GET | Performance & resource utilization |
| `/api/metrics/status` | - | GET | Simplified system status for monitoring |
| `/api/metrics/ping` | - | GET | Minimal probe endpoint (ping) |
| `/api/training/start` | - | POST | Start ML model training (background job) |
| `/api/training/status/{sessionId}` | - | GET | Get training session status |
| `/api/training/sessions` | - | GET | Get recent/active training sessions |
| `/api/examples/batch-organize-requests` | - | GET | Example batch organize requests (samples) |
| `/api/examples/api-usage` | - | GET | Example API usage and workflows |
| `/api/examples/system-info` | - | GET | System configuration & capability info |

---

If you spot any missing endpoints, or want this exported to CSV / added to `README.md`, tell me and I can update it. ✨
