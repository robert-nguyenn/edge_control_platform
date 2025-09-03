variable "environment" {
  description = "Environment name (e.g., dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "aws_region" {
  description = "AWS region where the S3 bucket for Terraform state is located"
  type        = string
  default     = "us-east-1"
}

variable "postgres_username" {
  description = "PostgreSQL username"
  type        = string
  default     = "postgres"
  sensitive   = true
}

variable "postgres_password" {
  description = "PostgreSQL password"
  type        = string
  default     = "postgres"
  sensitive   = true
}

variable "api_key" {
  description = "API key for authentication"
  type        = string
  default     = "demo-key"
  sensitive   = true
}

variable "image_tag" {
  description = "Docker image tag to deploy"
  type        = string
  default     = "latest"
}
