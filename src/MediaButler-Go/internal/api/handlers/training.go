package handlers

import (
	"encoding/json"
	"net/http"

	"github.com/chim331u/mediabutler-go/internal/service"
)

type TrainingHandler struct {
	mlClient service.MLClient
}

func NewTrainingHandler(mlClient service.MLClient) *TrainingHandler {
	return &TrainingHandler{
		mlClient: mlClient,
	}
}

func (h *TrainingHandler) TrainModel(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()

	// Delegate to ML service
	res := h.mlClient.TrainModel(ctx)

	if res.IsFailure() {
		http.Error(w, res.Error().Error(), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(res.Value())
}
