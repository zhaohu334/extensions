﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Logging;
using Polly;
using Xunit;

namespace Microsoft.Extensions.DependencyInjection
{
    // These are integration tests that verify basic end-to-ends.
    public class PollyHttpClientBuilderExtensionsTest
    {
        public PollyHttpClientBuilderExtensionsTest()
        {
            PrimaryHandler = new FaultyMessageHandler();

            NoOpPolicy = Policy.NoOpAsync<HttpResponseMessage>();
            RetryPolicy = Policy.Handle<OverflowException>().OrResult<HttpResponseMessage>(r => false).RetryAsync();
        }

        private FaultyMessageHandler PrimaryHandler { get; }

        // Allows the exception from our handler to propegate
        private IAsyncPolicy<HttpResponseMessage> NoOpPolicy { get; }

        // Matches what our client handler does 
        private IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; }

        [Fact]
        public async Task AddPolicyHandler_Policy_AddsPolicyHandler()
        {
            var serviceCollection = new ServiceCollection();

            HttpMessageHandlerBuilder builder = null;

            // Act1
            serviceCollection.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                .AddPolicyHandler(RetryPolicy)
                .ConfigureHttpMessageHandlerBuilder(b =>
                {
                    b.PrimaryHandler = PrimaryHandler;
                    builder = b;
                });

            var services = serviceCollection.BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            // Act2
            var client = factory.CreateClient("example.com");

            // Assert
            Assert.NotNull(client);

            Assert.Collection(
                builder.AdditionalHandlers,
                h => Assert.IsType<LoggingScopeHttpMessageHandler>(h),
                h => Assert.IsType<PolicyHttpMessageHandler>(h),
                h => Assert.IsType<LoggingHttpMessageHandler>(h));

            // Act 3
            var response = await client.SendAsync(new HttpRequestMessage());

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task AddPolicyHandler_PolicySelector_AddsPolicyHandler()
        {
            var serviceCollection = new ServiceCollection();

            HttpMessageHandlerBuilder builder = null;

            // Act1
            serviceCollection.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                .AddPolicyHandler((req) => req.RequestUri.AbsolutePath == "/" ? RetryPolicy : NoOpPolicy)
                .ConfigureHttpMessageHandlerBuilder(b =>
                {
                    b.PrimaryHandler = PrimaryHandler;
                    builder = b;
                });

            var services = serviceCollection.BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            // Act2
            var client = factory.CreateClient("example.com");

            // Assert
            Assert.NotNull(client);

            Assert.Collection(
                builder.AdditionalHandlers,
                h => Assert.IsType<LoggingScopeHttpMessageHandler>(h),
                h => Assert.IsType<PolicyHttpMessageHandler>(h),
                h => Assert.IsType<LoggingHttpMessageHandler>(h));

            // Act 3
            var response = await client.SendAsync(new HttpRequestMessage());

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            // Act 4
            await Assert.ThrowsAsync<OverflowException>(() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/throw")));
        }

        [Fact]
        public async Task AddPolicyHandler_PolicySelectorWithServices_AddsPolicyHandler()
        {
            var serviceCollection = new ServiceCollection();

            HttpMessageHandlerBuilder builder = null;

            // Act1
            serviceCollection.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                .AddPolicyHandler((req) => req.RequestUri.AbsolutePath == "/" ? RetryPolicy : NoOpPolicy)
                .ConfigureHttpMessageHandlerBuilder(b =>
                {
                    b.PrimaryHandler = PrimaryHandler;
                    builder = b;
                });

            var services = serviceCollection.BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            // Act2
            var client = factory.CreateClient("example.com");

            // Assert
            Assert.NotNull(client);

            Assert.Collection(
                builder.AdditionalHandlers,
                h => Assert.IsType<LoggingScopeHttpMessageHandler>(h),
                h => Assert.IsType<PolicyHttpMessageHandler>(h),
                h => Assert.IsType<LoggingHttpMessageHandler>(h));

            // Act 3
            var response = await client.SendAsync(new HttpRequestMessage());

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            // Act 4
            await Assert.ThrowsAsync<OverflowException>(() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/throw")));
        }

        [Fact]
        public async Task AddPolicyHandlerFromRegistry_Name_AddsPolicyHandler()
        {
            var serviceCollection = new ServiceCollection();

            var registry = serviceCollection.AddPolicyRegistry();
            registry.Add<IAsyncPolicy<HttpResponseMessage>>("retry", RetryPolicy);

            HttpMessageHandlerBuilder builder = null;

            // Act1
            serviceCollection.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                .AddPolicyHandlerFromRegistry("retry")
                .ConfigureHttpMessageHandlerBuilder(b =>
                {
                    b.PrimaryHandler = PrimaryHandler;

                    builder = b;
                });

            var services = serviceCollection.BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            // Act2
            var client = factory.CreateClient("example.com");

            // Assert
            Assert.NotNull(client);

            Assert.Collection(
                builder.AdditionalHandlers,
                h => Assert.IsType<LoggingScopeHttpMessageHandler>(h),
                h => Assert.IsType<PolicyHttpMessageHandler>(h),
                h => Assert.IsType<LoggingHttpMessageHandler>(h));

            // Act 3
            var response = await client.SendAsync(new HttpRequestMessage());

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task AddPolicyHandlerFromRegistry_Dynamic_AddsPolicyHandler()
        {
            var serviceCollection = new ServiceCollection();

            var registry = serviceCollection.AddPolicyRegistry();
            registry.Add<IAsyncPolicy<HttpResponseMessage>>("noop", NoOpPolicy);
            registry.Add<IAsyncPolicy<HttpResponseMessage>>("retry", RetryPolicy);

            HttpMessageHandlerBuilder builder = null;

            // Act1
            serviceCollection.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                .AddPolicyHandlerFromRegistry((reg, req) =>
                {
                    return req.RequestUri.AbsolutePath == "/" ?
                        reg.Get<IAsyncPolicy<HttpResponseMessage>>("retry") :
                        reg.Get<IAsyncPolicy<HttpResponseMessage>>("noop");
                })
                .ConfigureHttpMessageHandlerBuilder(b =>
                {
                    b.PrimaryHandler = PrimaryHandler;
                    builder = b;
                });

            var services = serviceCollection.BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            // Act2
            var client = factory.CreateClient("example.com");

            // Assert
            Assert.NotNull(client);

            Assert.Collection(
                builder.AdditionalHandlers,
                h => Assert.IsType<LoggingScopeHttpMessageHandler>(h),
                h => Assert.IsType<PolicyHttpMessageHandler>(h),
                h => Assert.IsType<LoggingHttpMessageHandler>(h));

            // Act 3
            var response = await client.SendAsync(new HttpRequestMessage());

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            // Act 4
            await Assert.ThrowsAsync<OverflowException>(() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/throw")));
        }

        [Theory]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData((HttpStatusCode)500)]
        [InlineData((HttpStatusCode)501)]
        [InlineData((HttpStatusCode)502)]
        [InlineData((HttpStatusCode)503)]
        public async Task AddTransientHttpErrorPolicy_AddsPolicyHandler_HandlesStatusCode(HttpStatusCode statusCode)
        {
            // Arrange
            var handler = new SequenceMessageHandler()
            {
                Responses =
                {
                    (req) => new HttpResponseMessage(statusCode),
                    (req) => new HttpResponseMessage(HttpStatusCode.OK),
                },
            };

            var serviceCollection = new ServiceCollection();

            HttpMessageHandlerBuilder builder = null;

            // Act1
            serviceCollection.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                .AddTransientHttpErrorPolicy(b => b.RetryAsync(5))
                .ConfigureHttpMessageHandlerBuilder(b =>
                {
                    b.PrimaryHandler = handler;
                    builder = b;
                });

            var services = serviceCollection.BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            // Act2
            var client = factory.CreateClient("example.com");

            // Assert
            Assert.NotNull(client);

            Assert.Collection(
                builder.AdditionalHandlers,
                h => Assert.IsType<LoggingScopeHttpMessageHandler>(h),
                h => Assert.IsType<PolicyHttpMessageHandler>(h),
                h => Assert.IsType<LoggingHttpMessageHandler>(h));

            // Act 3
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"));

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AddTransientHttpErrorPolicy_AddsPolicyHandler_HandlesHttpRequestException()
        {
            // Arrange
            var handler = new SequenceMessageHandler()
            {
                Responses =
                {
                    (req) => { throw new HttpRequestException("testing..."); },
                    (req) => new HttpResponseMessage(HttpStatusCode.OK),
                },
            };

            var serviceCollection = new ServiceCollection();

            HttpMessageHandlerBuilder builder = null;

            // Act1
            serviceCollection.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                .AddTransientHttpErrorPolicy(b => b.RetryAsync(5))
                .ConfigureHttpMessageHandlerBuilder(b =>
                {
                    b.PrimaryHandler = handler;
                    builder = b;
                });

            var services = serviceCollection.BuildServiceProvider();
            var factory = services.GetRequiredService<IHttpClientFactory>();

            // Act2
            var client = factory.CreateClient("example.com");

            // Assert
            Assert.NotNull(client);

            Assert.Collection(
                builder.AdditionalHandlers,
                h => Assert.IsType<LoggingScopeHttpMessageHandler>(h),
                h => Assert.IsType<PolicyHttpMessageHandler>(h),
                h => Assert.IsType<LoggingHttpMessageHandler>(h));

            // Act 3
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"));

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Throws an exception or fails on even numbered requests, otherwise succeeds.
        private class FaultyMessageHandler : DelegatingHandler
        {
            public int CallCount { get; private set; }

            public Func<Exception> CreateException { get; set; } = () => new OverflowException();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (CallCount++ % 2 == 0)
                {
                    throw CreateException();
                }
                else
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
                }
            }
        }

        private class SequenceMessageHandler : DelegatingHandler
        {
            public int CallCount { get; private set; }

            public List<Func<HttpRequestMessage, HttpResponseMessage>> Responses { get; } = new List<Func<HttpRequestMessage, HttpResponseMessage>>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var func = Responses[CallCount++ % Responses.Count];
                return Task.FromResult(func(request));
            }
        }
    }
}