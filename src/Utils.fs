namespace Lmc.Tracing.Extension

[<AutoOpen>]
module internal Utils =
    let tee f a =
        f a
        a
