# Preferred Patterns

## Core Principles

- Always go through the JSON:API envelope: a request is `{ data: { type, attributes } }`, never a bare attributes object.
- Treat every HTTP call as fallible: the client returns `AsyncResult<string, JsonApiHttpError>`, so branch on both `Ok` and `Error` paths.
- Use the provided content-type literal `JsonApi.ContentType` instead of typing `application/vnd.api+json` by hand.
- Keep request addressing as data: an `Api` plus a `Path` (`Api -> Url`) function, so the same `Api` can be reused across endpoints.
- Let the library own tracing tags and header propagation; do not set `http.*` tags or B3 headers manually.

## Recommended API Usage

- **Build a request**: `JsonApiRequest.create dataType attributes` produces a `JsonApiRequest<'Attributes>`. The first argument is the resource `type` string; the second is your attributes record.
- **Parse a request**: `JsonApiRequest.parse parseData request` takes a `JsonValue -> Result<'Attributes, 'Error>` decoder and a raw JSON string, returning `Result<JsonApiRequest<'Attributes>, JsonApiRequestParseError<'Error>>`. Parse failures surface as `InvalidRequest`; decoder failures as `InvalidRequestData`. See `examples.md` → Full Workflow.
- **Send a request**: `Http.get path api` and `Http.post path api request` cover the common case; `Http.getWithHeaders`/`Http.postWithHeaders` add a `(string * string) list` of extra headers. See `examples.md` → Basic Example and Realistic Example.
- **Address an endpoint**: define a `Path` as `fun (Api api) -> Url $"{api}/segment"`, or use `Path.id` to use the `Api` value unchanged. Convert with `Url.asUri` when you need a `System.Uri`.
- **Build error payloads**: `JsonApiErrorDto.badRequest`/`notFound`/`conflict` create a single DTO; wrap one or many with `JsonApiErrorResponseData.ofError`/`ofErrors`.

## Error Handling

- Compose calls inside the `asyncResult` computation expression from `Feather.ErrorHandling`; `let!` short-circuits on the first `Error`.
- Pattern-match `JsonApiHttpError` to distinguish a real HTTP failure (`ResponseError`, carrying URI, status code, request method, and body) from transport/exception failures (`ApiError`, `GenericResponseError`) and plain messages (`ApiErrorMessage`).
- Read details off a `ResponseError` with `ResponseError.statusCode`, `requestUri`, `responseContent`, and `requestContent` (which is `Some` only for POST). Use `JsonApiHttpError.format` or `ResponseError.format` for diagnostics. See `examples.md` → Realistic Example.

## Composition

- Because `Path` is a function `Api -> Url`, build parameterized paths by returning a `Path` from a function that closes over route arguments, then apply the shared `Api`.
- Chain dependent calls with the `asyncResult` CE rather than nesting `Async.RunSynchronously`.

## Integration with Other Libraries

- **Alma.Serializer**: POST bodies are serialized internally with `Serialize.toJson`; ensure attribute records are serializer-friendly rather than serializing manually before calling `Http.post`.
- **Alma.Tracing**: the client continues the current active trace (`Trace.Active.current`) and injects B3 headers automatically; create sub-spans for your own work with `Trace.ChildOf.start`.
- **Giraffe / Felicity tracing**: start a request-scoped trace with `Trace.Http.start name ctx`, retrieve it later with `Trace.Http.active ctx`, and append `Trace.Http.finishTraceHandler` in the pipeline so the final `http.status_code` is tagged. Starting a new HTTP trace finishes any previously active one first. See `examples.md` → Integration Example.
- **FSharp.Data**: `JsonApiRequest.parse` is backed by a `JsonProvider`; your `parseData` decoder typically uses a `JsonProvider`-generated type for the attributes.

## Naming Conventions

- Resource `Type` (`ResourceType`): plural, camelCase; a subresource is appended after a hyphen (e.g. `widgetItems-state`).
- Collection (`CollectionName`): plural, lowercase, hyphen-separated (e.g. `widget-items`).
- Unwrap either with its `value` accessor (`Type.value`, `CollectionName.value`).

## Testing Recommendations

- Exercise the client against a real in-process Giraffe server bound to a test port; assert on `Ok`/`Error` and, for failures, destructure `JsonApiHttpError.ResponseError` to check URI, status code, and body.
- Cover round-tripping (POST a request, decode the echoed response) including non-ASCII payloads to catch encoding regressions. See `examples.md` → Test Example.
