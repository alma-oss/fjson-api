module JsonApi.Encoding

open System
open System.Threading
open Expecto
open Giraffe

open Lmc.ErrorHandling
open Lmc.JsonApi

open JsonApi.TestUtils

let okOrFail = function
    | Ok ok -> ok
    | Error error -> failtestf "Fail on %A" error

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
type TextData = {
    Type: string
    Attributes: Text
}

[<CLIMutable>]
type JsonApiText = {
    Data: TextData
}

[<Tests>]
let encodingTest =
    testList "JsonApi - encoding" [
        testCase "request body from GET" <| fun _ ->
            let textValue =
                [
                    "<h2>1. Úvodní ustanovení</h2>"
                    "\n\n<p>\n    1.1 Společnost LMC s.r.o."
                    "(dále jen&nbsp;„LMC“)"
                    "LMC –\n    "
                    "Seduo.cz – vztahu ke&nbsp;koncovým uživatelům – fyzickým osobám\n    </p>"
                ]
                |> String.concat ""

            use cancellationTokenSource = new CancellationTokenSource()

            WebServer.start 9990 {
                Get = []
                Post = [
                    route "/post"
                        >=> mustAccept [ JsonApi.ContentType ]
                        >=> bindJson<JsonApiText> (fun { Data = { Attributes = text }} ->
                            // printfn "ResponseText:\n%A" text

                            Expect.equal text.Value textValue "Text value should be the same as an original value"
                            Expect.equal text.Language "cs" "Language should be the same as an original value"

                            Successful.OK text
                        )
                ]
            }
            |> fun aX -> Async.Start(aX, cancellationTokenSource.Token)

            let post: Path = fun (Api api) -> Url $"{api}/post"
            let api: Api = Api "http://localhost:9990"

            let _response =
                {
                    Value = textValue
                    Language = "cs"
                }
                |> JsonApiRequest.create "text"
                // |> tee (printfn "Request:\n%A")
                |> Http.post post api
                |> Async.RunSynchronously
                |> okOrFail

            cancellationTokenSource.Cancel()

            // printfn "Response:\n%A" response

        testCase "response from GET" <| fun _ ->
            let textValue =
                [
                    "<h2>1. Úvodní ustanovení</h2>"
                    "\n\n<p>\n    1.1 Společnost LMC s.r.o."
                    "(dále jen&nbsp;„LMC“)"
                    "LMC –\n    "
                    "Seduo.cz – vztahu ke&nbsp;koncovým uživatelům – fyzickým osobám\n    </p>"
                ]
                |> String.concat ""

            use cancellationTokenSource = new CancellationTokenSource()

            WebServer.start 9991 {
                Get = [
                    route "/get"
                        >=> json {
                            Value = textValue
                            Language = "cs"
                        }
                ]
                Post = []
            }
            |> fun aX -> Async.Start(aX, cancellationTokenSource.Token)

            let get: Path = fun (Api api) -> Url $"{api}/get"
            let api: Api = Api "http://localhost:9991"

            let response =
                Http.get get api
                |> Async.RunSynchronously
                |> okOrFail

            cancellationTokenSource.Cancel()

            let responseText = response |> Text.parse

            // printfn "Response:\n%A" response
            // printfn "ResponseText:\n%A" responseText

            Expect.equal responseText.Value textValue "Text value should be the same as an original value"
            Expect.equal responseText.Language "cs" "Language should be the same as an original value"
    ]
