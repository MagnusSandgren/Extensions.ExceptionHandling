using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Extensions.ExceptionHandling
{

    /// <summary>
    /// Represents an exception handler that can handle exceptions of <typeparamref name="TException"/> 
    /// or any of its sub exceptions while offering an overrideable default logging behaviour.
    /// </summary>
    /// <typeparam name="TSelf">Type of self.</typeparam>
    /// <typeparam name="TException">The type of <see cref="T:System.Exception"/> to handle.</typeparam>
    public abstract class AbstractLoggerExceptionHandler<TSelf, TException> : IExceptionHandler<TException>
        where TSelf : IExceptionHandler<TException>
        where TException : Exception
    {
        /// <summary>
        /// Log instance.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initialize a new instance of the <see cref="AbstractLoggerExceptionHandler{TSelf,TException}"/>.
        /// </summary>
        /// <param name="logger">The logging instance. Cannot be null.</param>
        protected AbstractLoggerExceptionHandler(ILogger<TSelf> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles an exception of <typeparamref name="TException"/>.
        /// </summary>
        /// <param name="exception">Exception to handle.</param>
        /// <param name="context">Context information.</param>
        /// <returns>A machine-readable format for specifying errors in HTTP API responses based on https://tools.ietf.org/html/rfc7807. </returns>
        public abstract Task<ProblemDetails> Handle(TException exception, ExceptionHandlerContext context);

        /// <summary>
        /// Logs exception through ILogger.LogError.
        /// </summary>
        /// <param name="exception">Exception to log</param>
        /// <param name="context">Context information.</param>
        protected virtual void Log(TException exception, ExceptionHandlerContext context)
        {
            Logger.LogError(exception, "{TraceId}: An unexpected error occurred.", context.TraceId);
        }

        Task<ProblemDetails> IExceptionHandler<TException>.Handle(TException exception, ExceptionHandlerContext context)
        {
            Log(exception, context);
            return Handle(exception, context);
        }
    }
}