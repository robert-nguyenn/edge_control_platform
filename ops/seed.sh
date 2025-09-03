#!/bin/bash

# Edge Control Platform - Seed Script
# This script seeds the database with initial feature flags

API_BASE_URL=${API_BASE_URL:-"http://localhost:5000"}
API_KEY=${API_KEY:-"demo-key"}

echo "🌱 Seeding Edge Control Platform database..."
echo "API Base URL: $API_BASE_URL"

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
    echo "Attempt $i/30: API not ready yet, waiting 2 seconds..."
    sleep 2
done

# Function to create or update a flag
create_flag() {
    local key=$1
    local description=$2
    local rollout=$3
    
    echo "Creating flag: $key"
    curl -X PUT "$API_BASE_URL/flags/$key" \
         -H "Content-Type: application/json" \
         -H "Authorization: Bearer $API_KEY" \
         -d "{
           \"description\": \"$description\",
           \"rolloutPercent\": $rollout,
           \"rules\": {}
         }" \
         -s -o /dev/null -w "HTTP %{http_code}\n"
}

# Create seed flags
echo "📊 Creating seed flags..."

create_flag "pricing.v2" "Enable new pricing model v2" 0
create_flag "exp.freeShipping" "Experiment: Free shipping promotion" 0

# Additional demo flags
create_flag "ui.newDashboard" "Enable new dashboard UI" 25
create_flag "feature.chatSupport" "Enable live chat support" 50
create_flag "exp.recommendationEngine" "ML-powered recommendation engine" 10

echo "🎉 Seeding completed successfully!"
echo ""
echo "📋 Available flags:"
curl -s "$API_BASE_URL/flags" | jq -r '.[] | "  - \(.key): \(.description) (\(.rolloutPercent)%)"'
echo ""
echo "🌐 Admin UI: http://localhost:3000"
echo "🔗 API Health: $API_BASE_URL/healthz"
