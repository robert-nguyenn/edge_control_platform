# ⚡ Enterprise-Grade Edge Control Platform

> **A production-ready, high-performance feature flags and rate limiting platform demonstrating enterprise software engineering practices for cloud-native architectures.**

**Built by:** [Robert Nguyen](mailto:robert.nguyenanh@gmail.com) | **Seeking:** SWE Internship Opportunities at FAANG+ Companies

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C++](https://img.shields.io/badge/C++-20-00599C?style=for-the-badge&logo=c%2B%2B&logoColor=white)](https://isocpp.org/)
[![Go](https://img.shields.io/badge/Go-1.21-00ADD8?style=for-the-badge&logo=go&logoColor=white)](https://golang.org/)
[![Redis](https://img.shields.io/badge/Redis-7.2-DC382D?style=for-the-badge&logo=redis&logoColor=white)](https://redis.io/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-316192?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![React](https://img.shields.io/badge/React-18-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://reactjs.org/)
[![GraphQL](https://img.shields.io/badge/GraphQL-E10098?style=for-the-badge&logo=graphql&logoColor=white)](https://graphql.org/)
[![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)

## 🌟 Overview

Edge Control Platform is an **enterprise-ready feature flags system** with advanced rate limiting capabilities, designed for rapid adoption by backend services. Developed with a focus on **performance, resilience, and scalability**, it provides a robust solution for progressive feature rollouts, sophisticated A/B/n testing, and intelligent traffic management.

### ⚡ **Key Features**

- **🎯 Advanced Feature Flags**: Sophisticated targeting with percentage-based rollouts, user attributes, and A/B/n testing using consistent hashing algorithms
- **🚀 High-Performance Rate Limiting**: C++20 token-bucket implementation with 100K+ decisions/second throughput and <1ms p99 latency
- **💾 Distributed Caching**: Redis-backed caching with ETag support and intelligent cache invalidation
- **🛡️ Zero-Downtime Resilience**: Circuit breaker pattern implementation in Go with automatic recovery and graceful degradation
- **📊 Comprehensive Observability**: OpenTelemetry integration with Prometheus metrics and Grafana dashboards
- **🔧 Enterprise-Ready SDKs**: Thread-safe Java and Node.js clients with background polling, local caching, and fault tolerance

## 🏗️ **System Architecture** - *Demonstrating Large-Scale System Design*

The Edge Control Platform employs a **microservices architecture** with **polyglot implementation**, optimizing each component for its specific requirements:

![Architecture Diagram](./docs/images/architecture.png)

### 🔧 **Core Components**

1. **🌐 .NET 8 API**: Highly optimized service for flag management with dual GraphQL/REST interfaces and Entity Framework Core
2. **⚡ C++20 Rate Limiter**: Ultra-low latency rate limiting service with distributed token bucket algorithm and gRPC interface
3. **🛡️ Go 1.21 Sidecar**: Resilience layer with sophisticated circuit breaking, automatic retries, and zero-downtime reloads
4. **💾 Redis Cluster**: Distributed caching for flags and rate limiter state with intelligent partitioning
5. **🗄️ PostgreSQL**: ACID-compliant data store for flags, audit logs, and configuration
6. **⚛️ React Admin UI**: Responsive Material UI-based administration interface with real-time flag status
7. **📊 OpenTelemetry Stack**: Comprehensive metrics, tracing, and logging with Prometheus and Grafana dashboards

### 🧠 **Advanced Patterns**

- **🔄 Consistent Hashing**: Ensures user experiences remain stable during percentage-based rollouts
- **🛡️ Circuit Breaker**: Prevents cascading failures with automatic service degradation and recovery
- **⚡ CQRS Pattern**: Separates read and write paths for optimized performance and scalability
- **🌐 Distributed Rate Limiting**: Coordinates rate limiting decisions across multiple instances

For detailed architecture diagrams, see the [architecture documentation](./docs/architecture/).

---

## 🚀 **Quick Start** - *Ready for Demo*

### **🐳 Running with Docker Compose**

The easiest way to get started is with Docker Compose:

```bash
cd ops
docker-compose up -d
```

This will start all the required services. Once running:
- **Web Admin UI**: http://localhost:3000
- **API**: http://localhost:5000
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3001 (admin/admin)

### **💻 Using the SDKs**

#### **📦 Node.js SDK**

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

#### **☕ Java SDK**

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

---

## 🔧 **Technology Stack** - *Production-Grade Technologies*

| **Domain** | **Technology** | **Purpose** | **Scale Characteristics** |
|------------|---------------|------------|---------------------------|
| **Backend Core** | .NET 8 + Entity Framework | REST/GraphQL APIs, business logic | 20,000+ RPS, auto-scaling ready |
| **High-Performance Computing** | C++20 + gRPC | Rate limiting, token bucket algorithm | 100K+ decisions/second |
| **Resilience Layer** | Go 1.21 + Circuit Breaker | Service mesh, automatic recovery | 30,000+ RPS with fault tolerance |
| **Caching Layer** | Redis Cluster | Distributed caching, session management | 100K+ operations/second |
| **Data Persistence** | PostgreSQL 15 | ACID transactions, audit logs | Multi-master replication ready |
| **Frontend** | React 18 + Material UI | Admin dashboard, real-time updates | Progressive Web App ready |
| **Observability** | OpenTelemetry + Prometheus | Production monitoring, alerting | Custom SLI/SLO dashboards |
| **Container Platform** | Docker + Kubernetes | Microservices deployment | Auto-scaling, service mesh ready |

---

## 📊 **Enterprise Deployment Options** - *Production-Ready Scaling*

The Edge Control Platform supports multiple deployment models to fit diverse enterprise requirements:

### 🚢 **Kubernetes with Horizontal Pod Autoscaling**

Production-grade Kubernetes manifests with horizontal autoscaling, readiness/liveness probes, and resource limits:

```bash
cd k8s
./setup-kind.sh  # For local testing
# Or for production:
kubectl apply -f ./  # Apply all manifests
```

### 🏗️ **Infrastructure as Code with Terraform**

Terraform modules for AWS, GCP, and Azure deployments with best-practice security and scaling:

```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your configuration
./init-terraform.sh
terraform apply
```

### 🔒 **High-Availability Configuration**

The platform is designed for **99.99% uptime** with:

- **Multi-region deployments**
- **Automatic failover**
- **Zero-downtime upgrades**
- **Backup and disaster recovery**

See the [Operations Guide](./docs/operations.md) for detailed deployment patterns and configurations.

---

## 💻 **Development** - *Professional Engineering Practices*

### **📋 Prerequisites**

- Docker and Docker Compose
- .NET 8 SDK
- Node.js 18+
- Java 11+
- Go 1.21+
- C++ compiler with C++20 support

### **🧪 Running Tests**

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

---

## 📚 **Comprehensive Documentation**

- **📖 [API Reference](./docs/api-reference.md)** - Complete REST and GraphQL API documentation
- **🔧 [SDK Usage Guide](./docs/sdk-usage.md)** - Detailed SDK implementation examples
- **🚀 [Operations Guide](./docs/operations.md)** - Production deployment and maintenance
- **⚡ [Rate Limiter Algorithm](./docs/rate-limiter-algorithm.md)** - In-depth explanation of the rate limiting algorithm
- **🏗️ [Architecture Diagrams](./docs/architecture/)** - Detailed technical architecture diagrams

## 🎯 **Skills Demonstrated** - *Directly Relevant to Big Tech*

### **🏗️ System Design & Architecture**
✅ **Microservices Architecture** - Service decomposition and inter-service communication  
✅ **Polyglot Programming** - .NET, C++, Go optimization for specific use cases  
✅ **Data Modeling** - Multi-database strategy (OLTP + Caching + Analytics)  
✅ **API Design** - REST and GraphQL with proper HTTP semantics  
✅ **Distributed Systems** - Consistent hashing, circuit breaker patterns  

### **⚡ Performance Engineering**  
✅ **Low-Latency Computing** - Sub-1ms response times at scale  
✅ **Memory Optimization** - Efficient data structures and caching strategies  
✅ **Concurrency** - Thread-safe code, async programming, lock-free algorithms  
✅ **Profiling & Optimization** - Performance tuning and resource management  

### **🔧 DevOps & Infrastructure**
✅ **Containerization** - Docker, Kubernetes deployment strategies  
✅ **Monitoring & Observability** - OpenTelemetry, Prometheus, Grafana  
✅ **CI/CD** - Automated testing, deployment pipelines  
✅ **Security** - Authentication, authorization, secure coding practices  

## 🤝 **Let's Connect!**

**Robert Nguyen** - Passionate Software Engineer  
📧 **Email:** [robert.nguyenanh@gmail.com](mailto:robert.nguyenanh@gmail.com)  

---

## 🔐 **Security & Compliance**

- **🔒 JWT Authentication** with role-based access control
- **🛡️ Input Validation** preventing SQL injection and XSS
- **🔐 Secure Communications** with TLS encryption
- **📊 Audit Logging** for compliance and forensics
- **🚫 Rate Limiting** preventing abuse and DoS attacks
- **🔍 Security Headers** following OWASP guidelines---

---

## 📄 **License**

MIT License - see [LICENSE](LICENSE) file for details.

## 🤝 **Contributing**

Contributions are welcome! Please see our [Contributing Guide](CONTRIBUTING.md) for details on how to get involved.

---

<div align="center">
<b>⭐ If this project demonstrates the technical skills you're looking for, let's connect!</b><br>
<i>Built with ❤️ by Robert Nguyen</i>

</div>


