package service

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/chim331u/mediabutler-go/internal/config"
)

// Test NewMLClient
func TestNewMLClient_CreatesClientWithConfig(t *testing.T) {
	cfg := config.MLConfig{
		ServiceURL: "http://localhost:5000",
		Timeout:    time.Second * 5,
	}

	client := NewMLClient(cfg)

	if client == nil {
		t.Error("expected non-nil client")
	}
}

// Test Classify with successful response
func TestMLClient_Classify_Success(t *testing.T) {
	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Verify request
		if r.Method != "POST" {
			t.Errorf("expected POST request, got %s", r.Method)
		}

		if r.URL.Path != "/internal/ml/classify" {
			t.Errorf("expected path '/internal/ml/classify', got '%s'", r.URL.Path)
		}

		if r.Header.Get("Content-Type") != "application/json" {
			t.Errorf("expected Content-Type 'application/json', got '%s'", r.Header.Get("Content-Type"))
		}

		// Decode request body
		var req classifyRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			t.Errorf("failed to decode request: %v", err)
		}

		if req.FilePath != "/path/to/Breaking.Bad.S01E01.mkv" {
			t.Errorf("expected filePath '/path/to/Breaking.Bad.S01E01.mkv', got '%s'", req.FilePath)
		}

		// Send response
		resp := classifyResponse{
			Category:   "BREAKING BAD",
			Confidence: 0.95,
		}

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		json.NewEncoder(w).Encode(resp)
	}))
	defer server.Close()

	// Create client
	cfg := config.MLConfig{
		ServiceURL: server.URL,
		Timeout:    time.Second * 5,
	}
	client := NewMLClient(cfg)

	// Test classification
	result := client.Classify(context.Background(), "/path/to/Breaking.Bad.S01E01.mkv")

	if result.IsFailure() {
		t.Errorf("expected success, got failure: %v", result.Error())
	}

	classification := result.Value()
	if classification.Category != "BREAKING BAD" {
		t.Errorf("expected category 'BREAKING BAD', got '%s'", classification.Category)
	}

	if classification.Confidence != 0.95 {
		t.Errorf("expected confidence 0.95, got %f", classification.Confidence)
	}
}

// Test Classify with server error
func TestMLClient_Classify_ServerError(t *testing.T) {
	// Create mock server that returns error
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusInternalServerError)
		w.Write([]byte("Internal Server Error"))
	}))
	defer server.Close()

	cfg := config.MLConfig{
		ServiceURL: server.URL,
		Timeout:    time.Second * 5,
	}
	client := NewMLClient(cfg)

	result := client.Classify(context.Background(), "/path/to/file.mkv")

	if result.IsSuccess() {
		t.Error("expected failure for server error, got success")
	}
}

// Test Classify with bad request
func TestMLClient_Classify_BadRequest(t *testing.T) {
	// Create mock server that returns 400
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusBadRequest)
		w.Write([]byte("Bad Request"))
	}))
	defer server.Close()

	cfg := config.MLConfig{
		ServiceURL: server.URL,
		Timeout:    time.Second * 5,
	}
	client := NewMLClient(cfg)

	result := client.Classify(context.Background(), "")

	if result.IsSuccess() {
		t.Error("expected failure for bad request, got success")
	}
}

// Test Classify with invalid JSON response
func TestMLClient_Classify_InvalidJSONResponse(t *testing.T) {
	// Create mock server that returns invalid JSON
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		w.Write([]byte("invalid json"))
	}))
	defer server.Close()

	cfg := config.MLConfig{
		ServiceURL: server.URL,
		Timeout:    time.Second * 5,
	}
	client := NewMLClient(cfg)

	result := client.Classify(context.Background(), "/path/to/file.mkv")

	if result.IsSuccess() {
		t.Error("expected failure for invalid JSON, got success")
	}
}

// Test Classify with timeout
func TestMLClient_Classify_Timeout(t *testing.T) {
	// Create mock server that delays response
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		time.Sleep(time.Second * 2)
		w.WriteHeader(http.StatusOK)
		json.NewEncoder(w).Encode(classifyResponse{
			Category:   "TEST",
			Confidence: 0.9,
		})
	}))
	defer server.Close()

	// Create client with short timeout
	cfg := config.MLConfig{
		ServiceURL: server.URL,
		Timeout:    time.Millisecond * 100,
	}
	client := NewMLClient(cfg)

	result := client.Classify(context.Background(), "/path/to/file.mkv")

	if result.IsSuccess() {
		t.Error("expected failure for timeout, got success")
	}
}

// Test Classify with context cancellation
func TestMLClient_Classify_ContextCancellation(t *testing.T) {
	// Create mock server that delays response
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		time.Sleep(time.Second * 2)
		w.WriteHeader(http.StatusOK)
	}))
	defer server.Close()

	cfg := config.MLConfig{
		ServiceURL: server.URL,
		Timeout:    time.Second * 10,
	}
	client := NewMLClient(cfg)

	// Create context that cancels immediately
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	result := client.Classify(ctx, "/path/to/file.mkv")

	if result.IsSuccess() {
		t.Error("expected failure for cancelled context, got success")
	}
}

// Test Classify with various confidence levels
func TestMLClient_Classify_VariousConfidenceLevels(t *testing.T) {
	testCases := []struct {
		name       string
		confidence float64
	}{
		{"High confidence", 0.99},
		{"Medium confidence", 0.75},
		{"Low confidence", 0.45},
		{"Zero confidence", 0.0},
		{"Perfect confidence", 1.0},
	}

	for _, tc := range testCases {
		t.Run(tc.name, func(t *testing.T) {
			server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				resp := classifyResponse{
					Category:   "TEST SERIES",
					Confidence: tc.confidence,
				}

				w.Header().Set("Content-Type", "application/json")
				w.WriteHeader(http.StatusOK)
				json.NewEncoder(w).Encode(resp)
			}))
			defer server.Close()

			cfg := config.MLConfig{
				ServiceURL: server.URL,
				Timeout:    time.Second * 5,
			}
			client := NewMLClient(cfg)

			result := client.Classify(context.Background(), "/path/to/file.mkv")

			if result.IsFailure() {
				t.Errorf("expected success, got failure: %v", result.Error())
			}

			classification := result.Value()
			if classification.Confidence != tc.confidence {
				t.Errorf("expected confidence %f, got %f", tc.confidence, classification.Confidence)
			}
		})
	}
}

// Test Classify with different file paths
func TestMLClient_Classify_DifferentFilePaths(t *testing.T) {
	testCases := []struct {
		name     string
		filePath string
	}{
		{"Simple filename", "/path/to/file.mkv"},
		{"With special characters", "/path/to/file (2023) [1080p].mkv"},
		{"With spaces", "/path/to/my file name.mkv"},
		{"With unicode", "/path/to/файл.mkv"},
		{"Long path", "/very/long/path/with/many/nested/directories/file.mkv"},
	}

	for _, tc := range testCases {
		t.Run(tc.name, func(t *testing.T) {
			var receivedPath string

			server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				var req classifyRequest
				json.NewDecoder(r.Body).Decode(&req)
				receivedPath = req.FilePath

				resp := classifyResponse{
					Category:   "TEST",
					Confidence: 0.9,
				}

				w.Header().Set("Content-Type", "application/json")
				w.WriteHeader(http.StatusOK)
				json.NewEncoder(w).Encode(resp)
			}))
			defer server.Close()

			cfg := config.MLConfig{
				ServiceURL: server.URL,
				Timeout:    time.Second * 5,
			}
			client := NewMLClient(cfg)

			result := client.Classify(context.Background(), tc.filePath)

			if result.IsFailure() {
				t.Errorf("expected success, got failure: %v", result.Error())
			}

			if receivedPath != tc.filePath {
				t.Errorf("expected filePath '%s', got '%s'", tc.filePath, receivedPath)
			}
		})
	}
}

// Test Classify with empty category response
func TestMLClient_Classify_EmptyCategoryResponse(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		resp := classifyResponse{
			Category:   "",
			Confidence: 0.1,
		}

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		json.NewEncoder(w).Encode(resp)
	}))
	defer server.Close()

	cfg := config.MLConfig{
		ServiceURL: server.URL,
		Timeout:    time.Second * 5,
	}
	client := NewMLClient(cfg)

	result := client.Classify(context.Background(), "/path/to/unknown.mkv")

	if result.IsFailure() {
		t.Errorf("expected success (with empty category), got failure: %v", result.Error())
	}

	classification := result.Value()
	if classification.Category != "" {
		t.Errorf("expected empty category, got '%s'", classification.Category)
	}
}
