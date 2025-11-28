Json Api
========

[![NuGet](https://img.shields.io/nuget/v/Alma.JsonApi.svg)](https://www.nuget.org/packages/Alma.JsonApi)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Alma.JsonApi.svg)](https://www.nuget.org/packages/Alma.JsonApi)
[![Tests](https://github.com/alma-oss/fjson-api/actions/workflows/tests.yaml/badge.svg)](https://github.com/alma-oss/fjson-api/actions/workflows/tests.yaml)

Json Api types and basic implementations.

## Install

Add following into `paket.references`
```
Alma.JsonApi
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
                use _ = "Create resource" |> Trace.ChildOf.start postOperationTrace.Trace      // trace create resource as a child of the active trace

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
            use _ = "After Create" |> Trace.ChildOf.start postOperationTrace.Trace     // trace after persist operation as another child of the active trace

            // ... persist resource ...

            resource
        )
```

## Release
1. Increment version in `JsonApi.fsproj`
2. Update `CHANGELOG.md`
3. Commit new version and tag it

## Development
### Requirements
- [dotnet core](https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial)

### Build
```bash
./build.sh build
```

### Tests
```bash
./build.sh -t tests
```
