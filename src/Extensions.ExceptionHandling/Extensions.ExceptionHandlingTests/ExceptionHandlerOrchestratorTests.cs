using System;
using System.IO;
using System.Threading.Tasks;
using Extensions.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Extensions.ExceptionHandlingTests
{
    public class ExceptionHandlerOrchestratorTests
    {
        [Fact]
        public void Constructor_WithNullAsServiceProviderParam_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ExceptionHandlerOrchestrator(null));
        }

        [Fact]
        public async Task TryHandleExceptionAsync_WithNullAsExceptionParam_ThrowsArgumentNullException()
        {
            var orchestrator = OrchestratorBuilder.Empty();
            await Assert.ThrowsAsync<ArgumentNullException>(() => orchestrator.TryHandleExceptionAsync(null, new DefaultHttpContext()));
        }

        [Fact]
        public async Task TryHandleExceptionAsync_WithNullAsHttpContextParam_ThrowsArgumentNullException()
        {
            var orchestrator = OrchestratorBuilder.Empty();
            await Assert.ThrowsAsync<ArgumentNullException>(() => orchestrator.TryHandleExceptionAsync(new Exception(), null));
        } 

        [Fact]
        public async Task TryHandleExceptionAsync_GivenNoRegisteredHandlersWhenTryingToHandleException_ReturnsFalse()
        {
            // Arrange
            var orchestrator = OrchestratorBuilder.Empty();

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), new DefaultHttpContext());

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenExceptionHandlerWhenTryingToHandleException_ReturnsTrue()
        {
            // Arrange
            var orchestrator = OrchestratorBuilder.WithoutLogger()
                .AddExceptionHandler<Exception>(out _)
                .Build();
            var context = new DefaultHttpContext();

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenExceptionHandlerWhenTryingToHandleArgumentException_ReturnsTrue()
        {
            // Arrange
            var orchestrator = OrchestratorBuilder.WithoutLogger()
                .AddExceptionHandler<Exception>(out _)
                .Build();
            var context = new DefaultHttpContext();

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new ArgumentException(), context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenArgumentExceptionHandlerWhenTryingToHandleException_ReturnsFalse()
        {
            // Arrange
            var orchestrator = OrchestratorBuilder.WithoutLogger()
                .AddExceptionHandler<ArgumentException>(out _)
                .Build();
            var context = new DefaultHttpContext();

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new Exception(), context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TryHandleExceptionAsync_GivenArgumentExceptionAndExceptionHandlersWhenTryingToHandleArgumentException_OnlyArgumentExceptionHandlerIsInvoked()
        {
            // Arrange
            var orchestrator = OrchestratorBuilder.WithoutLogger()
                .AddExceptionHandler<Exception>(out var exceptionHandlerMocker)
                .AddExceptionHandler<ArgumentException>(out var argumentExceptionHandlerMocker)
                .Build();
            var context = new DefaultHttpContext();

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
            var orchestrator = OrchestratorBuilder.WithoutLogger()
                .AddExceptionHandler<Exception>(out var exceptionHandlerMocker)
                .AddExceptionHandler<ArgumentNullException>(out var argumentExceptionHandlerMocker)
                .Build();
            var context = new DefaultHttpContext();

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
            var orchestrator = OrchestratorBuilder
                .WithLogger(out _)
                .AddExceptionHandler<Exception>(
                    out var exceptionHandlerMocker, 
                    () => throw new Exception("The exception handler is unsafe! O_o"))
                .Build();
            var context = new DefaultHttpContext();

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
                Instance = "This is a reference to the specific error that just occured.",
                Type = "Some type"
            };
            var orchestrator = OrchestratorBuilder
                .WithoutLogger()
                .AddExceptionHandler<Exception>(out _, () => problemDetails)
                .Build();
            var context = new DefaultHttpContext();
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
            var orchestrator = OrchestratorBuilder.WithoutLogger()
                .AddExceptionHandler<ApplicationException>(out var applicationExceptionHandlerMock, () => throw new Exception())
                .AddExceptionHandler<Exception>(out var exceptionHandlerMock)
                .Build();

            // Act
            var result = await orchestrator.TryHandleExceptionAsync(new ApplicationException(), new DefaultHttpContext());

            // Assert
            Assert.False(result);
            applicationExceptionHandlerMock.Verify(TestHelpers.HandleExpression<ApplicationException>(), Times.Once);
            exceptionHandlerMock.Verify(TestHelpers.HandleExpression<Exception>(), Times.Never);
        }

        #region Helpers

        private class OrchestratorBuilder
        {
            private readonly IServiceCollection _serviceCollection = new ServiceCollection();
            private readonly ILogger<ExceptionHandlerOrchestrator> _logger;

            private OrchestratorBuilder(ILogger<ExceptionHandlerOrchestrator> logger = null)
            {
                _logger = logger;
            }

            public OrchestratorBuilder AddExceptionHandler<TException>(
                out Mock<IExceptionHandler<TException>> exceptionHandlerMock,
                Func<ProblemDetails> problemDetailsFunc = null)
                where TException : Exception
            {
                _serviceCollection.AddExceptionHandlerMock(
                    out exceptionHandlerMock,
                    problemDetailsFunc);
                return this;
            }

            public ExceptionHandlerOrchestrator Build()
            {
                return new ExceptionHandlerOrchestrator(_serviceCollection.BuildServiceProvider(), _logger);
            }

            public static OrchestratorBuilder WithoutLogger()
            {
                return new OrchestratorBuilder();
            }

            public static OrchestratorBuilder WithLogger(
                out Mock<ILogger<ExceptionHandlerOrchestrator>> logMock)
            {
                logMock = new Mock<ILogger<ExceptionHandlerOrchestrator>>();
                return new OrchestratorBuilder(logMock.Object);
            }

            public static ExceptionHandlerOrchestrator Empty()
            {
                return new OrchestratorBuilder().Build();
            }
        } 
        #endregion
    }
}
