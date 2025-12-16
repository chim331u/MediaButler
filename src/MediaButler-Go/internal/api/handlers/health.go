package handlers

import (
	"encoding/json"
	"net/http"
	"runtime"
	"time"
)

// HealthHandler handles health check endpoints
type HealthHandler struct {
	version   string
	startTime time.Time
}

// NewHealthHandler creates a new HealthHandler
func NewHealthHandler(version string) *HealthHandler {
	return &HealthHandler{
		version:   version,
		startTime: time.Now(),
	}
}

// HealthResponse represents the basic health check response
type HealthResponse struct {
	Status    string    `json:"status"`
	Version   string    `json:"version"`
	Timestamp time.Time `json:"timestamp"`
}

// DetailedHealthResponse represents the detailed health check response
type DetailedHealthResponse struct {
	Status      string    `json:"status"`
	Version     string    `json:"version"`
	Timestamp   time.Time `json:"timestamp"`
	Uptime      string    `json:"uptime"`
	GoVersion   string    `json:"goVersion"`
	NumGoroutine int      `json:"numGoroutine"`
	MemoryUsage  MemoryUsage `json:"memoryUsage"`
}

// MemoryUsage represents memory usage statistics
type MemoryUsage struct {
	AllocMB      uint64 `json:"allocMB"`
	TotalAllocMB uint64 `json:"totalAllocMB"`
	SysMB        uint64 `json:"sysMB"`
	NumGC        uint32 `json:"numGC"`
}

// Health handles GET /api/health
// Returns basic health status
func (h *HealthHandler) Health(w http.ResponseWriter, r *http.Request) {
	response := HealthResponse{
		Status:    "healthy",
		Version:   h.version,
		Timestamp: time.Now().UTC(),
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(response)
}

// Ready handles GET /api/health/ready
// Returns readiness status (503 if not ready)
func (h *HealthHandler) Ready(w http.ResponseWriter, r *http.Request) {
	// TODO: Add actual readiness checks (database connectivity, etc.)
	// For now, always return ready
	response := HealthResponse{
		Status:    "ready",
		Version:   h.version,
		Timestamp: time.Now().UTC(),
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(response)
}

// Detailed handles GET /api/health/detailed
// Returns detailed health information including system metrics
func (h *HealthHandler) Detailed(w http.ResponseWriter, r *http.Request) {
	var m runtime.MemStats
	runtime.ReadMemStats(&m)

	uptime := time.Since(h.startTime)

	response := DetailedHealthResponse{
		Status:       "healthy",
		Version:      h.version,
		Timestamp:    time.Now().UTC(),
		Uptime:       uptime.String(),
		GoVersion:    runtime.Version(),
		NumGoroutine: runtime.NumGoroutine(),
		MemoryUsage: MemoryUsage{
			AllocMB:      m.Alloc / 1024 / 1024,
			TotalAllocMB: m.TotalAlloc / 1024 / 1024,
			SysMB:        m.Sys / 1024 / 1024,
			NumGC:        m.NumGC,
		},
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(response)
}
