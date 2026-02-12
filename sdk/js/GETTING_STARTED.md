# Getting Started - JavaScript SDK Test Harness

## Prerequisites

- Node.js 18+ (requires native `fetch` API)
- A running RecallDB instance

## Setup

No external dependencies are required.

## Running the Tests

```bash
node test-harness.js <endpoint> <api_key>
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `endpoint` | `http://localhost:8600` | RecallDB server URL |
| `api_key` | `recalldbadmin` | Bearer token for authentication |

### Examples

Run against a local instance with default credentials:

```bash
node test-harness.js
```

Run against a specific endpoint with a custom bearer token:

```bash
node test-harness.js https://recalldb.example.com my-bearer-token
```

## Output

The test harness runs 100+ integration tests and outputs results in this format:

```
=========================================
  RecallDB Integration Test Harness
  (JavaScript SDK)
=========================================
  Endpoint : http://localhost:8600
  API Key  : recalldbadmin
=========================================

  [PASS] Connectivity: GET / (15 ms)
  [PASS] Connectivity: HEAD / (4 ms)
  ...

=========================================
  Test Summary
=========================================
  Total    : 119
  Passed   : 119
  Failed   : 0
  Runtime  : 3892 ms
  Result   : PASS
=========================================
```

The process exits with code `0` on success or `1` if any tests fail.
