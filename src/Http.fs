namespace Alma.JsonApi

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open FSharp.Data.HttpRequestHeaders

open Alma.JsonApi
open Alma.Serializer
open Alma.ErrorHandling
open Alma.Tracing
open Alma.Tracing.Extension

//
// Common types
//

type Url = Url of string
type Api = Api of string
type Path = Api -> Url

[<RequireQualifiedAccess>]
module Url =
    let asUri (Url url) = Uri url

[<RequireQualifiedAccess>]
module Path =
    /// Identity path - it will use the given Api as is for the URL
    let id: Path = fun (Api api) -> Url api

//
// Errors
//

type Method =
    | Get
    | Post of string
    | Unsupported of HttpMethod

type ResponseError = {
    Uri: Uri
    StatusCode: HttpStatusCode
    Request: Method
    Response: string
    ResponseMessage: HttpResponseMessage
}

[<RequireQualifiedAccess>]
module HttpStatusCode =
    let parseExn (e: exn) =
        match e with
        | :? WebException as webException when webException.Message.Contains "(400) Bad Request" -> Some HttpStatusCode.BadRequest
        | :? WebException as webException when webException.Message.Contains "(401) Unauthorized" -> Some HttpStatusCode.Unauthorized
        | :? WebException as webException when webException.Message.Contains "(403) Forbidden" -> Some HttpStatusCode.Forbidden
        | :? WebException as webException when webException.Message.Contains "(404) Not Found" -> Some HttpStatusCode.NotFound
        | :? WebException as webException when webException.Message.Contains "(406) Not Acceptable" -> Some HttpStatusCode.NotAcceptable
        | :? WebException as webException when webException.Message.Contains "(408) Request Timeout" -> Some HttpStatusCode.RequestTimeout
        | :? WebException as webException when webException.Message.Contains "(409) Conflict" -> Some HttpStatusCode.Conflict
        | :? WebException as webException when webException.Message.Contains "(422) Unprocessable Entity" -> Some HttpStatusCode.UnprocessableEntity
        | _ -> None

    let asInt (statusCode: HttpStatusCode) =
        int statusCode

    let asString = asInt >> string

    let isError = asInt >> fun code -> code >= 400

[<RequireQualifiedAccess>]
module internal HttpContent =
    let asString (content: HttpContent) = asyncResult {
        return! content.ReadAsStringAsync()
    }

[<RequireQualifiedAccess>]
module internal HttpClient =
    let jsonApiClient (headers: (string * string) list) =
        let client = new HttpClient()
        client
            .DefaultRequestHeaders
            .Accept
            .Add(MediaTypeWithQualityHeaderValue(JsonApi.ContentType))

        headers
        |> List.iter (fun (key, value) ->
            client.DefaultRequestHeaders.TryAddWithoutValidation(key, value) |> ignore
        )

        client

[<RequireQualifiedAccess>]
module ResponseError =
    let internal fromResponse (response: HttpResponseMessage) = asyncResult {
        let! responseString = response.Content |> HttpContent.asString

        let! method =
            match response.RequestMessage.Method with
            | get when get = HttpMethod.Get -> AsyncResult.ofSuccess Get
            | post when post = HttpMethod.Post ->
                response.RequestMessage.Content
                |> HttpContent.asString
                |> AsyncResult.map Post
            | unsupported -> AsyncResult.ofSuccess (Unsupported unsupported)

        return {
            Uri = response.RequestMessage.RequestUri
            StatusCode = response.StatusCode
            Request = method
            Response = responseString
            ResponseMessage = response
        }
    }

    let response { ResponseMessage = response } = response
    let format: ResponseError -> string = sprintf "%A"

    let requestUri { Uri = uri } = uri
    let statusCode { StatusCode = code } = code

    let requestContent = function
        | { Request = Post request } -> Some request
        | { Request = _ } -> None

    let responseContent { Response = response } = response

[<RequireQualifiedAccess>]
type JsonApiHttpError =
    /// Generic Api Error exception
    | ApiError of exn
    /// Generic Api Error message
    | ApiErrorMessage of string
    /// Specific 4xx or 5xx response error
    | ResponseError of ResponseError
    /// Api handles the request but there is an error with the Response
    | GenericResponseError of exn

[<RequireQualifiedAccess>]
module JsonApiHttpError =
    let format = function
        | JsonApiHttpError.ApiError e -> sprintf "Error: %A" e
        | JsonApiHttpError.ApiErrorMessage e -> sprintf "Error: %A" e
        | JsonApiHttpError.ResponseError e -> e |> ResponseError.format
        | JsonApiHttpError.GenericResponseError e -> sprintf "Request was handled but there is a problem with the response: %A" e

    let internal statusCode = function
        | JsonApiHttpError.ResponseError e -> e |> ResponseError.statusCode |> Some
        | _ -> None

[<RequireQualifiedAccess>]
module Http =
    let private handleResponseTracedError (trace, error) =
        use trace = trace |> Trace.addError (TracedError.ofError JsonApiHttpError.format error)

        error
        |> JsonApiHttpError.statusCode
        |> Option.iter (fun statusCode ->
            trace
            |> Trace.addTags [ "http.status_code", statusCode |> HttpStatusCode.asString ]
            |> ignore
        )

        error

    let private handleResponseTracedSuccess (trace, response: HttpResponseMessage) =
        use trace = trace |> Trace.addTags [ "http.status_code", response.StatusCode |> HttpStatusCode.asString ]

        response.Content
        |> HttpContent.asString
        |> AsyncResult.mapError (fun e ->
            trace
            |> Trace.addError (TracedError.ofExn e)
            |> ignore

            JsonApiHttpError.ApiError e
        )

    let private handleResponse (result: AsyncResult<Trace * HttpResponseMessage, Trace * JsonApiHttpError>): AsyncResult<string, JsonApiHttpError> =
        result
        |> AsyncResult.mapError handleResponseTracedError
        |> AsyncResult.bind handleResponseTracedSuccess

    let private assertSuccessfulResponse (response: HttpResponseMessage): AsyncResult<unit, JsonApiHttpError> = asyncResult {
        if response.StatusCode |> HttpStatusCode.isError then
            let! responseError =
                response
                |> ResponseError.fromResponse
                |> AsyncResult.mapError JsonApiHttpError.GenericResponseError

            return! AsyncResult.ofError (JsonApiHttpError.ResponseError responseError)
    }

    let getWithHeaders (path: Path) (api: Api) headers: AsyncResult<string, JsonApiHttpError> =
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

            use client = HttpClient.jsonApiClient headers

            headers
            |> Http.inject trace
            |> List.iter (fun (key, value) ->
                client.DefaultRequestHeaders.TryAddWithoutValidation(key, value) |> ignore
            )
            let tracedError error = trace, error

            let! (response: HttpResponseMessage) =
                client.GetAsync(url)
                |> AsyncResult.ofTaskCatch (JsonApiHttpError.ApiError >> tracedError)

            do! assertSuccessfulResponse response |> AsyncResult.mapError tracedError

            return trace, response
        }
        |> handleResponse

    let get (path: Path) (api: Api): AsyncResult<string, JsonApiHttpError> =
        getWithHeaders path api []

    let postWithHeaders<'Request> (path: Path) (api: Api) headers (request: JsonApiRequest<'Request>): AsyncResult<string, JsonApiHttpError> =
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

            use client = HttpClient.jsonApiClient headers
            use requestBodyContent = new StringContent(requestBody, Text.Encoding.UTF8)

            ContentType JsonApi.ContentType :: headers
            |> Http.inject trace
            |> List.iter (fun (key, value) ->
                try requestBodyContent.Headers.Remove(key) |> ignore with _ -> ()
                requestBodyContent.Headers.TryAddWithoutValidation(key, value) |> ignore
            )
            let tracedError error = trace, error

            let! (response: HttpResponseMessage) =
                client.PostAsync(url, requestBodyContent)
                |> AsyncResult.ofTaskCatch (JsonApiHttpError.ApiError >> tracedError)

            do! assertSuccessfulResponse response |> AsyncResult.mapError tracedError

            return trace, response
        }
        |> handleResponse

    let post<'Request> path api (request: JsonApiRequest<'Request>): AsyncResult<string, JsonApiHttpError> =
        postWithHeaders path api [] request
