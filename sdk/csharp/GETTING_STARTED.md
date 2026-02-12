# Getting Started - C# SDK Test Harness

## Prerequisites

- .NET 10.0 SDK
- A running RecallDB instance

## Setup

No additional package installation is required. The test harness project references the SDK project directly.

## Running the Tests

```bash
cd sdk/csharp/RecallDb.Sdk.TestHarness
dotnet run -- <endpoint> <api_key>
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `endpoint` | `http://localhost:8600` | RecallDB server URL |
| `api_key` | `recalldbadmin` | Bearer token for authentication |

### Examples

Run against a local instance with default credentials:

```bash
dotnet run
```

Run against a specific endpoint with a custom bearer token:

```bash
dotnet run -- https://recalldb.example.com my-bearer-token
```

## Output

The test harness runs 100+ integration tests and outputs results in this format:

```
=========================================
  RecallDB Integration Test Harness
  (C# SDK)
=========================================
  Endpoint : http://localhost:8600
  API Key  : recalldbadmin
=========================================

  [PASS] Connectivity: GET / (25 ms)
  [PASS] Connectivity: HEAD / (5 ms)
  ...

=========================================
  Test Summary
=========================================
  Total    : 119
  Passed   : 119
  Failed   : 0
  Runtime  : 5214 ms
  Result   : PASS
=========================================
```

The process exits with code `0` on success or `1` if any tests fail.
