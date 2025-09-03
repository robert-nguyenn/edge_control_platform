#!/bin/bash
set -e

# Initialize terraform workspace with S3 backend
echo "🚀 Initializing Terraform workspace with S3 backend..."

# Check if the S3 bucket exists, if not create it
if ! aws s3 ls "s3://edge-control-terraform-state" 2>&1 | grep -q 'edge-control-terraform-state'; then
    echo "Creating S3 bucket for Terraform state..."
    aws s3api create-bucket \
        --bucket edge-control-terraform-state \
        --region us-east-1
    
    # Enable versioning on the bucket
    aws s3api put-bucket-versioning \
        --bucket edge-control-terraform-state \
        --versioning-configuration Status=Enabled
    
    # Enable server-side encryption
    aws s3api put-bucket-encryption \
        --bucket edge-control-terraform-state \
        --server-side-encryption-configuration '{
            "Rules": [
                {
                    "ApplyServerSideEncryptionByDefault": {
                        "SSEAlgorithm": "AES256"
                    }
                }
            ]
        }'
    
    # Block public access
    aws s3api put-public-access-block \
        --bucket edge-control-terraform-state \
        --public-access-block-configuration "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"
fi

# Initialize Terraform
terraform init

echo "✅ Terraform workspace initialized successfully!"
echo ""
echo "To apply the configuration:"
echo "1. Copy terraform.tfvars.example to terraform.tfvars"
echo "2. Edit terraform.tfvars with your desired configuration"
echo "3. Run 'terraform plan' to see what changes would be made"
echo "4. Run 'terraform apply' to apply the changes"
