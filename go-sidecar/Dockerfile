FROM golang:1.21-alpine AS builder

WORKDIR /app

# Copy go.mod and go.sum first to leverage Docker caching
COPY go.mod ./
COPY go.sum ./
RUN go mod download

# Copy source code
COPY *.go ./

# Build the binary
RUN CGO_ENABLED=0 GOOS=linux go build -ldflags="-s -w" -o sidecar .

# Use a minimal alpine image for the final container
FROM alpine:3.18

WORKDIR /app

# Install ca-certificates for HTTPS
RUN apk --no-cache add ca-certificates

# Copy the binary from builder
COPY --from=builder /app/sidecar .

# Expose the sidecar port
EXPOSE 8080

# Set entry point
ENTRYPOINT ["/app/sidecar"]
