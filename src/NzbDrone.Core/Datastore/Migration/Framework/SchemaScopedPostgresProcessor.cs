using System.Collections.Generic;
using FluentMigrator;
using FluentMigrator.Runner.Generators.Postgres;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using FluentMigrator.Runner.Processors.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NzbDrone.Core.Datastore.Migration.Framework
{
    /// <summary>
    /// PostgreSQL processor that resolves unqualified migration schema checks against the active schema.
    /// </summary>
    internal sealed class SchemaScopedPostgresProcessor : Postgres15_0Processor
    {
        public const string ProcessorId = "ChaptarrPostgreSQL";

        private readonly SchemaScopedQuerySchema _querySchema;

        public SchemaScopedPostgresProcessor(
            PostgresDbFactory factory,
            Postgres15_0Generator generator,
            ILogger<Postgres15_0Processor> logger,
            IOptionsSnapshot<ProcessorOptions> options,
            IConnectionStringAccessor connectionStringAccessor,
            PostgresOptions postgresOptions,
            string schemaName)
            : base(factory, generator, logger, options, connectionStringAccessor, postgresOptions)
        {
            _querySchema = new SchemaScopedQuerySchema(this, schemaName);
        }

        public override string DatabaseType => "PostgreSQL15_0";

        public override IList<string> DatabaseTypeAliases { get; } = new List<string>
        {
            ProcessorId,
            "PostgreSQL15_0",
            "PostgreSQL"
        };

        public override bool SchemaExists(string schemaName)
        {
            return _querySchema.SchemaExists(schemaName);
        }

        public override bool TableExists(string schemaName, string tableName)
        {
            return _querySchema.TableExists(schemaName, tableName);
        }

        public override bool ColumnExists(string schemaName, string tableName, string columnName)
        {
            return _querySchema.ColumnExists(schemaName, tableName, columnName);
        }

        public override bool ConstraintExists(string schemaName, string tableName, string constraintName)
        {
            return _querySchema.ConstraintExists(schemaName, tableName, constraintName);
        }

        public override bool IndexExists(string schemaName, string tableName, string indexName)
        {
            return _querySchema.IndexExists(schemaName, tableName, indexName);
        }

        public override bool SequenceExists(string schemaName, string sequenceName)
        {
            return _querySchema.SequenceExists(schemaName, sequenceName);
        }

        public override bool DefaultValueExists(string schemaName, string tableName, string columnName, object defaultValue)
        {
            return _querySchema.DefaultValueExists(schemaName, tableName, columnName, defaultValue);
        }

        public static void Register(IServiceCollection services, string schemaName)
        {
            services.AddScoped(sp => new SchemaScopedPostgresProcessor(
                sp.GetRequiredService<PostgresDbFactory>(),
                sp.GetRequiredService<Postgres15_0Generator>(),
                sp.GetRequiredService<ILogger<Postgres15_0Processor>>(),
                sp.GetRequiredService<IOptionsSnapshot<ProcessorOptions>>(),
                sp.GetRequiredService<IConnectionStringAccessor>(),
                sp.GetRequiredService<PostgresOptions>(),
                schemaName));
            services.AddScoped<IMigrationProcessor>(sp => sp.GetRequiredService<SchemaScopedPostgresProcessor>());
        }
    }
}
