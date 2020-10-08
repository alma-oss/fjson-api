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

[<RequireQualifiedAccess>]
module JsonApiRequest =
    let create dataType data =
        {
            Data = {
                Type = dataType
                Attributes = data
            }
        }

[<RequireQualifiedAccess>]
module Http =
    let (|BadRequest|Unauthorized|NotFound|Unknown|) (e: exn) =
        match e with
        | :? System.Net.WebException as webException when webException.Message.Contains "(400) Bad Request" -> BadRequest
        | :? System.Net.WebException as webException when webException.Message.Contains "(401) Unauthorized" -> Unauthorized
        | :? System.Net.WebException as webException when webException.Message.Contains "(404) Not Found" -> NotFound
        | e -> Unknown e
