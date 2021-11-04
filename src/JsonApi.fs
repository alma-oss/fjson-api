namespace Lmc.JsonApi

[<RequireQualifiedAccess>]
module JsonApi =
    let [<Literal>] ContentType = "application/vnd.api+json"

type JsonApiData<'Attributes> = {
    Type: string
    Attributes: 'Attributes
}

type JsonApiRequest<'Attributes> = {
    Data: JsonApiData<'Attributes>
}

type JsonApiRequestParseError<'InvalidRequestDataError> =
    | InvalidRequest of exn
    | InvalidRequestData of 'InvalidRequestDataError

[<RequireQualifiedAccess>]
module JsonApiRequest =
    open FSharp.Data
    open Lmc.ErrorHandling
    open Lmc.ErrorHandling.Result.Operators

    let create dataType data =
        {
            Data = {
                Type = dataType
                Attributes = data
            }
        }

    type private RequestSchema = JsonProvider<"src/schema/request.json", SampleIsList=true>

    let parse parseData request = result {
        try
            let parsedRequest =
                request
                |> RequestSchema.Parse

            let! data =
                parsedRequest.Data.Attributes.JsonValue
                |> parseData <@> InvalidRequestData

            return create parsedRequest.Data.Type data

        with e ->
            return! Error (InvalidRequest e)
    }

[<RequireQualifiedAccess>]
module Http =
    let (|BadRequest|Unauthorized|NotFound|Unknown|) (e: exn) =
        match e with
        | :? System.Net.WebException as webException when webException.Message.Contains "(400) Bad Request" -> BadRequest
        | :? System.Net.WebException as webException when webException.Message.Contains "(401) Unauthorized" -> Unauthorized
        | :? System.Net.WebException as webException when webException.Message.Contains "(404) Not Found" -> NotFound
        | e -> Unknown e

//
// Errors
//

type JsonApiErrorDto = {
    Status: string
    Title: string
    Detail: string
}

[<RequireQualifiedAccess>]
module JsonApiErrorDto =
    let notFound message =
        {
            Status = "404"
            Title = "Resource Not Found"
            Detail = message
        }

type JsonApiErrorResponseData = {
    Errors: JsonApiErrorDto list
}

[<RequireQualifiedAccess>]
module JsonApiErrorResponseData =
    let ofErrors errors = {
        Errors = errors
    }

    let ofError error = ofErrors [ error ]
