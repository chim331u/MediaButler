package handlers

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestProcessingHandler_QueueForMLEvaluation_Debug(t *testing.T) {
	handler := &ProcessingHandler{}

	// Case 1: Valid Request
	payload := MLEvaluationRequest{
		FilterByCategory:  nil,
		ForceReEvaluation: true,
	}
	body, _ := json.Marshal(payload)
	req, _ := http.NewRequest("POST", "/api/processing/ml-evaluation/queue", bytes.NewBuffer(body))
	rr := httptest.NewRecorder()

	handler.QueueForMLEvaluation(rr, req)

	if rr.Code != http.StatusOK {
		t.Errorf("Valid request failed: got %v want %v", rr.Code, http.StatusOK)
	}

	// Case 2: Invalid JSON (to trigger log)
	invalidBody := []byte(`{"forceReEvaluation": falsie}`)
	reqInvalid, _ := http.NewRequest("POST", "/api/processing/ml-evaluation/queue", bytes.NewBuffer(invalidBody))
	rrInvalid := httptest.NewRecorder()

	handler.QueueForMLEvaluation(rrInvalid, reqInvalid)

	if rrInvalid.Code != http.StatusBadRequest {
		t.Errorf("Invalid request should return 400: got %v", rrInvalid.Code)
	}
}
