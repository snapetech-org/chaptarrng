using System.Collections.Generic;
using FluentMigrator;

namespace NzbDrone.Core.Datastore.Migration.Framework
{
    /// <summary>
    /// Applies the active PostgreSQL schema to FluentMigrator's unqualified schema queries.
    /// FluentMigrator's Schema.Table(...) builders pass a null schema, which PostgreSQL's
    /// information_schema queries otherwise resolve to public instead of the connection's
    /// configured migration schema.
    /// </summary>
    internal sealed class SchemaScopedQuerySchema : IQuerySchema
    {
        private readonly IQuerySchema _inner;
        private readonly string _schemaName;

        public SchemaScopedQuerySchema(IQuerySchema inner, string schemaName)
        {
            _inner = inner;
            _schemaName = schemaName;
        }

        public string DatabaseType => _inner.DatabaseType;

        public IList<string> DatabaseTypeAliases => _inner.DatabaseTypeAliases;

        public bool SchemaExists(string schemaName)
        {
            return _inner.SchemaExists(schemaName);
        }

        public bool TableExists(string schemaName, string tableName)
        {
            return _inner.TableExists(schemaName ?? _schemaName, tableName);
        }

        public bool ColumnExists(string schemaName, string tableName, string columnName)
        {
            return _inner.ColumnExists(schemaName ?? _schemaName, tableName, columnName);
        }

        public bool ConstraintExists(string schemaName, string tableName, string constraintName)
        {
            return _inner.ConstraintExists(schemaName ?? _schemaName, tableName, constraintName);
        }

        public bool IndexExists(string schemaName, string tableName, string indexName)
        {
            return _inner.IndexExists(schemaName ?? _schemaName, tableName, indexName);
        }

        public bool SequenceExists(string schemaName, string sequenceName)
        {
            return _inner.SequenceExists(schemaName ?? _schemaName, sequenceName);
        }

        public bool DefaultValueExists(string schemaName, string tableName, string columnName, object defaultValue)
        {
            return _inner.DefaultValueExists(schemaName ?? _schemaName, tableName, columnName, defaultValue);
        }
    }
}
