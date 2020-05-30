using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
//using Newtonsoft.Json;

namespace Extensions.ExceptionHandling
{
    /// <summary>
    /// Exposes extension methods offered by the Extensions.ExceptionHandling library.
    /// </summary>
    public static class ExceptionHandlerExtensions
    {
        private static readonly Type CachedExceptionType = typeof(Exception);
        private static readonly Type CachedGenericExceptionHandlerInterfaceType = typeof(IExceptionHandler<>);

        /// <summary>
        /// Adds all exception handlers from assemblies specified in <paramref name="assemblies"/>.
        /// Defaults to current assembly when <paramref name="assemblies"/> is empty.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to add the service to.</param>
        /// <param name="assemblies">The assemblies search for and add exception handlers from.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="serviceCollection"/> is null.</exception>
        /// <exception cref="InvalidOperationException">When <paramref name="assemblies"/> contains duplicate exception handlers.</exception>
        public static IServiceCollection AddExceptionHandlers(this IServiceCollection serviceCollection, params Assembly[] assemblies)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            assemblies.DefaultIfEmpty(Assembly.GetCallingAssembly())
                .Where(x => x != null)
                .GetConcreteExceptionHandlers()
                .ToExceptionHandlerTypeMaps()
                .EnsureNoDuplicateHandlers(serviceCollection)
                .AddTransient(serviceCollection);

            serviceCollection.TryAddTransient<IExceptionHandlerOrchestrator, ExceptionHandlerOrchestrator>();

            return serviceCollection;
        }

        /// <summary>
        /// Adds all exception handlers from <paramref name="concreteHandlerTypes"/>.
        /// Throws an <see cref="InvalidOperationException"/> when called with invalid types.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to add the service to.</param>
        /// <param name="concreteHandlerTypes"></param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="serviceCollection"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// When <paramref name="concreteHandlerTypes"/> contains non exception handler types, or non-instantiable types, or duplicate exception handlers.
        /// </exception>
        public static IServiceCollection AddExceptionHandlersByTypes(this IServiceCollection serviceCollection,
            params Type[] concreteHandlerTypes)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            var inputTypes = concreteHandlerTypes
                .Where(x => x != null)
                .ToList();

            inputTypes.EnsureInstantiability()
                .ToExceptionHandlerTypeMaps()
                .EnsureSuccessfulConversionForAllTypes(inputTypes)
                .EnsureNoDuplicateHandlers(serviceCollection)
                .AddTransient(serviceCollection);

            serviceCollection.TryAddTransient<IExceptionHandlerOrchestrator, ExceptionHandlerOrchestrator>();

            return serviceCollection;
        }

        /// <summary>
        /// Adds middleware for globally handling exceptions through types implementing <see cref="IExceptionHandler{TException}"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IApplicationBuilder UseExceptionHandlerMiddleware(this IApplicationBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.UseMiddleware<ExceptionHandlerMiddleware>();
        }

        /// <summary>
        /// Configure a default overrideable invalid model state response adhering to RFC 7807.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to configure.</param>
        /// <param name="invalidModelStateResponseFactory">
        /// Delegate invoked on actions annotated with <see cref="ApiControllerAttribute" /> to convert invalid
        /// <see cref="ModelStateDictionary" /> into a <see cref="ProblemDetails" />.
        /// </param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IServiceCollection ConfigureInvalidModelStateResponse(this IServiceCollection serviceCollection, 
            Func<ActionContext, ProblemDetails> invalidModelStateResponseFactory = null)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            invalidModelStateResponseFactory = invalidModelStateResponseFactory ?? CreateValidationProblemDetails;

            return serviceCollection.Configure<ApiBehaviorOptions>(o =>
            {
                o.InvalidModelStateResponseFactory = actionContext =>
                {
                    var problemDetails = invalidModelStateResponseFactory(actionContext);
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    return new BadRequestObjectResult(problemDetails);
                };
            });
        }

        internal static async Task WriteJsonAsync(this HttpResponse response, object value)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(value);
            await response.WriteAsync(json);
        }

        internal static IEnumerable<object> GetExceptionHandlers(this IServiceProvider serviceProvider, Type exceptionType)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            return exceptionType?.GetInheritanceHierarchy()
                .Where(x => CachedExceptionType.IsAssignableFrom(x))
                .Select(x => CachedGenericExceptionHandlerInterfaceType.MakeGenericType(x))
                .Select(serviceProvider.GetService)
                .Where(x => x != null);
        }

        internal static async Task<T> FirstOrDefaultAsync<T>(this IEnumerable<Task<T>> tasks, Func<T, bool> predicate)
        {
            foreach (var task in tasks)
            {
                var result = await task;

                if (predicate(result))
                {
                    return result;
                }
            }

            return default;
        }

        private static IEnumerable<Type> GetInheritanceHierarchy(this Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                yield return current;
            }
        }

        private static bool IsGenericExceptionHandler(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == CachedGenericExceptionHandlerInterfaceType;
        }

        private static bool IsInstantiableClass(this Type x)
        {
            return x.IsClass && !x.IsAbstract && (!x.IsGenericType || x.IsConstructedGenericType);
        }

        private static IEnumerable<ExceptionHandlerTypeMap> EnsureNoDuplicateHandlers(
            this IEnumerable<ExceptionHandlerTypeMap> exceptionHandlerTypeMaps,
            IServiceCollection serviceCollection = null)
        {
            var inputMaps = exceptionHandlerTypeMaps as List<ExceptionHandlerTypeMap> ?? exceptionHandlerTypeMaps.ToList();

            var collectionMaps = serviceCollection?
                .Where(x => x.ServiceType.IsGenericExceptionHandler())
                .ToExceptionHandlerTypeMaps() ?? Enumerable.Empty<ExceptionHandlerTypeMap>();

            var duplicateHandlers = inputMaps
                .Concat(collectionMaps)
                .GroupBy(x => x.InterfaceHandlerType)
                .Where(x => x.Count() > 1)
                .ToList();

            if (!duplicateHandlers.Any())
            {
                return inputMaps;
            }

            var errorMessage = new StringBuilder("Invalid exception handlers detected. The following Exceptions has multiple handlers: ");
            duplicateHandlers.Aggregate(errorMessage, (builder, grouping) =>
                builder.AppendLine(
                    $"-{grouping.First().ExceptionType}: " +
                    $"[{string.Join(", ", grouping.Select(x => x.ConcreteHandlerType.FullName))}]"));

            throw new InvalidOperationException(errorMessage.ToString());
        }

        private static IEnumerable<ExceptionHandlerTypeMap> EnsureSuccessfulConversionForAllTypes(
            this IEnumerable<ExceptionHandlerTypeMap> exceptionHandlerTypeMaps,
            IEnumerable<Type> originalConcreteHandlerTypes)
        {
            var input = exceptionHandlerTypeMaps as List<ExceptionHandlerTypeMap> ?? exceptionHandlerTypeMaps.ToList();

            var typesNotImplementingHandler = originalConcreteHandlerTypes
                .Except(input
                    .Select(x => x.ConcreteHandlerType)
                    .Distinct())
                .ToList();

            if (typesNotImplementingHandler.Any())
            {
                throw new InvalidOperationException(
                    "Invalid exception handlers detected. The following types does not implement IExceptionHandler<T>: " +
                    $"[{string.Join(", ", typesNotImplementingHandler.Select(x => $"{x.FullName}"))}]");
            }

            return input;
        }

        private static IEnumerable<ExceptionHandlerTypeMap> ToExceptionHandlerTypeMaps(this IEnumerable<Type> concreteHandlerTypes)
        {
            return concreteHandlerTypes
                .SelectMany(x => x.GetInterfaces()
                    .Where(IsGenericExceptionHandler)
                    .Select(y => new ExceptionHandlerTypeMap
                    {
                        ConcreteHandlerType = x,
                        InterfaceHandlerType = y,
                        ExceptionType = y.GetGenericArguments().First(),
                    }));
        }

        private static IEnumerable<ExceptionHandlerTypeMap> ToExceptionHandlerTypeMaps(this IEnumerable<ServiceDescriptor> serviceDescriptors)
        {
            return serviceDescriptors
                .Select(x => new ExceptionHandlerTypeMap
                {
                    InterfaceHandlerType = x.ServiceType,
                    ConcreteHandlerType = x.ImplementationType,
                    ExceptionType = x.ServiceType.GetGenericArguments().First()
                });
        }

        private static IEnumerable<Type> EnsureInstantiability(this IEnumerable<Type> concreteHandlerTypes)
        {
            var input = concreteHandlerTypes as List<Type> ?? concreteHandlerTypes.ToList();

            var nonInstantiable = input
                .Where(x => !x.IsInstantiableClass())
                .ToList();

            if (nonInstantiable.Any())
            {
                throw new InvalidOperationException(
                    "Invalid exception handlers detected. The following types are not Instantiable: " +
                    $"[{string.Join(", ", nonInstantiable.Select(x => $"{x.FullName}"))}]");
            }

            return input;
        }

        private static void AddTransient(this IEnumerable<ExceptionHandlerTypeMap> exceptionHandlerTypeMaps,
            IServiceCollection serviceCollection)
        {
            var maps = exceptionHandlerTypeMaps as List<ExceptionHandlerTypeMap>
                       ?? exceptionHandlerTypeMaps.ToList();

            maps.ForEach(x => serviceCollection.AddTransient(
                x.InterfaceHandlerType,
                x.ConcreteHandlerType));
        }

        private static IEnumerable<Type> GetConcreteExceptionHandlers(this IEnumerable<Assembly> assemblies)
        {
            return assemblies.SelectMany(x => x.GetTypes())
                .Where(IsInstantiableClass)
                .Where(x => x.GetInterfaces().Any(IsGenericExceptionHandler));
        }

        private static ValidationProblemDetails CreateValidationProblemDetails(ActionContext x)
        {
            return new ValidationProblemDetails(x.ModelState)
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "See errors for details.",
                Instance = x.HttpContext.TraceIdentifier
            };
        }

        private class ExceptionHandlerTypeMap
        {
            public Type ConcreteHandlerType { get; set; }
            public Type InterfaceHandlerType { get; set; }
            public Type ExceptionType { get; set; }
        }
    }
}
