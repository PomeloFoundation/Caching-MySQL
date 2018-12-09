// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

namespace Pomelo.Extensions.Caching.MySql
{
	internal static class Columns
    {
        public static class Names
        {
            public const string CacheItemId = "Id";
            public const string CacheItemValue = "Value";
            public const string ExpiresAtTime = "ExpiresAtTime";
            public const string SlidingExpirationInSeconds = "SlidingExpirationInSeconds";
            public const string AbsoluteExpiration = "AbsoluteExpiration";
        }

        public static class Indexes
        {
            // The value of the following index positions is dependent on how the MySql queries
            // are selecting the columns.
            public const int CacheItemIdIndex = 0;
            public const int ExpiresAtTimeIndex = 1;
            public const int SlidingExpirationInSecondsIndex = 2;
            public const int AbsoluteExpirationIndex = 3;
            public const int CacheItemValueIndex = 4;
        }
    }
}
