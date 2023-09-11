module JsonApi.TestUtils

open System
open System.Security.Claims
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.AspNetCore.Authentication
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe

open Alma.ErrorHandling
open Alma.JsonApi

type Handlers = {
    Get: HttpHandler list
    Post: HttpHandler list
}

type WebServer =
    {
        Api: Api
        WebServer: Async<unit>
        Cancellation: CancellationTokenSource
    }

    member this.Run() =
        Async.Start(this.WebServer, this.Cancellation.Token)

    interface IDisposable with
        member this.Dispose() =
            this.Cancellation.Dispose()

[<RequireQualifiedAccess>]
module WebServer =
    /// See https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md#content-negotiation
    type private CustomNegotiationConfig (baseConfig : INegotiationConfig) =
        interface INegotiationConfig with
            member __.UnacceptableHandler =
                baseConfig.UnacceptableHandler

            member __.Rules =
                dict [
                    JsonApi.ContentType, (fun response -> json response >=> setHttpHeader "Content-Type" JsonApi.ContentType)
                ]

    let private configureApp webApp (app: IApplicationBuilder) =
        app
            .UseGiraffe webApp

    let private configureServices (services: IServiceCollection) =
        services
            .AddGiraffe()
            .AddSingleton<INegotiationConfig>(
                CustomNegotiationConfig(DefaultNegotiationConfig())
            )
        |> ignore

    let start (port: int) handlers =
        let webApp =
            choose [
                match handlers with
                | { Post = [] } -> ()
                | { Post = handlers } -> POST >=> choose handlers

                match handlers with
                | { Get = [] } -> ()
                | { Get = handlers } -> GET >=> choose handlers
            ]

        let webServer =
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(
                    fun webHostBuilder ->
                        webHostBuilder
                            .Configure(configureApp webApp)
                            .ConfigureServices(configureServices)
                            .UseUrls($"http://localhost:{port}")
                        |> ignore
                )
                .Build()
                .RunAsync()
            |> Async.AwaitTask

        {
            Api = Api $"http://localhost:{port}"
            WebServer = webServer
            Cancellation = new CancellationTokenSource()
        }
