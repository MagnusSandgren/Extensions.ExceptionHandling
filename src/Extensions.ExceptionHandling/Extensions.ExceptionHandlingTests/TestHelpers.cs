using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Extensions.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace Extensions.ExceptionHandlingTests
{
    internal static class TestHelpers
    {
        public static Task<string> ReadAllAsTextAsync(this Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(stream);
            return reader.ReadToEndAsync();
        }

        public static async Task<T> DeserializeJson<T>(this Task<string> value)
        {
            return JsonConvert.DeserializeObject<T>(await value);
        }

        public static bool IsMapFromTo<TService, TImplementation>(this ServiceDescriptor descriptor) 
            => descriptor.IsMapFromTo(typeof(TService), typeof(TImplementation));

        public static bool IsMapFromTo(this ServiceDescriptor descriptor, Type serviceType, Type implementationType)
        {
            return descriptor.ImplementationType == implementationType
                   && descriptor.ServiceType == serviceType;
        }

        public static IServiceCollection AddExceptionHandlerMock<TException>(
            this IServiceCollection serviceCollection,
            out Mock<IExceptionHandler<TException>> exceptionHandlerMock,
            Func<ProblemDetails> problemDetailsFunc = null)
            where TException : Exception
        {
            exceptionHandlerMock = CreateExceptionHandlerMock<TException>(problemDetailsFunc);
            var exceptionHandler = exceptionHandlerMock.Object;
            serviceCollection.AddTransient(x => exceptionHandler);
            return serviceCollection;
        }

        public static Mock<IExceptionHandler<TException>> CreateExceptionHandlerMock<TException>(
            Func<ProblemDetails> problemDetailsFunc = null)
            where TException : Exception
        {
            var exceptionHandlerMock = new Mock<IExceptionHandler<TException>>();
            exceptionHandlerMock
                .Setup(HandleExpression<TException>())
                .ReturnsAsync(problemDetailsFunc ?? GenerateEmptyProblemDetails);
            return exceptionHandlerMock;
        }

        public static Expression<Func<IExceptionHandler<TException>, Task<ProblemDetails>>> HandleExpression<TException>()
            where TException : Exception
        {
            return x => x.Handle(It.IsAny<TException>(), It.IsAny<ExceptionHandlerContext>());
        }

        private static ProblemDetails GenerateEmptyProblemDetails()
        {
            return new ProblemDetails();
        }
    }
}