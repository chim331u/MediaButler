package domain

// ProcessingStats represents aggregated processing metrics
type ProcessingStats struct {
	Total       int64 `json:"total"`
	Pending     int64 `json:"pending"`
	Classified  int64 `json:"classified"`
	InReview    int64 `json:"inReview"`
	ReadyToMove int64 `json:"readyToMove"`
	Moved       int64 `json:"moved"`
	Failed      int64 `json:"failed"`
	Ignored     int64 `json:"ignored"`
}
