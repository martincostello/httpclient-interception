﻿// Copyright (c) Just Eat, 2017. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JustEat.HttpClientInterception.GitHub;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;
using Refit;

namespace JustEat.HttpClientInterception;

/// <summary>
/// This class contains tests which provide example scenarios for using the library.
/// </summary>
public static partial class Examples
{
    [Fact]
    public static async Task Fault_Injection()
    {
        // begin-snippet: fault-injection
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

        // end-snippet
    }

    [Fact]
    public static async Task Intercept_Http_Get_For_Json_Object()
    {
        // begin-snippet: minimal-example

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

        // end-snippet

        // Assert
        var content = JObject.Parse(json);
        content.Value<int>("Id").ShouldBe(1);
        content.Value<string>("Link").ShouldBe("https://www.just-eat.co.uk/privacy-policy");
    }

    [Fact]
    public static async Task Intercept_Http_Get_For_Html_String()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForHost("www.google.co.uk")
            .ForPath("search")
            .ForQuery("q=Just+Eat")
            .WithMediaType("text/html")
            .WithContent(@"<!DOCTYPE html><html dir=""ltr"" lang=""en""><head><title>Just Eat</title></head></html>");

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();

        // Act
        string html = await client.GetStringAsync("http://www.google.co.uk/search?q=Just+Eat", TestContext.Current.CancellationToken);

        // Assert
        html.ShouldContain("Just Eat");
    }

    [Fact]
    public static async Task Intercept_Http_Get_For_Raw_Bytes()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForHttps()
            .ForHost("files.domain.com")
            .ForPath("setup.exe")
            .WithMediaType("application/octet-stream")
            .WithContent(() => [0, 1, 2, 3, 4]);

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();

        // Act
        byte[] content = await client.GetByteArrayAsync("https://files.domain.com/setup.exe", TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public static async Task Intercept_Http_Post_For_Json_String()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForPost()
            .ForHttps()
            .ForHost("public.je-apis.com")
            .ForPath("consumer")
            .WithStatus(HttpStatusCode.Created)
            .WithContent(@"{ ""id"": 123 }");

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();
        using var body = new StringContent(@"{ ""FirstName"": ""John"" }");

        // Act
        using var response = await client.PostAsync("https://public.je-apis.com/consumer", body, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var content = JObject.Parse(json);
        content.Value<int>("id").ShouldBe(123);
    }

    [Fact]
    public static async Task Intercept_Http_Get_For_Json_Object_Based_On_Request_Headers()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForGet()
            .ForHttps()
            .ForHost("public.je-apis.com")
            .ForPath("terms")
            .ForRequestHeader("Accept-Tenant", "uk")
            .WithJsonContent(new { Id = 1, Link = "https://www.just-eat.co.uk/privacy-policy" });

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();
        client.DefaultRequestHeaders.Add("Accept-Tenant", "uk");

        // Act
        string json = await client.GetStringAsync("https://public.je-apis.com/terms", TestContext.Current.CancellationToken);

        // Assert
        var content = JObject.Parse(json);
        content.Value<int>("Id").ShouldBe(1);
        content.Value<string>("Link").ShouldBe("https://www.just-eat.co.uk/privacy-policy");
    }

    [Fact]
    public static async Task Conditionally_Intercept_Http_Post_For_Json_Object()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForPost()
            .ForHttps()
            .ForHost("public.je-apis.com")
            .ForPath("consumer")
            .ForContent(
                async (requestContent) =>
                {
                    string requestBody = await requestContent.ReadAsStringAsync(TestContext.Current.CancellationToken);

                    var body = JObject.Parse(requestBody);

                    return body.Value<string>("FirstName") == "John";
                })
            .Responds()
            .WithStatus(HttpStatusCode.Created)
            .WithContent(@"{ ""id"": 123 }");

        var options = new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .Register(builder);

        using var client = options.CreateHttpClient();
        using var body = new StringContent(@"{ ""FirstName"": ""John"" }");

        // Act
        using var response = await client.PostAsync("https://public.je-apis.com/consumer", body, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var content = JObject.Parse(json);
        content.Value<int>("id").ShouldBe(123);
    }

    [Fact]
    public static async Task Intercept_Http_Put_For_Json_String()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForPut()
            .ForHttps()
            .ForHost("public.je-apis.com")
            .ForPath("baskets/123/user")
            .WithStatus(HttpStatusCode.Created)
            .WithContent(@"{ ""id"": 123 }");

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();
        using var body = new StringContent(@"{ ""User"": { ""DisplayName"": ""John"" } }");

        // Act
        using var response = await client.PutAsync("https://public.je-apis.com/baskets/123/user", body, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var content = JObject.Parse(json);
        content.Value<int>("id").ShouldBe(123);
    }

    [Fact]
    public static async Task Intercept_Http_Delete()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForDelete()
            .ForHttps()
            .ForHost("public.je-apis.com")
            .ForPath("baskets/123/orderitems/456")
            .WithStatus(HttpStatusCode.NoContent);

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();

        // Act
        using var response = await client.DeleteAsync("https://public.je-apis.com/baskets/123/orderitems/456", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public static async Task Intercept_Custom_Http_Method()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForMethod(new HttpMethod("custom"))
            .ForHost("custom.domain.com")
            .ForQuery("length=2")
            .WithContent(() => [0, 1]);

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();
        using var message = new HttpRequestMessage(new HttpMethod("custom"), "http://custom.domain.com?length=2");

        // Act
        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);
        byte[] content = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBe([0, 1]);
    }

    [Fact]
    public static void Intercept_Custom_Synchronous_Http_Method()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForMethod(new HttpMethod("custom"))
            .ForHost("custom.domain.com")
            .ForQuery("length=2")
            .WithContent(() => [0, 1]);

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();
        using var message = new HttpRequestMessage(new HttpMethod("custom"), "http://custom.domain.com?length=2");

        // Act
        using var response = client.Send(message, TestContext.Current.CancellationToken);
        using var responseStream = response.Content.ReadAsStream(TestContext.Current.CancellationToken);
        using var responseBuffer = new MemoryStream();
        responseStream.CopyTo(responseBuffer);
        byte[] content = responseBuffer.ToArray();

        // Assert
        content.ShouldBe([0, 1]);
    }

    [Fact]
    public static async Task Inject_Fault_For_Http_Get()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForHost("www.google.co.uk")
            .WithStatus(HttpStatusCode.InternalServerError);

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();

        // Act and Assert
        await Should.ThrowAsync<HttpRequestException>(() => client.GetStringAsync("http://www.google.co.uk", TestContext.Current.CancellationToken));
    }

    [Fact]
    public static async Task Inject_Latency_For_Http_Get()
    {
        // Arrange
        var latency = TimeSpan.FromMilliseconds(50);

        var builder = new HttpRequestInterceptionBuilder()
            .ForHost("www.google.co.uk")
            .WithInterceptionCallback((_) => Task.Delay(latency, TestContext.Current.CancellationToken));

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        var stopwatch = new Stopwatch();

        using (var client = options.CreateHttpClient())
        {
            stopwatch.Start();

            // Act
            await client.GetStringAsync("http://www.google.co.uk", TestContext.Current.CancellationToken);

            stopwatch.Stop();
        }

        // Assert
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(latency);
    }

    [Fact]
    public static async Task Intercept_Http_Get_To_Stream_Content_From_Disk()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForHost("xunit.github.io")
            .ForPath("settings.json")
            .WithContentStream(() => File.OpenRead("xunit.runner.json"));

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var client = options.CreateHttpClient();

        // Act
        string json = await client.GetStringAsync("http://xunit.github.io/settings.json", TestContext.Current.CancellationToken);

        // Assert
        json.ShouldNotBeNullOrWhiteSpace();

        var config = JObject.Parse(json);
        config.Value<string>("methodDisplay").ShouldBe("method");
    }

    [Fact]
    public static async Task Use_Scopes_To_Change_And_Then_Restore_Behavior()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForHost("public.je-apis.com")
            .WithJsonContent(new { value = 1 });

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        string json1;
        string json2;
        string json3;

        // Act
        using (var client = options.CreateHttpClient())
        {
            json1 = await client.GetStringAsync("http://public.je-apis.com", TestContext.Current.CancellationToken);

            using (options.BeginScope())
            {
                options.Register(builder.WithJsonContent(new { value = 2 }));

                json2 = await client.GetStringAsync("http://public.je-apis.com", TestContext.Current.CancellationToken);
            }

            json3 = await client.GetStringAsync("http://public.je-apis.com", TestContext.Current.CancellationToken);
        }

        // Assert
        json1.ShouldNotBe(json2);
        json1.ShouldBe(json3);
    }

    [Fact]
    public static async Task Use_With_Refit()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .ForHttps()
            .ForHost("api.github.com")
            .ForPath("orgs/justeattakeaway")
            .WithJsonContent(new { id = 1516790, login = "justeattakeaway", url = "https://api.github.com/orgs/justeattakeaway" });

        var options = new HttpClientInterceptorOptions().Register(builder);
        var service = RestService.For<IGitHub>(options.CreateHttpClient("https://api.github.com"));

        // Act
        Organization actual = await service.GetOrganizationAsync("justeattakeaway");

        // Assert
        actual.ShouldNotBeNull();
        actual.Id.ShouldBe(1516790);
        actual.Login.ShouldBe("justeattakeaway");
        actual.Url.ShouldBe("https://api.github.com/orgs/justeattakeaway");
    }

    [Fact]
    public static async Task Use_Multiple_Registrations()
    {
        // Arrange
        var justEat = new HttpRequestInterceptionBuilder()
            .ForHttps()
            .ForHost("api.github.com")
            .ForPath("orgs/justeattakeaway")
            .WithJsonContent(new { id = 1516790, login = "justeattakeaway", url = "https://api.github.com/orgs/justeattakeaway" });

        var dotnet = new HttpRequestInterceptionBuilder()
            .ForHttps()
            .ForHost("api.github.com")
            .ForPath("orgs/dotnet")
            .WithJsonContent(new { id = 9141961, login = "dotnet", url = "https://api.github.com/orgs/dotnet" });

        var options = new HttpClientInterceptorOptions()
            .Register(justEat, dotnet);

        var service = RestService.For<IGitHub>(options.CreateHttpClient("https://api.github.com"));

        // Act
        Organization justEatOrg = await service.GetOrganizationAsync("justeattakeaway");
        Organization dotnetOrg = await service.GetOrganizationAsync("dotnet");

        // Assert
        justEatOrg.ShouldNotBeNull();
        justEatOrg.Id.ShouldBe(1516790);
        justEatOrg.Login.ShouldBe("justeattakeaway");
        justEatOrg.Url.ShouldBe("https://api.github.com/orgs/justeattakeaway");

        // Assert
        dotnetOrg.ShouldNotBeNull();
        dotnetOrg.Id.ShouldBe(9141961);
        dotnetOrg.Login.ShouldBe("dotnet");
        dotnetOrg.Url.ShouldBe("https://api.github.com/orgs/dotnet");
    }

    [Fact]
    public static async Task Use_The_Same_Builder_For_Multiple_Registrations_On_The_Same_Host()
    {
        // Arrange
        var options = new HttpClientInterceptorOptions();

        // Configure a response for https://api.github.com/orgs/justeattakeaway
        var builder = new HttpRequestInterceptionBuilder()
            .ForHttps()
            .ForHost("api.github.com")
            .ForPath("orgs/justeattakeaway")
            .WithJsonContent(new { id = 1516790, login = "justeattakeaway", url = "https://api.github.com/orgs/justeattakeaway" });

        options.Register(builder);

        // Update the same builder to configure a response for https://api.github.com/orgs/dotnet
        builder.ForPath("orgs/dotnet")
                .WithJsonContent(new { id = 9141961, login = "dotnet", url = "https://api.github.com/orgs/dotnet" });

        options.Register(builder);

        var service = RestService.For<IGitHub>(options.CreateHttpClient("https://api.github.com"));

        // Act
        Organization justEatOrg = await service.GetOrganizationAsync("justeattakeaway");
        Organization dotnetOrg = await service.GetOrganizationAsync("dotnet");

        // Assert
        justEatOrg.ShouldNotBeNull();
        justEatOrg.Id.ShouldBe(1516790);
        justEatOrg.Login.ShouldBe("justeattakeaway");
        justEatOrg.Url.ShouldBe("https://api.github.com/orgs/justeattakeaway");

        // Assert
        dotnetOrg.ShouldNotBeNull();
        dotnetOrg.Id.ShouldBe(9141961);
        dotnetOrg.Login.ShouldBe("dotnet");
        dotnetOrg.Url.ShouldBe("https://api.github.com/orgs/dotnet");
    }

    [Fact]
    public static async Task Match_Any_Host_Name()
    {
        // Arrange
        string expected = @"{""id"":12}";
        string actual;

        var builder = new HttpRequestInterceptionBuilder()
            .ForHttp()
            .ForAnyHost()
            .ForPath("orders")
            .ForQuery("id=12")
            .WithStatus(HttpStatusCode.OK)
            .WithContent(expected);

        var options = new HttpClientInterceptorOptions().Register(builder);

        using (var client = options.CreateHttpClient())
        {
            // Act
            actual = await client.GetStringAsync("http://myhost.net/orders?id=12", TestContext.Current.CancellationToken);
        }

        // Assert
        actual.ShouldBe(expected);

        using (var client = options.CreateHttpClient())
        {
            // Act
            actual = await client.GetStringAsync("http://myotherhost.net/orders?id=12", TestContext.Current.CancellationToken);
        }

        // Assert
        actual.ShouldBe(expected);
    }

    [Fact]
    public static async Task Use_Default_Response_For_Unmatched_Requests()
    {
        // Arrange
        var options = new HttpClientInterceptorOptions()
        {
            OnMissingRegistration = (request) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
        };

        using var client = options.CreateHttpClient();

        // Act
        using var response = await client.GetAsync("https://google.com/", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public static async Task Use_Custom_Request_Matching()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .Requests().For((request) => request.RequestUri.Host == "google.com")
            .Responds().WithContent(@"<!DOCTYPE html><html dir=""ltr"" lang=""en""><head><title>Google Search</title></head></html>");

        var options = new HttpClientInterceptorOptions().Register(builder);

        using var client = options.CreateHttpClient();

        // Act and Assert
        (await client.GetStringAsync("https://google.com/", TestContext.Current.CancellationToken)).ShouldContain("Google Search");
        (await client.GetStringAsync("https://google.com/search", TestContext.Current.CancellationToken)).ShouldContain("Google Search");
        (await client.GetStringAsync("https://google.com/search?q=foo", TestContext.Current.CancellationToken)).ShouldContain("Google Search");
    }

    [Fact]
    public static async Task Use_Custom_Request_Matching_With_Priorities()
    {
        // Arrange
        var options = new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests().For((request) => request.RequestUri.Host == "google.com").HavingPriority(1)
            .Responds().WithContent(@"First")
            .RegisterWith(options)
            .Requests().For((request) => request.RequestUri.Host.Contains("google", StringComparison.OrdinalIgnoreCase)).HavingPriority(2)
            .Responds().WithContent(@"Second")
            .RegisterWith(options)
            .Requests().For((request) => request.RequestUri.PathAndQuery.Contains("html", StringComparison.OrdinalIgnoreCase)).HavingPriority(3)
            .Responds().WithContent(@"Third")
            .RegisterWith(options)
            .Requests().For((request) => true).HavingPriority(null)
            .Responds().WithContent(@"Fourth")
            .RegisterWith(options);

        using var client = options.CreateHttpClient();

        // Act and Assert
        (await client.GetStringAsync("https://google.com/", TestContext.Current.CancellationToken)).ShouldBe("First");
        (await client.GetStringAsync("https://google.co.uk", TestContext.Current.CancellationToken)).ShouldContain("Second");
        (await client.GetStringAsync("https://example.org/index.html", TestContext.Current.CancellationToken)).ShouldContain("Third");
        (await client.GetStringAsync("https://www.just-eat.co.uk/", TestContext.Current.CancellationToken)).ShouldContain("Fourth");
    }

    [Fact]
    public static async Task Intercept_Http_Requests_Registered_Using_A_Bundle_File()
    {
        // Arrange
        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleAsync("example-bundle.json", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        string content;

        using (var client = options.CreateHttpClient())
        {
            content = await client.GetStringAsync("https://www.just-eat.co.uk/", TestContext.Current.CancellationToken);
        }

        // Assert
        content.ShouldBe("<html><head><title>Just Eat</title></head></html>");

        // Act
        using (var client = options.CreateHttpClient())
        {
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.DefaultRequestHeaders.Add("Authorization", "bearer my-token");
            client.DefaultRequestHeaders.Add("User-Agent", "My-App/1.0.0");

            content = await client.GetStringAsync("https://api.github.com/orgs/justeattakeaway", TestContext.Current.CancellationToken);
        }

        // Assert
        var organization = JObject.Parse(content);
        organization.Value<int>("id").ShouldBe(1516790);
        organization.Value<string>("login").ShouldBe("justeattakeaway");
        organization.Value<string>("url").ShouldBe("https://api.github.com/orgs/justeattakeaway");
    }

    [Fact]
    public static async Task Intercept_Http_Get_For_Json_Object_Using_System_Text_Json()
    {
        // Arrange
        var options = new HttpClientInterceptorOptions();
        var builder = new HttpRequestInterceptionBuilder();

        builder.Requests().ForGet().ForHttps().ForHost("public.je-apis.com").ForPath("terms")
                .Responds().WithJsonContent(new { Id = 1, Link = "https://www.just-eat.co.uk/privacy-policy" })
                .RegisterWith(options);

        using var client = options.CreateHttpClient();

        // Act
        using var utf8Json = await client.GetStreamAsync("https://public.je-apis.com/terms", TestContext.Current.CancellationToken);

        // Assert
        using var content = await JsonDocument.ParseAsync(utf8Json, cancellationToken: TestContext.Current.CancellationToken);
        content.RootElement.GetProperty("Id").GetInt32().ShouldBe(1);
        content.RootElement.GetProperty("Link").GetString().ShouldBe("https://www.just-eat.co.uk/privacy-policy");
    }

    [Fact]
    public static async Task Inject_Latency_For_Http_Get_With_Cancellation()
    {
        // Arrange
        var latency = TimeSpan.FromMilliseconds(50);

        var builder = new HttpRequestInterceptionBuilder()
            .ForHost("www.google.co.uk")
            .WithInterceptionCallback(async (_, token) =>
            {
                try
                {
                    await Task.Delay(latency, token);
                }
                catch (TaskCanceledException)
                {
                    // Ignored
                }
                finally
                {
                    // Assert
                    token.IsCancellationRequested.ShouldBeTrue();
                }
            });

        var options = new HttpClientInterceptorOptions()
            .Register(builder);

        using var cts = new CancellationTokenSource(TimeSpan.Zero);

        using var client = options.CreateHttpClient();

        // Act
        await Should.ThrowAsync<TaskCanceledException>(
            () => client.GetAsync("http://www.google.co.uk", cts.Token));

        // Assert
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public static async Task Simulate_Http_Rate_Limiting()
    {
        // Arrange
        var options = new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration();

        // Keep track of how many HTTP requests to GitHub have been made
        int requestCount = 0;

        static bool IsHttpGetForJustEatGitHubOrg(HttpRequestMessage request)
        {
            return
                request.Method.Equals(HttpMethod.Get) &&
                request.RequestUri == new Uri("https://api.github.com/orgs/justeattakeaway");
        }

        void IncrementRequestCount(HttpRequestMessage message) => requestCount++;

        // Register an HTTP 429 error for the first three requests.
        var builder1 = new HttpRequestInterceptionBuilder()
            .ForAll([IsHttpGetForJustEatGitHubOrg, _ => requestCount < 2])
            .WithInterceptionCallback(IncrementRequestCount)
            .Responds()
            .WithStatus(HttpStatusCode.TooManyRequests)
            .WithSystemTextJsonContent(new { message = "Too many requests" })
            .RegisterWith(options);

        // Register another request for an HTTP 200 for all subsequent requests.
        var builder2 = new HttpRequestInterceptionBuilder()
            .ForAll([IsHttpGetForJustEatGitHubOrg, _ => requestCount >= 2])
            .WithInterceptionCallback(IncrementRequestCount)
            .Responds()
            .WithStatus(HttpStatusCode.OK)
            .WithSystemTextJsonContent(new { id = 1516790, login = "justeattakeaway", url = "https://api.github.com/orgs/justeattakeaway" })
            .RegisterWith(options);

        var service = RestService.For<IGitHub>(options.CreateHttpClient("https://api.github.com"));

        // Configure a Polly resilience pipeline that will retry an HTTP request up to three
        // times if the HTTP request fails due to an HTTP 429 response from the server.
        int retryCount = 3;
        var context = ResilienceContextPool.Shared.Get(TestContext.Current.CancellationToken);
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions()
            {
                ShouldHandle = new PredicateBuilder().Handle<ApiException>((ex) => ex.StatusCode == HttpStatusCode.TooManyRequests),
                MaxRetryAttempts = 3,
            })
            .Build();

        // Act
        Organization actual = await pipeline.ExecuteAsync(async (_) => await service.GetOrganizationAsync("justeattakeaway"), context);

        // Assert
        actual.ShouldNotBeNull();
        actual.Id.ShouldBe(1516790);
        actual.Login.ShouldBe("justeattakeaway");
        actual.Url.ShouldBe("https://api.github.com/orgs/justeattakeaway");

        // Verify that the expected number of attempts were made
        requestCount.ShouldBe(retryCount);
    }

    [Fact]
    public static async Task Dynamically_Compute_Http_Headers()
    {
        // Arrange
        int contentHeadersCounter = 0;
        int requestHeadersCounter = 0;
        int responseHeadersCounter = 0;

        var builder = new HttpRequestInterceptionBuilder()
            .ForHost("service.local")
            .ForPath("resource")
            .ForRequestHeaders(() =>
            {
                return new Dictionary<string, ICollection<string>>()
                {
                    ["x-sequence"] = [(++requestHeadersCounter).ToString(CultureInfo.InvariantCulture)],
                };
            })
            .WithContentHeaders(() =>
            {
                return new Dictionary<string, ICollection<string>>()
                {
                    ["content-type"] = ["application/json; v=" + (++contentHeadersCounter).ToString(CultureInfo.InvariantCulture)],
                };
            })
            .WithResponseHeaders(() =>
            {
                return new Dictionary<string, ICollection<string>>()
                {
                    ["x-count"] = [(++responseHeadersCounter).ToString(CultureInfo.InvariantCulture)],
                };
            });

        var options = new HttpClientInterceptorOptions()
            .Register(builder)
            .ThrowsOnMissingRegistration();

        var method = HttpMethod.Get;
        string requestUri = "http://service.local/resource";

        using var client = options.CreateHttpClient();

        // Act
        using var request1 = new HttpRequestMessage(method, requestUri);
        request1.Headers.Add("x-sequence", "1");
        using var response1 = await client.SendAsync(request1, TestContext.Current.CancellationToken);

        // Assert
        response1.Headers.TryGetValues("x-count", out var values).ShouldBeTrue();
        values.ShouldNotBeNull();
        values.ShouldBe(["1"]);

        response1.Content.Headers.TryGetValues("content-type", out values).ShouldBeTrue();
        values.ShouldNotBeNull();
        values.ShouldBe(["application/json; v=1"]);

        // Act
        using var request2 = new HttpRequestMessage(method, requestUri);
        request2.Headers.Add("x-sequence", "2");
        using var response2 = await client.SendAsync(request2, TestContext.Current.CancellationToken);

        // Assert
        response2.Headers.TryGetValues("x-count", out values).ShouldBeTrue();
        values.ShouldNotBeNull();
        values.ShouldBe(["2"]);

        response2.Content.Headers.TryGetValues("content-type", out values).ShouldBeTrue();
        values.ShouldNotBeNull();
        values.ShouldBe(["application/json; v=2"]);
    }

    [Fact]
    public static async Task Intercept_Http_Get_For_Json_Object_With_Json_Source_Generator()
    {
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
            .WithJsonContent(new() { Id = 1, Link = "https://www.just-eat.co.uk/privacy-policy" }, AppJsonSerializerContext.Default.TermsAndConditions)
            .RegisterWith(options);

        using var client = options.CreateHttpClient();

        // Act
        // The value of json will be: {"Id":1, "Link":"https://www.just-eat.co.uk/privacy-policy"}
        var content = await client.GetFromJsonAsync<TermsAndConditions>("https://public.je-apis.com/terms", TestContext.Current.CancellationToken);

        // Assert
        content.ShouldNotBeNull();
        content.Id.ShouldBe(1);
        content.Link.ShouldBe("https://www.just-eat.co.uk/privacy-policy");
    }

    [Fact]
    public static async Task Vary_The_Response_Using_Content_Negotiation()
    {
        // Arrange
        var expectedPull =
            """
            {
              "url": "https://api.github.com/repos/justeattakeaway/httpclient-interception/pulls/1004",
              "id": 2346177741,
              "node_id": "PR_kwDOBa7mxs6L19TN",
              "html_url": "https://github.com/justeattakeaway/httpclient-interception/pull/1004",
              "diff_url": "https://github.com/justeattakeaway/httpclient-interception/pull/1004.diff",
              "patch_url": "https://github.com/justeattakeaway/httpclient-interception/pull/1004.patch",
              "issue_url": "https://api.github.com/repos/justeattakeaway/httpclient-interception/issues/1004",
              "number": 1004,
              "state": "closed",
              "title": "Bump the xunit group with 2 updates",
              "user": {
                "login": "dependabot[bot]"
              },
              "body": "Bumps the xunit group with 2 updates: [xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit) and [xunit.v3](https://github.com/xunit/xunit).",
              "created_at": "2025-02-20T05:25:35Z",
              "updated_at": "2025-02-20T06:30:51Z",
              "closed_at": "2025-02-20T06:30:45Z",
              "merged_at": "2025-02-20T06:30:45Z",
              "merge_commit_sha": "97e1bfe247e3d79d5235a60b1725a6de44fa9411",
              "draft": false,
              "author_association": "CONTRIBUTOR",
              "merged": true
            }
            """;

        var expectedDiff =
            """
            diff --git a/Directory.Packages.props b/Directory.Packages.props
            index 39a1334b..165c7500 100644
            --- a/Directory.Packages.props
            +++ b/Directory.Packages.props
            @@ -19,8 +19,8 @@
                 <PackageVersion Include="StyleCop.Analyzers" Version="1.2.0-beta.556" />
                 <PackageVersion Include="System.Net.Http" Version="4.3.4" />
                 <PackageVersion Include="System.Text.Json" Version="8.0.5" />
            -    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.1" />
            -    <PackageVersion Include="xunit.v3" Version="1.0.1" />
            +    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
            +    <PackageVersion Include="xunit.v3" Version="1.1.0" />
               </ItemGroup>
               <ItemGroup>
                 <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="All" />
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new HttpClientInterceptorOptions().ThrowsOnMissingRegistration();

        var builder = new HttpRequestInterceptionBuilder()
            .ForHttps()
            .ForHost("api.github.com")
            .ForPath("repos/justeattakeaway/httpclient-interception/pulls/1004")
            .ForRequestHeader("Accept", "application/vnd.github.v3+json")
            .WithContentHeader("Content-Type", "application/json; charset=utf-8")
            .WithContent(expectedPull);

        options.Register(builder);

        builder.ForRequestHeader("Accept", "application/vnd.github.diff")
               .Responds()
               .WithContentHeader("Content-Type", "application/vnd.github.diff; charset=utf-8")
               .WithContent(expectedDiff);

        options.Register(builder);

        string requestUri = "/repos/justeattakeaway/httpclient-interception/pulls/1004";

        using var client = options.CreateHttpClient("https://api.github.com");

        using var pullRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        pullRequest.Headers.Accept.Add(new("application/vnd.github.v3+json"));

        // Act
        using var pull = await client.SendAsync(pullRequest, cancellationToken);
        var actualPull = await pull.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        actualPull.ShouldBe(expectedPull, StringCompareShould.IgnoreLineEndings);

        // Arrange
        using var diffRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        diffRequest.Headers.Accept.Add(new("application/vnd.github.diff"));

        // Act
        using var diff = await client.SendAsync(diffRequest, cancellationToken);
        var actualDiff = await diff.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        actualDiff.ShouldBe(expectedDiff, StringCompareShould.IgnoreLineEndings);
    }

    private sealed class TermsAndConditions
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Link")]
        public string Link { get; set; }
    }

    [JsonSerializable(typeof(TermsAndConditions))]
    private sealed partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }
}
