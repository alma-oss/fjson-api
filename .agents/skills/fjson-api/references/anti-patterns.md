# Anti-Patterns

Each entry is **mistake → why → fix**.

## Hardcoding the content type

- **Mistake**: writing the string `"application/vnd.api+json"` directly in headers or `Accept`.
- **Why**: duplicates a value the library already owns; a typo silently breaks content negotiation.
- **Fix**: use the `JsonApi.ContentType` literal.

## Treating an HTTP call as if it returns a plain string

- **Mistake**: using the result of `Http.get`/`Http.post` as the response body without handling the `Error` case.
- **Why**: the return type is `AsyncResult<string, JsonApiHttpError>`; a 4xx/5xx or transport failure produces `Error`, not an exception you can ignore.
- **Fix**: bind inside the `asyncResult` CE or match `Ok`/`Error`, and pattern-match `JsonApiHttpError` on failure.

## Assuming every failure is an HTTP status error

- **Mistake**: matching only `JsonApiHttpError.ResponseError` and discarding the other cases.
- **Why**: transport exceptions surface as `ApiError`/`GenericResponseError` and plain failures as `ApiErrorMessage`; only `ResponseError` carries a status code.
- **Fix**: handle all four `JsonApiHttpError` cases; read status/body only from `ResponseError`.

## Expecting request content on GET errors

- **Mistake**: assuming `ResponseError.requestContent` always returns a body.
- **Why**: it is `Some` only for POST requests and `None` for GET.
- **Fix**: treat `requestContent` as optional and only rely on it for POST.

## Sending raw attributes instead of the JSON:API envelope

- **Mistake**: posting an attributes record (or a hand-built JSON string) directly.
- **Why**: JSON:API servers expect `{ data: { type, attributes } }`; a bare object is rejected.
- **Fix**: wrap with `JsonApiRequest.create` and let `Http.post` serialize it.

## Pre-serializing the POST body

- **Mistake**: calling `Serialize.toJson` (or another serializer) yourself and passing the string to the client.
- **Why**: `Http.post` already serializes the `JsonApiRequest` internally; double-serializing produces an escaped string body.
- **Fix**: pass the `JsonApiRequest<'T>` value; let the client serialize it.

## Setting trace headers or `http.*` tags manually

- **Mistake**: adding B3 headers or `http.method`/`http.url`/`http.status_code` tags yourself around a call.
- **Why**: the client and `Trace.Http` middleware inject propagation headers and set these tags automatically; manual ones collide or duplicate.
- **Fix**: rely on the built-in injection; add only your own domain-neutral child spans via `Trace.ChildOf.start`.

## Using `finishTraceHandler` without an active scoped trace

- **Mistake**: appending `Trace.Http.finishTraceHandler` to the pipeline but never calling `Trace.Http.start` to create the `HttpScopedTrace`.
- **Why**: the finish handler tags `http.status_code` on the request-scoped trace; with nothing started there is no span to finish.
- **Fix**: start the trace at the operation entry with `Trace.Http.start name ctx`, then let `finishTraceHandler` close it.

## Assuming a pooled / reused HttpClient

- **Mistake**: relying on connection reuse across many high-frequency calls.
- **Why**: each `Http.get`/`Http.post` constructs a new `HttpClient` per request.
- **Fix**: for high-throughput paths, batch work or add pooling at a higher layer rather than expecting the client to reuse sockets.

## Misnaming resource types and collections

- **Mistake**: using snake_case, singular, or PascalCase names, or swapping type and collection conventions.
- **Why**: `ResourceType` must be camelCase plural (hyphen for subresources) and `CollectionName` lowercase hyphenated; inconsistent names break routing/contract expectations.
- **Fix**: follow the naming convention and unwrap with the `value` accessors.

## Editing generated or cached files

- **Mistake**: hand-editing `src/schema/request.json` without rebuilding, or modifying files under `packages/`.
- **Why**: the schema feeds the compile-time `JsonProvider`, so stale builds give wrong inferred types; `packages/` is Paket-managed.
- **Fix**: rebuild after any schema change and let Paket manage cached packages.
