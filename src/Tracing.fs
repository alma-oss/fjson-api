namespace Lmc.Tracing.Extension.Giraffe

open System
open Microsoft.AspNetCore.Http
open Giraffe

open Lmc.Tracing
open Lmc.Tracing.Extension
open Lmc.Tracing.CustomTracingScope

type HttpScopedTrace(ctx: HttpContext) =
    inherit ScopedTrace(TraceIdentifier ctx.TraceIdentifier)

[<RequireQualifiedAccess>]
module internal HttpScopedTrace =
    let finish (httpScopedTrace: HttpScopedTrace) =
        httpScopedTrace.Finish()

[<RequireQualifiedAccess>]
module Trace =
    let internal teeResponseContext (f: HttpContext -> unit): HttpHandler =
        fun next ctx -> task {
            match! next ctx with
            | None -> return None
            | Some responseCtx ->
                f responseCtx
                return Some responseCtx
        }

    [<RequireQualifiedAccess>]
    module Http =
        open Microsoft.Extensions.Primitives

        let active ctx = (new HttpScopedTrace(ctx)).Trace

        let injectToResponse (ctx: HttpContext) = function
            | Inactive -> ()
            | trace when not ctx.Response.HasStarted ->
                Http.inject trace []
                |> List.iter (fun (key, value) ->
                    ctx.Response.Headers.Add(key, StringValues(value))
                )
            | _ -> ()

        let start name (ctx: HttpContext) =
            let trace =
                name
                |> Trace.ChildOf.continueOrStartActive (fun () -> ctx |> Http.extractFromContext |> Trace.ofContextOption)
                |> Trace.addTags [
                    "span.kind", "server"
                    "http.method", ctx.Request.Method
                    "http.url",
                        sprintf "%s://%s%s%s"
                            ctx.Request.Scheme
                            ctx.Request.Host.Value
                            ctx.Request.Path.Value
                            ctx.Request.QueryString.Value
                ]
                |> tee (injectToResponse ctx)

            let httpTrace = new HttpScopedTrace(ctx)
            httpTrace.Save(trace)

            httpTrace

        let finishTraceHandler: HttpHandler =
            teeResponseContext (fun ctx ->
                use httpScopedTrace = new HttpScopedTrace(ctx)

                httpScopedTrace.Trace
                |> Trace.addTags [
                    "http.status_code", string ctx.Response.StatusCode
                ]
                |> ignore
            )

    [<RequireQualifiedAccess>]
    module Active =
        let http = Http.active
