using System.Linq;
using System.Linq.Expressions;
using EFSecondLevelCache.Core.Contracts;
using System;
using CacheManager.Core;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace EFSecondLevelCache.Core
{
    /// <summary>
    /// A custom cache key provider for EF queries.
    /// </summary>
    public class EFCacheKeyProvider : IEFCacheKeyProvider
    {
        private static readonly TypeInfo _queryCompilerTypeInfo = typeof(QueryCompiler).GetTypeInfo();

        private static readonly FieldInfo _queryCompilerField =
            typeof(EntityQueryProvider).GetTypeInfo().DeclaredFields.First(x => x.Name == "_queryCompiler");

        private static readonly FieldInfo _queryContextFactoryField =
            _queryCompilerTypeInfo.DeclaredFields.First(x => x.Name == "_queryContextFactory");

        private static readonly FieldInfo _loggerField =
            _queryCompilerTypeInfo.DeclaredFields.First(x => x.Name == "_logger");

        private static readonly TimeSpan _slidingExpirationTimeSpan = TimeSpan.FromMinutes(7);

        private readonly ICacheManager<EFCacheKey> _keysCacheManager;
        private readonly IEFCacheKeyHashProvider _cacheKeyHashProvider;

        /// <summary>
        /// A custom cache key provider for EF queries.
        /// </summary>
        /// <param name="cacheKeyHashProvider">Provides the custom hashing algorithm.</param>
        /// <param name="keysCacheManager">Provides the EFCacheKey cache manager.</param>
        public EFCacheKeyProvider(IEFCacheKeyHashProvider cacheKeyHashProvider, ICacheManager<EFCacheKey> keysCacheManager)
        {
            _cacheKeyHashProvider = cacheKeyHashProvider;
            _keysCacheManager = keysCacheManager;
        }

        /// <summary>
        /// Gets an EF query and returns its hashed key to store in the cache.
        /// </summary>
        /// <param name="query">The EF query.</param>
        /// <param name="expression">An expression tree that represents a LINQ query.</param>
        /// <param name="saltKey">If you think the computed hash of the query is not enough, set this value.</param>
        /// <returns>Information of the computed key of the input LINQ query.</returns>
        public EFCacheKey GetEFCacheKey(IQueryable query, Expression expression, string saltKey = "")
        {
            var queryCompiler = (QueryCompiler)_queryCompilerField.GetValue(query.Provider);
            var (expressionKeyHash, modifiedExpression) = GetExpressionKeyHash(queryCompiler, _cacheKeyHashProvider, expression);
            var cachedKey = _keysCacheManager.Get<EFCacheKey>(expressionKeyHash);
            if (cachedKey != null)
            {
                return cachedKey;
            }

            var expressionPrinter = new ExpressionPrinter();
            var sql = expressionPrinter.PrintDebug(modifiedExpression);

            var expressionVisitorResult = EFQueryExpressionVisitor.GetDebugView(expression);
            var key = $"{sql};{expressionVisitorResult.DebugView};{saltKey}";
            var keyHash = _cacheKeyHashProvider.ComputeHash(key);

            var cacheKey = new EFCacheKey
            {
                Key = key,
                KeyHash = keyHash,
                CacheDependencies = expressionVisitorResult.Types
            };
            SetCache(expressionKeyHash, cacheKey);
            return cacheKey;
        }

        private void SetCache(string expressionKeyHash, EFCacheKey value)
        {
            _keysCacheManager.Add(
                new CacheItem<EFCacheKey>(expressionKeyHash, value, ExpirationMode.Sliding, _slidingExpirationTimeSpan));
        }

        private (string ExpressionKeyHash, Expression ModifiedExpression) GetExpressionKeyHash(
            QueryCompiler queryCompiler,
            IEFCacheKeyHashProvider cacheKeyHashProvider,
            Expression expression)
        {
            var queryContextFactory = (IQueryContextFactory)_queryContextFactoryField.GetValue(queryCompiler);
            var queryContext = queryContextFactory.Create();
            var logger = (IDiagnosticsLogger<DbLoggerCategory.Query>)_loggerField.GetValue(queryCompiler);
            expression = queryCompiler.ExtractParameters(expression, queryContext, logger, parameterize: false);

            var expressionKey = $"{ExpressionEqualityComparer.Instance.GetHashCode(expression)};";
            var parameterValues = queryContext.ParameterValues;
            if (parameterValues.Any())
            {
                expressionKey = parameterValues.Aggregate(expressionKey,
                    (current, item) => current + $"{item.Key}={item.Value?.GetHashCode()};");
            }

            return (cacheKeyHashProvider.ComputeHash(expressionKey), expression);
        }
    }
}