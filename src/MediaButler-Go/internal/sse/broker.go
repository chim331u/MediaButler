package sse

import (
	"context"
	"encoding/json"
	"fmt"
	"sync"
	"time"

	"github.com/rs/zerolog"
)

// Event represents an SSE event to be sent to clients
type Event struct {
	Type  string          `json:"type"`
	Data  json.RawMessage `json:"data"`
	ID    string          `json:"id,omitempty"`
	Retry int             `json:"retry,omitempty"` // milliseconds
}

// Client represents a connected SSE client
type Client struct {
	ID      string
	Channel chan Event
	Context context.Context
}

// Broker manages SSE clients and broadcasts events
type Broker struct {
	clients    map[string]*Client
	mu         sync.RWMutex
	register   chan *Client
	unregister chan *Client
	broadcast  chan Event
	logger     zerolog.Logger
}

const (
	// ClientBufferSize is the buffer size for each client's event channel
	ClientBufferSize = 10
	// BroadcastBufferSize is the buffer size for the global broadcast channel
	BroadcastBufferSize = 100
	// SlowClientTimeout is the timeout for slow clients (events will be dropped)
	SlowClientTimeout = 1 * time.Second
)

// NewBroker creates a new SSE broker instance
func NewBroker(logger zerolog.Logger) *Broker {
	b := &Broker{
		clients:    make(map[string]*Client),
		register:   make(chan *Client),
		unregister: make(chan *Client),
		broadcast:  make(chan Event, BroadcastBufferSize),
		logger:     logger.With().Str("component", "sse-broker").Logger(),
	}
	go b.run()
	return b
}

// run is the main event loop for the broker
func (b *Broker) run() {
	b.logger.Info().Msg("SSE broker started")

	for {
		select {
		case client := <-b.register:
			b.mu.Lock()
			b.clients[client.ID] = client
			b.mu.Unlock()
			b.logger.Info().
				Str("client_id", client.ID).
				Int("total_clients", len(b.clients)).
				Msg("Client registered")

		case client := <-b.unregister:
			b.mu.Lock()
			if _, ok := b.clients[client.ID]; ok {
				close(client.Channel)
				delete(b.clients, client.ID)
				b.logger.Info().
					Str("client_id", client.ID).
					Int("total_clients", len(b.clients)).
					Msg("Client unregistered")
			}
			b.mu.Unlock()

		case event := <-b.broadcast:
			b.mu.RLock()
			clientCount := len(b.clients)
			droppedCount := 0

			for clientID, client := range b.clients {
				select {
				case client.Channel <- event:
					// Successfully sent
				case <-time.After(SlowClientTimeout):
					// Client too slow, skip this event
					droppedCount++
					b.logger.Warn().
						Str("client_id", clientID).
						Str("event_type", event.Type).
						Msg("Dropped event for slow client")
				}
			}
			b.mu.RUnlock()

			b.logger.Debug().
				Str("event_type", event.Type).
				Int("clients", clientCount).
				Int("dropped", droppedCount).
				Msg("Event broadcasted")
		}
	}
}

// Subscribe registers a new SSE client and returns the client instance
func (b *Broker) Subscribe(ctx context.Context, clientID string) *Client {
	client := &Client{
		ID:      clientID,
		Channel: make(chan Event, ClientBufferSize),
		Context: ctx,
	}
	b.register <- client
	return client
}

// Unsubscribe removes an SSE client
func (b *Broker) Unsubscribe(client *Client) {
	b.unregister <- client
}

// Broadcast sends an event to all connected clients
func (b *Broker) Broadcast(eventType string, data interface{}) error {
	jsonData, err := json.Marshal(data)
	if err != nil {
		return fmt.Errorf("marshal event data: %w", err)
	}

	event := Event{
		Type:  eventType,
		Data:  jsonData,
		ID:    fmt.Sprintf("%d", time.Now().UnixNano()),
		Retry: 3000, // 3 second retry
	}

	select {
	case b.broadcast <- event:
		return nil
	case <-time.After(100 * time.Millisecond):
		return fmt.Errorf("broadcast channel full, event dropped")
	}
}

// GetClientCount returns the current number of connected clients
func (b *Broker) GetClientCount() int {
	b.mu.RLock()
	defer b.mu.RUnlock()
	return len(b.clients)
}
