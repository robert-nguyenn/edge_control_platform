# SDK Usage Guide

This guide demonstrates how to use the Edge Control Platform SDKs in your applications to check and evaluate feature flags.

## Available SDKs

- [Node.js SDK](#nodejs-sdk) (TypeScript)
- [Java SDK](#java-sdk)

## Node.js SDK

### Installation

```bash
npm install @edge-control/node-sdk
# or
yarn add @edge-control/node-sdk
```

### Basic Usage

```typescript
import { init } from '@edge-control/node-sdk';

// Initialize the SDK client
const client = init({
  baseUrl: 'http://localhost:5000',
  apiKey: 'demo-key',
  pollIntervalMs: 30000,  // Polling interval in milliseconds
  defaultDecisions: {     // Default values if flags are unavailable
    'pricing.v2': false,
    'exp.freeShipping': false
  },
  onUpdate: (flags) => {
    console.log('Flags updated:', Object.keys(flags));
  },
  onError: (error) => {
    console.error('SDK Error:', error.message);
  }
});

// Check if a feature flag is enabled
async function checkFeatureFlag() {
  const isEnabled = await client.isEnabled('pricing.v2', { userId: 'user123' });
  
  if (isEnabled) {
    // Feature is enabled for this user
    console.log('New pricing model is enabled');
    // Implement new pricing logic
  } else {
    // Feature is disabled for this user
    console.log('Using original pricing model');
    // Implement original pricing logic
  }
}

// Get all flag data
function getAllFlags() {
  const flags = client.getAllFlags();
  console.log('All flags:', flags);
}

// Cleanup when your application shuts down
function cleanup() {
  client.stop(); // Stops background polling
}
```

### Advanced Usage

#### Custom Context

You can pass additional context data to evaluate flags with more complex rules:

```typescript
const context = {
  userId: 'user123',
  country: 'US',
  accountType: 'premium',
  deviceType: 'mobile'
};

const isEnabled = await client.isEnabled('exp.newUI', context);
```

#### Error Handling

```typescript
try {
  const isEnabled = await client.isEnabled('pricing.v2', { userId: 'user123' });
  // Use result
} catch (error) {
  // Handle error (API unreachable, etc.)
  console.error('Error checking flag:', error.message);
  // Use default behavior
}
```

#### Waiting for Initial Load

```typescript
function initializeFlags() {
  return new Promise((resolve) => {
    const client = init({
      baseUrl: 'http://localhost:5000',
      apiKey: 'demo-key',
      onUpdate: () => {
        // Called after initial flag load
        resolve(client);
      }
    });
  });
}

async function start() {
  const client = await initializeFlags();
  // Now you can safely use client
}
```

## Java SDK

### Installation

Add the SDK JAR to your project dependencies.

### Basic Usage

```java
import com.edgecontrol.FeatureFlagsClient;
import java.util.Map;

public class MyApplication {
    public static void main(String[] args) {
        // Configure the client
        FeatureFlagsClient.Config config = new FeatureFlagsClient.Config("http://localhost:5000");
        config.apiKey = "demo-key";
        config.pollIntervalMs = 30000;
        config.defaultDecisions.put("pricing.v2", false);
        config.defaultDecisions.put("exp.freeShipping", false);
        
        config.onUpdate = flags -> {
            System.out.println("Flags updated: " + flags.keySet());
        };
        
        config.onError = error -> {
            System.err.println("SDK Error: " + error.getMessage());
        };

        // Create the client
        FeatureFlagsClient client = new FeatureFlagsClient(config);

        // Check if a feature is enabled
        Map<String, String> context = Map.of("userId", "user123");
        boolean isEnabled = client.isEnabled("pricing.v2", context);

        if (isEnabled) {
            System.out.println("New pricing model is enabled");
            // Implement new pricing logic
        } else {
            System.out.println("Using original pricing model");
            // Implement original pricing logic
        }

        // Get all flag data
        Map<String, FeatureFlagsClient.FlagData> flags = client.getAllFlags();
        System.out.println("All flags: " + flags.keySet());

        // Remember to stop the client when your application shuts down
        Runtime.getRuntime().addShutdownHook(new Thread(client::stop));
    }
}
```

### Advanced Usage

#### Custom Context

```java
Map<String, String> context = new HashMap<>();
context.put("userId", "user456");
context.put("country", "CA");
context.put("accountType", "standard");
context.put("deviceType", "desktop");

boolean isEnabled = client.isEnabled("exp.newUI", context);
```

#### Error Handling

```java
try {
    boolean isEnabled = client.isEnabled("pricing.v2", Map.of("userId", "user123"));
    // Use result
} catch (Exception e) {
    // Handle error (API unreachable, etc.)
    System.err.println("Error checking flag: " + e.getMessage());
    // Use default behavior
}
```

## Best Practices

1. **Initialize Once**: Create a single client instance and reuse it throughout your application.

2. **Provide Default Values**: Always specify sensible defaults for your flags in case the API is unreachable.

3. **Handle Errors Gracefully**: Ensure your application can function even if the feature flag service is unavailable.

4. **Clean Up Resources**: Call the `stop()` method when your application shuts down to clean up resources.

5. **User ID Consistency**: Make sure to use consistent user IDs across requests to ensure a user gets the same experience.

6. **Keep Context Small**: Only include necessary data in the context object to minimize memory usage.

7. **Monitoring**: Use the `onUpdate` and `onError` callbacks to monitor the SDK's operation.

## Advanced Topics

### Performance Considerations

The SDKs are designed to be high-performance with minimal overhead:

- Flags are cached in memory
- Background polling minimizes API calls
- ETags are used to reduce bandwidth
- Non-blocking evaluation for most operations

### Caching Behavior

The SDKs implement a local in-memory cache with background polling to minimize API calls:

1. Initial load fetches all flags from the API
2. Subsequent polls use ETags to only fetch changed flags
3. If the API is unreachable, the SDK continues to use cached values
4. Default values are used if a flag is not in the cache
