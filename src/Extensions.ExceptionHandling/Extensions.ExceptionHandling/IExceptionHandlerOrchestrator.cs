using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Extensions.ExceptionHandling
{
    /// <summary>
    /// Represents an exception handling orchestrator which job is to route the exception to the right handler, or fail gracefully.
    /// </summary>
    public interface IExceptionHandlerOrchestrator
    {
        /// <summary>
        /// Attempt to handle the incoming exception.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="context">The http context in which the exception occurred.</param>
        /// <returns>True if the exception were handled successfully, otherwise false.</returns>
        Task<bool> TryHandleExceptionAsync(Exception exception, HttpContext context);
    }

    /// <summary>
    /// Concrete exception handling orchestrator which job is to route the exception to the right handler, or fail gracefully.
    /// </summary>
    public sealed class ExceptionHandlerOrchestrator : IExceptionHandlerOrchestrator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExceptionHandlerOrchestrator> _logger;

        /// <summary>
        /// Initialize a new instance of the <see cref="ExceptionHandlerOrchestrator"/> class. 
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="logger"></param>
        public ExceptionHandlerOrchestrator(IServiceProvider serviceProvider, ILogger<ExceptionHandlerOrchestrator> logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;
        }

        /// <summary>
        /// Attempt to handle the incoming exception.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="context">The http context in which the exception occurred.</param>
        /// <returns>True if the exception were handled successfully, otherwise false.</returns>
        public async Task<bool> TryHandleExceptionAsync(Exception exception, HttpContext context)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            

            

            var problemDetails = await NewMethod(exception, context);

            //if (!_serviceProvider.TryGetExceptionHandler(exceptionType, out var handler))
            //{
            //    return false;
            //}

            //var problemDetails = await InvokeHandler(handler, exception, exceptionType, CreateHandlerContext(context));

            if (problemDetails == null)
            {
                return false;
            }

            await WriteResponseAsync(context, problemDetails);

            return true;
        }

        private async Task<ProblemDetails> NewMethod(Exception exception, HttpContext context)
        {
            var exceptionType = exception.GetType();
            var handlerContext = CreateHandlerContext(context);

            foreach (var exceptionHandler in _serviceProvider.GetExceptionHandlers(exceptionType))
            {
                var p = await InvokeHandler(exceptionHandler, exception, exceptionType, handlerContext);

                if (p != null)
                {
                    return p;
                }
            }

            return default;
        }

        private static ExceptionHandlerContext CreateHandlerContext(HttpContext context)
        {
            return new ExceptionHandlerContext(context.TraceIdentifier);
        }

        private static async Task WriteResponseAsync(HttpContext context, ProblemDetails problemDetails)
        {
            problemDetails.Status = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
            context.Response.Clear();
            context.Response.StatusCode = problemDetails.Status.Value;
            await context.Response.WriteJsonAsync(problemDetails);
        }

        private async Task<ProblemDetails> InvokeHandler(object exceptionHandler, Exception exception, Type exceptionType, ExceptionHandlerContext handlerContext)
        {
            try
            {
                return await (Task<ProblemDetails>)InvokeGenericHandlerMethodInfo
                    .MakeGenericMethod(exceptionType)
                    .Invoke(null, new[] { exceptionHandler, exception, handlerContext });
            }
            catch (Exception e)
            {
                var handlerType = exceptionHandler.GetType();
                _logger?.LogError(e, $"Handler of type {handlerType.FullName} threw an unexpected error. " +
                                    "Exception handler middleware will not handle this exception.");
            }

            return default;
        }

        private static readonly MethodInfo InvokeGenericHandlerMethodInfo = typeof(ExceptionHandlerOrchestrator)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(x => x.IsGenericMethod && x.Name == nameof(InvokeGenericHandler));

        private static Task<ProblemDetails> InvokeGenericHandler<TException>(
            IExceptionHandler<TException> exceptionHandler, 
            TException exception,
            ExceptionHandlerContext handlerContext)
            where TException : Exception
        {
            return exceptionHandler.Handle(exception, handlerContext);
        }
    }
}
