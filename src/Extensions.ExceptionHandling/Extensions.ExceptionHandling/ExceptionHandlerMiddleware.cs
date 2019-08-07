using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.ExceptionHandling
{
    internal sealed class ExceptionHandlerMiddleware
    {
        private readonly IExceptionHandlerOrchestrator _orchestrator;
        private readonly RequestDelegate _next;

        public ExceptionHandlerMiddleware(RequestDelegate next, IExceptionHandlerOrchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception exception)
            {
                if (!await _orchestrator.TryHandleExceptionAsync(exception, httpContext))
                {
                    throw;
                }
            }
        }
    }
}