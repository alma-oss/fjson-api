# Examples

All example code for this skill lives here. Examples are ordered from simplest to most complete and use neutral placeholder names only.

## Basic Example — build and POST a request

```fsharp
open Alma.JsonApi

type WidgetAttributes = {
    Name: string
    Size: int
}

let api = Api "https://example-api.test"
let createPath: Path = fun (Api root) -> Url $"{root}/widgets"

let send () =
    { Name = "demo"; Size = 3 }
    |> JsonApiRequest.create "widgets"
    |> Http.post createPath api
```

## Realistic Example — GET and handle typed errors

```fsharp
open Alma.JsonApi
open System.Net
open Feather.ErrorHandling

let api = Api "https://example-api.test"
let getPath: Path = fun (Api root) -> Url $"{root}/widgets/42"

let fetch () = asyncResult {
    let! body = Http.get getPath api
    return body
}

let describeFailure (error: JsonApiHttpError) =
    match error with
    | JsonApiHttpError.ResponseError responseError ->
        let code = responseError |> ResponseError.statusCode
        let uri = responseError |> ResponseError.requestUri
        let payload = responseError |> ResponseError.responseContent
        sprintf "HTTP %A at %A: %s" code uri payload
    | JsonApiHttpError.ApiErrorMessage message -> message
    | other -> other |> JsonApiHttpError.format

let run () =
    match fetch () |> Async.RunSynchronously with
    | Ok body -> printfn "OK: %s" body
    | Error error -> error |> describeFailure |> printfn "Failed: %s"
```

## Integration Example — Giraffe pipeline with request-scoped tracing

```fsharp
open Giraffe
open Alma.Tracing
open Alma.Tracing.Extension.Giraffe

// Append finishTraceHandler so the final http.status_code is tagged on the request trace.
let webApp =
    choose [
        Trace.Http.finishTraceHandler
            >=> route "/widgets" >=> Successful.OK "ok"
    ]

// Inside a Felicity operation: start the request trace, then add child spans for sub-work.
let handleCreate (ctx: Microsoft.AspNetCore.Http.HttpContext) =
    let requestTrace = ctx |> Trace.Http.start "Create Widget"
    use _ = "Validate input" |> Trace.ChildOf.start requestTrace.Trace

    // ... do work ...

    let active = Trace.Http.active ctx
    use _ = "Persist widget" |> Trace.ChildOf.start active
    ()
```

## Test Example — Expecto round-trip against an in-process server

```fsharp
open Expecto
open Giraffe
open Alma.JsonApi
open JsonApi.TestUtils

[<CLIMutable>]
type Note = {
    Value: string
    Language: string
}

[<CLIMutable>]
type NoteData = { Type: string; Attributes: Note }

[<CLIMutable>]
type NoteEnvelope = { Data: NoteData }

[<Tests>]
let roundTrip =
    testCase "POST round-trips the JSON:API envelope" <| fun _ ->
        use webServer = WebServer.start 9990 {
            Get = []
            Post = [
                route "/notes"
                    >=> mustAccept [ JsonApi.ContentType ]
                    >=> bindJson<NoteEnvelope> (fun { Data = { Attributes = note } } ->
                        Successful.OK note
                    )
            ]
        }
        webServer.Run()

        let post: Path = fun (Api root) -> Url $"{root}/notes"

        let response =
            { Value = "ahoj světe"; Language = "cs" }
            |> JsonApiRequest.create "notes"
            |> Http.post post webServer.Api
            |> Async.RunSynchronously

        Expect.isOk response "Response should be OK for a 2xx round-trip"
```

## Full Workflow — parse an incoming request, build an error response

```fsharp
open Alma.JsonApi
open FSharp.Data
open Feather.ErrorHandling

type WidgetAttributes = {
    Name: string
    Size: int
}

// Decoder: JsonValue -> Result<WidgetAttributes, string>
let parseWidget (json: JsonValue) =
    match json.TryGetProperty "name", json.TryGetProperty "size" with
    | Some name, Some size ->
        Ok { Name = name.AsString(); Size = size.AsInteger() }
    | _ -> Error "Missing required attributes"

let handle (rawBody: string) =
    match JsonApiRequest.parse parseWidget rawBody with
    | Ok request ->
        // request.Data.Type and request.Data.Attributes are now typed
        Ok request.Data.Attributes
    | Error (JsonApiRequestParseError.InvalidRequestData detail) ->
        detail
        |> JsonApiErrorDto.badRequest
        |> JsonApiErrorResponseData.ofError
        |> Error
    | Error (JsonApiRequestParseError.InvalidRequest _) ->
        "Malformed JSON:API request"
        |> JsonApiErrorDto.badRequest
        |> JsonApiErrorResponseData.ofError
        |> Error
```
