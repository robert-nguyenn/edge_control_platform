package main

import (
	"context"
	"flag"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"net/http/httputil"
	"net/url"
	"os"
	"os/signal"
	"strconv"
	"sync"
	"syscall"
	"time"

	"github.com/gorilla/mux"
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promhttp"
)

var (
	// Command-line flags
	listenAddr     = flag.String("listen", ":8080", "Address to listen on")
	targetHost     = flag.String("target", "http://api-dotnet:80", "Target service to proxy")
	readTimeout    = flag.Duration("read-timeout", 5*time.Second, "HTTP read timeout")
	writeTimeout   = flag.Duration("write-timeout", 10*time.Second, "HTTP write timeout")
	maxIdleConns   = flag.Int("max-idle-conns", 100, "Maximum number of idle connections")
	maxConnPerHost = flag.Int("max-conn-per-host", 100, "Maximum connections per host")
	retryAttempts  = flag.Int("retry-attempts", 3, "Number of retry attempts for failed requests")
	retryWait      = flag.Duration("retry-wait", 100*time.Millisecond, "Wait time between retries")

	// Circuit breaker settings
	cbThreshold    = flag.Int("cb-threshold", 5, "Number of failures before circuit breaker opens")
	cbResetTimeout = flag.Duration("cb-reset-timeout", 30*time.Second, "Time before circuit breaker resets")

	// Prometheus metrics
	requestsTotal = prometheus.NewCounterVec(
		prometheus.CounterOpts{
			Name: "edge_sidecar_requests_total",
			Help: "Total number of requests processed by the sidecar",
		},
		[]string{"method", "path", "status"},
	)
	requestDuration = prometheus.NewHistogramVec(
		prometheus.HistogramOpts{
			Name:    "edge_sidecar_request_duration_seconds",
			Help:    "Request duration in seconds",
			Buckets: prometheus.DefBuckets,
		},
		[]string{"method", "path"},
	)
	retriesTotal = prometheus.NewCounter(
		prometheus.CounterOpts{
			Name: "edge_sidecar_retries_total",
			Help: "Total number of retry attempts",
		},
	)
	circuitBreakerOpen = prometheus.NewGauge(
		prometheus.GaugeOpts{
			Name: "edge_sidecar_circuit_breaker_open",
			Help: "Circuit breaker status (1 = open, 0 = closed)",
		},
	)
)

// Circuit breaker implementation
type CircuitBreaker struct {
	failureCount   int
	lastFailure    time.Time
	isOpen         bool
	threshold      int
	resetTimeout   time.Duration
	mutex          sync.RWMutex
	resetTimeoutCh chan struct{}
}

func NewCircuitBreaker(threshold int, resetTimeout time.Duration) *CircuitBreaker {
	return &CircuitBreaker{
		threshold:      threshold,
		resetTimeout:   resetTimeout,
		resetTimeoutCh: make(chan struct{}, 1),
	}
}

func (cb *CircuitBreaker) Allow() bool {
	cb.mutex.RLock()
	defer cb.mutex.RUnlock()
	return !cb.isOpen
}

func (cb *CircuitBreaker) RecordSuccess() {
	cb.mutex.Lock()
	defer cb.mutex.Unlock()

	cb.failureCount = 0
	if cb.isOpen {
		cb.isOpen = false
		circuitBreakerOpen.Set(0)
		log.Println("Circuit breaker closed")
	}
}

func (cb *CircuitBreaker) RecordFailure() {
	cb.mutex.Lock()
	defer cb.mutex.Unlock()

	cb.failureCount++
	cb.lastFailure = time.Now()

	if cb.failureCount >= cb.threshold && !cb.isOpen {
		cb.isOpen = true
		circuitBreakerOpen.Set(1)
		log.Printf("Circuit breaker opened after %d failures", cb.failureCount)

		// Start reset timeout
		select {
		case cb.resetTimeoutCh <- struct{}{}:
			go func() {
				time.Sleep(cb.resetTimeout)
				cb.mutex.Lock()
				defer cb.mutex.Unlock()
				cb.isOpen = false
				cb.failureCount = 0
				circuitBreakerOpen.Set(0)
				log.Println("Circuit breaker reset after timeout")
				<-cb.resetTimeoutCh
			}()
		default:
			// Reset already scheduled
		}
	}
}

// ProxyHandler handles the reverse proxy with circuit breaker and retries
type ProxyHandler struct {
	target        *url.URL
	proxy         *httputil.ReverseProxy
	circuitBreaker *CircuitBreaker
}

func NewProxyHandler(targetURL string, cb *CircuitBreaker) (*ProxyHandler, error) {
	target, err := url.Parse(targetURL)
	if err != nil {
		return nil, err
	}

	proxy := httputil.NewSingleHostReverseProxy(target)
	
	// Customize the transport
	defaultTransport := http.DefaultTransport.(*http.Transport).Clone()
	defaultTransport.MaxIdleConns = *maxIdleConns
	defaultTransport.MaxIdleConnsPerHost = *maxConnPerHost
	
	proxy.Transport = defaultTransport

	// Customize error handler
	proxy.ErrorHandler = func(w http.ResponseWriter, r *http.Request, err error) {
		log.Printf("Proxy error: %v", err)
		w.WriteHeader(http.StatusBadGateway)
		w.Write([]byte("Service temporarily unavailable"))
	}

	return &ProxyHandler{
		target:        target,
		proxy:         proxy,
		circuitBreaker: cb,
	}, nil
}

func (h *ProxyHandler) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	start := time.Now()
	path := r.URL.Path
	method := r.Method
	
	// Check circuit breaker
	if !h.circuitBreaker.Allow() {
		log.Printf("Circuit breaker open, rejecting request to %s", path)
		w.WriteHeader(http.StatusServiceUnavailable)
		w.Write([]byte("Service temporarily unavailable - circuit breaker open"))
		requestsTotal.WithLabelValues(method, path, "503").Inc()
		return
	}

	// Perform retries with exponential backoff
	var resp *http.Response
	var err error
	var statusCode int
	
	success := false
	
	for attempt := 0; attempt <= *retryAttempts; attempt++ {
		if attempt > 0 {
			retriesTotal.Inc()
			log.Printf("Retry attempt %d for %s %s", attempt, method, path)
			time.Sleep(*retryWait * time.Duration(attempt))
		}
		
		// Create a custom response writer to capture the status code
		rw := &responseWriter{
			ResponseWriter: w,
			statusCode:     http.StatusOK,
		}

		// Proxy the request
		h.proxy.ServeHTTP(rw, r)
		
		statusCode = rw.statusCode
		
		// Check if the request was successful (2xx or 3xx status)
		if statusCode < 400 {
			success = true
			h.circuitBreaker.RecordSuccess()
			break
		}
		
		// If this was the last attempt and still failed, record a failure
		if attempt == *retryAttempts {
			h.circuitBreaker.RecordFailure()
		}
	}

	duration := time.Since(start).Seconds()
	requestDuration.WithLabelValues(method, path).Observe(duration)
	requestsTotal.WithLabelValues(method, path, strconv.Itoa(statusCode)).Inc()
	
	log.Printf("%s %s - %d - %.2fs", method, path, statusCode, duration)
}

// Custom ResponseWriter to capture status code
type responseWriter struct {
	http.ResponseWriter
	statusCode int
}

func (rw *responseWriter) WriteHeader(code int) {
	rw.statusCode = code
	rw.ResponseWriter.WriteHeader(code)
}

func (rw *responseWriter) Write(b []byte) (int, error) {
	return rw.ResponseWriter.Write(b)
}

func main() {
	flag.Parse()

	// Register Prometheus metrics
	prometheus.MustRegister(requestsTotal, requestDuration, retriesTotal, circuitBreakerOpen)

	// Create circuit breaker
	cb := NewCircuitBreaker(*cbThreshold, *cbResetTimeout)

	// Create proxy handler
	proxyHandler, err := NewProxyHandler(*targetHost, cb)
	if err != nil {
		log.Fatalf("Failed to create proxy handler: %v", err)
	}

	// Create router
	router := mux.NewRouter()
	
	// Add metrics endpoint
	router.Path("/metrics").Handler(promhttp.Handler())
	
	// Add health check endpoint
	router.Path("/healthz").HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(`{"status":"ok","version":"1.0.0"}`))
	})
	
	// Add readiness probe that checks the target service
	router.Path("/ready").HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		targetURL := fmt.Sprintf("%s/healthz", *targetHost)
		resp, err := http.Get(targetURL)
		if err != nil || resp.StatusCode != http.StatusOK {
			w.WriteHeader(http.StatusServiceUnavailable)
			w.Write([]byte("Target service not ready"))
			return
		}
		defer resp.Body.Close()
		
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(`{"status":"ready"}`))
	})

	// All other paths go to the proxy
	router.PathPrefix("/").Handler(proxyHandler)

	// Create server with timeouts
	server := &http.Server{
		Addr:         *listenAddr,
		Handler:      router,
		ReadTimeout:  *readTimeout,
		WriteTimeout: *writeTimeout,
	}

	// Channel to listen for errors coming from the listener.
	serverErrors := make(chan error, 1)
	
	// Start the server
	log.Printf("Starting sidecar proxy on %s -> %s", *listenAddr, *targetHost)
	go func() {
		serverErrors <- server.ListenAndServe()
	}()

	// Set up graceful shutdown
	shutdown := make(chan os.Signal, 1)
	signal.Notify(shutdown, os.Interrupt, syscall.SIGTERM)

	// Block until receiving shutdown signal or server error
	select {
	case err := <-serverErrors:
		log.Fatalf("Server error: %v", err)
		
	case sig := <-shutdown:
		log.Printf("Shutdown signal received: %v", sig)
		
		// Give outstanding requests a deadline for completion
		ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
		defer cancel()

		// Gracefully shut down the server
		if err := server.Shutdown(ctx); err != nil {
			log.Printf("Graceful shutdown failed: %v", err)
			if err := server.Close(); err != nil {
				log.Printf("Error closing server: %v", err)
			}
		}
	}
}
