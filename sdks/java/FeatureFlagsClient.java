import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.security.MessageDigest;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.Consumer;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

/**
 * Java SDK for Edge Control Platform feature flags
 * 
 * Usage:
 * FeatureFlagsClient client = new FeatureFlagsClient(config);
 * boolean enabled = client.isEnabled("pricing.v2", Map.of("userId", "user123"));
 */
public class FeatureFlagsClient {
    
    public static class Config {
        public String baseUrl;
        public String apiKey = "";
        public long pollIntervalMs = 30000;
        public Map<String, Boolean> defaultDecisions = new HashMap<>();
        public Consumer<Map<String, FlagData>> onUpdate = flags -> {};
        public Consumer<Exception> onError = error -> {};
        
        public Config(String baseUrl) {
            this.baseUrl = baseUrl;
        }
    }
    
    public static class FlagData {
        public String key;
        public String description;
        public int rolloutPercent;
        public JsonNode rules;
        public String etag;
        
        public FlagData() {}
        
        public FlagData(String key, String description, int rolloutPercent, JsonNode rules) {
            this.key = key;
            this.description = description;
            this.rolloutPercent = rolloutPercent;
            this.rules = rules;
        }
    }
    
    private final Config config;
    private final HttpClient httpClient;
    private final ObjectMapper objectMapper;
    private final Map<String, FlagData> flags = new ConcurrentHashMap<>();
    private final Map<String, String> etags = new ConcurrentHashMap<>();
    private final ScheduledExecutorService scheduler = Executors.newScheduledThreadPool(1);
    private volatile boolean isPolling = false;
    
    public FeatureFlagsClient(Config config) {
        this.config = config;
        this.httpClient = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(10))
            .build();
        this.objectMapper = new ObjectMapper();
        
        startPolling();
    }
    
    /**
     * Check if a feature flag is enabled for the given context
     */
    public boolean isEnabled(String flagKey, Map<String, String> context) {
        FlagData flag = flags.get(flagKey);
        
        if (flag == null) {
            // Return default decision if available
            return config.defaultDecisions.getOrDefault(flagKey, false);
        }
        
        return evaluateFlag(flag, context);
    }
    
    /**
     * Get flag data for a specific flag
     */
    public FlagData getFlag(String flagKey) {
        return flags.get(flagKey);
    }
    
    /**
     * Get all loaded flags
     */
    public Map<String, FlagData> getAllFlags() {
        return new HashMap<>(flags);
    }
    
    /**
     * Stop the client and cleanup resources
     */
    public void stop() {
        isPolling = false;
        scheduler.shutdown();
        try {
            if (!scheduler.awaitTermination(5, TimeUnit.SECONDS)) {
                scheduler.shutdownNow();
            }
        } catch (InterruptedException e) {
            scheduler.shutdownNow();
            Thread.currentThread().interrupt();
        }
    }
    
    private void startPolling() {
        if (isPolling) return;
        
        isPolling = true;
        
        // Initial load
        CompletableFuture.runAsync(this::loadAllFlags)
            .exceptionally(throwable -> {
                config.onError.accept(new Exception("Initial load failed", throwable));
                return null;
            });
        
        // Schedule periodic polling
        scheduler.scheduleAtFixedRate(() -> {
            if (isPolling) {
                try {
                    loadAllFlags();
                } catch (Exception e) {
                    config.onError.accept(e);
                }
            }
        }, config.pollIntervalMs, config.pollIntervalMs, TimeUnit.MILLISECONDS);
    }
    
    private void loadAllFlags() {
        try {
            HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(config.baseUrl + "/flags"))
                .headers(getHeaders())
                .GET()
                .build();
            
            HttpResponse<String> response = httpClient.send(request, 
                HttpResponse.BodyHandlers.ofString());
            
            if (response.statusCode() != 200) {
                throw new IOException("HTTP " + response.statusCode() + ": " + response.body());
            }
            
            JsonNode flagsList = objectMapper.readTree(response.body());
            
            // Load individual flags with ETags
            List<CompletableFuture<Void>> futures = new ArrayList<>();
            for (JsonNode flagInfo : flagsList) {
                String flagKey = flagInfo.get("key").asText();
                futures.add(CompletableFuture.runAsync(() -> loadFlag(flagKey)));
            }
            
            // Wait for all flags to load
            CompletableFuture.allOf(futures.toArray(new CompletableFuture[0])).join();
            
        } catch (Exception e) {
            throw new RuntimeException("Failed to load flags: " + e.getMessage(), e);
        }
    }
    
    private void loadFlag(String flagKey) {
        try {
            HttpRequest.Builder requestBuilder = HttpRequest.newBuilder()
                .uri(URI.create(config.baseUrl + "/flags/" + flagKey))
                .GET();
            
            // Add If-None-Match header if we have an ETag
            String etag = etags.get(flagKey);
            if (etag != null) {
                requestBuilder.header("If-None-Match", "\"" + etag + "\"");
            }
            
            String[] headers = getHeaders();
            requestBuilder.headers(headers);
            
            HttpRequest request = requestBuilder.build();
            HttpResponse<String> response = httpClient.send(request, 
                HttpResponse.BodyHandlers.ofString());
            
            if (response.statusCode() == 304) {
                // Not modified, keep current flag data
                return;
            }
            
            if (response.statusCode() != 200) {
                throw new IOException("HTTP " + response.statusCode() + ": " + response.body());
            }
            
            JsonNode flagJson = objectMapper.readTree(response.body());
            FlagData flagData = new FlagData(
                flagJson.get("key").asText(),
                flagJson.get("description").asText(),
                flagJson.get("rolloutPercent").asInt(),
                flagJson.get("rules")
            );
            
            // Extract ETag from response headers
            Optional<String> etagHeader = response.headers().firstValue("etag");
            if (etagHeader.isPresent()) {
                String newEtag = etagHeader.get().replaceAll("\"", "");
                etags.put(flagKey, newEtag);
                flagData.etag = newEtag;
            }
            
            FlagData oldFlag = flags.get(flagKey);
            flags.put(flagKey, flagData);
            
            // Trigger update callback if flag changed
            if (oldFlag == null || !flagsEqual(oldFlag, flagData)) {
                config.onUpdate.accept(getAllFlags());
            }
            
        } catch (Exception e) {
            // On error, keep existing flag data and use defaults
            if (!flags.containsKey(flagKey) && config.defaultDecisions.containsKey(flagKey)) {
                FlagData defaultFlag = new FlagData(
                    flagKey,
                    "Default flag (API unavailable)",
                    config.defaultDecisions.get(flagKey) ? 100 : 0,
                    objectMapper.createObjectNode()
                );
                flags.put(flagKey, defaultFlag);
            }
            throw new RuntimeException("Failed to load flag " + flagKey, e);
        }
    }
    
    private boolean evaluateFlag(FlagData flag, Map<String, String> context) {
        String userId = context.get("userId");
        if (userId == null) {
            return false;
        }
        
        // Simple percentage-based evaluation using userId hash
        int userPercent = hashUserId(userId);
        
        // Basic rollout percentage check
        return userPercent < flag.rolloutPercent;
    }
    
    private int hashUserId(String userId) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] hash = md.digest(userId.getBytes());
            
            // Convert first 4 bytes to int
            int result = 0;
            for (int i = 0; i < 4; i++) {
                result = (result << 8) + (hash[i] & 0xFF);
            }
            
            return Math.abs(result) % 100;
        } catch (Exception e) {
            // Fallback to simple hash
            return Math.abs(userId.hashCode()) % 100;
        }
    }
    
    private String[] getHeaders() {
        List<String> headers = new ArrayList<>();
        headers.add("Content-Type");
        headers.add("application/json");
        headers.add("User-Agent");
        headers.add("EdgeControl-Java-SDK/1.0.0");
        
        if (!config.apiKey.isEmpty()) {
            headers.add("Authorization");
            headers.add("Bearer " + config.apiKey);
        }
        
        return headers.toArray(new String[0]);
    }
    
    private boolean flagsEqual(FlagData flag1, FlagData flag2) {
        return flag1.rolloutPercent == flag2.rolloutPercent &&
               Objects.equals(flag1.description, flag2.description) &&
               Objects.equals(flag1.rules, flag2.rules);
    }
}

// Example usage
class FeatureFlagsDemo {
    public static void main(String[] args) throws InterruptedException {
        FeatureFlagsClient.Config config = new FeatureFlagsClient.Config("http://localhost:5000");
        config.apiKey = "demo-key";
        config.pollIntervalMs = 2000;
        config.defaultDecisions.put("pricing.v2", false);
        config.defaultDecisions.put("exp.freeShipping", false);
        config.onUpdate = flags -> System.out.println("📊 Flags updated: " + flags.keySet());
        config.onError = error -> System.err.println("❌ SDK Error: " + error.getMessage());
        
        FeatureFlagsClient client = new FeatureFlagsClient(config);
        
        System.out.println("🚀 Starting Edge Control Java SDK Demo");
        
        // Demo loop
        while (true) {
            try {
                Map<String, String> context1 = Map.of("userId", "u123");
                Map<String, String> context2 = Map.of("userId", "u456");
                
                boolean user1 = client.isEnabled("pricing.v2", context1);
                boolean user2 = client.isEnabled("pricing.v2", context2);
                boolean freeShipping = client.isEnabled("exp.freeShipping", context1);
                
                System.out.println("🎯 pricing.v2 - u123: " + user1 + ", u456: " + user2);
                System.out.println("🚚 freeShipping - u123: " + freeShipping);
                System.out.println("---");
                
                Thread.sleep(2000);
            } catch (Exception e) {
                System.err.println("Error checking flags: " + e.getMessage());
                Thread.sleep(1000);
            }
        }
    }
}
