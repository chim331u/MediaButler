package service

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"net/http"

	"github.com/lucapaganotti/mediabutler-go/internal/config"
	"github.com/lucapaganotti/mediabutler-go/pkg/result"
)

// MLClient defines the interface for ML service interaction
type MLClient interface {
	Classify(ctx context.Context, filePath string) result.Result[ClassificationResult]
}

// ClassificationResult represents the ML prediction
type ClassificationResult struct {
	Category   string  `json:"category"`
	Confidence float64 `json:"confidence"`
}

// mlClient implements MLClient using HTTP
type mlClient struct {
	client  *http.Client
	baseURL string
}

// NewMLClient creates a new MLClient instance
func NewMLClient(cfg config.MLConfig) MLClient {
	return &mlClient{
		client: &http.Client{
			Timeout: cfg.Timeout,
		},
		baseURL: cfg.ServiceURL,
	}
}

type classifyRequest struct {
	FilePath string `json:"filePath"`
}

type classifyResponse struct {
	Category   string  `json:"category"`
	Confidence float64 `json:"confidence"`
}

// Classify calls the .NET internal API to classify a file
func (c *mlClient) Classify(ctx context.Context, filePath string) result.Result[ClassificationResult] {
	reqBody := classifyRequest{FilePath: filePath}
	jsonBody, err := json.Marshal(reqBody)
	if err != nil {
		return result.Failure[ClassificationResult](fmt.Errorf("marshal request: %w", err))
	}

	url := fmt.Sprintf("%s/internal/ml/classify", c.baseURL)
	req, err := http.NewRequestWithContext(ctx, "POST", url, bytes.NewBuffer(jsonBody))
	if err != nil {
		return result.Failure[ClassificationResult](fmt.Errorf("create request: %w", err))
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := c.client.Do(req)
	if err != nil {
		return result.Failure[ClassificationResult](fmt.Errorf("ml service request: %w", err))
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return result.Failure[ClassificationResult](fmt.Errorf("ml service error: status %d", resp.StatusCode))
	}

	var respBody classifyResponse
	if err := json.NewDecoder(resp.Body).Decode(&respBody); err != nil {
		return result.Failure[ClassificationResult](fmt.Errorf("decode response: %w", err))
	}

	return result.Success(ClassificationResult{
		Category:   respBody.Category,
		Confidence: respBody.Confidence,
	})
}
