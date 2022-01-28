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

[<RequireQualifiedAccess>]
module JsonApiResource =
    /// Resource type should be a plural name in cammel case.
    /// It can contain a subresource, separated by hyphen.
    /// Example:
    ///     type: personAggregates-state
    type Type = ResourceType of string

    /// Collection name should be a plural name in lowercase with values separated by hyphens.
    /// Example:
    ///     type: person-aggregates
    type Collection = CollectionName of string

    [<RequireQualifiedAccess>]
    module Type =
        let value (ResourceType resourceType) = resourceType

    [<RequireQualifiedAccess>]
    module CollectionName =
        let value (CollectionName collectionName) = collectionName
