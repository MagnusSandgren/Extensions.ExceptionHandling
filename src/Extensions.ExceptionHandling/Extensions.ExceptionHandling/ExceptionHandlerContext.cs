namespace Extensions.ExceptionHandling
{
    /// <summary>
    /// DTO containing useful information injected from the exception handler library.
    /// </summary>
    public class ExceptionHandlerContext
    {
        /// <summary>
        /// Unique identifier to represent this request in trace logs. 
        /// </summary>
        public string TraceId { get; }

        internal ExceptionHandlerContext(string traceId)
        {
            TraceId = traceId;
        }
    }
}