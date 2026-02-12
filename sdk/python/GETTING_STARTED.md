# Getting Started - Python SDK Test Harness

## Prerequisites

- Python 3.7+
- A running RecallDB instance

## Setup

Install dependencies:

```bash
pip install -r requirements.txt
```

## Running the Tests

```bash
python test_harness.py <endpoint> <api_key>
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `endpoint` | `http://localhost:8600` | RecallDB server URL |
| `api_key` | `recalldbadmin` | Bearer token for authentication |

### Examples

Run against a local instance with default credentials:

```bash
python test_harness.py
```

Run against a specific endpoint with a custom bearer token:

```bash
python test_harness.py https://recalldb.example.com my-bearer-token
```

## Output

The test harness runs 100+ integration tests and outputs results in this format:

```
=========================================
  RecallDB Integration Test Harness
  (Python SDK)
=========================================
  Endpoint : http://localhost:8600
  API Key  : recalldbadmin
=========================================

  [PASS] Connectivity: GET / (12 ms)
  [PASS] Connectivity: HEAD / (3 ms)
  ...

=========================================
  Test Summary
=========================================
  Total    : 119
  Passed   : 119
  Failed   : 0
  Runtime  : 4523 ms
  Result   : PASS
=========================================
```

The process exits with code `0` on success or `1` if any tests fail.
