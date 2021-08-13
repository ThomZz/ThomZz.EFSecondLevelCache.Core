using System;
using System.Collections.Generic;
using CacheManager.Core;
using EFSecondLevelCache.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace EFSecondLevelCache.Core
{
    /// <summary>
    /// ServiceCollection Extensions
    /// </summary>
    public static class EFServiceCollectionExtensions
    {

        /// <summary>
        /// Indicates whether or not if the EFSecondLevelCache required services has been added successfully.
        /// </summary>
        public static bool Added { get; private set; } = false;

        /// <summary>
        /// Registers the required services of the EFSecondLevelCache.Core.
        /// </summary>
        public static IServiceCollection AddEFSecondLevelCache(this IServiceCollection services)
        {
            services.AddSingleton<IEFCacheKeyHashProvider, EFCacheKeyHashProvider>();
            services.AddSingleton<IEFCacheKeyProvider, EFCacheKeyProvider>();
            services.AddSingleton<IEFCacheServiceProvider, EFCacheServiceProvider>();

            Added = true;

            return services;
        }
    }
}