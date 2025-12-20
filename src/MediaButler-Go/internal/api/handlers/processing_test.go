package handlers

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestProcessingHandler_QueueForMLEvaluation(t *testing.T) {
	// Setup
	handler := &ProcessingHandler{}

	// Create request
	payload := MLEvaluationRequest{
		FilterByCategory:  nil,
		ForceReEvaluation: true,
	}
	body, _ := json.Marshal(payload)
	req, err := http.NewRequest("POST", "/api/processing/ml-evaluation/queue", bytes.NewBuffer(body))
	if err != nil {
		t.Fatal(err)
	}

	// Execute
	rr := httptest.NewRecorder()
	handler.QueueForMLEvaluation(rr, req)

	// Verify
	if status := rr.Code; status != http.StatusOK {
		t.Errorf("handler returned wrong status code: got %v want %v",
			status, http.StatusOK)
	}

	var response MLEvaluationResponse
	if err := json.NewDecoder(rr.Body).Decode(&response); err != nil {
		t.Fatalf("failed to decode response: %v", err)
	}

	if !response.Success {
		t.Error("expected Success to be true")
	}
	if response.Message == "" {
		t.Error("expected a message in response")
	}
}
