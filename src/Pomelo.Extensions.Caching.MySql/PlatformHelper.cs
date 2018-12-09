// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using System;

namespace Pomelo.Extensions.Caching.MySql
{
	internal static class PlatformHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);

        public static bool IsMono
        {
            get
            {
                return _isMono.Value;
            }
        }
    }
}