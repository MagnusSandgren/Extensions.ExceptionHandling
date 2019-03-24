# Introduction 
This is a simple litle library built to ensure consistant global error handling, supporting [RFC 7807](https://tools.ietf.org/html/rfc7807) in ASP .net core APIs.

Traditionaly to handle errors globaly in ASP, one would create an error handling middleware and wrap the next delegate in a try-catch. Each globaly handled exception would get its own catch block, with a default "catch-all exceptions" in the last block. Adding a new exception handling to the mix would be as simple as adding another catch in your try-catch sandwitch just at the right place, and write an adiquite response through `HttpContext.Response`. 

This approch works, however there are some downsides. For one it violates SOLIDs open-closed principle. Check out the principle if you're wondring why it's a bad thing. 

Another downside is that it gives no guide for the developer as to what to return to the client through `HttpContext.Response`. This opens the posibility for unconsistancy in the error response format as the developers are free to modify the http response any way they see fit. Which potentially makes it harder to consume your API as your clients would need to know about, deserialize, and act upon multiple error response formats.

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
            Title = "An unexpected error occured.",
            Detail = "Something went wrong during the request. Please contact " +
                     "system administrators with the Instance value.",
            Instance = context.TraceId
        });
    }
}
```

<!-- # Contribute
TODO: Explain how other users and developers can contribute to make your code better. 

If you want to learn more about creating good readme files then refer the following [guidelines](https://www.visualstudio.com/en-us/docs/git/create-a-readme). You can also seek inspiration from the below readme files:
- [ASP.NET Core](https://github.com/aspnet/Home)
- [Visual Studio Code](https://github.com/Microsoft/vscode)
- [Chakra Core](https://github.com/Microsoft/ChakraCore) -->

# License
Copyright © 2019 Magnus Sandgren. All rights reserved.

Licensed under the [MIT license](LICENSE.txt).