#!/usr/bin/env python3

"""Seed a disposable BookshelfNG 158 database for the SeerrNG smoke test."""

from pathlib import Path
import argparse
import os
import sqlite3


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--config", type=Path, default=Path("/config"))
parser.add_argument("--library", type=Path, default=Path("/library"))
parser.add_argument("--schema", type=Path, default=Path("/fixture/schema.sql"))
parser.add_argument("--seed", type=Path, default=Path("/fixture/seed.sql"))
args = parser.parse_args()

CONFIG = args.config
LIBRARY = args.library
DATABASE = CONFIG / "readarr.db"
SCHEMA = args.schema
SEED = args.seed

require(CONFIG.is_dir(), "The disposable Chaptarr config volume is missing.")
require(LIBRARY.is_dir(), "The disposable library volume is missing.")
require(SCHEMA.is_file() and SEED.is_file(), "The migration fixture SQL is missing.")
require(not any(CONFIG.iterdir()), "Refusing to overwrite a non-empty config volume; run docker compose down -v first.")
require(not any(LIBRARY.iterdir()), "Refusing to overwrite a non-empty library volume; run docker compose down -v first.")

with sqlite3.connect(DATABASE) as connection:
    connection.executescript(SCHEMA.read_text(encoding="utf-8"))
    connection.executescript(SEED.read_text(encoding="utf-8"))
    version = connection.execute("SELECT MAX(Version) FROM VersionInfo").fetchone()[0]
    books = connection.execute("SELECT COUNT(*) FROM Books").fetchone()[0]
    editions = connection.execute("SELECT COUNT(*) FROM Editions").fetchone()[0]
    integrity = connection.execute("PRAGMA integrity_check").fetchone()[0]

require(version == 158, f"Expected BookshelfNG schema 158, found {version}.")
require(books == 1 and editions == 2, "The populated migration fixture does not match its expected library rows.")
require(integrity == "ok", "The seeded BookshelfNG source failed SQLite integrity_check.")

# Give the migrated file rows corresponding disposable files so ChaptarrNG sees
# the same paths it would see in a mounted library. The content is deliberately
# synthetic and is not intended to be decoded as media.
for name, size in (("book.m4b", 100), ("book.epub", 200)):
    path = LIBRARY / "Fixture Author" / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(bytes(size))

if os.geteuid() == 0:
    for root in (CONFIG, LIBRARY):
        for path in (root, *root.rglob("*")):
            os.chown(path, 99, 100)
            if path.is_dir():
                path.chmod(0o775)
            else:
                path.chmod(0o664)

print("Seeded disposable BookshelfNG schema 158 with one author, one mixed-format book, two editions, and two synthetic library files.")
