namespace Lmc.JsonApi

[<AutoOpen>]
module internal Utils =
    let tee f a =
        f a
        a
