using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Extensions.ExceptionHandling
{
    /// <summary>
    /// Represents an exception handler that can handle exceptions of <typeparamref name="TException"/> or any of its sub exceptions.
    /// </summary>
    /// <typeparam name="TException">The type of exception to handle.</typeparam>
    public interface IExceptionHandler<in TException> where TException : Exception
    {
        /// <summary>
        /// Handles an exception of <typeparamref name="TException"/>.
        /// </summary>
        /// <param name="exception">Exception to handle.</param>
        /// <param name="context">Context information.</param>
        /// <returns>A machine-readable format for specifying errors in HTTP API responses based on https://tools.ietf.org/html/rfc7807. </returns>
        Task<ProblemDetails> Handle(TException exception, ExceptionHandlerContext context);
    }
}