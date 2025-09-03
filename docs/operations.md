# Operations Guide

This guide provides information for deploying, operating, and maintaining the Edge Control Platform in production environments.

## Deployment Options

### Docker Compose

For simple deployments or development environments, Docker Compose is the quickest option:

```bash
cd ops
docker-compose up -d
```

### Kubernetes

For production deployments, we recommend using Kubernetes:

```bash
cd k8s
kubectl apply -f 00-config.yaml
kubectl apply -f 01-postgres.yaml
kubectl apply -f 02-redis.yaml
kubectl apply -f 03-rate-limiter.yaml
kubectl apply -f 04-api-dotnet.yaml
kubectl apply -f 05-go-sidecar.yaml
kubectl apply -f 06-web-admin.yaml
kubectl apply -f 07-prometheus.yaml
kubectl apply -f 08-grafana.yaml
kubectl apply -f 09-ingress.yaml
```

For local Kubernetes testing, we provide a script to set up a kind cluster:

```bash
cd k8s
./setup-kind.sh
```

### Terraform

For cloud deployments with infrastructure as code:

```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your configuration
./init-terraform.sh
terraform apply
```

## System Requirements

### Minimum Requirements

- **API Service**: 1 vCPU, 512MB RAM
- **Rate Limiter**: 1 vCPU, 256MB RAM
- **Go Sidecar**: 0.5 vCPU, 128MB RAM
- **Redis**: 1 vCPU, 256MB RAM
- **PostgreSQL**: 1 vCPU, 512MB RAM
- **Web Admin**: 0.5 vCPU, 128MB RAM

### Recommended Requirements

- **API Service**: 2 vCPU, 1GB RAM
- **Rate Limiter**: 2 vCPU, 512MB RAM
- **Go Sidecar**: 1 vCPU, 256MB RAM
- **Redis**: 2 vCPU, 1GB RAM
- **PostgreSQL**: 2 vCPU, 1GB RAM
- **Web Admin**: 1 vCPU, 256MB RAM

### Disk Space

- PostgreSQL: At least 10GB (scales with audit log volume)
- Redis: At least 1GB
- Prometheus: At least 10GB (scales with retention period)

## Scaling Considerations

The Edge Control Platform is designed to be horizontally scalable:

1. **API Service**: Stateless, can scale horizontally
2. **Rate Limiter**: Scales horizontally with Redis as a central store
3. **Go Sidecar**: Scales with the API service (one sidecar per API instance)
4. **Redis**: Can be configured as a cluster for high availability
5. **PostgreSQL**: Primary/replica configuration for read scaling

### Recommended Scaling Pattern

1. Start with a 2:1:1 ratio of API:Redis:PostgreSQL instances
2. Monitor query latency and CPU/memory usage
3. Scale the API tier first when latency increases
4. Consider Redis cluster when cache hit ratio decreases
5. Consider PostgreSQL read replicas when database load increases

## Database Management

### PostgreSQL

1. **Backups**: Set up regular backups of the PostgreSQL database
   ```bash
   pg_dump -h localhost -U postgres edge_control > backup.sql
   ```

2. **Maintenance**: Regularly run VACUUM and ANALYZE operations
   ```bash
   psql -h localhost -U postgres -d edge_control -c "VACUUM ANALYZE;"
   ```

3. **Monitoring**: Monitor slow queries and connection count
   ```sql
   SELECT * FROM pg_stat_activity WHERE state = 'active';
   ```

### Redis

1. **Persistence**: Redis is configured with AOF persistence by default
   ```bash
   redis-cli config get appendonly
   ```

2. **Memory Management**: Monitor memory usage and consider `maxmemory` settings
   ```bash
   redis-cli info memory
   ```

3. **Backups**: Set up regular RDB snapshots
   ```bash
   redis-cli bgsave
   ```

## Monitoring and Alerting

### Key Metrics to Monitor

1. **API Service**:
   - Request rate and latency
   - HTTP error rate
   - Cache hit ratio

2. **Rate Limiter**:
   - Request rate
   - Rejection rate
   - gRPC error rate

3. **Go Sidecar**:
   - Circuit breaker state
   - Retry rate
   - Request latency

4. **Redis**:
   - Memory usage
   - Eviction rate
   - Command latency

5. **PostgreSQL**:
   - Connection count
   - Query latency
   - Index hit ratio
   - Transaction rate

### Recommended Alerting Thresholds

1. **P95 Latency**: > 500ms for API endpoints
2. **Error Rate**: > 1% for any service
3. **Circuit Breaker**: Open state for > 5 minutes
4. **Redis Memory**: > 80% usage
5. **PostgreSQL Connections**: > 80% of max_connections

## Security Considerations

### API Key Management

1. Use a strong, random API key for production
2. Rotate keys periodically
3. Use separate keys for different environments

### Network Security

1. Use TLS for all external communication
2. Configure firewalls to restrict access between services
3. Use network policies in Kubernetes to control pod-to-pod traffic

### Updates and Patches

1. Subscribe to security advisories for all components
2. Regularly update dependencies and base images
3. Use vulnerability scanning in your CI/CD pipeline

## Disaster Recovery

### Backup Strategy

1. **PostgreSQL**: Daily full backups, point-in-time recovery with WAL archiving
2. **Redis**: RDB snapshots every hour, AOF for continuous persistence
3. **Configuration**: Store all configuration in version control

### Recovery Procedures

1. **API Service Failure**:
   - Automatic restart via Kubernetes or Docker restart policy
   - Check logs for root cause

2. **Database Corruption**:
   - Restore from latest backup
   - Apply WAL logs to recover recent transactions

3. **Complete Environment Recovery**:
   - Apply infrastructure as code (Terraform)
   - Restore database backups
   - Verify API health and flag evaluations

## Troubleshooting

### Common Issues

1. **High Latency**:
   - Check database query performance
   - Verify Redis cache hit ratio
   - Check for network issues between services

2. **Inconsistent Flag Evaluations**:
   - Verify SDK version compatibility
   - Check for cache inconsistencies
   - Inspect SDK polling logs

3. **Rate Limiter Errors**:
   - Check gRPC connectivity
   - Verify rate limiter configuration
   - Inspect rate limiter logs for errors

### Log Analysis

Key log patterns to look for:

1. HTTP 5xx errors in API logs
2. Circuit breaker open events in sidecar logs
3. Cache eviction notices in Redis logs
4. Deadlock or long transaction warnings in PostgreSQL logs

## Maintenance Procedures

### Zero-Downtime Updates

1. **API Service**:
   - Use Kubernetes rolling updates
   - Configure readiness probes correctly

2. **Database Schema Changes**:
   - Use migrations that work with existing code
   - Apply schema changes before code changes

3. **Redis Updates**:
   - Use replicas to minimize downtime
   - Schedule during low traffic periods

### Performance Tuning

1. **API Service**:
   - Increase cache TTLs for stable flags
   - Optimize EF Core queries

2. **Rate Limiter**:
   - Adjust bucket sizes and refill rates based on traffic patterns
   - Monitor token consumption patterns

3. **Go Sidecar**:
   - Tune circuit breaker thresholds based on error patterns
   - Adjust retry strategies for optimal resilience
