# AGENTS.md — Alma.JsonApi (fjson-api)

This repo ships Agent Skill for the `Alma.JsonApi` library. Compatible agents discover it automatically; see `.agents/skills/fjson-api/SKILL.md`.

## Project Purpose

F# library (`Alma.JsonApi`) providing JSON:API types, request/response parsing, HTTP client helpers (GET/POST with tracing), and Giraffe-based tracing middleware for Felicity JSON:API servers. Used by Alma microservices that expose or consume JSON:API endpoints. Published as a NuGet package.

## Tech Stack

| Component | Detail |
|---|---|
| Language | F# on .NET 10.0 |
| SDK | `global.json` pins .NET SDK 10.0.x (`rollForward: latestMinor`) |
| Web framework | Giraffe ~> 8.2 (ASP.NET Core middleware) |
| JSON parsing | FSharp.Data ~> 6.0 (JSON Type Provider) |
| Serialization | `Alma.Serializer` (JSON serialization) |
| State | `Alma.State` |
| Error handling | `Feather.ErrorHandling` (`asyncResult` CE) |
| Tracing | `Alma.Tracing` — OpenTracing-style spans, HTTP header propagation |
| Test framework | Expecto |
| Build system | FAKE (F# Make) v1.3.0 via `build/` project |
| Package manager | Paket |
| Lint | fsharplint |

## Commands

```bash
# Restore dependencies
dotnet paket install

# Build
./build.sh build

# Run tests
./build.sh -t tests

# Publish to NuGet (CI only — requires NUGET_API_KEY)
./build.sh -t publish
```

Build options:
- `no-clean` — skip cleaning output dirs (required on CI)
- `no-lint` — run lint but ignore failures

## Project Structure

```
fjson-api/
├── JsonApi.fsproj              # Library project (PackageId: Alma.JsonApi, v11.0.0)
├── AssemblyInfo.fs             # Auto-generated assembly metadata
├── src/
│   ├── Utils.fs                # Internal utility: `tee` function
│   ├── JsonApi.fs              # Core types: JsonApiRequest, JsonApiData, error DTOs, resource types
│   ├── Tracing.fs              # Giraffe HttpHandler tracing: HttpScopedTrace, Trace.Http module
│   ├── Http.fs                 # HTTP client: get, post, getWithHeaders, postWithHeaders
│   └── schema/
│       └── request.json        # JSON Type Provider schema for parsing JSON:API requests
├── tests/
│   ├── tests.fsproj            # Test project
│   ├── Tests.fs                # Expecto test runner entry point
│   ├── WebServer.fs            # Test web server setup
│   ├── Encoding.fs             # Encoding tests
│   └── ResponseHandling.fs     # Response handling tests
├── build/
│   ├── Build.fs                # FAKE build entry point
│   ├── Targets.fs              # FAKE target definitions
│   └── ...
├── paket.dependencies
├── paket.references
├── fsharplint.json
├── global.json
├── CHANGELOG.md
└── .github/workflows/
    ├── tests.yaml
    ├── pr-check.yaml
    └── publish.yaml
```

## Architecture & Key Concepts

### Module: `Alma.JsonApi.JsonApi`

- `JsonApi.ContentType` = `"application/vnd.api+json"` — the JSON:API media type.
- Core types: `JsonApiRequest<'Attributes>`, `JsonApiData<'Attributes>`.
- `JsonApiRequest.parse`: Parses a JSON string into a typed `JsonApiRequest` using a caller-provided `parseData` function. Uses FSharp.Data JSON Type Provider with `src/schema/request.json` as the sample.
- `JsonApiRequest.create`: Constructs a request programmatically.

### Error Types

- `JsonApiErrorDto`: `{ Status; Title; Detail }` — with factory functions `badRequest`, `notFound`, `conflict`.
- `JsonApiErrorResponseData`: `{ Errors: JsonApiErrorDto list }` — wraps multiple errors.
- `JsonApiRequestParseError`: `InvalidRequest of exn | InvalidRequestData of 'Error`.

### Resource Types

- `JsonApiResource.Type` (`ResourceType of string`): Plural camelCase, e.g., `personAggregates-state`.
- `JsonApiResource.Collection` (`CollectionName of string`): Plural lowercase with hyphens, e.g., `person-aggregates`.

### Module: `Alma.Tracing.Extension.Giraffe.Trace`

- `Trace.Http.start name ctx`: Starts an HTTP-scoped trace from an `HttpContext`. Extracts trace context from request headers (B3 propagation). Sets `span.kind=server`, `http.method`, `http.url`.
- `Trace.Http.finishTraceHandler`: Giraffe `HttpHandler` that finishes the active trace after the response, adding `http.status_code`.
- `Trace.Http.active ctx`: Retrieves the active trace from `HttpContext`.
- Pattern: Start trace at operation entry → use `Trace.ChildOf.start` for sub-operations → finish via `finishTraceHandler`.

### Module: `Alma.JsonApi.Http`

- `Http.get` / `Http.getWithHeaders`: GET request with JSON:API `Accept` header, trace injection, error handling.
- `Http.post` / `Http.postWithHeaders`: POST request with serialized `JsonApiRequest` body, trace injection.
- All return `AsyncResult<string, JsonApiHttpError>`.
- `JsonApiHttpError`: `ApiError of exn | ApiErrorMessage | ResponseError of ResponseError | GenericResponseError of exn`.
- `ResponseError`: Contains URI, status code, request method, response body, and raw `HttpResponseMessage`.

## Key Dependencies

| Package | Role |
|---|---|
| `Giraffe` | ASP.NET Core functional web framework (HttpHandler pipeline) |
| `FSharp.Data` | JSON Type Provider for request schema parsing |
| `Alma.Serializer` | JSON serialization (`Serialize.toJson`) |
| `Alma.State` | State management |
| `Alma.Tracing` | Distributed tracing with HTTP header propagation |
| `Feather.ErrorHandling` | `asyncResult` CE and `AsyncResult` combinators |

## Conventions

- **Namespace split**: `Alma.JsonApi` for core types and HTTP client; `Alma.Tracing.Extension.Giraffe` for tracing middleware.
- **JSON:API content type**: Always use `JsonApi.ContentType` (`application/vnd.api+json`), not raw strings.
- **Resource naming**: Resource types are camelCase plural (`personAggregates`); collection names are lowercase hyphenated (`person-aggregates`).
- **Type Provider schemas**: JSON schemas live in `src/schema/`. Changing the schema file changes the generated types.
- **Railway-oriented error handling**: All HTTP functions return `AsyncResult`. Errors are `JsonApiHttpError` DU.
- **Trace scoping**: HTTP traces are scoped to `HttpContext` via `HttpScopedTrace`. Always start with `Trace.Http.start` and finish with `finishTraceHandler`.

## CI/CD

| Workflow | Trigger | What it does |
|---|---|---|
| `tests.yaml` | PRs + nightly cron | Runs `./build.sh -t tests` on ubuntu-latest with .NET 10.x |
| `pr-check.yaml` | PRs | Blocks fixup commits + ShellCheck |
| `publish.yaml` | Git tags `[0-9]+.[0-9]+.[0-9]+` | Publishes to NuGet.org |

## Release Process

1. Increment `<Version>` in `JsonApi.fsproj`
2. Update `CHANGELOG.md`
3. Commit and create a git tag matching the version
4. Push tag — CI publishes automatically

## Pitfalls

- **JSON Type Provider schemas**: `src/schema/request.json` is used at compile time by `FSharp.Data.JsonProvider`. Changing this file affects type inference for `JsonApiRequest.parse`. Always rebuild after schema changes.
- **No docker-compose**: Library project — no local services needed.
- **`packages/` directory**: Contains locally cached NuGet packages (e.g., `Alma.Logging`). These are managed by Paket — do not modify manually.
- **Giraffe dependency**: The `Tracing.fs` module depends on Giraffe's `HttpHandler` type. This library is only usable in ASP.NET Core web applications.
- **`build/` is shared boilerplate**: FAKE build files are shared across Alma libraries. Do not modify `Targets.fs` or `SafeBuildHelpers.fs` without understanding cross-project impact.
- **HTTP client creates new instances**: `Http.get`/`Http.post` create a new `HttpClient` per request. For high-throughput scenarios, consider connection pooling.
