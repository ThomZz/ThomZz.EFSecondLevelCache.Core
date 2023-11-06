using Microsoft.AspNetCore.Builder;
using System;

namespace EFSecondLevelCache.Core
{
    /// <summary>
    /// ApplicationBuilder Extensions
    /// </summary>
    public static class EFApplicationBuilderExtensions
    {
        /// <summary>
        /// Configuration of EFSecondLevelCache.Core.
        /// </summary>
        public static IApplicationBuilder ConfigureEFSecondLevelCache(this IApplicationBuilder builder)
        {
            if (!EFServiceCollectionExtensions.Added) throw new InvalidOperationException("Please make sure `AddEFSecondLevelCache()` extension method has been called on your `IServiceCollection`.");
            EFStaticServiceProviderAccessor.Configure(builder.ApplicationServices);

            return builder;
        }
    }
}