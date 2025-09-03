using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace EdgeControlApi.Services
{
    /// <summary>
    /// Provides consistent hashing functionality for percentage-based rollouts.
    /// Ensures users consistently fall into the same buckets regardless of scaling.
    /// </summary>
    public class ConsistentHashingService
    {
        private readonly int _bucketCount;
        private readonly ILogger<ConsistentHashingService> _logger;
        
        public ConsistentHashingService(IConfiguration configuration, ILogger<ConsistentHashingService> logger)
        {
            _logger = logger;
            _bucketCount = configuration.GetValue<int>("Hashing:BucketCount", 10000);
            _logger.LogInformation("Initialized ConsistentHashingService with {BucketCount} buckets", _bucketCount);
        }

        /// <summary>
        /// Determines if a user falls into a specified percentage bucket.
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="flagKey">Feature flag key</param>
        /// <param name="percentage">Percentage of users (0-100)</param>
        /// <returns>True if user is included in rollout</returns>
        public bool IsUserInPercentage(string userId, string flagKey, int percentage)
        {
            if (percentage <= 0)
                return false;
                
            if (percentage >= 100)
                return true;
            
            try
            {
                int bucket = GetUserBucket(userId, flagKey);
                return bucket <= (_bucketCount * percentage / 100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in percentage calculation for user {UserId} and flag {FlagKey}", userId, flagKey);
                return false;
            }
        }
        
        /// <summary>
        /// Gets a deterministic bucket number for a user and flag combination.
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="flagKey">Feature flag key</param>
        /// <returns>Bucket number between 1 and _bucketCount</returns>
        private int GetUserBucket(string userId, string flagKey)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
            // Create a hash that combines user ID and flag key
            string input = $"{userId}:{flagKey}";
            
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            
            // Use first 4 bytes to create an int
            uint hash = BitConverter.ToUInt32(bytes, 0);
            
            // Convert to a bucket number between 1 and _bucketCount
            return (int)((hash % _bucketCount) + 1);
        }
        
        /// <summary>
        /// Determines variant assignment for an A/B/n test.
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="experimentKey">Experiment identifier</param>
        /// <param name="variants">List of variants with weights</param>
        /// <returns>The selected variant name</returns>
        public string AssignVariant(string userId, string experimentKey, IList<(string name, int weight)> variants)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                
            if (variants == null || variants.Count == 0)
                throw new ArgumentException("Variants list cannot be null or empty", nameof(variants));
            
            try
            {
                // Calculate total weight
                int totalWeight = 0;
                foreach (var (_, weight) in variants)
                {
                    totalWeight += weight;
                }
                
                if (totalWeight <= 0)
                    throw new ArgumentException("Total variant weight must be positive", nameof(variants));
                
                // Get user bucket normalized to total weight
                int bucket = GetUserBucket(userId, experimentKey) % totalWeight;
                
                // Find the variant for this bucket
                int accumulatedWeight = 0;
                foreach (var (name, weight) in variants)
                {
                    accumulatedWeight += weight;
                    if (bucket < accumulatedWeight)
                        return name;
                }
                
                // Default to last variant if something went wrong
                return variants[variants.Count - 1].name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in variant assignment for user {UserId} and experiment {ExperimentKey}", 
                    userId, experimentKey);
                return variants[0].name; // Return first variant as fallback
            }
        }
        
        /// <summary>
        /// Generates a list of users expected to be included in a rollout percentage.
        /// Used for testing and verification.
        /// </summary>
        /// <param name="userIds">List of user IDs to check</param>
        /// <param name="flagKey">Feature flag key</param>
        /// <param name="percentage">Percentage of users (0-100)</param>
        /// <returns>List of users included in the rollout</returns>
        public IList<string> GetIncludedUsers(IEnumerable<string> userIds, string flagKey, int percentage)
        {
            var result = new List<string>();
            
            foreach (var userId in userIds)
            {
                if (IsUserInPercentage(userId, flagKey, percentage))
                {
                    result.Add(userId);
                }
            }
            
            return result;
        }
    }
}
