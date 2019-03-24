# Introduction 
This is a simple little library built to ensure consistent global error handling, supporting [RFC 7807](https://tools.ietf.org/html/rfc7807) in ASP .net core APIs.

Traditionally to handle errors globally in ASP, one would create an error handling middleware and wrap the next delegate in a try-catch. Each globally handled exception would get its own catch block, with a default "catch-all exceptions" in the last block. Adding a new exception handling to the mix would be as simple as adding another catch in your try-catch sandwich just at the right place, and write an adequate response through `HttpContext.Response`. 

This approach works, however there are some downsides. For one it violates SOLIDs open-closed principle. Check out the principle if you're wondering why it's a bad thing. 

Another downside is that it gives no guide for the developer as to what to return to the client through `HttpContext.Response`. This opens the possibility for inconsistency in the error response format as the developers are free to modify the http response any way they see fit. Which potentially makes it harder to consume your API as your clients would need to know about, deserialize, and act upon multiple error response formats.

# Getting Started
Install the library through the package manager console:
```
PM> Install-Package PACKAGE NAME HERE.
```

Then register the exception handlers through Startup.ConfigureServices:
```csharp
// Adds all non-abstract classes implementing IExceptionHandler<TException>
// in the current assembly.
services.AddExceptionHandlers();

// OR

// Adds all non-abstract classes implementing IExceptionHandler<TException>
// in the target assembly. You can add multiple assemblies. 
services.AddExceptionHandlers(typeof(ExceptionHandler).Assembly);

// OR

// Manually add exception handlers by type. You can add multiple types.
services.AddExceptionHandlersByTypes(typeof(ExceptionHandler));
```

Next add the middleware through Startup.Configure:
```csharp
app.UseExceptionHandlerMiddleware();
```

Finally create your exception handlers by implementing `IExceptionHandler<TException>`. The following code shows an example of an exception handler handing Exceptions:
```csharp
public class ExceptionHandler : IExceptionHandler<Exception>
{
    public Task<ProblemDetails> Handle(Exception exception, ExceptionHandlerContext context)
    {
        return Task.FromResult(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Something went wrong during the request. Please contact " +
                     "system administrators with the Instance value.",
            Instance = context.TraceId
        });
    }
}
```

Alternatively you can create an exception handler by inheriting `AbstractLoggerExceptionHandler<TSelf, TException>` like so:

```csharp
public class ExceptionHandler : AbstractLoggerExceptionHandler<ExceptionHandler, Exception>
{
    public ExceptionHandler(ILogger<ExceptionHandler> logger) : base(logger)
    {
    }

    public override Task<ProblemDetails> Handle(Exception exception, ExceptionHandlerContext context)
    {
        return Task.FromResult(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Something went wrong during the request. Please contact " +
                     "system administrators with the Instance value.",
            Instance = context.TraceId
        });
    }
}
```

# License
Copyright © 2019 Magnus Sandgren. All rights reserved.

Licensed under the [MIT license](LICENSE.txt).