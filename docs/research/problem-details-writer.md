# Research: single ProblemDetails writer in ASP.NET Core 10

**status: completed** — facts for the wayfinder research ticket "single ProblemDetails writer"
(child of the error-contract map). This note records *mechanisms* with a primary-source citation
per claim; it is not a design. Versions in scope: ASP.NET Core 10 (`net10.0`, packages 10.0.x),
FluentValidation 12.x, `Microsoft.AspNetCore.OpenApi` 10.0.x with `Asp.Versioning.OpenApi`.

Anything not verifiable from a primary source is listed under "Unverified" at the end.

## 1. The writer pipeline: `IProblemDetailsService`, `IProblemDetailsWriter`, `CustomizeProblemDetails`

- `AddProblemDetails` registers the default `IProblemDetailsService`. Once registered,
  `ExceptionHandlerMiddleware` (when no custom exception handler is defined),
  `StatusCodePagesMiddleware` (by default) and `DeveloperExceptionPageMiddleware` generate
  problem-details responses, except when the request `Accept` header contains no media type the
  registered writer supports.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#problem-details
- `IProblemDetailsService` exposes `WriteAsync(ProblemDetailsContext)` and
  `TryWriteAsync(ProblemDetailsContext)`, both "using the registered `IProblemDetailsWriter`
  services".
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.iproblemdetailsservice?view=aspnetcore-10.0
- Source: `ProblemDetailsService.TryWriteAsync` iterates the registered writers and the first
  whose `CanWrite(context)` returns true writes; `WriteAsync` throws
  `InvalidOperationException("Unable to find a registered IProblemDetailsWriter that can write to
  the given context.")` when none can.
  https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Extensions/src/ProblemDetailsService.cs
- `TryWriteAsync` was added in .NET 8 so callers can fall back when no writer can handle the
  request.
  https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/release-notes/aspnetcore-8.0.md#new-apis-in-problemdetails-to-support-more-resilient-integrations
- `ProblemDetailsOptions.CustomizeProblemDetails` customisations "are applied to all
  auto-generated problem details"; the docs example adds an extension (`nodeId`) that appears as
  a flat top-level JSON member.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#customizeproblemdetails-operation
- Source: the default writer (`DefaultProblemDetailsWriter`) — `CanWrite` returns true when there
  is no `Accept` header, otherwise only when `Accept` matches `application/json`,
  `application/problem+json` or a wildcard (`*/*`, `application/*`). `WriteAsync` applies
  `ProblemDetailsDefaults` (title/type for the status), sets `Extensions["traceId"]` (with a
  source comment that STJ does not apply the naming policy to `JsonExtensionData` keys), invokes
  `CustomizeProblemDetails`, then serialises with `Microsoft.AspNetCore.Http.Json.JsonOptions`
  and content type `application/problem+json`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Extensions/src/DefaultProblemDetailsWriter.cs
- The docs restate the writer's supported media types (`application/json`,
  `application/problem+json`, `*/*`, `application/*`) and that non-JSON types such as
  `application/xml` or `text/html` make `TryWriteAsync` return false.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0&tabs=controllers#iproblemdetailsservice-fallback
- A custom `IProblemDetailsWriter` must be registered before `AddControllers`,
  `AddControllersWithViews` or `AddMvc` so it precedes the MVC writer.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#custom-iproblemdetailswriter
- Source: MVC registers its own writer, `DefaultApiProblemDetailsWriter`, whose `CanWrite` is true
  only for endpoints carrying `ControllerAttribute` metadata. Its `WriteAsync` is a no-op when the
  endpoint has no `IApiBehaviorMetadata` (i.e. not `[ApiController]`) or when
  `ApiBehaviorOptions.SuppressMapClientErrors` is set; otherwise it re-creates the problem via
  `ProblemDetailsFactory.CreateProblemDetails(...)`, copies `Extensions`, and writes through the
  MVC output formatters with `application/problem+json` / `application/problem+xml`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/DefaultApiProblemDetailsWriter.cs

## 2. `IExceptionHandler`: `IProblemDetailsService.TryWriteAsync` vs `WriteAsJsonAsync`

- Source: `ExceptionHandlerMiddlewareImpl` throws at construction if neither an
  `ExceptionHandler` delegate nor `ExceptionHandlingPath` is configured *and* no
  `IProblemDetailsService` is registered. On an exception it sets the status to
  `StatusCodeSelector(ex)` when configured, else `BadHttpRequestException.StatusCode`, else 500;
  returns 499 when the request was aborted (`OperationCanceledException`/`IOException`); then
  iterates every registered `IExceptionHandler.TryHandleAsync` and stops at the first that returns
  true. **Only when no handler handled it and no delegate/path is configured** does the middleware
  itself call `_problemDetailsService.TryWriteAsync(new() { HttpContext, AdditionalMetadata =
  endpoint metadata, ProblemDetails = { Status = ... }, Exception = ... })`. A 404 produced by a
  handler is rethrown unless `AllowStatusCode404Response` is set.
  https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs
- Consequence (from the source above): an `IExceptionHandler` that writes with
  `HttpResponse.WriteAsJsonAsync(problemDetails)` never enters the writer pipeline, so
  `CustomizeProblemDetails` and any custom `IProblemDetailsWriter` do not run for it. An
  `IExceptionHandler` that writes via `IProblemDetailsService.TryWriteAsync`/`WriteAsync` does.
- The docs' lambda-handler example for exceptions injects `IProblemDetailsService` and calls
  `WriteAsync(new ProblemDetailsContext { HttpContext, ProblemDetails = {...} })`; the standard
  auto-generated 500 body is `{ "type": ".../rfc7231#section-6.6.1", "title": "An error occurred
  while processing your request.", "status": 500, "traceId": "..." }`.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#produce-a-problemdetails-payload-for-exceptions
- `ProblemDetailsContext` carries `HttpContext`, `ProblemDetails`, `AdditionalMetadata` and
  `Exception`; the middleware populates `Exception` so `CustomizeProblemDetails` can inspect it.
  https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs
- Middleware note from the docs: when an API controller has already written a body (e.g.
  `BadRequest()`), a later middleware call to `IProblemDetailsService.WriteAsync` does not
  overwrite it; use `ControllerBase.Problem(...)` inside the action to shape that response.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#problem-details-from-middleware

## 3. `UseStatusCodePages` and problem details

- Source: parameterless `UseStatusCodePages()` "checks for responses with status codes between
  400 and 599 that do not have a body and, when an `IProblemDetailsService` is available,
  attempts to generate a `ProblemDetails` response ... falls back to a plain text response that
  includes the status code".
  https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Diagnostics/src/StatusCodePage/StatusCodePagesExtensions.cs
- Source: the default `StatusCodePagesOptions.HandleAsync` resolves `IProblemDetailsService`
  from `RequestServices` and calls `TryWriteAsync(new() { HttpContext, ProblemDetails = { Status =
  statusCode } })`; when the service is missing or returns false it writes
  `Status Code: <code>; <reason phrase>` as `text/plain`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Diagnostics/src/StatusCodePage/StatusCodePagesOptions.cs
- Source: `StatusCodePagesMiddleware` runs *after* `_next`, and does nothing when the response
  has started, the status is outside 400-599, `ContentLength` is set, or `ContentType` is
  non-empty; endpoints with `ISkipStatusCodePagesMetadata` are skipped.
  https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/Diagnostics/src/StatusCodePage/StatusCodePagesMiddleware.cs
- So a status-only response (e.g. a middleware that sets 404 with no body, or an authentication
  challenge with no body) becomes a problem-details body via the same service, and therefore
  honours `CustomizeProblemDetails`.

## 4. MVC: `[ApiController]`, `ControllerBase.Problem`/`ValidationProblem`, `ProblemDetailsFactory`

- Controller error responses are configured through three things: the problem details service,
  `ProblemDetailsFactory`, and `ApiBehaviorOptions.ClientErrorMapping`. "MVC uses
  `ProblemDetailsFactory` to produce all instances of `ProblemDetails` and
  `ValidationProblemDetails`. This factory is used for: client error responses, validation failure
  error responses, `ControllerBase.Problem` and `ControllerBase.ValidationProblem`." A custom
  factory is registered with `AddTransient<ProblemDetailsFactory, ...>`.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0&tabs=controllers
- Source: `DefaultProblemDetailsFactory` takes `IOptions<ApiBehaviorOptions>` and an optional
  `IOptions<ProblemDetailsOptions>`; `ApplyProblemDetailsDefaults` sets `Status`, `Title`/`Type`
  from `ClientErrorMapping`, `Extensions["traceId"]`, and then invokes
  `ProblemDetailsOptions.CustomizeProblemDetails` with a `ProblemDetailsContext { HttpContext,
  ProblemDetails }`. **MVC-produced problem details therefore honour `CustomizeProblemDetails`
  without a custom factory**, but the context has no `Exception` or `AdditionalMetadata`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/DefaultProblemDetailsFactory.cs
- `[ApiController]` enables automatic HTTP 400 responses for model-validation failures via the
  `ModelStateInvalidFilter`; the default 400 body type is `ValidationProblemDetails` with an
  `errors` dictionary. The docs recommend `ValidationProblem` over `BadRequest` for consistency,
  and state "By default, `InvalidModelStateResponseFactory` uses `ProblemDetailsFactory` to
  create an instance of `ValidationProblemDetails`". `SuppressModelStateInvalidFilter` disables
  the filter.
  https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0
- Source: `ModelStateInvalidFilter` (order -2000) sets `context.Result =
  ApiBehaviorOptions.InvalidModelStateResponseFactory(context)` when `ModelState` is invalid.
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/ModelStateInvalidFilter.cs
- Source: the default `InvalidModelStateResponseFactory` (`ApiBehaviorOptionsSetup`) resolves
  `ProblemDetailsFactory` from `RequestServices`, calls
  `CreateValidationProblemDetails(httpContext, modelState)`, returns `BadRequestObjectResult`
  when the status is 400 (else `ObjectResult { StatusCode }`), and adds content types
  `application/problem+json` and `application/problem+xml`. The same setup fills
  `ClientErrorMapping` from `ProblemDetailsDefaults.Defaults`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/DependencyInjection/ApiBehaviorOptionsSetup.cs
- "MVC transforms an error result (status code 400 or higher) to a result with `ProblemDetails`"
  for `[ApiController]` actions (`NotFound()` becomes a problem-details body with `traceId`);
  `SuppressMapClientErrors = true` disables it.
  https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0
- Source: that transformation is `ClientErrorResultFilter` (an `IAlwaysRunResultFilter`, order
  -2000) which replaces any `IClientErrorActionResult` with status >= 400 via
  `IClientErrorFactory.GetClientError`; the default `ProblemDetailsClientErrorFactory` calls
  `ProblemDetailsFactory.CreateProblemDetails(httpContext, statusCode)` and returns an
  `ObjectResult` with `application/problem+json` / `application/problem+xml`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/ClientErrorResultFilter.cs
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/ProblemDetailsClientErrorFactory.cs
- Source: `ObjectResult.OnFormatting` synchronises `StatusCode` and `ProblemDetails.Status` when
  the value is a `ProblemDetails`; it does **not** run the value through `ProblemDetailsFactory`
  or `IProblemDetailsService`. A hand-built `new ObjectResult(new ProblemDetails {...})` or
  `BadRequest(new ProblemDetails {...})` therefore bypasses `CustomizeProblemDetails` (the
  `ClientErrorResultFilter` only rewrites `IClientErrorActionResult` results, which are the
  status-only `StatusCodeResult` family, not object results carrying a body).
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/ObjectResult.cs
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/ClientErrorResultFilter.cs
- Source: `Mvc.ValidationProblemDetails` derives from `HttpValidationProblemDetails`; its
  `ModelStateDictionary` constructor builds the `errors` dictionary keyed by the raw ModelState
  key (ordinal comparer) with the error messages (default message when empty).
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/ValidationProblemDetails.cs
- Endpoints used only as error handlers can be excluded from OpenAPI with
  `[ApiExplorerSettings(IgnoreApi = true)]`.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0&tabs=controllers

## 5. Rate limiter `OnRejected`

- `RateLimiterOptions.RejectionStatusCode` sets the status for rejected requests; `OnRejected`
  is a callback that receives the `OnRejectedContext` and can set headers (the docs example sets
  `Retry-After`) and write the body — the documented example writes plain text with
  `Response.WriteAsync`. **The rate-limiting docs show no `IProblemDetailsService`
  integration**; writing a problem-details body is the app's job inside `OnRejected`.
  https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0
- Source: `RateLimitingMiddleware` sets `Response.StatusCode = RejectionStatusCode` and then
  invokes `OnRejected` (a policy-level `OnRejected` wins over the global one for endpoint
  limiters); the middleware itself writes no body. Because it writes no body, a rejection with
  no `OnRejected` body would fall through to `StatusCodePages` (section 3) if that middleware is
  outside it in the pipeline; an `OnRejected` that writes via
  `IProblemDetailsService.TryWriteAsync` goes through the same writer as everything else.
  https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/RateLimiting/src/RateLimitingMiddleware.cs

## 6. `ProblemDetails.Extensions` serialisation and naming policy

- `ProblemDetails.Extensions` is `[JsonExtensionData] public IDictionary<string, object?>
  Extensions { get; set; }`; remarks: round-tripping is determined by the formatters.
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.problemdetails.extensions?view=aspnetcore-10.0
- STJ `[JsonExtensionData]`: on serialisation the dictionary's key/value pairs "become JSON
  properties just as they were" — the property holding them does not appear, so extensions are
  flat top-level members.
  https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/handle-overflow
- Source: the five RFC members carry `[JsonPropertyName("type"|"title"|"status"|"detail"|
  "instance")]`, `[JsonPropertyOrder(-5..-1)]` and `[JsonIgnore(WhenWritingNull)]`, so their
  names are fixed regardless of naming policy and they precede extension members; `Extensions`
  is created with `StringComparer.Ordinal`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Abstractions/src/ProblemDetails/ProblemDetails.cs
- Source: `HttpValidationProblemDetails.Errors` is `[JsonPropertyName("errors")]
  IDictionary<string, string[]>`, default title "One or more validation errors occurred.".
  https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Abstractions/src/ProblemDetails/HttpValidationProblemDetails.cs
- .NET 8 breaking change: the custom `ProblemDetails`/`ValidationProblemDetails` JSON converters
  were removed; "Developers must specify a `JsonNamingPolicy`" if they relied on the converters'
  naming behaviour.
  https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/breaking-changes/8/problemdetails-custom-converters.md
- For MVC, the `ProblemDetails`/`ValidationProblemDetails` response is always camelCase even when
  the app sets a PascalCase (`null`) property naming policy; `[ApiController]` validation uses the
  model property names unchanged as `errors` keys. To camelCase the keys, add
  `SystemTextJsonValidationMetadataProvider` (optionally with a `JsonNamingPolicy`) to
  `MvcOptions.ModelMetadataDetailsProviders`; `[JsonPropertyName]` on a model property sets its
  key.
  https://learn.microsoft.com/en-us/aspnet/core/web-api/advanced/formatting?view=aspnetcore-10.0#format-problemdetails-and-validationproblemdetails-responses
- STJ naming of dictionary keys (`errors` is a `Dictionary<string, string[]>`) is governed by
  `JsonSerializerOptions.DictionaryKeyPolicy` (serialisation only), not `PropertyNamingPolicy`;
  extension-data keys are written as-is (see the `DefaultProblemDetailsWriter` source comment in
  section 1 — it hard-codes the lowercase key `traceId` for that reason).
  https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties#use-a-naming-policy-for-dictionary-keys
  https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Extensions/src/DefaultProblemDetailsWriter.cs
- Two serializer option objects are in play: MVC formatters use
  `AddControllers().AddJsonOptions(...)` (`Microsoft.AspNetCore.Mvc.JsonOptions`); the default
  problem-details writer, minimal APIs and OpenAPI schema generation use
  `Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(...)`. MVC JSON options have no
  influence on OpenAPI.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0&tabs=controllers#mvc-json-options-and-global-json-options
  https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Extensions/src/DefaultProblemDetailsWriter.cs

## 7. FluentValidation failures to `ValidationProblemDetails`

- `ValidationResult.ToDictionary()` (FluentValidation 11.1+) converts the failures to
  `IDictionary<string, string[]>` keyed by property name; the docs show
  `Results.ValidationProblem(validationResult.ToDictionary())` for minimal APIs and iterating
  `error.PropertyName` / `error.ErrorMessage` for manual handling. ASP.NET auto-validation via
  the FluentValidation.AspNetCore package is no longer recommended.
  https://github.com/fluentvalidation/fluentvalidation/blob/main/docs/aspnet.md
- Each `ValidationFailure` also exposes `ErrorCode` (default is the validator name, e.g.
  `NotNullValidator`; overridable with `WithErrorCode`).
  https://github.com/fluentvalidation/fluentvalidation/blob/main/docs/error-codes.md
- `ValidationProblemDetails(IDictionary<string, string[]> errors)` and
  `HttpValidationProblemDetails(IDictionary<string, string[]> errors)` constructors accept that
  dictionary directly; `ProblemDetailsFactory.CreateValidationProblemDetails` accepts a
  `ModelStateDictionary` instead, so a FluentValidation result must either be copied into
  `ModelState` (`AddModelError`) to go through the factory, or built via the dictionary
  constructor and then passed through `IProblemDetailsService` to get `CustomizeProblemDetails`.
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/ValidationProblemDetails.cs
  https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Abstractions/src/ProblemDetails/HttpValidationProblemDetails.cs
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/DependencyInjection/ApiBehaviorOptionsSetup.cs
- Wolverine's `UseFluentValidation()` middleware runs validators before the handler and, on
  failure, throws `FluentValidation.ValidationException` carrying all failures; the throw
  behaviour is replaceable by registering `IFailureAction<T>`. (Wolverine also ships a separate
  WolverineFx.Http middleware that emits `ProblemDetails`, which does not apply to MVC
  controllers.)
  https://wolverinefx.net/guide/handlers/fluent-validation.html

## 8. OpenAPI: custom `ProblemDetails` schema with extensions and global 4xx/5xx responses

- Response metadata for controllers comes from `[ProducesResponseType]` /
  `[ProducesResponseType<T>]` (status, body type, content types), `[ProducesDefaultResponseType]`
  (the `default` response), and `[ProducesErrorResponseType]` (body type for 4xx responses that
  only complements a `[ProducesResponseType]` with a 4xx status). When not specified, "the schema
  for the response body of 4xx responses is inferred to be a problem details object" and 3xx/5xx
  bodies are unspecified. All of these attributes may be placed on the controller class to apply
  to every action.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0&tabs=controllers#describe-response-types
- Web API conventions (`[ApiConventionType]` on a controller or at assembly level,
  `[ApiConventionMethod]` on an action) are "a substitute for decorating individual actions with
  `[ProducesResponseType]`"; a convention is a static type whose methods carry
  `[ProducesResponseType]` / `[ProducesDefaultResponseType]` and are matched by name/parameter
  hints. Conventions do not compose (one per action), and the associated web API analyzers are
  deprecated in .NET 10 (`ASPDEPR007`).
  https://learn.microsoft.com/en-us/aspnet/core/web-api/advanced/conventions?view=aspnetcore-10.0
- Source: `ApiResponseTypeProvider` reads response metadata from every filter on the action that
  implements `IApiResponseMetadataProvider` (`ProducesResponseTypeAttribute` implements it),
  keyed by `FilterDescriptor.Scope` — so a `ProducesResponseTypeAttribute` added to
  `MvcOptions.Filters` (global scope) is picked up for every action, with action/controller
  attributes for the same status code taking precedence. Conventions are used only when the
  action has no significant metadata provider of its own. 4xx entries without a type receive the
  `[ProducesErrorResponseType]` type (`action.Properties[typeof(ProducesErrorResponseTypeAttribute)]`).
  https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.ApiExplorer/src/ApiResponseTypeProvider.cs
- Transformers: `AddOpenApi(options => { options.AddDocumentTransformer(...);
  options.AddOperationTransformer(...); options.AddSchemaTransformer(...); })`. Execution order
  is schema transformers, then operation transformers, then document transformers. The docs'
  operation-transformer example adds a `500` response to every operation.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0
- .NET 10: transformer contexts expose `GetOrCreateSchemaAsync(Type, ...)` which generates a
  schema "using the same logic as ASP.NET Core OpenAPI document generation", and the operation and
  schema contexts expose `Document` so a transformer can register the schema with
  `Document.AddComponent(...)` and reference it elsewhere (e.g. from a shared error response).
  https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/release-notes/aspnetcore-10/includes/OpenApiSchemasInTransformers.md
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0#support-for-generating-openapischemas-in-transformers
- Schema shape for extensions: generated schemas omit `additionalProperties` by default (implying
  `true`), matching STJ's tolerance of unknown members; a `Dictionary<string, T>` property yields
  `additionalProperties` with `T`'s schema. Declaring specific extension members
  (`traceId`, `code`, `errors`, ...) as named properties therefore requires either a dedicated
  C# type deriving from `ProblemDetails` with real properties, or a schema transformer that edits
  the generated `ProblemDetails` schema.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0&tabs=controllers#additionalproperties
- Schema generation uses the *global* `Microsoft.AspNetCore.Http.Json.JsonOptions` naming policy
  (camelCase by default) and honours `[JsonPropertyName]`; MVC's `AddJsonOptions` has no effect
  on the document.
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0&tabs=controllers#include-openapi-metadata-for-data-types
- Schema reference ids can be renamed with `CreateSchemaReferenceId`, and a schema transformer
  can override any generated metadata (descriptions, examples, `required`).
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0

## 9. What Wallow does today (inventory, read from the tree)

Paths relative to the repo root; all writers are in-process MVC controllers, so sections 1, 3
and 4 apply directly.

- `api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs` — `AddProblemDetails(options =>
  options.CustomizeProblemDetails = ...)` adds `api`/`version` extensions;
  `AddExceptionHandler<GlobalExceptionHandler>()`; `AddWallowRateLimiting` sets
  `RejectionStatusCode = 429` and an `OnRejected` that builds a `ProblemDetails` and writes it
  with `Response.WriteAsJsonAsync(..., contentType: "application/problem+json")` — bypasses the
  service, so `CustomizeProblemDetails` does not run for 429s.
- `api/src/Wallow.Api/Middleware/GlobalExceptionHandler.cs` — `IExceptionHandler` that maps
  domain/FluentValidation/auth exceptions to a hand-built `ProblemDetails` (with
  `Extensions["errors"]` as an array of `{field, message}` objects for `ValidationException`,
  `traceId`, `code`, `exception` in Development) and writes it with `WriteAsJsonAsync`, returning
  true — bypasses the service (section 2), and its validation shape differs from the
  `errors: { field: [messages] }` dictionary that `ValidationProblemDetails` emits (section 6).
- `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/AuthProblemResponse.cs`
  and `api/src/Wallow.Api/Middleware/SetupMiddleware.cs` — call
  `IProblemDetailsService.TryWriteAsync` and fall back to `WriteAsJsonAsync` on false; these do
  go through the writer.
- `api/src/Shared/Wallow.Shared.Api/Extensions/ResultExtensions.cs` — `ToErrorResult` returns
  `new ObjectResult(problemDetails) { StatusCode }` with hand-built title/type and a `code`
  extension — not produced by `ProblemDetailsFactory`, so `CustomizeProblemDetails` does not run
  (section 4, `ObjectResult.OnFormatting`).
- Controllers: 30 `[ApiController]` controllers; some use `Problem(...)`,
  `ValidationProblem(ModelState)` and `NotFound()` (factory path, customised), while
  `ApiKeysController` uses `BadRequest(new ProblemDetails {...})` and the Identity
  `UsersController` returns `BadRequest(new { succeeded = false, error = ... })` anonymous
  objects — neither is a problem-details body produced by the factory.
- `api/src/Wallow.Api/Program.cs` — `AddControllersWithViews()` with no
  `ConfigureApiBehaviorOptions` and no `AddJsonOptions`; pipeline uses parameterless
  `UseExceptionHandler()` and `UseStatusCodePages()`, a middleware that sets 404 for unmatched
  endpoints and relies on StatusCodePages for the body, `UseRateLimiter()` outside Development,
  and `AddOpenApi("v1")` + `Asp.Versioning` `AddOpenApi(options => ConfigureVersionedOpenApiDocument(...))`
  whose document/operation transformers are registered on `options.Document`; there is no
  schema transformer and no global `ProducesResponseType`/`ProducesErrorResponseType`.
- Wolverine is configured with `UseFluentValidation(RegistrationBehavior.ExplicitRegistration)`,
  so validation failures reach the API as `FluentValidation.ValidationException` (section 7).

## Unverified

- `ApiBehaviorOptionsSetup` was fetched from `src/Mvc/Mvc.Core/src/DependencyInjection/`; the
  first path tried (`Infrastructure/`) returned 404. The content cited in section 4 is from the
  successful fetch.
- No official document states that the rate limiter integrates with `IProblemDetailsService`; the
  claim in section 5 that `OnRejected` must do it itself is inferred from the docs example and the
  middleware source, not from an explicit statement.
- The claim in section 4 that `BadRequest(new ProblemDetails {...})` bypasses the factory rests on
  `ObjectResult.OnFormatting` and `ClientErrorResultFilter` source; the MVC docs do not state it
  in those words. `BadRequestObjectResult` is an `ObjectResult`, and whether it implements
  `IClientErrorActionResult` was not checked in source.
- Whether `Microsoft.AspNetCore.OpenApi` emits `additionalProperties` for `ProblemDetails`'
  `[JsonExtensionData]` member specifically was not verified from the generator source; the
  general `additionalProperties` rule cited in section 8 is the documented behaviour.
