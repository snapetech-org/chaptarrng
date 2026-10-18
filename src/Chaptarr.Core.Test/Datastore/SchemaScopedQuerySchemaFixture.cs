using System.Collections.Generic;
using FluentMigrator;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class SchemaScopedQuerySchemaFixture
    {
        [Test]
        public void should_apply_the_active_schema_to_unqualified_queries_and_preserve_explicit_schemas()
        {
            var aliases = new List<string> { "postgres" };
            var inner = new Mock<IQuerySchema>(MockBehavior.Strict);
            inner.SetupGet(query => query.DatabaseType).Returns("PostgreSQL");
            inner.SetupGet(query => query.DatabaseTypeAliases).Returns(aliases);
            inner.Setup(query => query.SchemaExists("custom")).Returns(true);
            inner.Setup(query => query.TableExists("staging", "Books")).Returns(true);
            inner.Setup(query => query.TableExists("custom", "Books")).Returns(true);
            inner.Setup(query => query.ColumnExists("staging", "Books", "Title")).Returns(true);
            inner.Setup(query => query.ConstraintExists("staging", "Books", "PK_Books")).Returns(true);
            inner.Setup(query => query.IndexExists("staging", "Books", "IX_Books_Title")).Returns(true);
            inner.Setup(query => query.SequenceExists("staging", "Books_Id_seq")).Returns(true);
            inner.Setup(query => query.DefaultValueExists("staging", "Books", "Monitored", false)).Returns(true);

            var querySchema = new SchemaScopedQuerySchema(inner.Object, "staging");

            Assert.Multiple(() =>
            {
                Assert.That(querySchema.DatabaseType, Is.EqualTo("PostgreSQL"));
                Assert.That(querySchema.DatabaseTypeAliases, Is.SameAs(aliases));
                Assert.That(querySchema.SchemaExists("custom"), Is.True);
                Assert.That(querySchema.TableExists(null, "Books"), Is.True);
                Assert.That(querySchema.TableExists("custom", "Books"), Is.True);
                Assert.That(querySchema.ColumnExists(null, "Books", "Title"), Is.True);
                Assert.That(querySchema.ConstraintExists(null, "Books", "PK_Books"), Is.True);
                Assert.That(querySchema.IndexExists(null, "Books", "IX_Books_Title"), Is.True);
                Assert.That(querySchema.SequenceExists(null, "Books_Id_seq"), Is.True);
                Assert.That(querySchema.DefaultValueExists(null, "Books", "Monitored", false), Is.True);
            });
        }
    }
}
