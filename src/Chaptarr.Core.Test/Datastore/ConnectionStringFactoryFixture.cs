using System.IO;
using FluentAssertions;
using Moq;
using Npgsql;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Exceptions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class ConnectionStringFactoryFixture
    {
        private const string Secret = "s3cret-value";

        private Mock<IConfigFileProvider> _config;
        private Mock<IAppFolderInfo> _folders;

        [SetUp]
        public void Setup()
        {
            _config = new Mock<IConfigFileProvider>();
            _config.SetupGet(c => c.PostgresHost).Returns("db.example");
            _config.SetupGet(c => c.PostgresPort).Returns(5432);
            _config.SetupGet(c => c.PostgresUser).Returns("user");
            _config.SetupGet(c => c.PostgresPassword).Returns(Secret);
            _config.SetupGet(c => c.PostgresMainDb).Returns("main");
            _config.SetupGet(c => c.PostgresLogDb).Returns("log");
            _config.SetupGet(c => c.PostgresCacheDb).Returns("cache");
            _config.SetupGet(c => c.PostgresNoResetOnClose).Returns(true);

            _folders = new Mock<IAppFolderInfo>();
            _folders.Setup(f => f.GetDatabase()).Returns("/tmp/chaptarr.db");
            _folders.Setup(f => f.GetLogDatabase()).Returns("/tmp/logs.db");
            _folders.Setup(f => f.GetCacheDatabase()).Returns("/tmp/cache.db");
        }

        private ConnectionStringFactory Build() => new ConnectionStringFactory(_folders.Object, _config.Object);

        private static NpgsqlConnectionStringBuilder Parse(DatabaseConnectionInfo info) => new NpgsqlConnectionStringBuilder(info.ConnectionString);

        [Test]
        public void should_skip_connection_reset_on_all_postgres_databases_by_default()
        {
            var factory = Build();

            Parse(factory.MainDbConnection).NoResetOnClose.Should().BeTrue();
            Parse(factory.LogDbConnection).NoResetOnClose.Should().BeTrue();
            Parse(factory.CacheDbConnection).NoResetOnClose.Should().BeTrue();
        }

        [Test]
        public void should_allow_connection_reset_to_be_restored()
        {
            _config.SetupGet(c => c.PostgresNoResetOnClose).Returns(false);

            Parse(Build().MainDbConnection).NoResetOnClose.Should().BeFalse();
        }

        [Test]
        public void should_leave_tls_settings_untouched_when_not_configured()
        {
            var csb = Parse(Build().MainDbConnection);

            csb.SslMode.Should().Be(new NpgsqlConnectionStringBuilder().SslMode);
            csb.RootCertificate.Should().BeNull();
        }

        [TestCase("disable", SslMode.Disable)]
        [TestCase("prefer", SslMode.Prefer)]
        [TestCase("require", SslMode.Require)]
        [TestCase("verify-ca", SslMode.VerifyCA)]
        [TestCase("Verify-Full", SslMode.VerifyFull)]
        [TestCase("VerifyFull", SslMode.VerifyFull)]
        public void should_apply_ssl_mode_to_every_postgres_database(string value, SslMode expected)
        {
            _config.SetupGet(c => c.PostgresSslMode).Returns(value);

            var factory = Build();

            Parse(factory.MainDbConnection).SslMode.Should().Be(expected);
            Parse(factory.LogDbConnection).SslMode.Should().Be(expected);
            Parse(factory.CacheDbConnection).SslMode.Should().Be(expected);
        }

        [Test]
        public void should_reject_unknown_ssl_mode_without_leaking_password()
        {
            _config.SetupGet(c => c.PostgresSslMode).Returns("bogus");

            var ex = Assert.Throws<ChaptarrStartupException>(() => Build());

            ex.Message.Should().Contain("bogus");
            ex.Message.Should().NotContain(Secret);
        }

        [Test]
        public void should_apply_existing_root_certificate()
        {
            var path = Path.GetTempFileName();

            try
            {
                _config.SetupGet(c => c.PostgresRootCert).Returns(path);

                Parse(Build().CacheDbConnection).RootCertificate.Should().Be(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void should_fail_clearly_when_root_certificate_is_missing()
        {
            var path = Path.Combine(Path.GetTempPath(), "missing-root-" + System.Guid.NewGuid() + ".crt");
            _config.SetupGet(c => c.PostgresRootCert).Returns(path);

            var ex = Assert.Throws<ChaptarrStartupException>(() => Build());

            ex.Message.Should().Contain(path);
            ex.Message.Should().NotContain(Secret);
        }

        [Test]
        public void should_not_apply_postgres_settings_to_sqlite()
        {
            _config.SetupGet(c => c.PostgresHost).Returns(string.Empty);
            _config.SetupGet(c => c.PostgresSslMode).Returns("bogus");
            _config.SetupGet(c => c.PostgresRootCert).Returns("/nonexistent.crt");

            var factory = Build();

            factory.MainDbConnection.DatabaseType.Should().Be(DatabaseType.SQLite);
            factory.CacheDbConnection.DatabaseType.Should().Be(DatabaseType.SQLite);
        }
    }
}
