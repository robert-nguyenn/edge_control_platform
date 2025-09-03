terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }
  }
  
  backend "s3" {
    bucket = "edge-control-terraform-state"
    key    = "edge-control/terraform.tfstate"
    region = "us-east-1"
    encrypt = true
  }

  required_version = ">= 1.0.0"
}

provider "aws" {
  region = var.aws_region
}

provider "kubernetes" {
  config_path = "~/.kube/config"
}

# Local variables
locals {
  namespace = "edge-control"
  app_name  = "edge-control-platform"
  common_tags = {
    Project     = local.app_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

# Create Kubernetes namespace
resource "kubernetes_namespace" "edge_control" {
  metadata {
    name = local.namespace
  }
}

# Create ConfigMap for configuration
resource "kubernetes_config_map" "config" {
  metadata {
    name      = "edge-control-config"
    namespace = kubernetes_namespace.edge_control.metadata[0].name
  }

  data = {
    "prometheus.yml" = file("${path.module}/../ops/prometheus.yml")
  }
}

# Create Secret for database credentials
resource "kubernetes_secret" "postgres_secret" {
  metadata {
    name      = "postgres-secret"
    namespace = kubernetes_namespace.edge_control.metadata[0].name
  }

  data = {
    username = var.postgres_username
    password = var.postgres_password
  }

  type = "Opaque"
}

# Create Secret for API key
resource "kubernetes_secret" "api_key_secret" {
  metadata {
    name      = "api-key-secret"
    namespace = kubernetes_namespace.edge_control.metadata[0].name
  }

  data = {
    "api-key" = var.api_key
  }

  type = "Opaque"
}

# PostgreSQL StatefulSet
resource "kubernetes_stateful_set" "postgres" {
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace.edge_control.metadata[0].name
  }

  spec {
    service_name = "postgres"
    replicas     = 1

    selector {
      match_labels = {
        app = "postgres"
      }
    }

    template {
      metadata {
        labels = {
          app = "postgres"
        }
      }

      spec {
        container {
          name  = "postgres"
          image = "postgres:15-alpine"

          port {
            container_port = 5432
          }

          env {
            name  = "POSTGRES_DB"
            value = "edge_control"
          }

          env {
            name = "POSTGRES_USER"
            value_from {
              secret_key_ref {
                name = kubernetes_secret.postgres_secret.metadata[0].name
                key  = "username"
              }
            }
          }

          env {
            name = "POSTGRES_PASSWORD"
            value_from {
              secret_key_ref {
                name = kubernetes_secret.postgres_secret.metadata[0].name
                key  = "password"
              }
            }
          }

          volume_mount {
            name       = "postgres-data"
            mount_path = "/var/lib/postgresql/data"
          }

          readiness_probe {
            exec {
              command = ["pg_isready", "-U", "postgres"]
            }
            initial_delay_seconds = 5
            period_seconds        = 10
          }

          liveness_probe {
            exec {
              command = ["pg_isready", "-U", "postgres"]
            }
            initial_delay_seconds = 30
            period_seconds        = 10
          }

          resources {
            requests = {
              memory = "256Mi"
              cpu    = "100m"
            }
            limits = {
              memory = "512Mi"
              cpu    = "500m"
            }
          }
        }
      }
    }

    volume_claim_template {
      metadata {
        name = "postgres-data"
      }

      spec {
        access_modes = ["ReadWriteOnce"]
        resources {
          requests = {
            storage = "1Gi"
          }
        }
      }
    }
  }
}

# PostgreSQL Service
resource "kubernetes_service" "postgres" {
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace.edge_control.metadata[0].name
  }

  spec {
    selector = {
      app = "postgres"
    }

    port {
      port        = 5432
      target_port = 5432
    }
  }
}

# More resources would be defined here for Redis, API, Web Admin, etc.
# Similar to the Kubernetes manifests in the k8s directory
