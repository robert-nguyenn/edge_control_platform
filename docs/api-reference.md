# API Reference

This document provides reference information for the Edge Control Platform API endpoints.

## Base URL

The default base URL for all API endpoints is `http://localhost:5000` when running locally.

## Authentication

All API requests (except health checks) require authentication using a Bearer token:

```
Authorization: Bearer demo-key
```

## REST API Endpoints

### Health Check

```
GET /healthz
```

Returns the health status of the API.

**Response**:
```json
{
  "status": "ok",
  "version": "1.0.0"
}
```

### Feature Flags

#### List All Flags

```
GET /flags
```

Returns a list of all feature flags.

**Response**:
```json
[
  {
    "key": "pricing.v2",
    "description": "Enable new pricing model v2",
    "rolloutPercent": 10,
    "updatedAt": "2025-09-03T12:00:00Z"
  },
  {
    "key": "exp.freeShipping",
    "description": "Experiment: Free shipping promotion",
    "rolloutPercent": 0,
    "updatedAt": "2025-09-03T12:00:00Z"
  }
]
```

#### Get a Specific Flag

```
GET /flags/{key}
```

Returns details for a specific feature flag.

**Response**:
```json
{
  "key": "pricing.v2",
  "description": "Enable new pricing model v2",
  "rolloutPercent": 10,
  "rules": {}
}
```

**Headers**:
- `ETag`: Used for caching
- `Cache-Control`: Caching instructions

**Notes**:
- Supports `If-None-Match` for conditional requests
- Returns 304 Not Modified when appropriate

#### Update a Flag

```
PUT /flags/{key}
```

Creates or updates a feature flag.

**Request Body**:
```json
{
  "description": "Enable new pricing model v2",
  "rolloutPercent": 50,
  "rules": {}
}
```

**Response**:
```json
{
  "key": "pricing.v2",
  "description": "Enable new pricing model v2",
  "rolloutPercent": 50,
  "rules": {}
}
```

### Audit Logs

```
GET /audit?flag={flagKey}&limit={limit}
```

Returns audit logs for flag changes.

**Query Parameters**:
- `flag`: (Optional) Filter by flag key
- `limit`: (Optional, default: 10) Number of logs to return

**Response**:
```json
[
  {
    "id": 1,
    "flagKey": "pricing.v2",
    "action": "update",
    "actor": "admin",
    "payloadHash": "a1b2c3d4e5f6",
    "createdAt": "2025-09-03T12:00:00Z"
  }
]
```

## GraphQL API

The GraphQL API is available at `/graphql` and provides more flexibility in querying the platform data.

### Example Queries

#### List All Flags
```graphql
query {
  flags {
    key
    description
    rolloutPercent
    updatedAt
  }
}
```

#### Get a Specific Flag
```graphql
query {
  flag(key: "pricing.v2") {
    key
    description
    rolloutPercent
    rules
  }
}
```

#### Evaluate a Flag for a User
```graphql
query {
  evaluateFlag(key: "pricing.v2", userId: "user123") {
    key
    isEnabled
    rolloutPercent
    reason
  }
}
```

### Example Mutations

#### Update a Flag
```graphql
mutation {
  upsertFlag(
    key: "pricing.v2", 
    description: "Enable new pricing model v2", 
    rolloutPercent: 50,
    rules: "{}"
  ) {
    key
    description
    rolloutPercent
  }
}
```

## Error Handling

The API uses standard HTTP status codes:
- 200: Success
- 304: Not Modified (when using ETags)
- 400: Bad Request
- 401: Unauthorized
- 404: Not Found
- 429: Too Many Requests (rate limited)
- 500: Internal Server Error

Error responses include details about the error:

```json
{
  "error": "Invalid request",
  "details": "Missing required field: description"
}
```
