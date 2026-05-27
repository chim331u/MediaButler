package main

import (
	"fmt"
	"log/slog"
	"net/http"
	"sync"
)

type SSEBroker struct {
	mu      sync.RWMutex
	clients map[chan string]bool
}

func NewSSEBroker() *SSEBroker {
	return &SSEBroker{
		clients: make(map[chan string]bool),
	}
}

func (b *SSEBroker) Register(ch chan string) {
	b.mu.Lock()
	defer b.mu.Unlock()
	b.clients[ch] = true
	slog.Debug("SSE Client registered", "total_clients", len(b.clients))
}

func (b *SSEBroker) Unregister(ch chan string) {
	b.mu.Lock()
	defer b.mu.Unlock()
	if _, ok := b.clients[ch]; ok {
		delete(b.clients, ch)
		close(ch)
		slog.Debug("SSE Client unregistered", "total_clients", len(b.clients))
	}
}

func (b *SSEBroker) Broadcast(event, data string) {
	b.mu.RLock()
	defer b.mu.RUnlock()

	slog.Debug("Broadcasting SSE event", "event", event, "clients", len(b.clients))
	// SSE format is:
	// event: name
	// data: payload
	// \n\n
	message := fmt.Sprintf("event: %s\ndata: %s\n\n", event, data)
	for ch := range b.clients {
		select {
		case ch <- message:
		default:
			slog.Warn("SSE Client channel full, skipping message")
		}
	}
}

func (b *SSEBroker) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	flusher, ok := w.(http.Flusher)
	if !ok {
		http.Error(w, "Streaming unsupported", http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "text/event-stream")
	w.Header().Set("Cache-Control", "no-cache, no-transform")
	w.Header().Set("Connection", "keep-alive")
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.Header().Set("X-Accel-Buffering", "no")

	ch := make(chan string, 10)
	b.Register(ch)
	defer b.Unregister(ch)

	// Send an initial ping / handshake
	_, _ = fmt.Fprintf(w, "event: connected\ndata: {}\n\n")
	flusher.Flush()

	notify := r.Context().Done()
	for {
		select {
		case <-notify:
			return
		case msg, ok := <-ch:
			if !ok {
				return
			}
			_, err := fmt.Fprint(w, msg)
			if err != nil {
				slog.Error("Failed to write SSE message", "err", err)
				return
			}
			flusher.Flush()
		}
	}
}
