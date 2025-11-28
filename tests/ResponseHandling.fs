module JsonApi.ResponseHandling

open System
open System.Threading
open Expecto
open Microsoft.AspNetCore.Http
open Giraffe

open Feather.ErrorHandling
open Alma.JsonApi

open JsonApi.TestUtils
open System.Net

let okOrFail = function
    | Ok ok -> ok
    | Error error -> failtestf "Fail on %A" error

let awaitOkOrFail xA = xA |> Async.RunSynchronously |> okOrFail

[<CLIMutable>]
type Text = {
    Value: string
    Language: string
}

[<RequireQualifiedAccess>]
module Text =
    open FSharp.Data

    type private TextSchema = JsonProvider<""" { "value": "...text...", "language": "cs" } """>

    let parse string =
        let parsed = string |> TextSchema.Parse
        {
            Value = parsed.Value
            Language = parsed.Language
        }

[<CLIMutable>]
type JsonApiData<'Attributes> = {
    Type: string
    Attributes: 'Attributes
}

[<CLIMutable>]
type JsonApi<'Attributes> = {
    Data: JsonApiData<'Attributes>
}

[<CLIMutable>]
type Response = {
    Status: int
    Response: string
}

type ExpectedError = {
    Uri: Uri
    StatusCode: HttpStatusCode
    RequestContent: string option
    ResponseContent: string
}

[<Tests>]
let responseHandlingTest =
    testList "JsonApi - response handling" [
        testCase "GET 2xx" <| fun _ ->
            let textValue = "text"

            use webServer = WebServer.start 9980 {
                Get = [
                    route "/get"
                        >=> json {
                            Value = textValue
                            Language = "cs"
                        }
                ]
                Post = []
            }
            webServer.Run()

            let get: Path = fun (Api api) -> Url $"{api}/get"

            let response =
                Http.get get webServer.Api
                |> Async.RunSynchronously

            Expect.isOk response "Response should be OK with 2xx response"

            // printfn "Response:\n%A" response

        testCase "GET 4xx and 5xx" <| fun _ ->
            use webServer = WebServer.start 9981 {
                Get = [
                    routef "/get/%i/%s" (fun (code, title) ->
                        setStatusCode code
                        >=> json {
                            Status = string code
                            Title = title
                            Detail = ""
                        }
                    )
                ]
                Post = []
            }
            webServer.Run()

            let get code title: Path = fun (Api api) -> Url $"{api}/get/{code}/{title}"

            let expectedResponseError statusCode title =
                let code = statusCode |> HttpStatusCode.asString

                {
                    Uri = webServer.Api |> get code title |> Url.asUri
                    StatusCode = statusCode
                    RequestContent = None
                    ResponseContent =
                        sprintf "{\"status\":\"%s\",\"title\":\"%s\",\"detail\":\"%s\"}" code title ""
                }

            [
                400, "some generic error", expectedResponseError HttpStatusCode.BadRequest
                404, "item was not found", expectedResponseError HttpStatusCode.NotFound
                409, "item already exists", expectedResponseError HttpStatusCode.Conflict

                500, "some generic error", expectedResponseError HttpStatusCode.InternalServerError
                502, "service error", expectedResponseError HttpStatusCode.BadGateway
            ]
            |> List.iter (fun (statusCode, title, expectedError) ->
                let response =
                    Http.get (get statusCode title) webServer.Api
                    |> Async.RunSynchronously

                Expect.isError response "Response should be Error with 4xx / 5xx response"

                // printfn "Response for post 4xx:\n%A" response

                match response with
                | Error (JsonApiHttpError.ResponseError responseError) ->
                    let expected = expectedError title

                    Expect.equal (responseError |> ResponseError.requestUri) expected.Uri "Response error should have request Uri"
                    Expect.equal (responseError |> ResponseError.statusCode) expected.StatusCode "Response error should have request StatusCode"
                    Expect.isNone (responseError |> ResponseError.requestContent) "Response error should not have request RequestContent in GET request"
                    Expect.equal (responseError |> ResponseError.responseContent) expected.ResponseContent "Response error should have request ResponseContent"
                | _ -> failtest "Unexpected error"
            )

        testCase "POST 2xx" <| fun _ ->
            let textValue = "text"

            use webServer = WebServer.start 9982 {
                Get = []
                Post = [
                    route "/post"
                        >=> bindJson<JsonApi<Text>> (fun { Data = { Attributes = text }} ->
                            // printfn "ResponseText:\n%A" text

                            Expect.equal text.Value textValue "Text value should be the same as an original value"
                            Expect.equal text.Language "cs" "Language should be the same as an original value"

                            Successful.OK text
                        )
                ]
            }
            webServer.Run()

            let post: Path = fun (Api api) -> Url $"{api}/post"

            let response =
                {
                    Value = textValue
                    Language = "cs"
                }
                |> JsonApiRequest.create "text"
                // |> tee (printfn "Request:\n%A")
                |> Http.post post webServer.Api
                |> Async.RunSynchronously

            Expect.isOk response "Response should be OK with 2xx response"

            // printfn "Response:\n%A" response

        testCase "POST 4xx and 5xx" <| fun _ ->
            use webServer = WebServer.start 9983 {
                Get = []
                Post = [
                    route "/post"
                        >=> bindJson<JsonApi<Response>> (fun { Data = { Attributes = response }} ->
                            // printfn "Response: %A" response
                            match response with
                            | { Status = 400; Response = response } -> RequestErrors.BAD_REQUEST (JsonApiErrorDto.badRequest response)
                            | { Status = 404; Response = response } -> RequestErrors.NOT_FOUND (JsonApiErrorDto.notFound response)
                            | { Status = 409; Response = response } -> RequestErrors.CONFLICT (JsonApiErrorDto.conflict response)
                            | { Status = 500; Response = response } -> ServerErrors.INTERNAL_ERROR ({| Status = "500"; Detail = response |})
                            | { Status = 502; Response = response } -> ServerErrors.BAD_GATEWAY ({| Status = "502"; Detail = response |})
                            | { Status = code } -> failtestf "Status code %A is not implemented yet." code
                        )
                ]
            }
            webServer.Run()

            let post: Path = fun (Api api) -> Url $"{api}/post"

            let expectedResponseError title statusCode response =
                let code = statusCode |> HttpStatusCode.asString

                {
                    Uri = webServer.Api |> post |> Url.asUri
                    StatusCode = statusCode
                    RequestContent = Some (sprintf "{\"data\":{\"type\":\"response\",\"attributes\":{\"status\":%s,\"response\":\"%s\"}}}" code response)
                    ResponseContent =
                        match title with
                        | Some title -> sprintf "{\"status\":\"%s\",\"title\":\"%s\",\"detail\":\"%s\"}" code title response
                        | _ -> sprintf "{\"detail\":\"%s\",\"status\":\"%s\"}" response code
                }

            [
                400, "some generic error", expectedResponseError (Some "Bad Request") HttpStatusCode.BadRequest
                404, "item was not found", expectedResponseError (Some "Resource Not Found") HttpStatusCode.NotFound
                409, "item already exists", expectedResponseError (Some "Conflict") HttpStatusCode.Conflict

                500, "some generic error", expectedResponseError None HttpStatusCode.InternalServerError
                502, "service error", expectedResponseError None HttpStatusCode.BadGateway
            ]
            |> List.iter (fun (statusCode, responseString, expectedError) ->
                let response =
                    {
                        Status = statusCode
                        Response = responseString
                    }
                    |> JsonApiRequest.create "response"
                    // |> tee (printfn "Request:\n%A")
                    |> Http.post post webServer.Api
                    |> Async.RunSynchronously

                Expect.isError response "Response should be Error with 4xx / 5xx response"

                // printfn "Response for post 4xx:\n%A" response

                match response with
                | Error (JsonApiHttpError.ResponseError responseError) ->
                    let expected = expectedError responseString

                    Expect.equal (responseError |> ResponseError.requestUri) expected.Uri "Response error should have request Uri"
                    Expect.equal (responseError |> ResponseError.statusCode) expected.StatusCode "Response error should have request StatusCode"
                    Expect.equal (responseError |> ResponseError.requestContent) expected.RequestContent "Response error should have request RequestContent"
                    Expect.equal (responseError |> ResponseError.responseContent) expected.ResponseContent "Response error should have request ResponseContent"
                | _ -> failtest "Unexpected error"
            )
    ]
