package handlers

import (
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/lucapaganotti/mediabutler-go/internal/sse"
	"github.com/rs/zerolog"
)

// SSEHandler handles Server-Sent Events connections
type SSEHandler struct {
	broker *sse.Broker
	logger zerolog.Logger
}

// NewSSEHandler creates a new SSE handler
func NewSSEHandler(broker *sse.Broker, logger zerolog.Logger) *SSEHandler {
	return &SSEHandler{
		broker: broker,
		logger: logger.With().Str("handler", "sse").Logger(),
	}
}

// HandleSSE handles SSE connections from clients
func (h *SSEHandler) HandleSSE(w http.ResponseWriter, r *http.Request) {
	// Verify that response writer supports flushing
	flusher, ok := w.(http.Flusher)
	if !ok {
		h.logger.Error().Msg("Streaming unsupported - ResponseWriter does not support Flusher")
		http.Error(w, "Streaming unsupported", http.StatusInternalServerError)
		return
	}

	// Set SSE headers
	w.Header().Set("Content-Type", "text/event-stream")
	w.Header().Set("Cache-Control", "no-cache")
	w.Header().Set("Connection", "keep-alive")
	w.Header().Set("X-Accel-Buffering", "no") // Disable nginx buffering

	// CORS headers (should already be set by middleware, but double-check)
	if origin := r.Header.Get("Origin"); origin != "" {
		w.Header().Set("Access-Control-Allow-Origin", origin)
		w.Header().Set("Access-Control-Allow-Credentials", "true")
	}

	// Generate unique client ID
	clientID := uuid.New().String()

	h.logger.Info().
		Str("client_id", clientID).
		Str("remote_addr", r.RemoteAddr).
		Msg("New SSE client connecting")

	// Subscribe client to broker
	client := h.broker.Subscribe(r.Context(), clientID)
	defer func() {
		h.broker.Unsubscribe(client)
		h.logger.Info().
			Str("client_id", clientID).
			Msg("SSE client disconnected")
	}()

	// Send initial connection event
	connectedEvent := sse.ConnectedEvent{
		ClientID: clientID,
		Time:     time.Now(),
	}
	h.sendEvent(w, flusher, sse.EventConnected, connectedEvent, "")

	// Heartbeat ticker (every 30 seconds to keep connection alive)
	heartbeat := time.NewTicker(30 * time.Second)
	defer heartbeat.Stop()

	// Event loop - listen for events or context cancellation
	for {
		select {
		case <-r.Context().Done():
			// Client disconnected
			h.logger.Debug().
				Str("client_id", clientID).
				Msg("Client context cancelled")
			return

		case <-heartbeat.C:
			// Send heartbeat comment (keeps connection alive, not a data event)
			fmt.Fprintf(w, ": heartbeat\n\n")
			flusher.Flush()

		case event := <-client.Channel:
			// Send actual event to client
			h.sendSSEEvent(w, flusher, event)
		}
	}
}

// sendSSEEvent formats and sends an SSE event to the client
func (h *SSEHandler) sendSSEEvent(w http.ResponseWriter, flusher http.Flusher, event sse.Event) {
	// Write event ID (for client reconnection)
	if event.ID != "" {
		fmt.Fprintf(w, "id: %s\n", event.ID)
	}

	// Write event type
	fmt.Fprintf(w, "event: %s\n", event.Type)

	// Write event data (JSON)
	fmt.Fprintf(w, "data: %s\n", string(event.Data))

	// Write retry interval (optional)
	if event.Retry > 0 {
		fmt.Fprintf(w, "retry: %d\n", event.Retry)
	}

	// End event with double newline
	fmt.Fprintf(w, "\n")

	// Flush to client immediately
	flusher.Flush()
}

// sendEvent is a helper to send a typed event (used for initial connection event)
func (h *SSEHandler) sendEvent(w http.ResponseWriter, flusher http.Flusher, eventType string, data interface{}, eventID string) {
	// Marshal data manually for connection event
	if eventType == sse.EventConnected {
		connEvent, ok := data.(sse.ConnectedEvent)
		if ok {
			fmt.Fprintf(w, "event: %s\n", eventType)
			fmt.Fprintf(w, "data: {\"clientId\":\"%s\",\"time\":\"%s\"}\n", connEvent.ClientID, connEvent.Time.Format(time.RFC3339))
			fmt.Fprintf(w, "\n")
			flusher.Flush()
		}
	}
}

// GetStats returns statistics about SSE connections
func (h *SSEHandler) GetStats(w http.ResponseWriter, r *http.Request) {
	stats := map[string]interface{}{
		"connectedClients": h.broker.GetClientCount(),
		"timestamp":        time.Now(),
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)

	// Simple JSON response
	fmt.Fprintf(w, `{"connectedClients":%d,"timestamp":"%s"}`,
		stats["connectedClients"],
		stats["timestamp"].(time.Time).Format(time.RFC3339))
}
