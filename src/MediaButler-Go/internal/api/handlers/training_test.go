package handlers

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/chim331u/mediabutler-go/internal/service"
	"github.com/chim331u/mediabutler-go/pkg/result"
)

// MockMLClient for testing
type MockMLClient struct{}

func (m *MockMLClient) Classify(ctx context.Context, filePath string) result.Result[service.ClassificationResult] {
	return result.Success(service.ClassificationResult{})
}

func (m *MockMLClient) TrainModel(ctx context.Context) result.Result[service.TrainingStartResponse] {
	return result.Success(service.TrainingStartResponse{
		SessionId: "test_session",
		Status:    "Started",
		Message:   "Test started",
		StartedAt: time.Now(),
	})
}

func TestTrainingHandler_TrainModel(t *testing.T) {
	mockClient := &MockMLClient{}
	handler := NewTrainingHandler(mockClient)

	// Test GET method as requested by frontend
	req, err := http.NewRequest("GET", "/api/training/trainModel", nil)
	if err != nil {
		t.Fatal(err)
	}

	rr := httptest.NewRecorder()
	handler.TrainModel(rr, req)

	if status := rr.Code; status != http.StatusOK {
		t.Errorf("handler returned wrong status code: got %v want %v",
			status, http.StatusOK)
	}

	var response service.TrainingStartResponse
	if err := json.NewDecoder(rr.Body).Decode(&response); err != nil {
		t.Fatalf("failed to decode response: %v", err)
	}

	if response.SessionId != "test_session" {
		t.Errorf("unexpected session id: got %v want %v", response.SessionId, "test_session")
	}
}
