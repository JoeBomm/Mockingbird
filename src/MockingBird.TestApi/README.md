# MockingBird.TestApi

A small fixture API used to demonstrate `MockingBird.Middleware` end to end. It has two kinds of
endpoints, side by side in the same OpenAPI document:

| Endpoint | Kind | Backed by |
|---|---|---|
| `GET /v1/people` | **Real** | `PeopleController` + an in-memory store |
| `GET /v1/people/{id}` | **Real** | `PeopleController` + an in-memory store |
| `GET /v1/orders/{id}` | **Mock-only** | nothing — flagged `x-mockingbird: true`, MockingBird generates the response |
| `GET /v1/orders` | **Mock-only** | nothing — flagged `x-mockingbird: true`, MockingBird generates the response |
| `POST /v1/orders` | **Mock-only** | nothing — flagged `x-mockingbird: true`, MockingBird generates the response |

The mock-only endpoints exist purely in the OpenAPI document — injected by
[`Swagger/MockOnlyEndpointsDocumentFilter.cs`](Swagger/MockOnlyEndpointsDocumentFilter.cs) — with no
controller behind them. They show up in Swagger UI exactly like a real endpoint. MockingBird reads
the generated document at request time, sees the `x-mockingbird` extension, and answers with
generated data matching the `Order` schema instead of a 404.

## Running it

```bash
export PATH="$HOME/.dotnet:$PATH"   # if dotnet isn't already on PATH
dotnet run --project src/MockingBird.TestApi
```

By default no OpenRouter API key is configured, so MockingBird falls back to its deterministic
offline provider — schema-valid data, but not LLM-generated (you'll see a startup warning saying so).
To see real LLM-generated data instead, give it a key.

The simplest way: copy [`.env.example`](.env.example) to `.env` in this directory (it's
git-ignored) and fill in `MockingBird__OpenRouterApiKey`:

```bash
cp src/MockingBird.TestApi/.env.example src/MockingBird.TestApi/.env
# edit .env, then:
dotnet run --project src/MockingBird.TestApi
```

It's loaded automatically on startup. Alternatively, set the environment variable directly:

```bash
export MockingBird__OpenRouterApiKey="sk-or-..."
dotnet run --project src/MockingBird.TestApi
```

(or `dotnet user-secrets set MockingBird:OpenRouterApiKey sk-or-...` from this directory).

To use Amazon Bedrock instead of OpenRouter, set `MockingBird__Provider=Bedrock` plus the
`MockingBird__Bedrock__*` variables in `.env` (see `.env.example`) — AWS credentials can either go
there directly or come from the AWS SDK's default credential chain (env vars, shared credentials
file, instance/task role, etc.) if you leave `AccessKeyId` blank.

Swagger UI is at `http://localhost:5066/swagger` (port from `Properties/launchSettings.json`) and
shows both real and mock-only paths — the mock-only ones carry an `x-mockingbird` entry in the raw
document (`/swagger/v1/swagger.json`).

## Seeing the difference with curl

**Real endpoints** — ordinary controller logic, nothing MockingBird-specific:

```bash
curl -s http://localhost:5066/v1/people | jq
curl -s http://localhost:5066/v1/people/1 | jq
curl -s -i http://localhost:5066/v1/people/999   # 404 — MockingBird never touches this
```

**Mock-only endpoints** — same URL shape, same status codes and content-type as a real endpoint would
return, but the body is generated on the fly. Look for the `X-MockingBird-Mocked: true` response
header, which only exists to make this obvious during development:

```bash
curl -s -i http://localhost:5066/v1/orders/42 | head -20
curl -s http://localhost:5066/v1/orders | jq
curl -s -X POST http://localhost:5066/v1/orders \
  -H "Content-Type: application/json" \
  -d '{"items":[{"productName":"Widget","quantity":2,"unitPrice":9.99}]}' | jq
```

**Cache stability** — hit the same mocked URL twice; the body is identical (generated once per
route/params, then cached):

```bash
curl -s http://localhost:5066/v1/orders/42
curl -s http://localhost:5066/v1/orders/42   # same body as above
```

**Forcing regeneration** — bypass the cache with the refresh header:

```bash
curl -s -H "X-MockingBird-Refresh: true" http://localhost:5066/v1/orders/42
```

With the deterministic offline provider this still comes back identical (it's seeded from the
request, not random), but with a real OpenRouter key configured you'll see different data each time
you pass this header.
