// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using Microsoft.Extensions.Caching.Distributed;
using Pomelo.Extensions.Caching.MySql;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for setting up MySQL Server distributed cache services in an <see cref="IServiceCollection" />.
    /// </summary>
    /// TODO: make this internal again
    public static class MySqlServerCachingServicesExtensions
    {
        /// <summary>
        /// Adds MySQL Server distributed caching services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="setupAction">An <see cref="Action{MySqlCacheOptions}"/> to configure the provided <see cref="MySqlCacheOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddDistributedMySqlCache(this IServiceCollection services, Action<MySqlCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            AddMySqlCacheServices(services);
            services.Configure(setupAction);

            return services;
        }

		// to enable unit testing
		/// TODO: make this internal again
		internal static void AddMySqlCacheServices(IServiceCollection services)
        {
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, MySqlCache>());
        }
    }
}