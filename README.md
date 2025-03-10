# HttpClient Interception

A .NET Standard library for intercepting server-side HTTP dependencies.

[![NuGet version](https://img.shields.io/nuget/v/JustEat.HttpClientInterception?logo=nuget&label=NuGet&color=blue)](https://www.nuget.org/packages/JustEat.HttpClientInterception/)
[![Build status](https://github.com/justeattakeaway/httpclient-interception/actions/workflows/build.yml/badge.svg?branch=main&event=push)](https://github.com/justeattakeaway/httpclient-interception/actions/workflows/build.yml?query=branch%3Amain+event%3Apush)

[![codecov](https://codecov.io/gh/justeattakeaway/httpclient-interception/branch/main/graph/badge.svg)](https://codecov.io/gh/justeattakeaway/httpclient-interception)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/justeattakeaway/httpclient-interception/badge)](https://securityscorecards.dev/viewer/?uri=github.com/justeattakeaway/httpclient-interception)

## Introduction

This library provides functionality for intercepting HTTP requests made using the `HttpClient` class in code targeting .NET Standard 2.0 (and later), and .NET Framework 4.7.2.

The primary use-case is for providing stub responses for use in tests for applications, such as an ASP.NET Core application, to drive your functional test scenarios.

The library is based around an implementation of [`DelegatingHandler`](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler "DelegatingHandler documentation"), which can either be used directly as an implementation of `HttpMessageHandler`, or can be provided to instances of `HttpClient`. This also allows it to be registered via Dependency Injection to make it available for use in code under test without the application itself requiring any references to `JustEat.HttpClientInterception` or any custom abstractions of `HttpClient`.

This design means that no HTTP server needs to be hosted to proxy traffic to/from, so does not consume any additional system resources, such as needing to bind a port for HTTP traffic, making it lightweight to use.

### Installation

To install the library from [NuGet](https://www.nuget.org/packages/JustEat.HttpClientInterception/ "JustEat.HttpClientInterception on NuGet.org") using the .NET SDK run:

```text
dotnet add package JustEat.HttpClientInterception
```

### Basic Examples

#### Request Interception

##### Fluent API

Below is a minimal example of intercepting an HTTP GET request to an API for a JSON resource to return a custom response using the fluent API:

<!-- markdownlint-disable MD031 -->

<!-- snippet: minimal-example -->
<a id='snippet-minimal-example'></a>
```cs
// Arrange
var options = new HttpClientInterceptorOptions();
var builder = new HttpRequestInterceptionBuilder();

builder
    .Requests()
    .ForGet()
    .ForHttps()
    .ForHost("public.je-apis.com")
    .ForPath("terms")
    .Responds()
    .WithJsonContent(new { Id = 1, Link = "https://www.just-eat.co.uk/privacy-policy" })
    .RegisterWith(options);

using var client = options.CreateHttpClient();

// Act
// The value of json will be: {"Id":1, "Link":"https://www.just-eat.co.uk/privacy-policy"}
string json = await client.GetStringAsync("https://public.je-apis.com/terms", TestContext.Current.CancellationToken);
```
<sup><a href='/tests/HttpClientInterception.Tests/Examples.cs#L46-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-minimal-example' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`HttpRequestInterceptionBuilder` objects are mutable, so properties can be freely changed once a particular setup has been registered with an instance of `HttpClientInterceptorOptions` as the state is captured at the point of registration. This allows multiple responses and paths to be configured from a single `HttpRequestInterceptionBuilder` instance where multiple registrations against a common hostname.

##### _HTTP Bundle_ Files

HTTP requests to intercept can also be configured in an _"HTTP bundle"_ file, which can be used to store the HTTP requests to intercept and their corresponding responses as JSON.

This functionality is analogous to our [_Shock_](https://github.com/justeat/Shock "Shock") pod for iOS.

###### JSON

Below is an example bundle file, which can return content in formats such as a string, JSON and base64-encoded data.

The full JSON schema for HTTP bundle files can be found [here](https://raw.githubusercontent.com/justeattakeaway/httpclient-interception/main/src/HttpClientInterception/Bundles/http-request-bundle-schema.json "JSON Schema for HTTP request interception bundles for use with JustEat.HttpClientInterception.").

<!-- snippet: sample-bundle.json -->
<a id='snippet-sample-bundle.json'></a>
```json
{
  "$schema": "https://raw.githubusercontent.com/justeattakeaway/httpclient-interception/main/src/HttpClientInterception/Bundles/http-request-bundle-schema.json",
  "id": "my-bundle",
  "comment": "A bundle of HTTP requests",
  "items": [
    {
      "id": "home",
      "comment": "Returns the home page",
      "uri": "https://www.just-eat.co.uk",
      "contentString": "<html><head><title>Just Eat</title></head></html>"
    },
    {
      "id": "terms",
      "comment": "Returns the Ts & Cs",
      "uri": "https://public.je-apis.com/terms",
      "contentFormat": "json",
      "contentJson": {
        "Id": 1,
        "Link": "https://www.just-eat.co.uk/privacy-policy"
      }
    }
  ]
}
```
<sup><a href='/tests/HttpClientInterception.Tests/Bundles/sample-bundle.json#L1-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-sample-bundle.json' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

###### Code

```cs
// using JustEat.HttpClientInterception;

var options = new HttpClientInterceptorOptions().RegisterBundle("my-bundle.json");

var client = options.CreateHttpClient();

// The value of html will be "<html><head><title>Just Eat</title></head></html>"
var html = await client.GetStringAsync("https://www.just-eat.co.uk");

// The value of json will be "{\"Id\":1,\"Link\":\"https://www.just-eat.co.uk/privacy-policy\"}"
var json = await client.GetStringAsync("https://public.je-apis.com/terms");
```

Further examples of using HTTP bundles can be found in the [tests](https://github.com/justeattakeaway/httpclient-interception/blob/main/tests/HttpClientInterception.Tests/Bundles/BundleExtensionsTests.cs "BundleExtensionsTests.cs"), such as for changing the response code, the HTTP method, and matching to HTTP requests based on the request headers.

#### Fault Injection

Below is a minimal example of intercepting a request to inject an HTTP fault:

<!-- snippet: fault-injection -->
<a id='snippet-fault-injection'></a>
```cs
var options = new HttpClientInterceptorOptions();

var builder = new HttpRequestInterceptionBuilder()
    .Requests()
    .ForHost("public.je-apis.com")
    .WithStatus(HttpStatusCode.InternalServerError)
    .RegisterWith(options);

var client = options.CreateHttpClient();

// Throws an HttpRequestException
await Assert.ThrowsAsync<HttpRequestException>(
    () => client.GetStringAsync("http://public.je-apis.com", TestContext.Current.CancellationToken));
```
<sup><a href='/tests/HttpClientInterception.Tests/Examples.cs#L25-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-fault-injection' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### Registering Request Interception When Using IHttpClientFactory

If you are using [`IHttpClientFactory`](https://learn.microsoft.com/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests "Use IHttpClientFactory to implement resilient HTTP requests") to register `HttpClient` for Dependency Injection in a .NET application (or later), you can implement a custom [`IHttpMessageHandlerBuilderFilter`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.http.ihttpmessagehandlerbuilderfilter "IHttpMessageHandlerBuilderFilter Interface") to register during test setup, which makes an instance of `HttpClientInterceptorOptions` available to inject an HTTP handler.

A working example of this approach can be found in the [sample application](https://github.com/justeattakeaway/httpclient-interception/blob/main/samples/README.md "Sample application that uses JustEat.HttpClientInterception").

<!-- snippet: interception-filter -->
<a id='snippet-interception-filter'></a>
```cs
/// <summary>
/// A class that registers an intercepting HTTP message handler at the end of
/// the message handler pipeline when an <see cref="HttpClient"/> is created.
/// </summary>
public sealed class HttpClientInterceptionFilter(HttpClientInterceptorOptions options) : IHttpMessageHandlerBuilderFilter
{
    /// <inheritdoc/>
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return (builder) =>
        {
            // Run any actions the application has configured for itself
            next(builder);

            // Add the interceptor as the last message handler
            builder.AdditionalHandlers.Add(options.CreateHttpMessageHandler());
        };
    }
}
```
<sup><a href='/samples/SampleApp.Tests/HttpClientInterceptionFilter.cs#L9-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-interception-filter' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### Setting Up HttpClient for Dependency Injection Manually

Below is an example of setting up `IServiceCollection` to register `HttpClient` for Dependency Injection in a manner that allows tests to use `HttpClientInterceptorOptions` to intercept HTTP requests. Similar approaches can be used with other IoC containers.

You may wish to consider registering `HttpClient` as a singleton, rather than as transient, if you do not use properties such as `BaseAddress` on instances of `HttpClient`. This allows the same instance to be used throughout the application, which improves performance and resource utilisation under heavy server load. If using a singleton instance, ensure that you manage the lifetime of your message handlers appropriately so they are not disposed of incorrectly and update the registration for your `HttpClient` instance appropriately.

```csharp
services.AddTransient(
    (serviceProvider) =>
    {
        // Create a handler that makes actual HTTP calls
        HttpMessageHandler handler = new HttpClientHandler();

        // Have any delegating handlers been registered?
        var handlers = serviceProvider
            .GetServices<DelegatingHandler>()
            .ToList();

        if (handlers.Count > 0)
        {
            // Attach the initial handler to the first delegating handler
            DelegatingHandler previous = handlers.First();
            previous.InnerHandler = handler;

            // Chain any remaining handlers to each other
            foreach (DelegatingHandler next in handlers.Skip(1))
            {
                next.InnerHandler = previous;
                previous = next;
            }

            // Replace the initial handler with the last delegating handler
            handler = previous;
        }

        // Create the HttpClient using the inner HttpMessageHandler
        return new HttpClient(handler);
    });
```

Then in the test project register `HttpClientInterceptorOptions` to provide an implementation of `DelegatingHandler`. If using a singleton for `HttpClient` as described above, update the registration for the tests appropriately so that the message handler is shared.

```csharp
var options = new HttpClientInterceptorOptions();

var server = new WebHostBuilder()
    .UseStartup<Startup>()
    .ConfigureServices(
        (services) => services.AddTransient((_) => options.CreateHttpMessageHandler()))
    .Build();

server.Start();
```

### Further Examples

Further examples of using the library can be found by following the links below:

  1. [Example tests](https://github.com/justeattakeaway/httpclient-interception/blob/main/tests/HttpClientInterception.Tests/Examples.cs "Sample tests using JustEat.HttpClientInterception")
  1. [Sample application with tests](https://github.com/justeattakeaway/httpclient-interception/blob/main/samples/README.md "Sample application that uses JustEat.HttpClientInterception")
  1. This library's [own tests](https://github.com/justeattakeaway/httpclient-interception/blob/main/tests/HttpClientInterception.Tests/HttpClientInterceptorOptionsTests.cs "Tests for JustEat.HttpClientInterception itself")

### Benchmarks

Generated with the [Benchmarks project](https://github.com/justeattakeaway/httpclient-interception/blob/main/tests/HttpClientInterception.Benchmarks/InterceptionBenchmarks.cs "JustEat.HttpClientInterception benchmark code") using [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet "BenchmarkDotNet on GitHub.com") using commit [ef93cb5](https://github.com/justeattakeaway/httpclient-interception/commit/ef93cb5a54b518fe488de63be962ddf15644ef42 "Benchmark commit") on 04/02/2024.

<!-- markdownlint-disable MD058 -->

```text

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
12th Gen Intel Core i7-1270P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-IMNGZO : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Arguments=/p:UseArtifactsOutput=false

```
| Method                       | Mean       | Error     | StdDev     | Median     | Gen0   | Allocated |
|----------------------------- |-----------:|----------:|-----------:|-----------:|-------:|----------:|
| GetBytes                     |   1.708 μs | 0.0338 μs |  0.0898 μs |   1.698 μs | 0.3223 |   2.98 KB |
| GetHtml                      |   1.618 μs | 0.0324 μs |  0.0703 μs |   1.614 μs | 0.3319 |   3.06 KB |
| GetJsonDocument              |   1.942 μs | 0.0381 μs |  0.0604 μs |   1.921 μs | 0.3071 |   2.84 KB |
| GetJsonObject                |   2.460 μs | 0.0778 μs |  0.2195 μs |   2.413 μs | 0.3281 |   3.05 KB |
| GetJsonObjectSourceGenerator |   2.345 μs | 0.0421 μs |  0.1102 μs |   2.311 μs | 0.3281 |   3.05 KB |
| GetJsonObjectWithRefit       |   5.169 μs | 0.1025 μs |  0.2117 μs |   5.120 μs | 0.6714 |   6.35 KB |
| GetStream                    | 209.607 μs | 4.5593 μs | 13.3717 μs | 210.077 μs | 0.2441 |   3.16 KB |

<!-- markdownlint-enable MD058 -->

## Feedback

Any feedback or issues can be added to the issues for this project in [GitHub](https://github.com/justeattakeaway/httpclient-interception/issues "This project's issues on GitHub.com").

## Repository

The repository is hosted in [GitHub](https://github.com/justeattakeaway/httpclient-interception "This project on GitHub.com"): <https://github.com/justeattakeaway/httpclient-interception.git>

## Building and Testing

Compiling the library yourself requires Git and the [.NET SDK](https://www.microsoft.com/net/download/core "Download the .NET SDK") to be installed (version 7.0.100 or later).

To build and test the library locally from a terminal/command-line, run one of the following set of commands:

```powershell
git clone https://github.com/justeattakeaway/httpclient-interception.git
cd httpclient-interception
./build.ps1
```

## License

This project is licensed under the [Apache 2.0](https://www.apache.org/licenses/LICENSE-2.0.html "The Apache 2.0 license") license.
