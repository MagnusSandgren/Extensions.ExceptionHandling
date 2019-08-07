using System;
using System.IO;
using System.Threading.Tasks;
using Extensions.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Extensions.ExceptionHandlingTests
{
    public class ExceptionHandlerOrchestratorTests
    {
        [Fact]
        public async Task TryHandleExceptionAsync_WithNullAsExceptionParam_ThrowsArgumentNullException()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var context = CreateHttpContext();

            // Assert 
            await Assert.ThrowsAsync<ArgumentNullException>(() => orchestrator.TryHandleExceptionAsync(null, context));
        }

        [Fact]
        public async Task TryHandleExceptionAsync_WithNullAsHttpContextParam_ThrowsArgumentNullException()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => orchestrator.TryHandleExceptionAsync(new Exception(), null));
        }

        [Fact]
        public async Task TryHandleExceptionAsync_HttpContextWithoutRequestServices_ThrowsArgumentNullException()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var context = new DefaultHttpContext();

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.TryHandleExceptionAsync(new Exception(), context));
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenNoRegisteredHandlersWhenTryingToHandleException_ReturnsFalse()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var context = CreateHttpContext();

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenExceptionHandlerWhenTryingToHandleException_ReturnsTrue()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<Exception>(out _)
                .BuildServiceProvider();
            var context = CreateHttpContext(services);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenExceptionHandlerWhenTryingToHandleArgumentException_ReturnsTrue()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<Exception>(out _)
                .BuildServiceProvider();
            var context = CreateHttpContext(services);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new ArgumentException(), context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenArgumentExceptionHandlerWhenTryingToHandleException_ReturnsFalse()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<ArgumentNullException>(out _)
                .BuildServiceProvider();
            var context = CreateHttpContext(services);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenArgumentExceptionAndExceptionHandlersWhenTryingToHandleArgumentException_OnlyArgumentExceptionHandlerIsInvoked()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<Exception>(out var exceptionHandlerMocker)
                .AddExceptionHandlerMock<ArgumentException>(out var argumentExceptionHandlerMocker)
                .BuildServiceProvider();
            var context = CreateHttpContext(services);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new ArgumentException(), context);

            // Assert
            Assert.True(result);
            argumentExceptionHandlerMocker.Verify(TestHelpers.HandleExpression<ArgumentException>(), Times.Once);
            exceptionHandlerMocker.Verify(TestHelpers.HandleExpression<Exception>(), Times.Never);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenArgumentNullExceptionAndExceptionHandlersWhenTryingToHandleArgumentException_OnlyExceptionHandlerIsInvoked()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<Exception>(out var exceptionHandlerMocker)
                .AddExceptionHandlerMock<ArgumentNullException>(out var argumentExceptionHandlerMocker)
                .BuildServiceProvider();
            var context = CreateHttpContext(services);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new ArgumentException(), context);

            // Assert
            Assert.True(result);
            argumentExceptionHandlerMocker.Verify(TestHelpers.HandleExpression<ArgumentNullException>(), Times.Never);
            exceptionHandlerMocker.Verify(TestHelpers.HandleExpression<Exception>(), Times.Once);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenFaultyExceptionHandlerWhenTryingToHandleException_ReturnsFalse()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<Exception>(
                    out var exceptionHandlerMocker,
                    () => throw new Exception("The exception handler is unsafe! O_o"))
                .BuildServiceProvider();
            var context = CreateHttpContext(services);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);

            // Assert
            Assert.False(result);
            exceptionHandlerMocker.Verify(TestHelpers.HandleExpression<Exception>(), Times.Once);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenExceptionHandlerReturningDetailedProblemDetailsWhenTryingToHandleException_ResultsInCorrectResponse()
        {
            // Arrange
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Something bad just happen.",
                Detail = "It was really-really bad!",
                Instance = "This is a reference to the specific error that just occurred.",
                Type = "Some type"
            };
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<Exception>(out _, () => problemDetails)
                .BuildServiceProvider();
            var context = CreateHttpContext(services);
            context.Response.Body = new MemoryStream();
            
            // Act 
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);
            var problemDetailsResult = await context.Response.Body
                .ReadAllAsTextAsync()
                .DeserializeJson<ProblemDetails>();

            // Assert
            Assert.True(result);
            Assert.Equal("application/json", context.Response.ContentType);
            Assert.Equal(problemDetails.Status, context.Response.StatusCode);

            Assert.Equal(problemDetails.Status, problemDetailsResult.Status);
            Assert.Equal(problemDetails.Title, problemDetailsResult.Title);
            Assert.Equal(problemDetails.Detail, problemDetailsResult.Detail);
            Assert.Equal(problemDetails.Instance, problemDetailsResult.Instance);
            Assert.Equal(problemDetails.Type, problemDetailsResult.Type);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenFaultyApplicationExceptionHandlerAndExceptionHandlerWhenTryingToHandleApplicationException_ExceptionHandlerNotInvokedAndReturnsFalse()
        {
            // Arrange
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddExceptionHandlerMock<ApplicationException>(out var applicationExceptionHandlerMock, () => throw new Exception())
                .AddExceptionHandlerMock<Exception>(out var exceptionHandlerMock)
                .BuildServiceProvider();
            var context = CreateHttpContext(services);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new ApplicationException(), context);

            // Assert
            Assert.False(result);
            applicationExceptionHandlerMock.Verify(TestHelpers.HandleExpression<ApplicationException>(), Times.Once);
            exceptionHandlerMock.Verify(TestHelpers.HandleExpression<Exception>(), Times.Never);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenServiceProviderWithScopeValidationAndScopeDependingExceptionHandler_SucceedsAndReturnsTrue()
        {
            // Arrange 
            var orchestrator = new ExceptionHandlerOrchestrator();
            var services = new ServiceCollection()
                .AddTransient<IExceptionHandler<Exception>, DependingExceptionHandler>()
                .AddScoped<DependingType>()
                .BuildServiceProvider(true);
            var scope = services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);

            // Assert
            Assert.True(result);
        }

        private static HttpContext CreateHttpContext(IServiceProvider services = null)
        {
            return new DefaultHttpContext
            {
                RequestServices = services ?? new ServiceCollection().BuildServiceProvider(true)
            };
        }

        //public static OrchestratorBuilder WithLogger(
        //    out Mock<ILogger<ExceptionHandlerOrchestrator>> logMock)
        //{
        //    logMock = new Mock<ILogger<ExceptionHandlerOrchestrator>>();
        //    return new OrchestratorBuilder(logMock.Object);
        //}

        private class DependingExceptionHandler : IExceptionHandler<Exception>
        {
            private readonly DependingType _depending;

            public DependingExceptionHandler(DependingType depending)
            {
                _depending = depending ?? throw new ArgumentNullException(nameof(depending));
            }

            public Task<ProblemDetails> Handle(Exception exception, ExceptionHandlerContext context)
            {
                return Task.FromResult(new ProblemDetails());
            }
        }

        private class DependingType
        {
        }
    }
}
