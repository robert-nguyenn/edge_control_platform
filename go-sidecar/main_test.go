package main

import (
	"fmt"
	"log"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"
)

func TestCircuitBreaker(t *testing.T) {
	threshold := 3
	resetTimeout := 100 * time.Millisecond
	
	cb := NewCircuitBreaker(threshold, resetTimeout)
	
	// Circuit should start closed
	if !cb.Allow() {
		t.Fatal("Circuit breaker should start closed")
	}
	
	// Record failures until threshold
	for i := 0; i < threshold; i++ {
		cb.RecordFailure()
	}
	
	// Circuit should now be open
	if cb.Allow() {
		t.Fatal("Circuit breaker should be open after threshold failures")
	}
	
	// Wait for reset
	time.Sleep(resetTimeout + 10*time.Millisecond)
	
	// Circuit should be closed again
	if !cb.Allow() {
		t.Fatal("Circuit breaker should reset after timeout")
	}
}

func TestProxyHandler(t *testing.T) {
	// Create a test server
	testServer := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write([]byte("OK"))
	}))
	defer testServer.Close()
	
	// Create circuit breaker
	cb := NewCircuitBreaker(3, 100*time.Millisecond)
	
	// Create proxy handler
	handler, err := NewProxyHandler(testServer.URL, cb)
	if err != nil {
		t.Fatalf("Failed to create proxy handler: %v", err)
	}
	
	// Create a test request
	req := httptest.NewRequest("GET", "/test", nil)
	recorder := httptest.NewRecorder()
	
	// Test the proxy
	handler.ServeHTTP(recorder, req)
	
	// Check the response
	if recorder.Code != http.StatusOK {
		t.Fatalf("Expected status code %d, got %d", http.StatusOK, recorder.Code)
	}
	
	if recorder.Body.String() != "OK" {
		t.Fatalf("Expected body %q, got %q", "OK", recorder.Body.String())
	}
	
	fmt.Println("All tests passed!")
}
