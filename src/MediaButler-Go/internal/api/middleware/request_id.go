package middleware

import (
	"net/http"

	"github.com/go-chi/chi/v5/middleware"
)

// RequestID is a middleware that injects a request ID into the context of each request
// It uses Chi's built-in RequestID middleware
func RequestID() func(next http.Handler) http.Handler {
	return middleware.RequestID
}

// GetRequestID retrieves the request ID from the context
func GetRequestID(r *http.Request) string {
	return middleware.GetReqID(r.Context())
}
