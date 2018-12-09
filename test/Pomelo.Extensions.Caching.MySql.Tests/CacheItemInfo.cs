// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using System;

namespace Pomelo.Extensions.Caching.MySql.Tests
{
	public class CacheItemInfo
    {
        public string Id { get; set; }

        public byte[] Value { get; set; }

        public DateTimeOffset ExpiresAtTime { get; set; }

        public TimeSpan? SlidingExpirationInSeconds { get; set; }

        public DateTimeOffset? AbsoluteExpiration { get; set; }
    }
}
