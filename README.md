# RecallDB

A multi-tenant RESTful vector database service built on PostgreSQL with pgvector.

## Quick Start

```bash
cd docker
docker compose up
```

API available at `http://localhost:8600`, dashboard at `http://localhost:8601`.

### Default Credentials

- **Admin API Key:** `recalldbadmin`
- **User:** `admin@recall` / `password`
- **Bearer Token:** `default`

## Configuration

See `recalldb.json` for server configuration. Environment variables override database settings:

| Variable | Description |
|----------|-------------|
| `RECALLDB_DB_HOST` | PostgreSQL hostname |
| `RECALLDB_DB_PORT` | PostgreSQL port |
| `RECALLDB_DB_NAME` | Database name |
| `RECALLDB_DB_USER` | Database username |
| `RECALLDB_DB_PASS` | Database password |

## SDKs

- **C#:** `sdk/csharp/RecallDb.Sdk/`
- **Python:** `sdk/python/`
- **JavaScript:** `sdk/js/`

## API Documentation

See [REST_API.md](REST_API.md) for complete endpoint documentation.

## Building

```bash
dotnet restore src/RecallDb.sln
dotnet build src/RecallDb.sln
```

## License

MIT - see [LICENSE.md](LICENSE.md)
