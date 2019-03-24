using System;
using System.Reflection;
using System.Threading.Tasks;
using Extensions.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Extensions.ExceptionHandlingTests
{
    public class ServiceCollectionTests
    {
        [Fact]
        public void AddExceptionHandlersByAssembly_GivenNullAsServiceCollection_ThrowArgumentNullException()
        {
            IServiceCollection serviceCollection = null;
            Assert.Throws<ArgumentNullException>(() => serviceCollection.AddExceptionHandlers());
        }

        [Fact]
        public void AddExceptionHandlersByAssembly_GivenNullAsAssembly_IgnoresNullAndAddsOrchestrator()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();

            // Act
            serviceCollection.AddExceptionHandlers((Assembly) null, null, null);

            //Assert
            var serviceDescriptor = Assert.Single(serviceCollection);
            Assert.NotNull(serviceDescriptor);
            Assert.Equal(typeof(IExceptionHandlerOrchestrator), serviceDescriptor.ServiceType);
            Assert.Equal(typeof(ExceptionHandlerOrchestrator), serviceDescriptor.ImplementationType);
        }

        [Fact]
        public void AddExceptionHandlersByAssembly_GivenAssemblyWithOneHandler_HandlerAdded()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();
            var assemblyMock = GetMockAssemblyWith(typeof(ExceptionHandler));

            // Act
            serviceCollection.AddExceptionHandlers(assemblyMock.Object);

            // Assert
            Assert.Single(serviceCollection, x => 
                x.IsMapFromTo<IExceptionHandler<Exception>, ExceptionHandler>());
        }

        [Fact]
        public void AddExceptionHandlersByAssembly_GivenAssemblyWithDuplicateHandlers_ThrowsInvalidOperationException()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();
            var assemblyMock = GetMockAssemblyWith(
                typeof(ExceptionHandler),
                typeof(DuplicateExceptionHandler));

            // Act
            void TestAction() => serviceCollection.AddExceptionHandlers(assemblyMock.Object);

            // Assert
            Assert.Throws<InvalidOperationException>(() => TestAction());
        }

        [Fact]
        public void AddExceptionHandlersByAssembly_GivenAssemblyWithMultipleDistinctHandlers_HandlersAdded()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();
            var assemblyMock = GetMockAssemblyWith(
                typeof(ExceptionHandler),
                typeof(ApplicationExceptionHandler),
                typeof(ArgumentExceptionHandler),
                typeof(ArgumentNullExceptionHandler)
            );

            // Act
            serviceCollection.AddExceptionHandlers(assemblyMock.Object);

            // Assert
            Assert.Single(serviceCollection, x => x.IsMapFromTo<IExceptionHandler<Exception>, ExceptionHandler>());
            Assert.Single(serviceCollection, x => x.IsMapFromTo<IExceptionHandler<ApplicationException>, ApplicationExceptionHandler>());
            Assert.Single(serviceCollection, x => x.IsMapFromTo<IExceptionHandler<ArgumentException>, ArgumentExceptionHandler>());
            Assert.Single(serviceCollection, x => x.IsMapFromTo<IExceptionHandler<ArgumentNullException>, ArgumentNullExceptionHandler>());
        }

        [Fact]
        public void AddExceptionHandlersByAssembly_GivenAssemblyWithAbstractHandler_IgnoresAbstractHandler()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();
            var assemblyMock = GetMockAssemblyWith(typeof(AbstractExceptionHandler));

            // Act
            serviceCollection.AddExceptionHandlers(assemblyMock.Object);

            // Assert
            Assert.DoesNotContain(serviceCollection, x => x.ImplementationType == typeof(AbstractExceptionHandler));
        }

        [Fact]
        public void AddExceptionHandlersByAssembly_GivenAssemblyWithOneHandlerAndOtherObjects_AddHandlerAndIgnoreTheRest()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();
            var assemblyMock = GetMockAssemblyWith(
                typeof(Numbers),
                typeof(Foo),
                typeof(ExceptionHandler),
                typeof(Bar)
            );

            // Act
            serviceCollection.AddExceptionHandlers(assemblyMock.Object);

            // Assert
            Assert.Collection(serviceCollection, 
                x => x.IsMapFromTo<IExceptionHandler<Exception>, ExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandlerOrchestrator, ExceptionHandlerOrchestrator>()
            );
        }

        [Fact]
        public void AddExceptionHandlersByAssembly_GivenMultipleAssembliesWithHandlers_AddAllHandlersFromAllAssemblies()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();
            var assemblyMock1 = GetMockAssemblyWith(typeof(ExceptionHandler));
            var assemblyMock2 = GetMockAssemblyWith(typeof(ApplicationExceptionHandler));

            // Act
            serviceCollection.AddExceptionHandlers(assemblyMock1.Object, assemblyMock2.Object);

            // Assert
            Assert.Collection(serviceCollection,
                x => x.IsMapFromTo<IExceptionHandler<Exception>, ExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandler<ApplicationException>, ApplicationExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandlerOrchestrator, ExceptionHandlerOrchestrator>()
            );
        }

        [Fact]
        public void AddExceptionHandlersByType_GivenNullAsServiceCollection_ThrowArgumentNullException()
        {
            IServiceCollection serviceCollection = null;
            Assert.Throws<ArgumentNullException>(() => serviceCollection.AddExceptionHandlersByTypes(typeof(ExceptionHandler)));
        }

        [Fact]
        public void AddExceptionHandlersByType_GivenNullAsType_IgnoresNullAndAddsOrchestrator()
        {
            // Arrange 
            var serviceCollection = GetServiceCollection();

            // Act
            serviceCollection.AddExceptionHandlersByTypes((Type)null, null, null, null);

            // Assert
            Assert.Single(serviceCollection);
        }

        [Theory]
        [InlineData(typeof(AbstractExceptionHandler))]
        [InlineData(typeof(Numbers))]
        [InlineData(typeof(StaticClass))]
        [InlineData(typeof(GenericHandler<>))]
        public void AddExceptionHandlersByType_GivenNonInstantiableType_ThrowsInvalidOperationException(Type type)
        {
            // Arrange
            var serviceCollection = GetServiceCollection();

            // Act
            void TestAction() => serviceCollection.AddExceptionHandlersByTypes(type);

            // Assert
            Assert.Throws<InvalidOperationException>(() => TestAction());
        }

        [Fact]
        public void AddExceptionHandlersByType_GivenDuplicateHandlers_ThrowsInvalidOperationException()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();

            // Act
            void TestAction() => serviceCollection.AddExceptionHandlersByTypes(
                typeof(ExceptionHandler),
                typeof(DuplicateExceptionHandler)
            );

            // Assert
            Assert.Throws<InvalidOperationException>(() => TestAction());
        }

        [Fact]
        public void AddExceptionHandlersByType_GivenValidExceptionHandlers_HandlersAndOrchestratorAdded()
        {
            // Arrange 
            var serviceCollection = GetServiceCollection();

            // Act
            serviceCollection.AddExceptionHandlersByTypes(
                typeof(ExceptionHandler),
                typeof(ArgumentNullExceptionHandler),
                typeof(ApplicationExceptionHandler),
                typeof(ArgumentExceptionHandler)
            );

            // Assert
            Assert.Collection(serviceCollection,
                x => x.IsMapFromTo<IExceptionHandler<Exception>, ExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandler<ArgumentNullException>, ArgumentNullExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandler<ApplicationException>, ApplicationExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandler<ArgumentException>, ArgumentExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandlerOrchestrator, ExceptionHandlerOrchestrator>()
            );
        }

        [Fact]
        public void AddExceptionHandlersByType_GivenNonHandlerType_ThrowInvalidOperationException()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();

            // Act
            void TestAction() => serviceCollection.AddExceptionHandlersByTypes(typeof(Foo));

            // Assert
            Assert.Throws<InvalidOperationException>(() => TestAction());
        }

        [Fact]
        public void AddExceptionHandlersByTypes_GivenValidHandlerType_AddsHandlerTypeAndOrchestrator()
        {
            // Arrange
            var serviceCollection = GetServiceCollection();

            // Act
            serviceCollection.AddExceptionHandlersByTypes(typeof(ExceptionHandler));

            // Assert
            Assert.Collection(serviceCollection,
                x => x.IsMapFromTo<IExceptionHandler<Exception>, ExceptionHandler>(),
                x => x.IsMapFromTo<IExceptionHandlerOrchestrator, ExceptionHandlerOrchestrator>()
            );
        }

        private static IServiceCollection GetServiceCollection() => new ServiceCollection();

        private static Mock<Assembly> GetMockAssemblyWith(params Type[] types)
        {
            var assemblyMock = new Mock<Assembly>();
            assemblyMock.Setup(x => x.GetTypes())
                .Returns(types);
            return assemblyMock;
        }

        #region TestObjects
        private class ExceptionHandler : IExceptionHandler<Exception>
        {
            public Task<ProblemDetails> Handle(Exception exception, ExceptionHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        private class DuplicateExceptionHandler : IExceptionHandler<Exception>
        {
            public Task<ProblemDetails> Handle(Exception exception, ExceptionHandlerContext context)
            {
                throw new NotImplementedException();
            }
        } 

        private class ApplicationExceptionHandler : IExceptionHandler<ApplicationException>
        {
            public Task<ProblemDetails> Handle(ApplicationException exception, ExceptionHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        private class ArgumentExceptionHandler : IExceptionHandler<ArgumentException>
        {
            public Task<ProblemDetails> Handle(ArgumentException exception, ExceptionHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        private class ArgumentNullExceptionHandler : IExceptionHandler<ArgumentNullException>
        {
            public Task<ProblemDetails> Handle(ArgumentNullException exception, ExceptionHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        private abstract class AbstractExceptionHandler : IExceptionHandler<Exception>
        {
            public Task<ProblemDetails> Handle(Exception exception, ExceptionHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }


        private class GenericHandler<TException> : IExceptionHandler<TException> where TException : Exception
        {
            public Task<ProblemDetails> Handle(TException exception, ExceptionHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        private static class StaticClass { }

        private enum Numbers { }

        private class Foo { }

        private class Bar { }
        #endregion
    }
}
