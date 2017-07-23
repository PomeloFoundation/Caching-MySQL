using Microsoft.Extensions.Options;

namespace Pomelo.Extensions.Caching.MySql.Tests
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
