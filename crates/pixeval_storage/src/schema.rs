// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use rusqlite::{Connection, Result};

pub fn init_schema(conn: &Connection) -> Result<()> {
    conn.execute_batch(
        r#"
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS ArtworkPayloadEntry (
            ArtworkPayloadEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            SerializedArtwork TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS BrowseHistoryEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            ArtworkPayloadEntryId INTEGER NOT NULL,
            SerializeKey TEXT,
            WorkKey TEXT NOT NULL,
            Id TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_BrowseHistoryEntry_WorkKey ON BrowseHistoryEntry (WorkKey);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_BrowseHistoryEntry_Payload ON BrowseHistoryEntry (ArtworkPayloadEntryId);

        CREATE TABLE IF NOT EXISTS WatchLaterEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            ArtworkPayloadEntryId INTEGER NOT NULL,
            SerializeKey TEXT,
            WorkKey TEXT NOT NULL,
            Id TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_WatchLaterEntry_WorkKey ON WatchLaterEntry (WorkKey);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_WatchLaterEntry_Payload ON WatchLaterEntry (ArtworkPayloadEntryId);

        CREATE TABLE IF NOT EXISTS DownloadHistoryEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            ArtworkPayloadEntryId INTEGER NOT NULL,
            SerializeKey TEXT,
            Destination TEXT NOT NULL,
            State INTEGER NOT NULL DEFAULT 1,
            FormatToken TEXT,
            ErrorMessage TEXT
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_DownloadHistoryEntry_Destination ON DownloadHistoryEntry (Destination);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_DownloadHistoryEntry_Payload ON DownloadHistoryEntry (ArtworkPayloadEntryId);

        CREATE TABLE IF NOT EXISTS SubscriptionDownloadHistoryEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            ArtworkPayloadEntryId INTEGER NOT NULL,
            SerializeKey TEXT,
            Destination TEXT NOT NULL,
            State INTEGER NOT NULL DEFAULT 1,
            FormatToken TEXT,
            ErrorMessage TEXT,
            WorkSubscriptionId INTEGER NOT NULL,
            ArtworkId TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_SubscriptionDownloadHistoryEntry_Identity ON SubscriptionDownloadHistoryEntry (WorkSubscriptionId, ArtworkId, Destination);
        CREATE INDEX IF NOT EXISTS IX_SubscriptionDownloadHistoryEntry_SubId ON SubscriptionDownloadHistoryEntry (WorkSubscriptionId);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_SubscriptionDownloadHistoryEntry_Payload ON SubscriptionDownloadHistoryEntry (ArtworkPayloadEntryId);

        CREATE TABLE IF NOT EXISTS SearchHistoryEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            Value TEXT NOT NULL,
            TranslatedName TEXT,
            Time TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_SearchHistoryEntry_Value ON SearchHistoryEntry (Value);

        CREATE TABLE IF NOT EXISTS WorkSubscriptionEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            Id INTEGER NOT NULL,
            SubscriptionType INTEGER NOT NULL,
            WorkKind INTEGER NOT NULL,
            Name TEXT NOT NULL DEFAULT '',
            AvatarUrl TEXT NOT NULL DEFAULT '',
            Account TEXT NOT NULL DEFAULT ''
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_WorkSubscriptionEntry_Key ON WorkSubscriptionEntry (Id, SubscriptionType, WorkKind);

        CREATE TABLE IF NOT EXISTS BlockedUserEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            Id INTEGER NOT NULL,
            Name TEXT NOT NULL DEFAULT '',
            AvatarUrl TEXT NOT NULL DEFAULT '',
            Account TEXT NOT NULL DEFAULT ''
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_BlockedUserEntry_Id ON BlockedUserEntry (Id);

        CREATE TABLE IF NOT EXISTS LoginUserEntry (
            HistoryEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            RefreshToken TEXT NOT NULL,
            Name TEXT NOT NULL DEFAULT '',
            Account TEXT NOT NULL DEFAULT '',
            MailAddress TEXT NOT NULL DEFAULT '',
            IsPremium INTEGER NOT NULL DEFAULT 0,
            XRestrict INTEGER NOT NULL DEFAULT 0,
            IsMailAuthorized INTEGER NOT NULL DEFAULT 0,
            RequirePolicyAgreement INTEGER NOT NULL DEFAULT 0,
            Avatar16Url TEXT NOT NULL DEFAULT '',
            Avatar50Url TEXT NOT NULL DEFAULT '',
            Avatar170Url TEXT NOT NULL DEFAULT ''
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_LoginUserEntry_UserId ON LoginUserEntry (UserId);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_LoginUserEntry_RefreshToken ON LoginUserEntry (RefreshToken);
        "#,
    )?;

    ensure_column(conn, "DownloadHistoryEntry", "State", "INTEGER NOT NULL DEFAULT 1")?;
    ensure_column(conn, "DownloadHistoryEntry", "FormatToken", "TEXT")?;
    ensure_column(conn, "DownloadHistoryEntry", "ErrorMessage", "TEXT")?;

    ensure_column(conn, "SubscriptionDownloadHistoryEntry", "State", "INTEGER NOT NULL DEFAULT 1")?;
    ensure_column(conn, "SubscriptionDownloadHistoryEntry", "FormatToken", "TEXT")?;
    ensure_column(conn, "SubscriptionDownloadHistoryEntry", "ErrorMessage", "TEXT")?;

    ensure_column(conn, "BlockedUserEntry", "AvatarUrl", "TEXT NOT NULL DEFAULT ''")?;
    ensure_column(conn, "BlockedUserEntry", "Account", "TEXT NOT NULL DEFAULT ''")?;

    ensure_column(conn, "WorkSubscriptionEntry", "Name", "TEXT NOT NULL DEFAULT ''")?;
    ensure_column(conn, "WorkSubscriptionEntry", "AvatarUrl", "TEXT NOT NULL DEFAULT ''")?;
    ensure_column(conn, "WorkSubscriptionEntry", "Account", "TEXT NOT NULL DEFAULT ''")?;

    // Migration (Fix 2.5): Upgrade legacy State 0 to 1 (Queued = 1)
    conn.execute_batch(
        "UPDATE DownloadHistoryEntry SET State = 1 WHERE State = 0;
         UPDATE SubscriptionDownloadHistoryEntry SET State = 1 WHERE State = 0;",
    )?;

    // Self-healing (Fix 2.5): Clean up broken history entries and orphan payloads
    conn.execute_batch(
        "DELETE FROM BrowseHistoryEntry WHERE ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM ArtworkPayloadEntry);
         DELETE FROM WatchLaterEntry WHERE ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM ArtworkPayloadEntry);
         DELETE FROM DownloadHistoryEntry WHERE ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM ArtworkPayloadEntry);
         DELETE FROM SubscriptionDownloadHistoryEntry WHERE ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM ArtworkPayloadEntry);

         DELETE FROM ArtworkPayloadEntry WHERE
             ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM BrowseHistoryEntry) AND
             ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM WatchLaterEntry) AND
             ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM DownloadHistoryEntry) AND
             ArtworkPayloadEntryId NOT IN (SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry);

         DELETE FROM BrowseHistoryEntry WHERE HistoryEntryId NOT IN (
             SELECT MAX(HistoryEntryId) FROM BrowseHistoryEntry GROUP BY ArtworkPayloadEntryId
         );
         DELETE FROM WatchLaterEntry WHERE HistoryEntryId NOT IN (
             SELECT MAX(HistoryEntryId) FROM WatchLaterEntry GROUP BY ArtworkPayloadEntryId
         );
         DELETE FROM DownloadHistoryEntry WHERE HistoryEntryId NOT IN (
             SELECT MAX(HistoryEntryId) FROM DownloadHistoryEntry GROUP BY ArtworkPayloadEntryId
         );
         DELETE FROM SubscriptionDownloadHistoryEntry WHERE HistoryEntryId NOT IN (
             SELECT MAX(HistoryEntryId) FROM SubscriptionDownloadHistoryEntry GROUP BY ArtworkPayloadEntryId
         );

         DROP INDEX IF EXISTS IX_BrowseHistoryEntry_Payload;
         CREATE UNIQUE INDEX IF NOT EXISTS IX_BrowseHistoryEntry_Payload ON BrowseHistoryEntry (ArtworkPayloadEntryId);

         DROP INDEX IF EXISTS IX_WatchLaterEntry_Payload;
         CREATE UNIQUE INDEX IF NOT EXISTS IX_WatchLaterEntry_Payload ON WatchLaterEntry (ArtworkPayloadEntryId);

         DROP INDEX IF EXISTS IX_DownloadHistoryEntry_Payload;
         CREATE UNIQUE INDEX IF NOT EXISTS IX_DownloadHistoryEntry_Payload ON DownloadHistoryEntry (ArtworkPayloadEntryId);

         DROP INDEX IF EXISTS IX_SubscriptionDownloadHistoryEntry_Payload;
         CREATE UNIQUE INDEX IF NOT EXISTS IX_SubscriptionDownloadHistoryEntry_Payload ON SubscriptionDownloadHistoryEntry (ArtworkPayloadEntryId);",
    )?;

    Ok(())
}

fn ensure_column(conn: &Connection, table: &str, column: &str, col_def: &str) -> Result<()> {
    let mut stmt = conn.prepare(&format!("PRAGMA table_info({table})"))?;
    let cols = stmt.query_map([], |row| row.get::<_, String>(1))?;
    let mut exists = false;
    for col in cols {
        if col?.eq_ignore_ascii_case(column) {
            exists = true;
            break;
        }
    }
    if !exists {
        conn.execute(&format!("ALTER TABLE {table} ADD COLUMN {column} {col_def}"), [])?;
    }
    Ok(())
}
