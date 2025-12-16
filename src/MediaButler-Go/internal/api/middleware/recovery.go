package middleware

import (
	"fmt"
	"net/http"
	"runtime/debug"

	"github.com/rs/zerolog/log"
)

// Recovery is a middleware that recovers from panics and logs the error
func Recovery() func(next http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			defer func() {
				if err := recover(); err != nil {
					// Log the panic
					log.Error().
						Str("method", r.Method).
						Str("path", r.URL.Path).
						Str("request_id", GetRequestID(r)).
						Interface("panic", err).
						Bytes("stack", debug.Stack()).
						Msg("Panic recovered")

					// Return 500 Internal Server Error
					w.Header().Set("Content-Type", "application/json")
					w.WriteHeader(http.StatusInternalServerError)
					fmt.Fprintf(w, `{"error": "Internal server error", "request_id": "%s"}`, GetRequestID(r))
				}
			}()

			next.ServeHTTP(w, r)
		})
	}
}
