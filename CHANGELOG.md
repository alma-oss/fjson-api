# Changelog

<!-- There is always Unreleased section on the top. Subsections (Add, Changed, Fix, Removed) should be Add as needed. -->
## Unreleased
- [**BC**] Use net9.0

## 8.2.1 - 2024-06-10
- Inject headers to client as well

## 8.2.0 - 2024-06-07
- Add `Http` functions
    - `getWithHeaders`
    - `postWithHeaders`

## 8.1.0 - 2024-01-11
- Update dependencies

## 8.0.0 - 2024-01-09
- [**BC**] Use net8.0
- Fix package metadata

## 7.0.0 - 2023-09-11
- [**BC**] Use `Alma` namespace

## 6.0.0 - 2023-08-10
- [**BC**] Use net 7.0

## 5.1.0 - 2023-02-03
- Update dependencies
- Add `Path.id` function

## 5.0.0 - 2022-06-29
- Add `ResponseError` type and module
- Unify `JsonApiHttpError`
    - Add `JsonApiHttpError` type and module
    - [**BC**] Remove type and module for
        - `JsonApiHttpGetError`
        - `JsonApiHttpPostError`
- Internally use ASP.net core for handling both `Http.get` and `Http.post` instead of `FSharp.Data`
- Add `JsonApiErrorDto` functions
    - `JsonApiErrorDto.badRequest`
    - `JsonApiErrorDto.conflict`
- Add `HttpStatusCode` module
- [**BC**] Remove `Http.{Error}` match in favor of better ResponseError type and `HttpStatusCode.parseExn` function

## 4.5.0 - 2022-06-28
- Update dependencies
- Fix `Http.post` request body encoding

## 4.4.0 - 2022-06-24
- Add `Http.Conflict` case

## 4.3.0 - 2022-05-23
- Update dependencies

## 4.2.0 - 2022-05-23
- Finish current active span on starting a http context based span

## 4.1.0 - 2022-05-17
- Update dependencies

## 4.0.0 - 2022-05-16
- Update dependencies
    - [**BC**] Use `OpenTelemetry` tracing

## 3.4.0 - 2022-04-27
- Update dependencies

## 3.3.0 - 2022-02-28
- Update dependencies

## 3.2.0 - 2022-02-22
- Update dependencies

## 3.1.0 - 2022-01-28
- Add `JsonApiResource` module with a base types
    - `Type`
    - `Collection`

## 3.0.0 - 2022-01-05
- [**BC**] Use net6.0

## 2.6.0 - 2021-11-16
- Add `Http` module with `post` and `get` functions and common types and errors

## 2.5.0 - 2021-11-04
- Add `JsonApiErrorDto` type and module
- Add `JsonApiErrorResponseData` type and module

## 2.4.0 - 2021-11-04
- Add `Trace.Http.injectToResponse` function
- Add trace headers to the response on start trace automatically

## 2.3.0 - 2021-11-01
- Update dependencies

## 2.2.0 - 2021-09-13
- Update dependencies
- Add giraffe extension for tracing (`Lmc.Tracing.Extension.Giraffe`)

## 2.1.0 - 2020-12-11
- Allow to parse `JsonApiRequest`

## 2.0.0 - 2020-11-20
- Use .netcore 5.0

## 1.1.0 - 2020-11-20
- Update dependencies

## 1.0.0 - 2020-10-08
- Initial implementation
