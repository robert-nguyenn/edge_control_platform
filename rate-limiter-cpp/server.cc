#include <iostream>
#include <memory>
#include <string>
#include <unordered_map>
#include <mutex>
#include <chrono>
#include <grpcpp/grpcpp.h>
#include "ratelimiter.grpc.pb.h"

using grpc::Server;
using grpc::ServerBuilder;
using grpc::ServerContext;
using grpc::Status;
using ratelimiting::RateLimiter;
using ratelimiting::AllowRequest;
using ratelimiting::AllowResponse;
using ratelimiting::StatusRequest;
using ratelimiting::StatusResponse;
using ratelimiting::ConfigureRequest;
using ratelimiting::ConfigureResponse;

// Token bucket rate limiter implementation
class TokenBucket {
public:
    TokenBucket(double refill_rate, double bucket_capacity)
        : refill_rate_(refill_rate),
          bucket_capacity_(bucket_capacity),
          tokens_(bucket_capacity),
          last_refill_(std::chrono::steady_clock::now()) {}

    bool allow(uint32_t token_cost, int64_t* retry_after_ms, double* remaining) {
        std::lock_guard<std::mutex> lock(mutex_);
        
        // Refill tokens based on time elapsed
        refill();
        
        // Check if we have enough tokens
        if (tokens_ >= token_cost) {
            tokens_ -= token_cost;
            *remaining = tokens_;
            *retry_after_ms = 0;
            return true;
        } else {
            // Calculate how long until we have enough tokens
            double missing_tokens = token_cost - tokens_;
            double seconds_to_wait = missing_tokens / refill_rate_;
            *retry_after_ms = static_cast<int64_t>(seconds_to_wait * 1000);
            *remaining = tokens_;
            return false;
        }
    }
    
    double get_tokens() {
        std::lock_guard<std::mutex> lock(mutex_);
        refill();
        return tokens_;
    }
    
    double get_refill_rate() const {
        return refill_rate_;
    }
    
    double get_capacity() const {
        return bucket_capacity_;
    }
    
    int64_t get_last_refill_ms() const {
        auto now = std::chrono::steady_clock::now();
        auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_refill_).count();
        return ms;
    }

private:
    void refill() {
        auto now = std::chrono::steady_clock::now();
        double elapsed_seconds = std::chrono::duration<double>(now - last_refill_).count();
        double new_tokens = elapsed_seconds * refill_rate_;
        
        if (new_tokens > 0) {
            tokens_ = std::min(bucket_capacity_, tokens_ + new_tokens);
            last_refill_ = now;
        }
    }

    double refill_rate_;          // tokens per second
    double bucket_capacity_;      // maximum tokens
    double tokens_;               // current tokens
    std::chrono::steady_clock::time_point last_refill_;
    std::mutex mutex_;
};

// RateLimiter service implementation
class RateLimiterServiceImpl final : public RateLimiter::Service {
public:
    RateLimiterServiceImpl() {
        // Create default rate limiters for common operations
        rate_limiters_["flags_list"] = std::make_shared<TokenBucket>(10.0, 100.0);   // 10 req/s
        rate_limiters_["flag_write"] = std::make_shared<TokenBucket>(5.0, 50.0);     // 5 req/s
        std::cout << "Rate Limiter service initialized with default limiters" << std::endl;
    }

    Status Allow(ServerContext* context, const AllowRequest* request,
                 AllowResponse* response) override {
        std::string key = request->key();
        uint32_t token_cost = request->token_cost();
        if (token_cost == 0) token_cost = 1; // Default cost

        // Create rate limiter for this key if it doesn't exist
        auto limiter = get_or_create_limiter(key);
        
        int64_t retry_after_ms;
        double remaining;
        bool allowed = limiter->allow(token_cost, &retry_after_ms, &remaining);
        
        response->set_allowed(allowed);
        response->set_retry_after_ms(retry_after_ms);
        response->set_quota_remaining(remaining);
        
        std::cout << "Allow request for key: " << key << ", cost: " << token_cost 
                  << ", allowed: " << (allowed ? "yes" : "no") 
                  << ", remaining: " << remaining << std::endl;
                  
        return Status::OK;
    }

    Status Status(ServerContext* context, const StatusRequest* request,
                  StatusResponse* response) override {
        std::string key = request->key();
        auto limiter = get_or_create_limiter(key);
        
        response->set_key(key);
        response->set_tokens_remaining(limiter->get_tokens());
        response->set_refill_rate(limiter->get_refill_rate());
        response->set_bucket_capacity(limiter->get_capacity());
        response->set_last_refill_time_ms(limiter->get_last_refill_ms());
        
        std::cout << "Status request for key: " << key 
                  << ", tokens: " << limiter->get_tokens() << std::endl;
                  
        return Status::OK;
    }

    Status Configure(ServerContext* context, const ConfigureRequest* request,
                     ConfigureResponse* response) override {
        std::string key = request->key();
        double refill_rate = request->refill_rate();
        double bucket_capacity = request->bucket_capacity();
        
        if (refill_rate <= 0 || bucket_capacity <= 0) {
            response->set_success(false);
            response->set_message("Invalid rate limiter configuration. Values must be positive.");
            return Status::OK;
        }
        
        {
            std::lock_guard<std::mutex> lock(mutex_);
            rate_limiters_[key] = std::make_shared<TokenBucket>(refill_rate, bucket_capacity);
        }
        
        response->set_success(true);
        response->set_message("Rate limiter configured successfully");
        
        std::cout << "Configured rate limiter for key: " << key 
                  << ", rate: " << refill_rate << ", capacity: " << bucket_capacity << std::endl;
                  
        return Status::OK;
    }

private:
    std::shared_ptr<TokenBucket> get_or_create_limiter(const std::string& key) {
        std::lock_guard<std::mutex> lock(mutex_);
        
        auto it = rate_limiters_.find(key);
        if (it != rate_limiters_.end()) {
            return it->second;
        }
        
        // Default configuration: 20 requests per second, bucket of 50
        auto limiter = std::make_shared<TokenBucket>(20.0, 50.0);
        rate_limiters_[key] = limiter;
        return limiter;
    }

    std::unordered_map<std::string, std::shared_ptr<TokenBucket>> rate_limiters_;
    std::mutex mutex_;
};

void RunServer() {
    std::string server_address("0.0.0.0:50051");
    RateLimiterServiceImpl service;

    ServerBuilder builder;
    builder.AddListeningPort(server_address, grpc::InsecureServerCredentials());
    builder.RegisterService(&service);
    
    std::unique_ptr<Server> server(builder.BuildAndStart());
    std::cout << "Rate Limiter server listening on " << server_address << std::endl;
    server->Wait();
}

int main(int argc, char** argv) {
    RunServer();
    return 0;
}
