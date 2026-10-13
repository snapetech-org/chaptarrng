# Moving a BookshelfNG database to ChaptarrNG

ChaptarrNG can convert a BookshelfNG SQLite database that matches migration
158. The converter builds a fresh ChaptarrNG database beside the source,
accounts for every row in every source table, checks database integrity and
foreign keys, and keeps a copy of the original before replacing it. Normal
ChaptarrNG startup also detects and converts a supported BookshelfNG database.

The preview command runs the same staged conversion and row-accounting checks
without replacing the source database. It removes the temporary snapshot and
staged database when it finishes. It leaves a small conversion lock file in the
data directory. Keep the source application stopped while previewing or
converting.

## Before you start

Stop BookshelfNG and make an independent copy of its full data directory. Keep
the copy until you have verified the converted library. Use the same data
directory and configuration file with ChaptarrNG; the converter does not
rewrite `config.xml`.

The preview command must use the same ChaptarrNG release that will perform the
conversion. These Docker commands keep the application's normal entrypoint and
mount the existing data directory at `/config`:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech/chaptarrng:<version> \
  dotnet /app/Chaptarr.dll -preview-bookshelf-migration -data=/config
```

The preview prints a row-disposition report for each source table. Review that
report and keep the independent data-directory copy before applying the
conversion.

## Apply the conversion

You can run the explicit apply command to print the report and exit after
conversion:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech/chaptarrng:<version> \
  dotnet /app/Chaptarr.dll -migrate-bookshelf -data=/config
```

The original database is retained beside the converted database with a name
like `readarr.db.bookshelfng-backup-<timestamp>-<id>.db`. You can also let
normal ChaptarrNG startup perform the conversion. Afterward, check the library,
root folders, profiles, indexers, download clients, notifications, and any
SeerrNG connection before removing your independent copy or retained database
backup.

## Supported data and limits

The current converter accepts SQLite databases with the supported BookshelfNG
migration-158 schema. It rejects other lineages, unsupported schemas, and
incomplete row-accounting reports without promoting a staged database. It does
not convert PostgreSQL databases or older BookshelfNG schemas. Automated
converter fixtures use a synthetic populated database; they do not establish
compatibility with every real installation or every provider setting.

If conversion fails, keep the independent copy and retained source database.
Do not start BookshelfNG against the converted ChaptarrNG database.
