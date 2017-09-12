// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using System.Threading.Tasks;

namespace Pomelo.Extensions.Caching.MySql
{
	internal interface IDatabaseOperations
    {
        byte[] GetCacheItem(string key);

        Task<byte[]> GetCacheItemAsync(string key, CancellationToken token = default(CancellationToken));

        void RefreshCacheItem(string key);

        Task RefreshCacheItemAsync(string key, CancellationToken token = default(CancellationToken));

        void DeleteCacheItem(string key);

        Task DeleteCacheItemAsync(string key, CancellationToken token = default(CancellationToken));

        void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options);

        Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken));

        Task<int> DeleteExpiredCacheItemsAsync();

        Task<int> DeleteExpiredCacheItemsAsync();

		int DeleteExpiredCacheItems();
	}
}