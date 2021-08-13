using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace EFSecondLevelCache.Core
{
    /// <summary>
    /// A singleton App ServiceProvider Accessor.
    /// It's required for static `Cacheable()` methods.
    /// </summary>
    public static class EFStaticServiceProviderAccessor
    {

        private static IServiceProvider _serviceProvider = null;
        
        /// <summary>
        /// The application main service provider.
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get
            {
                return _serviceProvider ?? 
                    throw new InvalidOperationException("Please make sure `ConfigureEFSecondLevelCache()` extension method has been called on your `IApplicationBuilder`.");
            }
            private set
            {
                _serviceProvider = value;
            }
        }

        /// <summary>
        /// Configure ServiceActivator with full serviceProvider
        /// </summary>
        /// <param name="serviceProvider"></param>
        public static void Configure(IServiceProvider serviceProvider)
        {
            if (!EFServiceCollectionExtensions.Added) throw new InvalidOperationException("Please make sure `AddEFSecondLevelCache()` extension method has been called on your `IServiceCollection`.");
            ServiceProvider = serviceProvider;
        }

        /// <summary>
        /// Get a service scope. It is usually a good pratice to dispose it once done with it.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public static IServiceScope GetScope(IServiceProvider serviceProvider = null)
        {
            var provider = serviceProvider ?? ServiceProvider;

            return provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        }
    }
}