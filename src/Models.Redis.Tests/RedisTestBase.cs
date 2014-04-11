using NUnit.Framework;
using OrigoDB.Models.Redis;

namespace Models.Redis.Tests
{
    public abstract class RedisTestBase
    {
        protected RedisModel _target;

        [SetUp]
        public void Setup()
        {
            _target = new RedisModel();
        }
    }
}