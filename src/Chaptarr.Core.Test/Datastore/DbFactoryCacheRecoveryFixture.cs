using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Exceptions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class DbFactoryCacheRecoveryFixture
    {
        private const string CachePath = "/data/cache.db";

        private Mock<IMigrationController> _migrations;
        private Mock<IConnectionStringFactory> _connections;
        private Mock<IDiskProvider> _disk;
        private DbFactory _factory;

        [SetUp]
        public void Setup()
        {
            _migrations = new Mock<IMigrationController>();
            _connections = new Mock<IConnectionStringFactory>();
            _disk = new Mock<IDiskProvider>();

            var info = new DatabaseConnectionInfo(DatabaseType.SQLite, "Data Source=" + CachePath);
            _connections.SetupGet(c => c.CacheDbConnection).Returns(info);
            _connections.SetupGet(c => c.MainDbConnection).Returns(info);
            _connections.Setup(c => c.GetDatabasePath(It.IsAny<string>())).Returns(CachePath);

            _factory = new DbFactory(_migrations.Object, _connections.Object, _disk.Object, Mock.Of<IRestoreDatabase>());
        }

        private void FailMigrations(int times)
        {
            var remaining = times;

            _migrations
                .Setup(m => m.Migrate(It.IsAny<string>(), It.IsAny<MigrationContext>(), It.IsAny<DatabaseType>()))
                .Callback(() =>
                {
                    if (remaining-- > 0)
                    {
                        throw new InvalidOperationException("migration failed");
                    }
                });
        }

        [Test]
        public void should_recreate_cache_database_when_migration_fails_once()
        {
            FailMigrations(1);

            Assert.DoesNotThrow(() => _factory.Create(MigrationType.Cache));

            _migrations.Verify(m => m.Migrate(It.IsAny<string>(), It.IsAny<MigrationContext>(), DatabaseType.SQLite), Times.Exactly(2));
            _disk.Verify(d => d.DeleteFile(CachePath), Times.Once());
            _disk.Verify(d => d.DeleteFile(CachePath + "-wal"), Times.Once());
            _disk.Verify(d => d.DeleteFile(CachePath + "-shm"), Times.Once());
        }

        [Test]
        public void should_not_touch_cache_database_when_migration_succeeds()
        {
            FailMigrations(0);

            _factory.Create(MigrationType.Cache);

            _migrations.Verify(m => m.Migrate(It.IsAny<string>(), It.IsAny<MigrationContext>(), It.IsAny<DatabaseType>()), Times.Once());
            _disk.Verify(d => d.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_throw_when_cache_migration_fails_after_recreation()
        {
            FailMigrations(2);

            Assert.Throws<ChaptarrStartupException>(() => _factory.Create(MigrationType.Cache));

            _migrations.Verify(m => m.Migrate(It.IsAny<string>(), It.IsAny<MigrationContext>(), It.IsAny<DatabaseType>()), Times.Exactly(2));
        }

        [Test]
        public void should_not_delete_main_database_when_its_migration_fails()
        {
            FailMigrations(1);

            Assert.Throws<ChaptarrStartupException>(() => _factory.Create(MigrationType.Main));

            _disk.Verify(d => d.DeleteFile(It.IsAny<string>()), Times.Never());
        }
    }
}
