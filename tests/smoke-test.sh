#!/bin/bash

# Edge Control Platform - Smoke Tests
# Tests basic functionality and caching behavior

API_BASE_URL=${API_BASE_URL:-"http://localhost:5000"}
API_KEY=${API_KEY:-"demo-key"}

echo "🧪 Running Edge Control Platform Smoke Tests"
echo "API Base URL: $API_BASE_URL"
echo "=================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counter
TESTS_RUN=0
TESTS_PASSED=0

# Helper function to run tests
run_test() {
    local test_name="$1"
    local test_command="$2"
    local expected_code="$3"
    
    TESTS_RUN=$((TESTS_RUN + 1))
    echo -n "Test $TESTS_RUN: $test_name... "
    
    if [ -n "$expected_code" ]; then
        response_code=$(eval "$test_command" 2>/dev/null | tail -n1)
        if [ "$response_code" = "HTTP $expected_code" ]; then
            echo -e "${GREEN}PASS${NC}"
            TESTS_PASSED=$((TESTS_PASSED + 1))
        else
            echo -e "${RED}FAIL${NC} (Expected: HTTP $expected_code, Got: $response_code)"
        fi
    else
        if eval "$test_command" >/dev/null 2>&1; then
            echo -e "${GREEN}PASS${NC}"
            TESTS_PASSED=$((TESTS_PASSED + 1))
        else
            echo -e "${RED}FAIL${NC}"
        fi
    fi
}

# Wait for API to be ready
echo "⏳ Waiting for API to be ready..."
for i in {1..30}; do
    if curl -f "$API_BASE_URL/healthz" > /dev/null 2>&1; then
        echo "✅ API is ready!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "❌ API failed to start after 30 attempts"
        exit 1
    fi
    sleep 1
done

echo ""

# Test 1: Health check
run_test "Health check" "curl -s -o /dev/null -w 'HTTP %{http_code}' '$API_BASE_URL/healthz'" "200"

# Test 2: Get existing flag
run_test "Get pricing.v2 flag" "curl -s -o /dev/null -w 'HTTP %{http_code}' '$API_BASE_URL/flags/pricing.v2'" "200"

# Test 3: Get flag with ETag
echo -n "Test $((TESTS_RUN + 1)): ETag caching... "
TESTS_RUN=$((TESTS_RUN + 1))

# First request to get ETag
etag=$(curl -s -I "$API_BASE_URL/flags/pricing.v2" | grep -i etag | cut -d' ' -f2 | tr -d '\r\n')
if [ -n "$etag" ]; then
    # Second request with If-None-Match
    response_code=$(curl -s -o /dev/null -w '%{http_code}' -H "If-None-Match: $etag" "$API_BASE_URL/flags/pricing.v2")
    if [ "$response_code" = "304" ]; then
        echo -e "${GREEN}PASS${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo -e "${RED}FAIL${NC} (Expected: 304, Got: $response_code)"
    fi
else
    echo -e "${RED}FAIL${NC} (No ETag received)"
fi

# Test 4: Update flag
run_test "Update pricing.v2 to 10%" "curl -s -X PUT -H 'Content-Type: application/json' -H 'Authorization: Bearer $API_KEY' -d '{\"rolloutPercent\": 10}' '$API_BASE_URL/flags/pricing.v2' -o /dev/null -w 'HTTP %{http_code}'" "200"

# Test 5: Verify update
echo -n "Test $((TESTS_RUN + 1)): Verify flag update... "
TESTS_RUN=$((TESTS_RUN + 1))

rollout=$(curl -s "$API_BASE_URL/flags/pricing.v2" | jq -r '.rolloutPercent')
if [ "$rollout" = "10" ]; then
    echo -e "${GREEN}PASS${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}FAIL${NC} (Expected: 10, Got: $rollout)"
fi

# Test 6: List all flags
run_test "List all flags" "curl -s -o /dev/null -w 'HTTP %{http_code}' '$API_BASE_URL/flags'" "200"

# Test 7: Get audit log
run_test "Get audit log" "curl -s -o /dev/null -w 'HTTP %{http_code}' '$API_BASE_URL/audit?flag=pricing.v2&limit=5'" "200"

# Test 8: Create new flag
run_test "Create new test flag" "curl -s -X PUT -H 'Content-Type: application/json' -H 'Authorization: Bearer $API_KEY' -d '{\"description\": \"Test flag for smoke test\", \"rolloutPercent\": 25}' '$API_BASE_URL/flags/test.smokeTest' -o /dev/null -w 'HTTP %{http_code}'" "200"

# Test 9: Delete test flag (cleanup)
run_test "Cleanup test flag" "curl -s -X PUT -H 'Content-Type: application/json' -H 'Authorization: Bearer $API_KEY' -d '{\"rolloutPercent\": 0}' '$API_BASE_URL/flags/test.smokeTest' -o /dev/null -w 'HTTP %{http_code}'" "200"

# Test 10: Cache headers
echo -n "Test $((TESTS_RUN + 1)): Cache-Control header... "
TESTS_RUN=$((TESTS_RUN + 1))

cache_header=$(curl -s -I "$API_BASE_URL/flags/pricing.v2" | grep -i cache-control | cut -d' ' -f2- | tr -d '\r\n')
if [[ "$cache_header" == *"max-age=5"* ]] && [[ "$cache_header" == *"stale-while-revalidate=30"* ]]; then
    echo -e "${GREEN}PASS${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}FAIL${NC} (Expected cache headers, Got: $cache_header)"
fi

# Reset pricing.v2 to 0% for next test run
curl -s -X PUT -H 'Content-Type: application/json' -H 'Authorization: Bearer demo-key' -d '{"rolloutPercent": 0}' "$API_BASE_URL/flags/pricing.v2" > /dev/null

echo ""
echo "=================================="
echo "🎯 Test Results: $TESTS_PASSED/$TESTS_RUN tests passed"

if [ $TESTS_PASSED -eq $TESTS_RUN ]; then
    echo -e "${GREEN}✅ All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}❌ Some tests failed!${NC}"
    exit 1
fi
