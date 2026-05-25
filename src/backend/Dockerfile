# Stage 1: Build the Go application
FROM golang:1.23-alpine AS builder

# Set Go environment variables for pure Go build
ENV CGO_ENABLED=0

WORKDIR /app

# Copy dependency manifests
COPY go.mod go.sum ./
RUN go mod download

# Copy source code (Svelte frontend dist folder must be compiled and present in dist/)
COPY . .

# Build the native binary
RUN go build -o mediabutler .

# Stage 2: Minimal runtime image
FROM alpine:latest

# Install dependencies (ca-certificates for external API integrations if needed, tzdata for logs)
RUN apk --no-cache add ca-certificates tzdata

WORKDIR /app

# Copy the binary from builder
COPY --from=builder /app/mediabutler .

# Expose server port
EXPOSE 8080

# Run the Go server
CMD ["./mediabutler"]
