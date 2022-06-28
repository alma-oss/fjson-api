namespace Lmc.JsonApi

//
// Common types
//

type Url = Url of string
type Api = Api of string
type Path = Api -> Url

//
// Errors
//

[<RequireQualifiedAccess>]
type JsonApiHttpGetError =
    | ApiError of exn
    | ApiErrorMessage of string
    | NotFound

[<RequireQualifiedAccess>]
module JsonApiHttpGetError =
    let format = function
        | JsonApiHttpGetError.ApiError e -> sprintf "Error: %A" e
        | JsonApiHttpGetError.ApiErrorMessage e -> sprintf "Error: %A" e
        | JsonApiHttpGetError.NotFound -> sprintf "Resource was not found."

[<RequireQualifiedAccess>]
type JsonApiHttpPostError =
    | ApiError of exn
    | ApiErrorMessage of string
    | NotFound
    | ResponseError of exn

[<RequireQualifiedAccess>]
module JsonApiHttpPostError =
    let format = function
        | JsonApiHttpPostError.ApiError e -> sprintf "Error: %A" e
        | JsonApiHttpPostError.ApiErrorMessage e -> sprintf "Error: %A" e
        | JsonApiHttpPostError.NotFound -> sprintf "Resource not found."
        | JsonApiHttpPostError.ResponseError e -> sprintf "Response ends with error.\nError: %A" e

[<RequireQualifiedAccess>]
module Http =
    open System
    open System.Net
    open System.Net.Http
    open System.Net.Http.Headers
    open FSharp.Data
    open FSharp.Data.HttpRequestHeaders
    open Lmc.JsonApi
    open Lmc.Serializer
    open Lmc.ErrorHandling
    open Lmc.Tracing
    open Lmc.Tracing.Extension

    [<AutoOpen>]
    module internal Utils =
        open System.Text.RegularExpressions

        // http://www.fssnip.net/29/title/Regular-expression-active-pattern
        let (|Regex|_|) pattern input =
            let m = Regex.Match(input, pattern)
            if m.Success then Some (List.tail [ for g in m.Groups -> g.Value ])
            else None

    let (|BadRequest|Unauthorized|NotFound|Conflict|Unknown|) (e: exn) =
        match e with
        | :? WebException as webException when webException.Message.Contains "(400) Bad Request" -> BadRequest
        | :? WebException as webException when webException.Message.Contains "(401) Unauthorized" -> Unauthorized
        | :? WebException as webException when webException.Message.Contains "(404) Not Found" -> NotFound
        | :? WebException as webException when webException.Message.Contains "(409) Conflict" -> Conflict
        | e -> Unknown e

    type private Response =
        | HttpResponse of HttpResponse
        | HttpResponseMessage of HttpResponseMessage

        member this.StatusCode =
            match this with
            | HttpResponse response -> string response.StatusCode
            | HttpResponseMessage response -> response.StatusCode.ToString()

    let private handleResponse<'Error>
        (notFoundError: 'Error)
        (apiError: exn -> 'Error)
        (apiErrorMessage: string -> 'Error)
        (result: AsyncResult<Trace * Response, Trace * exn>): AsyncResult<string, 'Error> =

        result
        |> AsyncResult.mapError (fun (trace, error) ->
            use _ =
                let statusCode =
                    match error, error.Message with
                    | Unauthorized, _ -> "401"
                    | NotFound, _ -> "404"
                    | _, Regex "^\((\d{3})\)" [ statusCode ] -> statusCode
                    | _ -> "400"

                trace
                |> Trace.addTags [ "http.status_code", statusCode ]
                |> Trace.addError (TracedError.ofExn error)

            match error with
            | NotFound -> notFoundError
            | e -> e |> apiError
        )
        |> AsyncResult.bind (fun (trace, response) ->
            use _ = trace |> Trace.addTags [ "http.status_code", response.StatusCode ]

            match response with
            | HttpResponse { Body = Text text } -> AsyncResult.ofSuccess text
            | HttpResponse { Body = Binary binary } ->
                sprintf "Expecting text, but got a binary response (%d bytes)" binary.Length
                |> apiErrorMessage
                |> AsyncResult.ofError

            | HttpResponseMessage response ->
                response.Content.ReadAsStringAsync()
                |> AsyncResult.ofTaskCatch (sprintf "%A" >> apiErrorMessage)
        )

    let get (path: Path) (api: Api): AsyncResult<string, JsonApiHttpGetError> =
        asyncResult {
            let trace =
                "[JsonApi] Get response"
                |> Trace.ChildOf.continueOrStart Trace.Active.current
                |> Trace.addTags [
                    "component", (sprintf "fJsonApi (%s)" AssemblyVersionInformation.AssemblyVersion)
                    "http.method", "GET"
                    "span.kind", "client"
                ]

            let (Url url) = api |> path
            let trace = trace |> Trace.addTags [ "http.url", url ]

            let! response =
                Http.AsyncRequest (
                    url,
                    httpMethod = "GET",
                    headers = (
                        [
                            Accept JsonApi.ContentType
                        ]
                        |> Http.inject trace
                    )
                )
                |> AsyncResult.ofAsyncCatch (fun e -> trace, e)

            return trace, HttpResponse response
        }
        |> handleResponse
            JsonApiHttpGetError.NotFound
            JsonApiHttpGetError.ApiError
            JsonApiHttpGetError.ApiErrorMessage

    let post<'Request> (path: Path) (api: Api) (request: JsonApiRequest<'Request>): AsyncResult<string, JsonApiHttpPostError> =
        asyncResult {
            let trace =
                "[JsonApi] Post response"
                |> Trace.ChildOf.continueOrStart Trace.Active.current
                |> Trace.addTags [
                    "component", (sprintf "fJsonApi (%s)" AssemblyVersionInformation.AssemblyVersion)
                    "http.method", "POST"
                    "span.kind", "client"
                ]

            let (Url url) = api |> path
            let trace = trace |> Trace.addTags [ "http.url", url ]

            let requestBody =
                request
                |> Serialize.toJson

            use client = new HttpClient()
            client
                .DefaultRequestHeaders
                .Accept
                .Add(MediaTypeWithQualityHeaderValue(JsonApi.ContentType))

            use requestBodyContent = new StringContent(requestBody, Text.Encoding.UTF8)

            [
                ContentType JsonApi.ContentType
            ]
            |> Http.inject trace
            |> List.iter (fun (key, value) ->
                try requestBodyContent.Headers.Remove(key) |> ignore with _ -> ()
                requestBodyContent.Headers.TryAddWithoutValidation(key, value) |> ignore
            )

            let! response =
                client.PostAsync(url, requestBodyContent)
                |> AsyncResult.ofTaskCatch (fun e -> trace, e)

            return trace, HttpResponseMessage response
        }
        |> handleResponse
            JsonApiHttpPostError.NotFound
            JsonApiHttpPostError.ApiError
            JsonApiHttpPostError.ApiErrorMessage
