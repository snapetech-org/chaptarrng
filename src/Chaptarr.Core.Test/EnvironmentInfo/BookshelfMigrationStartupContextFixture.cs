using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;

namespace Chaptarr.Core.Test.EnvironmentInfo
{
    [TestFixture]
    public class BookshelfMigrationStartupContextFixture
    {
        [Test]
        public void should_recognize_explicit_bookshelf_migration_flag()
        {
            var context = new StartupContext("-migrate-bookshelf");

            Assert.That(context.MigrateBookshelf, Is.True);
            Assert.That(context.PreviewBookshelfMigration, Is.False);
        }

        [Test]
        public void should_recognize_preview_bookshelf_migration_flag()
        {
            var context = new StartupContext("--preview-bookshelf-migration");

            Assert.That(context.PreviewBookshelfMigration, Is.True);
            Assert.That(context.MigrateBookshelf, Is.False);
        }
    }
}
