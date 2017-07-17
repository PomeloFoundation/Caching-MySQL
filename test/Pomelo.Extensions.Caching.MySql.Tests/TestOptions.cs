using Microsoft.Extensions.Options;
using Pomelo.Extensions.Caching.MySql;

namespace Microsoft.Extensions.Caching.SqlServer
{
    internal class TestMySqlCacheOptions : IOptions<MySqlCacheOptions>
    {
        private readonly MySqlCacheOptions _innerOptions;

        public TestMySqlCacheOptions(MySqlCacheOptions innerOptions)
        {
            _innerOptions = innerOptions;
        }

        public MySqlCacheOptions Value
        {
            get
            {
                return _innerOptions;
            }
        }
    }
}
