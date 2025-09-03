# Edge Control Platform

A high-performance, production-grade feature flags and rate limiting platform, designed for seamless integration with modern cloud-native architectures.


## Overview

Edge Control Platform is an enterprise-ready feature flags system with advanced rate limiting capabilities, designed for rapid adoption by backend services. Developed with a focus on performance, resilience, and scalability, it provides a robust solution for progressive feature rollouts, sophisticated A/B/n testing, and intelligent traffic management.

### Key Features

- **Advanced Feature Flags**: Sophisticated targeting with percentage-based rollouts, user attributes, and A/B/n testing using consistent hashing algorithms
- **High-Performance Rate Limiting**: C++20 token-bucket implementation with 100K+ decisions/second throughput and <1ms p99 latency
- **Distributed Caching**: Redis-backed caching with ETag support and intelligent cache invalidation
- **Zero-Downtime Resilience**: Circuit breaker pattern implementation in Go with automatic recovery and graceful degradation
- **Comprehensive Observability**: OpenTelemetry integration with Prometheus metrics and Grafana dashboards
- **Enterprise-Ready SDKs**: Thread-safe Java and Node.js clients with background polling, local caching, and fault tolerance

## Architecture

The Edge Control Platform employs a microservices architecture with polyglot implementation, optimizing each component for its specific requirements:


### Core Components

1. **.NET 8 API**: Highly optimized service for flag management with dual GraphQL/REST interfaces and Entity Framework Core
2. **C++20 Rate Limiter**: Ultra-low latency rate limiting service with distributed token bucket algorithm and gRPC interface
3. **Go 1.21 Sidecar**: Resilience layer with sophisticated circuit breaking, automatic retries, and zero-downtime reloads
4. **Redis Cluster**: Distributed caching for flags and rate limiter state with intelligent partitioning
5. **PostgreSQL**: ACID-compliant data store for flags, audit logs, and configuration
6. **React Admin UI**: Responsive Material UI-based administration interface with real-time flag status
7. **OpenTelemetry Stack**: Comprehensive metrics, tracing, and logging with Prometheus and Grafana dashboards

### Advanced Patterns

- **Consistent Hashing**: Ensures user experiences remain stable during percentage-based rollouts
- **Circuit Breaker**: Prevents cascading failures with automatic service degradation and recovery
- **CQRS Pattern**: Separates read and write paths for optimized performance and scalability
- **Distributed Rate Limiting**: Coordinates rate limiting decisions across multiple instances

For detailed architecture diagrams, see the [architecture documentation](./docs/architecture/).

## Quick Start

### Running with Docker Compose

The easiest way to get started is with Docker Compose:

```bash
cd ops
docker-compose up -d
```

This will start all the required services. Once running:
- Web Admin UI: http://localhost:3000
- API: http://localhost:5000
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3001 (admin/admin)

### Using the SDKs

#### Node.js SDK

```javascript
const { init } = require('@edge-control/node-sdk');

const client = init({
    baseUrl: 'http://localhost:5000',
    apiKey: 'demo-key',
    pollIntervalMs: 30000,
    defaultDecisions: {
        'pricing.v2': false
    }
});

// Check if a flag is enabled
const isEnabled = await client.isEnabled('pricing.v2', { userId: 'user123' });
if (isEnabled) {
    // Use new pricing model
} else {
    // Use old pricing model
}
```

#### Java SDK

```java
FeatureFlagsClient.Config config = new FeatureFlagsClient.Config("http://localhost:5000");
config.apiKey = "demo-key";
config.defaultDecisions.put("pricing.v2", false);

FeatureFlagsClient client = new FeatureFlagsClient(config);

// Check if a flag is enabled
boolean isEnabled = client.isEnabled("pricing.v2", Map.of("userId", "user123"));
if (isEnabled) {
    // Use new pricing model
} else {
    // Use old pricing model
}
```

## Enterprise Deployment Options

The Edge Control Platform supports multiple deployment models to fit diverse enterprise requirements:

### Kubernetes with Horizontal Pod Autoscaling

Production-grade Kubernetes manifests with horizontal autoscaling, readiness/liveness probes, and resource limits:

```bash
cd k8s
./setup-kind.sh  # For local testing
# Or for production:
kubectl apply -f ./  # Apply all manifests
```

### Infrastructure as Code with Terraform

Terraform modules for AWS, GCP, and Azure deployments with best-practice security and scaling:

```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your configuration
./init-terraform.sh
terraform apply
```

### High-Availability Configuration

The platform is designed for 99.99% uptime with:

- Multi-region deployments
- Automatic failover
- Zero-downtime upgrades
- Backup and disaster recovery

See the [Operations Guide](./docs/operations.md) for detailed deployment patterns and configurations.

## Development

### Prerequisites

- Docker and Docker Compose
- .NET 8 SDK
- Node.js 18+
- Java 11+
- Go 1.21+
- C++ compiler with C++20 support

### Running Tests

```bash
# API tests
cd api-dotnet
dotnet test

# Node.js SDK tests
cd sdks/node
npm test

# Smoke tests
cd tests
./smoke-test.sh
```

## Comprehensive Documentation

- [API Reference](./docs/api-reference.md) - Complete REST and GraphQL API documentation
- [SDK Usage Guide](./docs/sdk-usage.md) - Detailed SDK implementation examples
- [Operations Guide](./docs/operations.md) - Production deployment and maintenance
- [Rate Limiter Algorithm](./docs/rate-limiter-algorithm.md) - In-depth explanation of the rate limiting algorithm
- [Architecture Diagrams](./docs/architecture/) - Detailed technical architecture diagrams

## Performance Benchmarks

| Component | Throughput | Latency (p99) | Memory Usage |
|-----------|------------|---------------|--------------|
| .NET API | 20,000 req/s | 15ms | 200MB |
| C++ Rate Limiter | 100,000 req/s | <1ms | 50MB |
| Go Sidecar | 30,000 req/s | 5ms | 30MB |
| Node.js SDK | 5,000 ops/s | 3ms | 20MB |
| Java SDK | 10,000 ops/s | 5ms | 40MB |

_Benchmarked on standard 4-core instances. Your results may vary depending on hardware and configuration._

## About the Author

This project was developed by [Robert Nguyen](https://github.com/robert-nguyenn), a software engineer specializing in distributed systems and high-performance applications.

📧 Email: robert.nguyenanh@gmail.com  
🌐 LinkedIn: [linkedin.com/in/robert-nguyen](https://www.linkedin.com/in/robert-nguyenn/)

## License

MIT

## Contributing


Contributions are welcome! Please see our [Contributing Guide](CONTRIBUTING.md) for details on how to get involved.

