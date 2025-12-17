package handlers

import (
	"net/http"
	"net/http/httputil"
	"net/url"
	"strings"

	"github.com/rs/zerolog"
)

// SignalRProxy handles proxying SignalR requests to the backend .NET service
type SignalRProxy struct {
	target *url.URL
	proxy  *httputil.ReverseProxy
	logger zerolog.Logger
}

// NewSignalRProxy creates a new SignalRProxy
func NewSignalRProxy(targetURL string, logger zerolog.Logger) (*SignalRProxy, error) {
	url, err := url.Parse(targetURL)
	if err != nil {
		return nil, err
	}

	proxy := httputil.NewSingleHostReverseProxy(url)

	// Custom director to handle path rewriting if necessary
	originalDirector := proxy.Director
	proxy.Director = func(req *http.Request) {
		originalDirector(req)

		// Ensure Host header matches target for correct routing/CORS in .NET
		req.Host = url.Host

		// SignalR specific headers
		if req.Header.Get("Connection") == "Upgrade" && req.Header.Get("Upgrade") == "websocket" {
			req.Header.Set("Connection", "Upgrade")
			req.Header.Set("Upgrade", "websocket")
		}
	}

	// Error handler
	proxy.ErrorHandler = func(w http.ResponseWriter, r *http.Request, err error) {
		logger.Error().Err(err).
			Str("method", r.Method).
			Str("url", r.URL.String()).
			Msg("SignalR proxy error")
		http.Error(w, "SignalR Gateway Error", http.StatusBadGateway)
	}

	return &SignalRProxy{
		target: url,
		proxy:  proxy,
		logger: logger,
	}, nil
}

// ServeHTTP implements http.Handler
func (p *SignalRProxy) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	// Log the proxy request for debugging
	p.logger.Debug().
		Str("method", r.Method).
		Str("path", r.URL.Path).
		Msg("Proxying SignalR request")

	// Verify it's a notification request
	if !strings.HasPrefix(r.URL.Path, "/notifications") {
		p.logger.Warn().
			Str("path", r.URL.Path).
			Msg("Invalid proxy path")
		http.NotFound(w, r)
		return
	}

	p.proxy.ServeHTTP(w, r)
}
