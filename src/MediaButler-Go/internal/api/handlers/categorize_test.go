package handlers

import (
	"bytes"
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/chim331u/mediabutler-go/internal/service"
	"github.com/chim331u/mediabutler-go/pkg/result"
)

// MockMLClientCategorize for testing
type MockMLClientCategorize struct{}

func (m *MockMLClientCategorize) Classify(ctx context.Context, filePath string) result.Result[service.ClassificationResult] {
	if filePath == "error.jpg" {
		return result.Failure[service.ClassificationResult](nil)
	}
	return result.Success(service.ClassificationResult{
		Category:   "Documents",
		Confidence: 0.95,
	})
}

func (m *MockMLClientCategorize) TrainModel(ctx context.Context) result.Result[service.TrainingStartResponse] {
	return result.Success(service.TrainingStartResponse{})
}

func TestProcessingHandler_CategorizeFile(t *testing.T) {
	// Setup
	mockML := &MockMLClientCategorize{}
	handler := NewProcessingHandler(nil, nil, mockML)

	// Test case: Success
	payload := map[string]string{"filename": "test.pdf"}
	body, _ := json.Marshal(payload)
	req, _ := http.NewRequest("POST", "/api/processing/ml/categorize", bytes.NewBuffer(body))
	rr := httptest.NewRecorder()

	handler.CategorizeFile(rr, req)

	if rr.Code != http.StatusOK {
		t.Errorf("handler returned wrong status code: got %v want %v", rr.Code, http.StatusOK)
	}

	var response service.ClassificationResult
	json.NewDecoder(rr.Body).Decode(&response)
	if response.Category != "Documents" {
		t.Errorf("handler returned unexpected category: got %v want %v", response.Category, "Documents")
	}

	// Test case: Missing filename
	payload = map[string]string{"filename": ""}
	body, _ = json.Marshal(payload)
	req, _ = http.NewRequest("POST", "/api/processing/ml/categorize", bytes.NewBuffer(body))
	rr = httptest.NewRecorder()

	handler.CategorizeFile(rr, req)

	if rr.Code != http.StatusBadRequest {
		t.Errorf("handler returned wrong status code for empty filename: got %v want %v", rr.Code, http.StatusBadRequest)
	}
}
