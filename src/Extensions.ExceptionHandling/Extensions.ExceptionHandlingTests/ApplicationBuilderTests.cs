using System;
using System.IO;
using System.Threading.Tasks;
using Extensions.ExceptionHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Extensions.ExceptionHandlingTests
{
    public class ApplicationBuilderTests
    {
        [Fact]
        public void UseExceptionHandlerMiddleware_WhenApplicationBuilderIsNull_ThrowsArgumentNullException()
        {
            IApplicationBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.UseExceptionHandlerMiddleware());
        }

        [Fact]
        public async Task UseExceptionHandlerMiddleware_WhenEverythingIsValid_MiddlewareAddedToPipelineAndRunsSuccessfully()
        {
            // Arrange
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Something bad just happen.",
                Detail = "It was really-really bad!",
                Instance = "This is a reference to the specific error that just occured.",
                Type = "Some type"
            };
            var serviceProvider = new ServiceCollection()
                .AddExceptionHandlerMock<Exception>(out var exceptionHandlerMock, () => problemDetails)
                .AddTransient<IExceptionHandlerOrchestrator, ExceptionHandlerOrchestrator>()
                .BuildServiceProvider();
            var pipeline = new ApplicationBuilder(serviceProvider)
                .UseExceptionHandlerMiddleware()
                .UseMiddleware<ExceptionThrowingMiddleware>()
                .Build();
            var context = GetHttpContext(serviceProvider);

            // Act
            await pipeline.Invoke(context);
            var problemDetailsResult = await context.Response.Body
                .ReadAllAsTextAsync()
                .DeserializeJson<ProblemDetails>();

            // Assert
            exceptionHandlerMock.Verify(TestHelpers.HandleExpression<Exception>(), Times.Once);
            Assert.Equal("application/json", context.Response.ContentType);
            Assert.Equal(problemDetails.Status, context.Response.StatusCode);

            Assert.Equal(problemDetails.Status, problemDetailsResult.Status);
            Assert.Equal(problemDetails.Title, problemDetailsResult.Title);
            Assert.Equal(problemDetails.Detail, problemDetailsResult.Detail);
            Assert.Equal(problemDetails.Instance, problemDetailsResult.Instance);
            Assert.Equal(problemDetails.Type, problemDetailsResult.Type);
        }

        private static HttpContext GetHttpContext(IServiceProvider services)
        {
            var httpContext = new DefaultHttpContext {RequestServices = services};
            httpContext.Response.Body = new MemoryStream();
            return httpContext;
        }

        private class ExceptionThrowingMiddleware
        {
            private readonly RequestDelegate _next;

            public ExceptionThrowingMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public Task Invoke(HttpContext httpContext)
            {
                throw new Exception();
            }
        }
    }
}
