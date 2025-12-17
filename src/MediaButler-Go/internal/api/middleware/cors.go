package middleware

import (
	"net/http"

	"github.com/go-chi/cors"
)

// CORS returns a middleware that handles CORS with default allowed origins
func CORS() func(http.Handler) http.Handler {
	return cors.Handler(cors.Options{
		AllowedOrigins: []string{
			"http://localhost:*",
			"http://127.0.0.1:*",
			"http://192.168.1.5:*",
			"http://balerion331.myqnapcloud.com:4711",
			"http://balerion331.myqnapcloud.com:4712",
		},
		AllowedMethods:   []string{"GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH"},
		AllowedHeaders:   []string{"Accept", "Authorization", "Content-Type", "X-Request-ID"},
		ExposedHeaders:   []string{"Link", "X-Total-Count", "X-Request-ID"},
		AllowCredentials: true,
		MaxAge:           300, // Maximum value not ignored by any of major browsers
	})
}

// CORSWithConfig returns a CORS middleware with custom configuration
func CORSWithConfig(allowedOrigins []string, allowCredentials bool) func(http.Handler) http.Handler {
	return cors.Handler(cors.Options{
		AllowedOrigins:   allowedOrigins,
		AllowedMethods:   []string{"GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH"},
		AllowedHeaders:   []string{"Accept", "Authorization", "Content-Type", "X-Request-ID"},
		ExposedHeaders:   []string{"Link", "X-Total-Count", "X-Request-ID"},
		AllowCredentials: allowCredentials,
		MaxAge:           300,
	})
}
