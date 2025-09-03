#!/bin/bash
set -e

# Create a local kind cluster for Edge Control Platform
echo "🚀 Setting up local Kubernetes cluster for Edge Control Platform..."

# Check if kind is installed
if ! command -v kind &> /dev/null; then
    echo "❌ kind is not installed. Please install kind first."
    exit 1
fi

# Check if kubectl is installed
if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl is not installed. Please install kubectl first."
    exit 1
fi

# Create kind cluster if it doesn't exist
if ! kind get clusters | grep -q "edge-control"; then
    echo "📦 Creating kind cluster 'edge-control'..."
    kind create cluster --name edge-control --config - <<EOF
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
- role: control-plane
  kubeadmConfigPatches:
  - |
    kind: InitConfiguration
    nodeRegistration:
      kubeletExtraArgs:
        node-labels: "ingress-ready=true"
  extraPortMappings:
  - containerPort: 80
    hostPort: 80
    protocol: TCP
  - containerPort: 443
    hostPort: 443
    protocol: TCP
EOF
else
    echo "ℹ️ kind cluster 'edge-control' already exists, reusing..."
fi

# Install NGINX Ingress Controller
echo "🌐 Installing NGINX Ingress Controller..."
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml

# Wait for ingress controller to be ready
echo "⏳ Waiting for NGINX Ingress Controller to be ready..."
kubectl wait --namespace ingress-nginx \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=90s

# Build and load Docker images into kind
echo "🏗️ Building and loading Docker images into kind..."
cd ..

# Build and load api-dotnet
echo "📦 Building api-dotnet image..."
docker build -t edge-control/api-dotnet:latest ./api-dotnet/
kind load docker-image edge-control/api-dotnet:latest --name edge-control

# Build and load rate-limiter
echo "📦 Building rate-limiter image..."
docker build -t edge-control/rate-limiter:latest ./rate-limiter-cpp/
kind load docker-image edge-control/rate-limiter:latest --name edge-control

# Build and load go-sidecar
echo "📦 Building go-sidecar image..."
docker build -t edge-control/go-sidecar:latest ./go-sidecar/
kind load docker-image edge-control/go-sidecar:latest --name edge-control

# Build and load web-admin
echo "📦 Building web-admin image..."
docker build -t edge-control/web-admin:latest ./web-admin/
kind load docker-image edge-control/web-admin:latest --name edge-control

# Apply Kubernetes manifests
echo "🔄 Applying Kubernetes manifests..."
cd k8s
kubectl apply -f 00-config.yaml
kubectl apply -f 01-postgres.yaml
kubectl apply -f 02-redis.yaml

echo "⏳ Waiting for database to be ready..."
kubectl wait --namespace edge-control \
  --for=condition=ready pod \
  --selector=app=postgres \
  --timeout=60s

kubectl wait --namespace edge-control \
  --for=condition=ready pod \
  --selector=app=redis \
  --timeout=60s

kubectl apply -f 03-rate-limiter.yaml
kubectl apply -f 04-api-dotnet.yaml

echo "⏳ Waiting for API to be ready..."
kubectl wait --namespace edge-control \
  --for=condition=ready pod \
  --selector=app=api-dotnet \
  --timeout=120s

kubectl apply -f 05-go-sidecar.yaml
kubectl apply -f 06-web-admin.yaml
kubectl apply -f 07-prometheus.yaml
kubectl apply -f 08-grafana.yaml
kubectl apply -f 09-ingress.yaml

echo "⏳ Waiting for all services to be ready..."
kubectl wait --namespace edge-control \
  --for=condition=ready pod \
  --selector=app=go-sidecar \
  --timeout=60s

# Run seed script to set up initial data
echo "🌱 Running seed script..."
kubectl exec -it -n edge-control deployment/api-dotnet -- curl -s http://localhost:80/healthz

echo "🎉 Edge Control Platform is now running on Kubernetes!"
echo ""
echo "📊 Access the services at:"
echo "- Web Admin: http://localhost/"
echo "- API: http://localhost/api"
echo "- Prometheus: http://localhost/metrics"
echo "- Grafana: http://localhost/grafana (admin/admin)"
echo ""
echo "📝 Use kubectl commands to interact with the cluster:"
echo "kubectl get pods -n edge-control"
echo "kubectl logs -n edge-control deployment/api-dotnet"
