package middleware

import (
	"net/http"
	"time"

	"github.com/go-chi/chi/v5/middleware"
	"github.com/rs/zerolog"
	"github.com/rs/zerolog/log"
)

// ZerologLogger is a middleware that logs HTTP requests using zerolog
func ZerologLogger(logger zerolog.Logger) func(next http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			start := time.Now()

			// Wrap response writer to capture status code
			ww := middleware.NewWrapResponseWriter(w, r.ProtoMajor)

			// Process request
			next.ServeHTTP(ww, r)

			// Log after request completes
			duration := time.Since(start)

			logger.Info().
				Str("method", r.Method).
				Str("path", r.URL.Path).
				Str("query", r.URL.RawQuery).
				Int("status", ww.Status()).
				Int("bytes", ww.BytesWritten()).
				Dur("duration", duration).
				Str("remote_addr", r.RemoteAddr).
				Str("user_agent", r.UserAgent()).
				Str("request_id", middleware.GetReqID(r.Context())).
				Msg("HTTP request")
		})
	}
}

// DefaultLogger returns a middleware with default zerolog configuration
func DefaultLogger() func(next http.Handler) http.Handler {
	return ZerologLogger(log.Logger)
}
