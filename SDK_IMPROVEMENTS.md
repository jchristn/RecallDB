# SDK Improvements

A plan for the RecallDB SDKs (C#, JavaScript, Python): what a published SDK must carry so clients such as Isis can use
the server's current search features, and the client-side defects and gaps found while auditing the three SDKs. It
covers code, tests, documentation, the changelog, and release.

**Status (2026-09-26):** implemented, with the SDKs at a common version, 0.2.2, as the owner directed (section 9).
The server work it depended on (recency, collapse, minimum-should-match, capabilities) was implemented at the same
time from `HYBRID_SEARCH_IMPROVEMENTS.md`. Publishing the packages and the Isis move (I1) are still open.

This plan owns the SDK side of `HYBRID_SEARCH_IMPROVEMENTS.md`. That plan's section 6.4 and checklist items K1 to K5
(new hybrid fields, capabilities helper, harness cases) are adopted here as items S3 and S5, so the SDK work can be
tracked in one place. Server-side changes stay in that plan.

---

## 1. Summary

The SDK source in this repository is ahead of what clients can install. The C# package on NuGet, `RecallDb.Sdk`
0.2.1, predates the hybrid search work (`d8ce32c`), so a client using it cannot request single-call hybrid search,
read per-leg ranks and scores, request stored vectors, or read the server's notices. Isis, the main consumer, works
around every one of these today: it runs two searches and fuses them itself, and it cannot use stored vectors for
result diversity or duplicate detection.

The work falls into four groups:

1. **Ship what is already written** (S1, S2): publish an SDK that carries the hybrid and include-embeddings fields,
   and fix version drift between the three SDKs.
2. **Carry the next server features** (S3 to S5): recency, collapse, minimum-should-match, group and recency fields on
   hits, stored vectors in the JS and Python SDKs, and a typed capabilities call.
3. **Fix client defects** (S6 to S10): connection lifetime and timeouts, inconsistent URL encoding of path segments,
   `Exists` calls that report a server failure as "not found", no cancellation of body reads, and indented JSON on the
   wire.
4. **Quality** (S11 to S14): named constants for string enums, nullable annotations and complete XML docs in C#,
   `127.0.0.1` in docs, and harness coverage for all of the above.

## 2. Current state (verified against the code)

Source is at `0d022e6`. Server `_Version` and the C# package version are `0.2.1`; `sdk/js/package.json` says `0.2.0`.

### 2.1 Published C# package vs source

The published package was inspected in the local NuGet cache (`recalldb.sdk/0.2.1/lib/net10.0`), by its XML
documentation file and the member names in its metadata.

| Member | Source (`sdk/csharp/RecallDb.Sdk`) | Published 0.2.1 |
|---|---|---|
| `SearchQuery.Hybrid` / `HybridQuery` (`Strategy`, `RrfK`, `CandidatePool`) | yes (`Models/HybridQuery.cs`) | no |
| `FullTextQuery.MatchMode` (`Any`, `All`) | yes | no |
| `FullTextQuery.TextWeight` | yes | yes |
| `DocumentRecord.VectorScore`, `VectorRank`, `TextRank` | yes (`Models/DocumentRecord.cs` L85-103) | no |
| `DocumentRecord.TextScore` | yes | yes |
| `DocumentRecord.Embeddings` | yes | yes |
| `SearchQuery.IncludeEmbeddings` | yes (`Models/SearchQuery.cs` L91, default false) | no |
| `SearchResult.Notice` | yes (`Models/SearchResult.cs` L55) | no |

The published package can read `Embeddings` but cannot ask for them, and servers since `d5d4e30` only return them when
asked. So a client on the published package never gets stored vectors from a current server.

### 2.2 Client behavior, all three SDKs

| Area | C# (`RecallDbClient.cs`) | JS (`recalldb-sdk.js`) | Python (`recalldb_sdk.py`) |
|---|---|---|---|
| Transport | Creates its own `HttpClient` in the constructor (L44); no way to pass one, a handler, or a timeout | `fetch`, no `AbortSignal` or timeout | `requests.Session` with no timeout (L37, L754-771), so a stalled server blocks forever |
| Path encoding | `Seg()` (`Uri.EscapeDataString`) on 6 segments (documents); 38 other segments concatenated raw (tenants, users, credentials, collections, labels, tags, search) | `encodeURIComponent` on 5 segments; the rest interpolated raw | `quote` on 5 segments; the rest interpolated raw |
| `...ExistsAsync` / `exists` | `HeadAsync` returns `status == 200` (L824-829); 401, 403, 429, and 5xx read as "does not exist" | Same (L614-620) | Same (L758-760) |
| Errors | `RecallDbException(StatusCode, ResponseBody)`; body not parsed | `RecallDbException(statusCode, responseBody)` | `RecallDbException(status_code, response_body)` |
| Cancellation | `CancellationToken` on sends, but `ReadAsStringAsync()` without it (L849, L869, L878) | none | none |
| Request JSON | `WriteIndented = true` (L50); a 768-dimension vector is sent pretty-printed | compact | compact |
| Health | `HealthAsync` returns `Dictionary<string, object>` of `JsonElement` values | object | dict |
| Retries | none | none | none |

Server ids are validated since `c73cc7a`, so today's tenant, collection, and tag ids happen to be URL-safe; the raw
concatenation is a latent defect rather than a live one, except for any caller-chosen id the server accepts.

### 2.3 How Isis uses the C# SDK

`src/Isis.Core/Stores/RecallDb/RecallDbClientPool.cs` caches one `RecallDbClient` per endpoint and key, because the
SDK creates an `HttpClient` per instance. `RecallDbMemoryStore.cs` runs a vector search and a full-text search and
fuses them in `HybridFusion.cs` (text weight, RRF constant, recency, collapse by parent key). Its result diversity and
similar-memory check use word overlap because it cannot get stored vectors. `HYBRID_SEARCH_IMPROVEMENTS.md` section 8
describes moving Isis to the single call.

---

## 3. Items

Priority: **P1** blocks Isis from using features the server already has; **P2** needed for the next server features or
fixes a defect that can cause wrong behavior; **P3** quality.

| Id | Item | Priority | SDKs |
|---|---|---|---|
| S1 | Publish an SDK built from current source, and align versions across the three SDKs | P1 | all |
| S2 | Confirm source parity for the existing hybrid, rank, notice, and include-embeddings fields | P1 | all |
| S3 | New hybrid-plan fields: recency, collapse, minimum-should-match, group and recency fields | P2 | all |
| S4 | Stored vectors: `IncludeEmbeddings` in JS and Python; group mean vectors when the server adds them | P2 | all |
| S5 | Typed server info and capabilities | P2 | all |
| S6 | Transport: inject `HttpClient` or handler, timeouts, cancellation | P2 | all |
| S7 | URL-encode every path segment | P2 | all |
| S8 | `Exists` calls throw on anything but 200 and 404 | P2 | all |
| S9 | Structured errors: parse the server's error body | P3 | all |
| S10 | Compact request JSON in C# | P2 | C# |
| S11 | Named constants for string enums | P3 | all |
| S12 | Nullable annotations and complete XML docs in C# | P3 | C# |
| S13 | `127.0.0.1` in docs and doc comments | P3 | all |
| S14 | Harness coverage for S2 to S10 | P1 | all |

---

## 4. Design

### S1. Publish from current source; align versions

- Build and publish `RecallDb.Sdk` from source that includes `d8ce32c` onward and this plan's changes. NuGet will
  not accept a second 0.2.1, so publishing needs a new version number; that decision is the owner's (section 9).
- Bring `sdk/js/package.json` in line with the other SDKs when the owner sets the version (it says 0.2.0 today).
- Whether the JS and Python SDKs are published to npm and PyPI was not verified; if they are, the same applies.
- Update `PackageReleaseNotes` in `RecallDb.Sdk.csproj`, which still describes the OpenTelemetry release.

### S2. Source parity for existing fields

No new code in C#. For JS and Python, confirm the `search()` documentation lists every field the server accepts and
returns today: `Hybrid.Strategy`, `Hybrid.RrfK`, `Hybrid.CandidatePool`, `FullText.MatchMode`, `FullText.TextWeight`,
`IncludeEmbeddings`, and on hits `VectorScore`, `TextScore`, `VectorRank`, `TextRank`, and `Notice` on the result.
Add harness cases (S14) that send each request field and assert each response field, so a server change that drops
one fails an SDK test rather than a client.

### S3. Fields from the hybrid plan

As specified in `HYBRID_SEARCH_IMPROVEMENTS.md` sections 4.1 to 4.3 and 6.4. C# shapes:

```csharp
public class HybridQuery
{
    // existing: Strategy, RrfK, CandidatePool
    public double? RecencyWeight { get; set; }        // null: omitted, server default 0 (off); range 0.0 to 1.0
}

public class CollapseQuery                              // new
{
    public string Field { get; set; } = "DocumentId";  // "DocumentId" or "Tag"
    public string TagKey { get; set; }                  // required when Field is "Tag"
    public int? CandidatePool { get; set; }             // single-leg searches
}

public class SearchQuery    { public CollapseQuery Collapse { get; set; } }       // null: no collapse
public class FullTextQuery  { public int? MinimumShouldMatch { get; set; } }      // null: server default 1
public class DocumentRecord { public int? RecencyRank { get; set; } public string GroupKey { get; set; } public int? GroupHits { get; set; } }
```

New request fields are nullable and omitted when null (the client already writes with `WhenWritingNull`), so a new
SDK can talk to an older server. JS and Python pass objects through; the work there is documentation and harness
cases.

### S4. Stored vectors

- JS and Python: document `IncludeEmbeddings` on `search()` and `Embeddings` on hits (C# has both in source).
- Document the cost in all three READMEs: vectors are JSON numbers, about 4 KB per hit at 384 dimensions and 8 KB at
  768, so ask for them only when needed (for example result diversity or duplicate detection).
- When the server adds a group mean vector (`HYBRID_SEARCH_IMPROVEMENTS.md` section 12,
  `Collapse.IncludeMeanEmbedding`), add `CollapseQuery.IncludeMeanEmbedding` (`bool?`) and
  `DocumentRecord.GroupEmbeddings` (`List<float>`). Not in scope until the server design exists.

### S5. Typed server info and capabilities

The hybrid plan adds a `Capabilities` list to `GET /`. Clients need it because servers with different features report
the same version.

```csharp
public class ServerInfo                                 // new
{
    public string Version { get; set; }
    public List<string> Capabilities { get; set; } = new List<string>();   // empty for servers that predate it
    // plus the other fields GET / returns today, typed
}

public Task<ServerInfo> GetServerInfoAsync(CancellationToken token = default);
public Task<bool> SupportsAsync(string capability, CancellationToken token = default);  // cached per client
```

- Keep `HealthAsync` as is for compatibility; `GetServerInfoAsync` is the typed form.
- Capability names as constants: `Capabilities.SearchHybrid`, `SearchCollapse`, `SearchRecency`,
  `SearchIncludeEmbeddings`, `SearchMinimumShouldMatch`, matching the strings the server emits.
- `SupportsAsync` caches the list for the client's lifetime (a server restart with new features needs a new client, or
  a `refresh` parameter).
- JS: `getServerInfo()`, `supports(name)`. Python: `get_server_info()`, `supports(name)`.

### S6. Transport: connection lifetime, timeouts, cancellation

C#:

```csharp
public RecallDbClient(string endpoint, string bearerToken);                                   // unchanged: owns its HttpClient
public RecallDbClient(string endpoint, string bearerToken, HttpClient httpClient);            // caller owns httpClient; not disposed by Dispose()
public RecallDbClient(string endpoint, string bearerToken, HttpMessageHandler handler);       // client owns the HttpClient, caller owns the handler
public TimeSpan Timeout { get; set; }                                                         // default 100 s (HttpClient default); must be positive
```

- The injected forms let callers share a connection pool, add retry or telemetry handlers, and use test handlers.
  Isis would pass one shared handler and keep its per-endpoint pool only for the bearer token.
- Pass the `CancellationToken` to `ReadAsStringAsync` in `HandleResponseAsync`, `PostAsync`, and `DeleteAsync`.
- No built-in retries. Retry policy belongs to the caller's handler; document an example that retries 429, 502, and 503
  with backoff.

JS: an optional `options` argument `{ timeoutMs, fetch }` on the constructor (custom `fetch` for proxies and tests),
and an optional `{ signal }` on each method, combined with the timeout through `AbortSignal.any` where available.

Python: an optional `timeout` (seconds, default 100, `None` to disable) on the constructor, passed to every
`requests` call, and an optional `session` argument so callers can mount retry adapters.

### S7. URL-encode every path segment

Apply the existing encoder to every caller-supplied segment: C# `Seg()` on the 38 raw concatenations, JS
`encodeURIComponent`, Python `urllib.parse.quote(value, safe="")`. No behavior change for today's server-generated ids;
it removes the latent defect for ids containing reserved characters. Harness case: create a document whose key and id
contain `#`, `?`, `/`, `%`, and a space, then read, check existence, search by id, and delete it.

### S8. `Exists` calls report failures

`...ExistsAsync` / `exists` returns `true` for 200, `false` for 404, and throws `RecallDbException` for anything else
(401, 403, 429, 5xx). Today an unreachable or unauthorized server reads as "not found", which can make a caller create
duplicates or skip cleanup. This is a behavior change for callers that relied on `false` during an outage, so it goes
in the changelog under a clear heading.

### S9. Structured errors

When the error body is the server's JSON error object, parse it and expose the fields, keeping the raw body:

```csharp
public class RecallDbException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }          // unchanged
    public string ErrorCode { get; }             // null when the body is not the server's error object
    public string ErrorMessage { get; }          // likewise
}
```

The message becomes `"RecallDB API returned 400 (BadRequest): RrfK must be between 1 and 100000."` when parsed. JS and
Python get the same properties (`errorCode`, `errorMessage`; `error_code`, `error_message`). Field names follow the
server's error model; confirm against `RecallDbServer.cs` when implementing.

### S10. Compact request JSON (C#)

Use `WriteIndented = false` for requests. Indentation adds whitespace to every request and roughly one line per float
to vector payloads. Keep a separate indented options object only if anything logs request bodies.

### S11. Named constants for string enums

Keep strings on the wire and in the models, so no model type changes, and add constant holders:
`HybridStrategies` (`Rrf`, `Linear`, `Filter`), `FullTextSearchTypes`, `FullTextMatchModes` (`Any`, `All`),
`VectorSearchTypes`, `SortOrders`, `CollapseFields` (`DocumentId`, `Tag`). JS and Python get the same as frozen objects
and module constants.

### S12. Nullable annotations and XML docs (C#)

- Enable `<Nullable>enable</Nullable>` and annotate the public surface: optional request fields are nullable, and
  response fields that the server may omit are nullable. Consumers compiling with nullable warnings then see which
  fields can be missing, for example `VectorScore` on a text-only hit.
- Remove `<NoWarn>1591</NoWarn>` and complete the missing XML docs, so undocumented members fail the build. This
  matches the repository's code-style requirement of documentation on every public member and zero warnings.

### S13. Loopback address in docs

Replace `localhost` with `127.0.0.1` in the SDK READMEs, GETTING_STARTED guides, the `RecallDbClient` constructor doc
comment, and the JS and Python docstrings, per the repository requirements. The harnesses already use `127.0.0.1`.

### S14. Harness coverage

See section 6.

---

## 5. Changes by area

| Area | File | Change | Items |
|---|---|---|---|
| C# | `sdk/csharp/RecallDb.Sdk/RecallDbClient.cs` | New constructors and `Timeout`; `Seg()` on every segment; `HeadAsync` 200/404/throw; token on body reads; `WriteIndented = false`; `GetServerInfoAsync`, `SupportsAsync` | S5, S6, S7, S8, S10 |
| | `Models/HybridQuery.cs`, `SearchQuery.cs`, `FullTextQuery.cs`, `DocumentRecord.cs` | S3 fields | S3 |
| | `Models/CollapseQuery.cs`, `Models/ServerInfo.cs` (new) | New types | S3, S5 |
| | `Constants/*.cs` (new; one class per file) | `Capabilities`, `HybridStrategies`, `FullTextSearchTypes`, `FullTextMatchModes`, `VectorSearchTypes`, `SortOrders`, `CollapseFields` | S5, S11 |
| | `RecallDbException.cs` | `ErrorCode`, `ErrorMessage`, parsed message | S9 |
| | `RecallDb.Sdk.csproj` | Nullable on, `NoWarn 1591` removed, release notes | S1, S12 |
| | `README.md`, `GETTING_STARTED.md` | Single-call hybrid example, capabilities check, vector cost note, injected `HttpClient` and retry-handler example, `127.0.0.1` | S3 to S6, S13 |
| JS | `sdk/js/recalldb-sdk.js` | Constructor options, per-call `signal`, encoding, `_head` 200/404/throw, error fields, `getServerInfo()`, `supports()`, constants, JSDoc for S2 to S4 fields | S2 to S9, S11 |
| | `package.json` | Version aligned when the owner sets it | S1 |
| | `README.md`, `GETTING_STARTED.md` | As for C# | S3 to S6, S13 |
| Python | `sdk/python/recalldb_sdk.py` | `timeout` and `session` arguments, encoding, `_head` 200/404/raise, error fields, `get_server_info()`, `supports()`, constants, docstrings for S2 to S4 fields | S2 to S9, S11 |
| | `README.md`, `GETTING_STARTED.md` | As for C# | S3 to S6, S13 |
| Repo | `CHANGELOG.md` | Section 8 | all |
| | `TESTING.md` | How to run the JS and Python harnesses alongside the C# one, and the new cases | S14 |

---

## 6. Tests

All three harnesses (`sdk/csharp/RecallDb.Sdk.TestHarness/Program.cs`, `sdk/js/test-harness.js`,
`sdk/python/test_harness.py`) get the same cases, run against a local server on `127.0.0.1`.

| Case | Checks | Items |
|---|---|---|
| Hybrid round trip | `Hybrid.Strategy`, `RrfK`, `CandidatePool`, `FullText.MatchMode`, `TextWeight` accepted; hits carry `VectorScore`, `TextScore`, `VectorRank`, `TextRank`; text-only hits have a null vector rank | S2 |
| Notice | A request that triggers a server notice returns it on the result | S2 |
| Include embeddings | Off by default (no vectors on hits); on returns a vector of the collection's dimension on every hit | S2, S4 |
| Collapse and recency | Collapse by tag returns one hit per group with `GroupKey`, `GroupHits`, `RecencyRank`; recency weight 1.5 raises a 400 `RecallDbException` | S3 |
| Minimum should match | `MinimumShouldMatch` 2 excludes a document matching one term | S3 |
| Server info | `GetServerInfoAsync` returns the version and the capability list; `SupportsAsync` true for a listed capability and false for an unknown one | S5 |
| Injected transport (C#) | A client built with a caller's `HttpClient` works, and disposing the client leaves that `HttpClient` usable | S6 |
| Timeout | A timeout shorter than a delayed response (a test handler in C#, a stub server in JS and Python) raises a timeout error rather than hanging | S6 |
| Cancellation | A cancelled token or aborted signal stops a request | S6 |
| Reserved characters | Document key and id containing `#`, `?`, `/`, `%`, and a space: create, read, exists, search by id, delete | S7 |
| Exists on failure | `exists` with a bad token throws with 401 instead of returning false; 404 still returns false | S8 |
| Structured error | An invalid `RrfK` raises an exception whose `ErrorMessage` names the field, with `ResponseBody` unchanged | S9 |
| Compact JSON (C#) | A test handler sees a request body with no newlines | S10 |

Also: the C# SDK builds with zero warnings after S12, and a consumer project compiled with nullable warnings on sees
no warnings for correct use of the documented examples.

## 7. Isis integration

Once an SDK with S1 to S6 is published:

1. Move Isis to the new package version and run its full suite and benchmark retrieval rounds (isis-live, Atlas,
   SciFact, LongMemEval) to confirm no change in results on the two-call path.
2. Check `SupportsAsync(Capabilities.SearchHybrid)` and move `RecallDbMemoryStore` to the single call per
   `HYBRID_SEARCH_IMPROVEMENTS.md` section 8, keeping the two-call path as the fallback. Pass Isis's own settings:
   `RrfK` from `query.RrfK ?? HybridFusion.DefaultRrfK` (default 20, not 60), `TextWeight` from
   `query.TextWeight ?? HybridFusion.DefaultTextWeight`, recency weight, and `CandidatePool` equal to Isis's current
   fetch size so results match.
3. When `SearchIncludeEmbeddings` is supported, request vectors only when diversity is on or for the similar-memory
   check on upsert, and move `SearchDiversifier` from word overlap to cosine similarity, falling back to word overlap
   when vectors are absent.
4. Replace `RecallDbClientPool`'s per-client `HttpClient` with one shared handler through the S6 constructor.
5. Benchmark again and record the round in Isis's `benchmarks/RESULTS.md`, with the history command.

## 8. Changelog

Bullets for `CHANGELOG.md`, under whatever version heading the owner chooses:

```markdown
- SDKs (C#, JS, Python): single-call hybrid search options and per-leg ranks and scores, stored vectors on request
  (`IncludeEmbeddings`), search notices, and the recency, collapse, and minimum-should-match options
- SDKs: typed server info with a capabilities list (`GetServerInfoAsync`/`SupportsAsync`, `getServerInfo`/`supports`,
  `get_server_info`/`supports`), so clients can detect features on servers that report the same version
- C# SDK: constructors that accept an `HttpClient` or `HttpMessageHandler`, a `Timeout` property, cancellation of
  response reads, and compact request JSON. JS SDK: `timeoutMs`, a custom `fetch`, and per-call `signal`. Python SDK:
  `timeout` (default 100 s; previously none) and a custom `session`
- SDKs: every path segment is URL-encoded
- SDKs: `RecallDbException` exposes the server's error code and message
- SDKs: named constants for strategies, search types, match modes, sort orders, collapse fields, and capabilities
- C# SDK: nullable annotations on the public surface
- **Behavior change:** `...ExistsAsync` / `exists` now throws on any status other than 200 or 404, instead of returning
  false when the server is unreachable, unauthorized, or failing
```

## 9. Release and versioning

**Decision (owner, 2026-09-26):** all three SDKs move to a common version, **0.2.2**: `<Version>` in
`RecallDb.Sdk.csproj`, `version` in `sdk/js/package.json`, and a new `__version__` in `recalldb_sdk.py`. The server
stays 0.2.1. The CHANGELOG has an "SDKs v0.2.2" entry. The text below is the original analysis.

Following `C:\code\agents\requirements\VERSIONING.md`, this plan changed no version number on its own: not
`RecallDb.Sdk.csproj`, not `package.json`, not the Python module. Two things needed the owner:

- **A publish decision.** NuGet will not accept a second `RecallDb.Sdk` 0.2.1, so getting any of this to Isis through
  NuGet needs a new version. Until then, Isis can validate against a project reference to `sdk/csharp/RecallDb.Sdk` in
  a local branch.
- **Version alignment.** `sdk/js/package.json` (0.2.0) differs from the rest (0.2.1).

## 10. Acceptance criteria

- A published C# package exposes every member in section 2.1 and the S3, S5, S6, S9, and S11 additions.
- All three harnesses pass every section 6 case against a current server, and the C# harness also passes against a
  server that predates capabilities (empty capability list, S3 fields omitted on the wire).
- The C# SDK builds with zero warnings with nullable enabled and `NoWarn 1591` removed.
- No SDK document or doc comment uses `localhost`.
- Isis passes its suite and matches its benchmark results on the new package before switching to the single call.

## 11. Checklist

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| S1 | Publish decision and version alignment | 4, 9 | done | owner | Version 0.2.2 in all three SDKs; release notes updated. Publishing to NuGet/npm/PyPI not done |
| S2 | JS and Python docs for existing fields; parity harness cases | 4 | done | |  |
| S3 | Hybrid-plan fields in all SDKs (replaces hybrid plan K1, K3, K4) | 4 | done | | depends on server work; Server work implemented alongside |
| S4 | `IncludeEmbeddings` in JS and Python; vector cost note | 4 | done | | Group mean vectors remain out of scope |
| S5 | `ServerInfo`, `SupportsAsync`, capability constants | 4 | done | | depends on server `Capabilities`; Server reports Capabilities in GET / and MCP server/info |
| S6 | Transport options, timeouts, cancellation | 4 | done | |  |
| S7 | Encode every path segment | 4 | done | |  |
| S8 | `Exists` 200/404/throw | 4 | done | | behavior change; Behavior change in CHANGELOG |
| S9 | Structured errors | 4 | done | | Handles both server error shapes (Context, or Message/Description) |
| S10 | Compact request JSON (C#) | 4 | done | |  |
| S11 | Enum constants | 4 | done | |  |
| S12 | Nullable and XML docs (C#) | 4 | done | | Zero warnings; typed calls throw on an empty success body |
| S13 | `127.0.0.1` in docs | 4 | done | |  |
| S14 | Harness cases in all three SDKs (replaces hybrid plan K5) | 6 | done | | C# 164, JS 165, Python 160 cases pass |
| D1 | READMEs and GETTING_STARTED (replaces hybrid plan K2) | 5 | done | |  |
| D2 | CHANGELOG and TESTING.md | 5, 8 | done | |  |
| I1 | Isis moves to the published package, then to the single call | 7 | todo | | after S1 |

## 12. Verified, and not verified

**Verified:** the published package's members (from its XML documentation and assembly metadata in the local NuGet
cache); the source models and client code at the line numbers given; the raw and encoded path segment counts; the
`Exists` implementations in all three SDKs; the Python client's lack of a timeout; how Isis creates and pools clients.

**Not verified:** whether the JS and Python SDKs are published to npm or PyPI; the exact field names of the server's
error object (S9); the capability strings, which the hybrid plan defines but the server does not emit yet.
