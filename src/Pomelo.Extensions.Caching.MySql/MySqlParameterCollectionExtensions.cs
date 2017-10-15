// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Pomelo.Data.MySql;
using System;

namespace Pomelo.Extensions.Caching.MySql
{
	/// <summary>
	/// TODO: make this class internal again
	/// </summary>
	internal static class MySqlParameterCollectionExtensions
    {
        // For all values where the length is less than the below value, try setting the size of the
        // parameter for better performance.
        public const int DefaultValueColumnWidth = 8000;

		// The index key prefix length limit is 767 bytes for InnoDB tables that use the REDUNDANT or COMPACT row format.
		// That is why we are using 'CHARACTER SET ascii COLLATE ascii_bin' column and index
		// https://dev.mysql.com/doc/refman/5.7/en/innodb-restrictions.html
		public const int CacheItemIdColumnWidth = 449;

        public static MySqlParameterCollection AddCacheItemId(this MySqlParameterCollection parameters, string value)
        {
            return parameters.AddWithValue(Columns.Names.CacheItemId, MySqlDbType.VarChar, CacheItemIdColumnWidth, value);
        }

        public static MySqlParameterCollection AddCacheItemValue(this MySqlParameterCollection parameters, byte[] value)
        {
            if (value != null && value.Length < DefaultValueColumnWidth)
            {
                return parameters.AddWithValue(
                    Columns.Names.CacheItemValue,
                    MySqlDbType.VarBinary,
                    DefaultValueColumnWidth,
                    value);
            }
            else
            {
                // do not mention the size
                return parameters.AddWithValue(Columns.Names.CacheItemValue, MySqlDbType.VarBinary, value);
            }
        }

        public static MySqlParameterCollection AddSlidingExpirationInSeconds(
            this MySqlParameterCollection parameters,
            TimeSpan? value)
        {
            if (value.HasValue)
            {
                return parameters.AddWithValue(
                    Columns.Names.SlidingExpirationInSeconds, MySqlDbType.Int64, value.Value.TotalSeconds);
            }
            else
            {
                return parameters.AddWithValue(Columns.Names.SlidingExpirationInSeconds, MySqlDbType.Int64, DBNull.Value);
            }
        }

        public static MySqlParameterCollection AddAbsoluteExpiration(
            this MySqlParameterCollection parameters,
            DateTime? utcTime)
        {
            if (utcTime.HasValue)
            {
                return parameters.AddWithValue(
                    Columns.Names.AbsoluteExpiration, MySqlDbType.DateTime, utcTime.Value);
            }
            else
            {
                return parameters.AddWithValue(
                    Columns.Names.AbsoluteExpiration, MySqlDbType.DateTime, DBNull.Value);
            }
        }

        public static MySqlParameterCollection AddWithValue(
            this MySqlParameterCollection parameters,
            string parameterName,
            MySqlDbType dbType,
            object value)
        {
            var parameter = new MySqlParameter(parameterName, dbType);
            parameter.Value = value;
            parameters.Add(parameter);
            parameter.ResetDbType();
            return parameters;
        }

        public static MySqlParameterCollection AddWithValue(
            this MySqlParameterCollection parameters,
            string parameterName,
            MySqlDbType dbType,
            int size,
            object value)
        {
            var parameter = new MySqlParameter(parameterName, dbType, size);
            parameter.Value = value;
            parameters.Add(parameter);
            parameter.ResetDbType();
            return parameters;
        }
    }
}
