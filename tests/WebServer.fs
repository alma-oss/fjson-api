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

open Lmc.ErrorHandling

type Handlers = {
    Get: HttpHandler list
    Post: HttpHandler list
}

[<RequireQualifiedAccess>]
module WebServer =
    let private configureApp webApp (app: IApplicationBuilder) =
        app
            .UseGiraffe webApp

    let private configureServices (services: IServiceCollection) =
        services
            .AddGiraffe()
        |> ignore

    let start port handlers =
        let webApp =
            choose [
                match handlers with
                | { Post = [] } -> ()
                | { Post = handlers } -> POST >=> choose handlers

                match handlers with
                | { Get = [] } -> ()
                | { Get = handlers } -> GET >=> choose handlers
            ]

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
