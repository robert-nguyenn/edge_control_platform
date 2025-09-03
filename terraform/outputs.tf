output "api_endpoint" {
  description = "API endpoint URL"
  value       = "http://${kubernetes_service.api_gateway.status[0].load_balancer[0].ingress[0].hostname}/api"
}

output "web_admin_endpoint" {
  description = "Web Admin UI endpoint URL"
  value       = "http://${kubernetes_service.api_gateway.status[0].load_balancer[0].ingress[0].hostname}"
}

output "prometheus_endpoint" {
  description = "Prometheus endpoint URL"
  value       = "http://${kubernetes_service.api_gateway.status[0].load_balancer[0].ingress[0].hostname}/metrics"
}

output "grafana_endpoint" {
  description = "Grafana endpoint URL"
  value       = "http://${kubernetes_service.api_gateway.status[0].load_balancer[0].ingress[0].hostname}/grafana"
}
