

# ThomZz.EFSecondLevelCache.Core

Entity Framework Core 5 Second Level Caching Library.

Second level caching is a query cache. The results of EF commands will be stored in the cache, so that the same EF commands will retrieve their data from the cache rather than executing them against the database again.

##  Little bit of context 
Directly forked from https://github.com/VahidN/EFSecondLevelCache.Core (Thanks!), i've modified it to support EFCore 5 (Still maybe have some serious work to do ...). Being well aware of the new version with interceptors (https://github.com/VahidN/EFCoreSecondLevelCacheInterceptor), that version didn't fit my need as i wanted to retrieve objects in the cache handles, and not DbReaders. It allows me to apply some specific predicates against cache entries, like the cached object type, or other more sophisticated conditions.

Supports for netstandard2.0 and .net456 has been dropped. Now Only supporting dotnetcore3.1.

## Install via NuGet

To install EFSecondLevelCache.Core, run the following command in the Package Manager Console:

[![Nuget](https://img.shields.io/nuget/v/ThomZz.EFSecondLevelCache.Core)](https://github.com/ThomZz/ThomZz.EFSecondLevelCache.Core)

```
PM> Install-Package ThomZz.EFSecondLevelCache.Core
```

You can also view the [package page](http://www.nuget.org/packages/ThomZz.EFSecondLevelCache.Core/) on NuGet.

This library also uses the [CacheManager.Core](https://github.com/MichaCo/CacheManager), as a highly configurable cache manager.
To use its in-memory caching mechanism, add these entries to the `.csproj` file:

```xml
  <ItemGroup>
    <PackageReference Include="ThomZz.EFSecondLevelCache.Core" Version="1.0.0" />
    <PackageReference Include="CacheManager.Core" Version="1.2.0" />
    <PackageReference Include="CacheManager.Microsoft.Extensions.Caching.Memory" Version="1.2.0" />
    <PackageReference Include="CacheManager.Serialization.Json" Version="1.2.0" />
  </ItemGroup>
```

And to get the latest versions of these libraries you can run the following command in the Package Manager Console:

```
PM> Update-Package
```

## Usage

1- [Register the required services](/src/Tests/EFSecondLevelCache.Core.AspNetCoreSample/Startup.cs) of `ThomZz.EFSecondLevelCache.Core` and also `CacheManager.Core`

```csharp
namespace EFSecondLevelCache.Core.AspNetCoreSample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddEFSecondLevelCache();

            // Add an in-memory cache service provider
            services.AddSingleton(typeof(ICacheManager<>), typeof(BaseCacheManager<>));
            services.AddSingleton(typeof(ICacheManagerConfiguration),
                new CacheManager.Core.ConfigurationBuilder()
                        .WithJsonSerializer()
                        .WithMicrosoftMemoryCacheHandle(instanceName: "MemoryCache1")
                        .WithExpiration(ExpirationMode.Absolute, TimeSpan.FromMinutes(10))
                        .Build());
        }
    }
}
```

If you want to use the Redis as the preferred cache provider, first install the `CacheManager.StackExchange.Redis` package and then register its required services:

```csharp
// Add Redis cache service provider
var jss = new JsonSerializerSettings
{
    NullValueHandling = NullValueHandling.Ignore,
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
};

const string redisConfigurationKey = "redis";
services.AddSingleton(typeof(ICacheManagerConfiguration),
    new CacheManager.Core.ConfigurationBuilder()
        .WithJsonSerializer(serializationSettings: jss, deserializationSettings: jss)
        .WithUpdateMode(CacheUpdateMode.Up)
        .WithRedisConfiguration(redisConfigurationKey, config =>
        {
            config.WithAllowAdmin()
                .WithDatabase(0)
                .WithEndpoint("localhost", 6379)
                // Enables keyspace notifications to react on eviction/expiration of items.
                // Make sure that all servers are configured correctly and 'notify-keyspace-events' is at least set to 'Exe', otherwise CacheManager will not retrieve any events.
                // See https://redis.io/topics/notifications#configuration for configuration details.
                .EnableKeyspaceEvents();
        })
        .WithMaxRetries(100)
        .WithRetryTimeout(50)
        .WithRedisCacheHandle(redisConfigurationKey)
        .WithExpiration(ExpirationMode.Absolute, TimeSpan.FromMinutes(10))
        .Build());
services.AddSingleton(typeof(ICacheManager<>), typeof(BaseCacheManager<>));
```

You'll also need to configure the EFSecondLevelCache on your IApplicationBuilder :

```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
{
    //...
    app.ConfigureEFSecondLevelCache()
    //...
}
```


2- [Setting up the cache invalidation](/src/Tests/EFSecondLevelCache.Core.AspNetCoreSample/DataLayer/SampleContext.cs) by overriding the SaveChanges method to prevent stale reads:

```csharp
namespace EFSecondLevelCache.Core.AspNetCoreSample.DataLayer
{
    public class SampleContext : DbContext
    {
        public SampleContext(DbContextOptions<SampleContext> options) : base(options)
        { }

        public virtual DbSet<Post> Posts { get; set; }

        public override int SaveChanges()
        {
            var changedEntityNames = this.GetChangedEntityNames();

            this.ChangeTracker.AutoDetectChangesEnabled = false; // for performance reasons, to avoid calling DetectChanges() again.
            var result = base.SaveChanges();
            this.ChangeTracker.AutoDetectChangesEnabled = true;

            this.GetService<IEFCacheServiceProvider>().InvalidateCacheDependencies(changedEntityNames);

            return result;
        }
    }
}
```

3- Then to cache the results of the normal queries like:

```csharp
var products = context.Products.Include(x => x.Tags).FirstOrDefault();
```

We can use the new `Cacheable()` extension method:

```csharp
// If you don't specify the `EFCachePolicy`, the global `new CacheManager.Core.ConfigurationBuilder().WithExpiration()` setting will be used automatically.
var products = context.Products.Include(x => x.Tags).Cacheable().FirstOrDefault(); // Async methods are supported too.

// Or you can specify the `EFCachePolicy` explicitly to override the global settings.
var post1 = context.Posts
                   .Where(x => x.Id > 0)
                   .OrderBy(x => x.Id)
                   .Cacheable(CacheExpirationMode.Sliding, TimeSpan.FromMinutes(5))
                   .FirstOrDefault();

// NOTE: It's better to add the `Cacheable()` method before the materialization methods such as `ToList()` or `FirstOrDefault()` to cover the whole expression tree.
```

Also AutoMapper's `ProjectTo()` method is supported:

```csharp
var posts = context.Posts
                   .Where(x => x.Id > 0)
                   .OrderBy(x => x.Id)
                   .Cacheable()
                   .ProjectTo<PostDto>(configuration: _mapper.ConfigurationProvider)
                   .ToList();
```

## Guidance

### When to use

Good candidates for query caching are global site settings and public data, such as infrequently changing articles or comments. It can also be beneficial to cache data specific to a user so long as the cache expires frequently enough relative to the size of the user base that memory consumption remains acceptable. Small, per-user data that frequently exceeds the cache's lifetime, such as a user's photo path, is better held in user claims, which are stored in cookies, than in this cache.

### Scope

This cache is scoped to the application, not the current user. It does not use session variables. Accordingly, when retriveing cached per-user data, be sure queries in include code such as `.Where(x => .... && x.UserId == id)`.

### Invalidation

This cache is updated when an entity is changed (insert, update, or delete) via a DbContext that uses this library. If the database is updated through some other means, such as a stored procedure or trigger, the cache becomes stale.
