---
name: fjson-api
description: Use whenever generating or reviewing F# code that builds, parses, or sends JSON:API requests/responses with Alma.JsonApi — calls to JsonApiRequest.create or JsonApiRequest.parse, Http.get/Http.post (and the *WithHeaders variants returning AsyncResult<string, JsonApiHttpError>), constructs JsonApiErrorDto (badRequest/notFound/conflict) or JsonApiErrorResponseData, matches on ResponseError, or wires Giraffe tracing via Trace.Http.start, Trace.Http.active and Trace.Http.finishTraceHandler. Trigger also on mentions of JSON:API, application/vnd.api+json, Felicity, HttpScopedTrace, resource type vs collection naming, or Path/Api/Url request composition.
---

# F-Json-Api

Library: [alma-oss/fjson-api](https://github.com/alma-oss/fjson-api)
NuGet: `Alma.JsonApi`

## Purpose

`Alma.JsonApi` provides JSON:API request/response types, a typed request parser, an `AsyncResult`-based HTTP client (GET/POST with trace propagation), and Giraffe tracing middleware for Felicity JSON:API servers. It standardizes the `application/vnd.api+json` content type, error DTOs, and resource naming across services that expose or consume JSON:API endpoints.

## When to Use

- Building or parsing JSON:API request bodies (`{ data: { type, attributes } }`).
- Calling a JSON:API endpoint and handling typed success/error results.
- Producing JSON:API error payloads with correct status/title/detail.
- Adding distributed tracing to a Giraffe/Felicity JSON:API pipeline.

## When NOT to Use

- Non-JSON:API HTTP calls — use a plain HTTP client instead.
- Plain JSON serialization unrelated to the JSON:API envelope.
- Tracing outside an ASP.NET Core / Giraffe `HttpContext` (the tracing module depends on Giraffe).

## Main Concepts

- `JsonApi.ContentType` — the `application/vnd.api+json` media type literal.
- `JsonApiData<'Attributes>` — the `{ Type; Attributes }` resource object inside a request.
- `JsonApiRequest<'Attributes>` — the `{ Data }` envelope; built with `JsonApiRequest.create`, read with `JsonApiRequest.parse`.
- `JsonApiRequestParseError<'Error>` — `InvalidRequest of exn | InvalidRequestData of 'Error`, the failure DU from parsing.
- `JsonApiErrorDto` — a single error `{ Status; Title; Detail }` with `badRequest`/`notFound`/`conflict` factories.
- `JsonApiErrorResponseData` — `{ Errors }` wrapper built from `ofError`/`ofErrors`.
- `JsonApiResource.Type` (`ResourceType`) — resource type, camelCase plural, hyphen for subresources.
- `JsonApiResource.Collection` (`CollectionName`) — collection name, lowercase plural with hyphens.
- `Url` / `Api` / `Path` — request addressing; `Path = Api -> Url`, with `Path.id` and `Url.asUri`.
- `Http.get` / `Http.post` (+ `*WithHeaders`) — JSON:API calls returning `AsyncResult<string, JsonApiHttpError>`.
- `JsonApiHttpError` — `ApiError | ApiErrorMessage | ResponseError | GenericResponseError`, the HTTP failure DU.
- `ResponseError` — captures request URI, status code, method, and response body of a 4xx/5xx.
- `HttpScopedTrace` — a trace scoped to an `HttpContext`; managed via the `Trace.Http` helpers.
- `Trace.Http.start` / `Trace.Http.active` / `Trace.Http.finishTraceHandler` — start, retrieve, and finish a request-scoped trace.

## Related Libraries

- `Alma.Tracing` — span model, B3 header propagation, `Trace.ChildOf` child spans.
- `Alma.Serializer` — `Serialize.toJson` used to serialize request bodies.
- `Feather.ErrorHandling` — `asyncResult`/`result` computation expressions and `AsyncResult` combinators.
- `Giraffe` — `HttpHandler` pipeline the tracing middleware plugs into.
- `FSharp.Data` — `JsonProvider` backing `JsonApiRequest.parse`.

## Keywords for Search

JSON:API, application/vnd.api+json, Alma.JsonApi, JsonApiRequest, create, parse, JsonApiData, JsonApiErrorDto, badRequest, notFound, conflict, JsonApiErrorResponseData, JsonApiResource, ResourceType, CollectionName, Http.get, Http.post, getWithHeaders, postWithHeaders, AsyncResult, JsonApiHttpError, ResponseError, Url, Api, Path, HttpScopedTrace, Trace.Http.start, finishTraceHandler, Trace.ChildOf, Giraffe, Felicity, FSharp.Data JsonProvider, Alma.Serializer, Feather.ErrorHandling

## Reference Files

- For composition principles, recommended API usage, error handling, integration, naming, and testing guidance, read `references/preferred-patterns.md`.
- For known pitfalls, incorrect assumptions, and legacy usage, read `references/anti-patterns.md`.
- For worked, self-contained code examples ordered by complexity, read `references/examples.md`.
