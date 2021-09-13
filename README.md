Json Api
========

Json Api types and basic implementations.

## Install

Add following into `paket.dependencies`
```
git ssh://git@bitbucket.lmc.cz:7999/archi/nuget-server.git master Packages: /nuget/
# LMC Nuget dependencies:
nuget Lmc.JsonApi
```

Add following into `paket.references`
```
Lmc.JsonApi
```

## Use

### Tracing with Felicity Json Api

`Trace.Http.finishTraceHandler` will be called after a jsonApi finish the HttpContext, it will finish current active trace and set a real `http.status_code` as a tag

```fs
let webApp =
    choose [
        Trace.Http.finishTraceHandler
            >=> jsonApi<JsonApiContext>
    ]
```

**Note**: Do not forget that you need to start an active trace as a `HttpScopedTrace`.

```fs
let post =
    define.Operation
        .Post(fun ctx parser ->
            asyncResult {
                let postOperationTrace = ctx.HttpContext |> Trace.Http.start "Post Operation"   // start current active trace
                use __ = "Create resource" |> Trace.ChildOf.start postOperationTrace.Trace      // trace create resource as a child of the active trace

                // ... create resource ...

                return resource
            }
            |> AsyncResult.teeError (fun e ->   // add resource error to the active trace
                ctx.HttpContext
                |> Trace.Http.active
                |> Trace.addError (TracedError.ofError e)
                |> ignore
            )
            |> parser.ForAsyncRes
        )
        .AfterCreate(fun ctx resource ->
            let postOperationTrace = Trace.Http.active ctx.HttpContext                  // retrieve current active trace
            use __ = "After Create" |> Trace.ChildOf.start postOperationTrace.Trace     // trace after persist operation as another child of the active trace

            // ... persist resource ...

            resource
        )
```

## Release
1. Increment version in `JsonApi.fsproj`
2. Update `CHANGELOG.md`
3. Commit new version and tag it
4. Run `$ fake build target release`
5. Go to `nuget-server` repo, run `faket build target copyAll` and push new versions

## Development
### Requirements
- [dotnet core](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial)
- [FAKE](https://fake.build/fake-gettingstarted.html)

### Build
```bash
fake build
```

### Watch
```bash
fake build target watch
```
