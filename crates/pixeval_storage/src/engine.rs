// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::sync::Arc;
use parking_lot::Mutex;
use rusqlite::{params, Connection, OptionalExtension};

use crate::error::StorageError;
use crate::models::*;
use crate::schema::init_schema;

#[derive(Clone, uniffi::Object)]
pub struct StorageEngine {
    conn: Arc<Mutex<Connection>>,
}

#[uniffi::export]
impl StorageEngine {
    #[uniffi::constructor]
    pub fn new(db_path: String) -> Result<Self, StorageError> {
        let conn = if db_path == ":memory:" {
            Connection::open_in_memory()?
        } else {
            let conn = Connection::open(&db_path)?;
            conn.execute_batch(
                "PRAGMA journal_mode = WAL;
                 PRAGMA synchronous = NORMAL;
                 PRAGMA busy_timeout = 5000;",
            )?;
            conn
        };

        init_schema(&conn)?;

        Ok(Self {
            conn: Arc::new(Mutex::new(conn)),
        })
    }

    // --- Search History ---

    pub fn insert_search_history(
        &self,
        value: String,
        translated_name: Option<String>,
        time: String,
    ) -> Result<SearchHistoryRecord, StorageError> {
        let conn = self.conn.lock();
        conn.execute(
            "INSERT INTO SearchHistoryEntry (Value, TranslatedName, Time) VALUES (?1, ?2, ?3)",
            params![value, translated_name, time],
        )?;
        let id = conn.last_insert_rowid();
        Ok(SearchHistoryRecord {
            history_entry_id: id,
            value,
            translated_name,
            time,
        })
    }

    pub fn upsert_search_history(
        &self,
        value: String,
        translated_name: Option<String>,
        time: String,
    ) -> Result<SearchHistoryRecord, StorageError> {
        let conn = self.conn.lock();
        conn.execute(
            "DELETE FROM SearchHistoryEntry WHERE Value = ?1",
            params![value],
        )?;
        conn.execute(
            "INSERT INTO SearchHistoryEntry (Value, TranslatedName, Time) VALUES (?1, ?2, ?3)",
            params![value, translated_name, time],
        )?;
        let id = conn.last_insert_rowid();
        Ok(SearchHistoryRecord {
            history_entry_id: id,
            value,
            translated_name,
            time,
        })
    }

    pub fn get_search_history_by_value(
        &self,
        value: String,
    ) -> Result<Option<SearchHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, Value, TranslatedName, CAST(Time AS TEXT) FROM SearchHistoryEntry WHERE Value = ?1",
        )?;
        let record = stmt
            .query_row(params![value], |row| {
                Ok(SearchHistoryRecord {
                    history_entry_id: row.get(0)?,
                    value: row.get(1)?,
                    translated_name: row.get(2)?,
                    time: row.get(3)?,
                })
            })
            .optional()?;
        Ok(record)
    }

    pub fn stream_search_histories(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<SearchHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, Value, TranslatedName, CAST(Time AS TEXT) FROM SearchHistoryEntry ORDER BY HistoryEntryId DESC LIMIT ?1 OFFSET ?2",
        )?;
        let rows = stmt.query_map(params![take, skip], |row| {
            Ok(SearchHistoryRecord {
                history_entry_id: row.get(0)?,
                value: row.get(1)?,
                translated_name: row.get(2)?,
                time: row.get(3)?,
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            results.push(r?);
        }
        Ok(results)
    }

    pub fn try_delete_search_history_by_value(&self, value: String) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let affected = conn.execute(
            "DELETE FROM SearchHistoryEntry WHERE Value = ?1",
            params![value],
        )?;
        Ok(affected > 0)
    }

    pub fn clear_search_history(&self) -> Result<(), StorageError> {
        let conn = self.conn.lock();
        conn.execute("DELETE FROM SearchHistoryEntry", [])?;
        Ok(())
    }

    pub fn count_search_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM SearchHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    // --- Browse History ---

    pub fn add_or_replace_browse_history(
        &self,
        id: String,
        serialize_key: Option<String>,
        work_key: String,
        payload_json: String,
    ) -> Result<BrowseHistoryRecord, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        // Delete existing if any, along with its payload
        let existing_payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM BrowseHistoryEntry WHERE WorkKey = ?1",
                params![work_key],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(payload_id) = existing_payload_id {
            tx.execute("DELETE FROM BrowseHistoryEntry WHERE WorkKey = ?1", params![work_key])?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![payload_id])?;
        }

        // Insert new payload
        tx.execute(
            "INSERT INTO ArtworkPayloadEntry (SerializedArtwork) VALUES (?1)",
            params![payload_json],
        )?;
        let payload_id = tx.last_insert_rowid();

        // Insert new browse entry
        tx.execute(
            "INSERT INTO BrowseHistoryEntry (ArtworkPayloadEntryId, SerializeKey, WorkKey, Id) VALUES (?1, ?2, ?3, ?4)",
            params![payload_id, serialize_key, work_key, id],
        )?;
        let history_id = tx.last_insert_rowid();
        tx.commit()?;

        Ok(BrowseHistoryRecord {
            history_entry_id: history_id,
            id,
            serialize_key,
            work_key,
            payload_json: Some(payload_json),
        })
    }

    pub fn get_browse_history_by_work_key(
        &self,
        work_key: String,
    ) -> Result<Option<BrowseHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT b.HistoryEntryId, b.Id, b.SerializeKey, b.WorkKey, p.SerializedArtwork
             FROM BrowseHistoryEntry b
             LEFT JOIN ArtworkPayloadEntry p ON b.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             WHERE b.WorkKey = ?1",
        )?;
        let record = stmt
            .query_row(params![work_key], |row| {
                Ok(BrowseHistoryRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    serialize_key: row.get(2)?,
                    work_key: row.get(3)?,
                    payload_json: row.get(4)?,
                })
            })
            .optional()?;

        if let Some(ref r) = record {
            // Check for broken payload
            if r.payload_json.is_none() {
                drop(stmt);
                conn.execute(
                    "DELETE FROM BrowseHistoryEntry WHERE HistoryEntryId = ?1",
                    params![r.history_entry_id],
                )?;
                return Ok(None);
            }
        }

        Ok(record)
    }

    pub fn stream_browse_history(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<BrowseHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT b.HistoryEntryId, b.Id, b.SerializeKey, b.WorkKey, p.SerializedArtwork
             FROM BrowseHistoryEntry b
             LEFT JOIN ArtworkPayloadEntry p ON b.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             ORDER BY b.HistoryEntryId DESC
             LIMIT ?1 OFFSET ?2",
        )?;
        let rows = stmt.query_map(params![take, skip], |row| {
            Ok(BrowseHistoryRecord {
                history_entry_id: row.get(0)?,
                id: row.get(1)?,
                serialize_key: row.get(2)?,
                work_key: row.get(3)?,
                payload_json: row.get(4)?,
            })
        })?;
        let mut results = Vec::new();
        let mut broken_ids = Vec::new();
        for r in rows {
            let record = r?;
            if record.payload_json.is_none() {
                broken_ids.push(record.history_entry_id);
            } else {
                results.push(record);
            }
        }

        if !broken_ids.is_empty() {
            drop(stmt);
            for id in broken_ids {
                let _ = conn.execute("DELETE FROM BrowseHistoryEntry WHERE HistoryEntryId = ?1", params![id]);
            }
        }

        Ok(results)
    }

    pub fn try_delete_browse_history_by_work_key(&self, work_key: String) -> Result<bool, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        let payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM BrowseHistoryEntry WHERE WorkKey = ?1",
                params![work_key],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(pid) = payload_id {
            tx.execute("DELETE FROM BrowseHistoryEntry WHERE WorkKey = ?1", params![work_key])?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![pid])?;
            tx.commit()?;
            Ok(true)
        } else {
            Ok(false)
        }
    }

    pub fn clear_browse_history(&self) -> Result<(), StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        tx.execute(
            "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (SELECT ArtworkPayloadEntryId FROM BrowseHistoryEntry)",
            [],
        )?;
        tx.execute("DELETE FROM BrowseHistoryEntry", [])?;
        tx.commit()?;
        Ok(())
    }

    pub fn count_browse_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM BrowseHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    // --- Watch Later ---

    pub fn add_or_replace_watch_later(
        &self,
        id: String,
        serialize_key: Option<String>,
        work_key: String,
        payload_json: String,
    ) -> Result<WatchLaterRecord, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        let existing_payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM WatchLaterEntry WHERE WorkKey = ?1",
                params![work_key],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(payload_id) = existing_payload_id {
            tx.execute("DELETE FROM WatchLaterEntry WHERE WorkKey = ?1", params![work_key])?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![payload_id])?;
        }

        tx.execute(
            "INSERT INTO ArtworkPayloadEntry (SerializedArtwork) VALUES (?1)",
            params![payload_json],
        )?;
        let payload_id = tx.last_insert_rowid();

        tx.execute(
            "INSERT INTO WatchLaterEntry (ArtworkPayloadEntryId, SerializeKey, WorkKey, Id) VALUES (?1, ?2, ?3, ?4)",
            params![payload_id, serialize_key, work_key, id],
        )?;
        let history_id = tx.last_insert_rowid();
        tx.commit()?;

        Ok(WatchLaterRecord {
            history_entry_id: history_id,
            id,
            serialize_key,
            work_key,
            payload_json: Some(payload_json),
        })
    }

    pub fn contains_watch_later(&self, work_key: String) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row(
            "SELECT COUNT(*) FROM WatchLaterEntry WHERE WorkKey = ?1",
            params![work_key],
            |row| row.get(0),
        )?;
        Ok(count > 0)
    }

    pub fn remove_watch_later(&self, work_key: String) -> Result<bool, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        let payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM WatchLaterEntry WHERE WorkKey = ?1",
                params![work_key],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(pid) = payload_id {
            tx.execute("DELETE FROM WatchLaterEntry WHERE WorkKey = ?1", params![work_key])?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![pid])?;
            tx.commit()?;
            Ok(true)
        } else {
            Ok(false)
        }
    }

    pub fn stream_watch_later(&self, skip: u32, take: u32) -> Result<Vec<WatchLaterRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT w.HistoryEntryId, w.Id, w.SerializeKey, w.WorkKey, p.SerializedArtwork
             FROM WatchLaterEntry w
             LEFT JOIN ArtworkPayloadEntry p ON w.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             ORDER BY w.HistoryEntryId DESC
             LIMIT ?1 OFFSET ?2",
        )?;
        let rows = stmt.query_map(params![take, skip], |row| {
            Ok(WatchLaterRecord {
                history_entry_id: row.get(0)?,
                id: row.get(1)?,
                serialize_key: row.get(2)?,
                work_key: row.get(3)?,
                payload_json: row.get(4)?,
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            let record = r?;
            if record.payload_json.is_some() {
                results.push(record);
            }
        }
        Ok(results)
    }

    pub fn clear_watch_later(&self) -> Result<(), StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        tx.execute(
            "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (SELECT ArtworkPayloadEntryId FROM WatchLaterEntry)",
            [],
        )?;
        tx.execute("DELETE FROM WatchLaterEntry", [])?;
        tx.commit()?;
        Ok(())
    }

    pub fn count_watch_later(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM WatchLaterEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    pub fn get_watch_later_by_work_key(
        &self,
        work_key: String,
    ) -> Result<Option<WatchLaterRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT w.HistoryEntryId, w.Id, w.SerializeKey, w.WorkKey, p.SerializedArtwork
             FROM WatchLaterEntry w
             LEFT JOIN ArtworkPayloadEntry p ON w.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             WHERE w.WorkKey = ?1",
        )?;
        let record = stmt
            .query_row(params![work_key], |row| {
                Ok(WatchLaterRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    serialize_key: row.get(2)?,
                    work_key: row.get(3)?,
                    payload_json: row.get(4)?,
                })
            })
            .optional()?;

        if let Some(ref r) = record {
            if r.payload_json.is_none() {
                drop(stmt);
                conn.execute(
                    "DELETE FROM WatchLaterEntry WHERE HistoryEntryId = ?1",
                    params![r.history_entry_id],
                )?;
                return Ok(None);
            }
        }

        Ok(record)
    }

    // --- Download History ---

    pub fn add_or_replace_download_history(
        &self,
        id: String,
        serialize_key: Option<String>,
        destination: String,
        state: u32,
        format_token: Option<String>,
        error_message: Option<String>,
        payload_json: String,
    ) -> Result<DownloadHistoryRecord, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        let existing_payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM DownloadHistoryEntry WHERE Destination = ?1",
                params![destination],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(payload_id) = existing_payload_id {
            tx.execute("DELETE FROM DownloadHistoryEntry WHERE Destination = ?1", params![destination])?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![payload_id])?;
        }

        tx.execute(
            "INSERT INTO ArtworkPayloadEntry (SerializedArtwork) VALUES (?1)",
            params![payload_json],
        )?;
        let payload_id = tx.last_insert_rowid();

        tx.execute(
            "INSERT INTO DownloadHistoryEntry (ArtworkPayloadEntryId, SerializeKey, Destination, State, FormatToken, ErrorMessage)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![payload_id, serialize_key, destination, state, format_token, error_message],
        )?;
        let history_id = tx.last_insert_rowid();
        tx.commit()?;

        Ok(DownloadHistoryRecord {
            history_entry_id: history_id,
            id,
            serialize_key,
            destination,
            state,
            format_token,
            error_message,
            payload_json: Some(payload_json),
        })
    }

    pub fn try_delete_download_history_by_destination(&self, destination: String) -> Result<bool, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        let payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM DownloadHistoryEntry WHERE Destination = ?1",
                params![destination],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(pid) = payload_id {
            tx.execute("DELETE FROM DownloadHistoryEntry WHERE Destination = ?1", params![destination])?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![pid])?;
            tx.commit()?;
            Ok(true)
        } else {
            Ok(false)
        }
    }

    pub fn stream_download_history(&self, skip: u32, take: u32) -> Result<Vec<DownloadHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT d.HistoryEntryId, d.SerializeKey, d.Destination, d.State, d.FormatToken, d.ErrorMessage, p.SerializedArtwork
             FROM DownloadHistoryEntry d
             LEFT JOIN ArtworkPayloadEntry p ON d.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             ORDER BY d.HistoryEntryId DESC
             LIMIT ?1 OFFSET ?2",
        )?;
        let rows = stmt.query_map(params![take, skip], |row| {
            Ok(DownloadHistoryRecord {
                history_entry_id: row.get(0)?,
                id: String::new(),
                serialize_key: row.get(1)?,
                destination: row.get(2)?,
                state: row.get(3)?,
                format_token: row.get(4)?,
                error_message: row.get(5)?,
                payload_json: row.get(6)?,
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            let record = r?;
            if record.payload_json.is_some() {
                results.push(record);
            }
        }
        Ok(results)
    }

    pub fn clear_download_history(&self) -> Result<(), StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        tx.execute(
            "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (SELECT ArtworkPayloadEntryId FROM DownloadHistoryEntry)",
            [],
        )?;
        tx.execute("DELETE FROM DownloadHistoryEntry", [])?;
        tx.commit()?;
        Ok(())
    }

    pub fn count_download_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM DownloadHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    // --- Subscription Download History ---

    pub fn add_or_replace_subscription_download_history(
        &self,
        id: String,
        serialize_key: Option<String>,
        destination: String,
        state: u32,
        format_token: Option<String>,
        error_message: Option<String>,
        work_subscription_id: i64,
        artwork_id: String,
        payload_json: String,
    ) -> Result<SubscriptionDownloadHistoryRecord, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        let existing_payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry
                 WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
                params![work_subscription_id, artwork_id, destination],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(payload_id) = existing_payload_id {
            tx.execute(
                "DELETE FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
                params![work_subscription_id, artwork_id, destination],
            )?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![payload_id])?;
        }

        tx.execute(
            "INSERT INTO ArtworkPayloadEntry (SerializedArtwork) VALUES (?1)",
            params![payload_json],
        )?;
        let payload_id = tx.last_insert_rowid();

        tx.execute(
            "INSERT INTO SubscriptionDownloadHistoryEntry (ArtworkPayloadEntryId, SerializeKey, Destination, WorkSubscriptionId, ArtworkId, State, FormatToken, ErrorMessage)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
            params![payload_id, serialize_key, destination, work_subscription_id, artwork_id, state, format_token, error_message],
        )?;
        let history_id = tx.last_insert_rowid();
        tx.commit()?;

        Ok(SubscriptionDownloadHistoryRecord {
            history_entry_id: history_id,
            id,
            serialize_key,
            destination,
            state,
            format_token,
            error_message,
            work_subscription_id,
            artwork_id,
            payload_json: Some(payload_json),
        })
    }

    pub fn add_or_replace_subscription_download_history_batch(
        &self,
        entries: Vec<SubscriptionDownloadHistoryRecord>,
    ) -> Result<(), StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        for entry in entries {
            let payload_str = entry.payload_json.ok_or_else(|| StorageError::ConstraintViolation {
                message: "Payload cannot be empty in batch insertion".to_string(),
            })?;

            let existing_payload_id: Option<i64> = tx
                .query_row(
                    "SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry
                     WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
                    params![entry.work_subscription_id, entry.artwork_id, entry.destination],
                    |row| row.get(0),
                )
                .optional()?;

            if let Some(payload_id) = existing_payload_id {
                tx.execute(
                    "DELETE FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
                    params![entry.work_subscription_id, entry.artwork_id, entry.destination],
                )?;
                tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![payload_id])?;
            }

            tx.execute(
                "INSERT INTO ArtworkPayloadEntry (SerializedArtwork) VALUES (?1)",
                params![payload_str],
            )?;
            let payload_id = tx.last_insert_rowid();

            tx.execute(
                "INSERT INTO SubscriptionDownloadHistoryEntry (ArtworkPayloadEntryId, SerializeKey, Destination, WorkSubscriptionId, ArtworkId, State, FormatToken, ErrorMessage)
                 VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
                params![payload_id, entry.serialize_key, entry.destination, entry.work_subscription_id, entry.artwork_id, entry.state, entry.format_token, entry.error_message],
            )?;
        }

        tx.commit()?;
        Ok(())
    }

    pub fn contains_subscription_download_identity(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
    ) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row(
            "SELECT COUNT(*) FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
            params![work_subscription_id, artwork_id, destination],
            |row| row.get(0),
        )?;
        Ok(count > 0)
    }

    pub fn try_delete_subscription_download_by_identity(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
    ) -> Result<bool, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        let payload_id: Option<i64> = tx
            .query_row(
                "SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry
                 WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
                params![work_subscription_id, artwork_id, destination],
                |row| row.get(0),
            )
            .optional()?;

        if let Some(pid) = payload_id {
            tx.execute(
                "DELETE FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
                params![work_subscription_id, artwork_id, destination],
            )?;
            tx.execute("DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId = ?1", params![pid])?;
            tx.commit()?;
            Ok(true)
        } else {
            Ok(false)
        }
    }

    pub fn delete_subscription_downloads_by_work_subscription_id(
        &self,
        work_subscription_id: i64,
    ) -> Result<i64, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        tx.execute(
            "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (
                SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1
            )",
            params![work_subscription_id],
        )?;

        let count = tx.execute(
            "DELETE FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1",
            params![work_subscription_id],
        )?;

        tx.commit()?;
        Ok(count as i64)
    }

    pub fn delete_orphan_subscription_downloads(
        &self,
        valid_work_subscription_ids: Vec<i64>,
    ) -> Result<i64, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        let mut total_deleted = 0;
        if valid_work_subscription_ids.is_empty() {
            tx.execute(
                "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry)",
                [],
            )?;
            total_deleted = tx.execute("DELETE FROM SubscriptionDownloadHistoryEntry", [])?;
        } else {
            // Find all work_subscription_ids in DB not in valid list
            let mut stmt = tx.prepare("SELECT DISTINCT WorkSubscriptionId FROM SubscriptionDownloadHistoryEntry")?;
            let ids: Vec<i64> = stmt
                .query_map([], |row| row.get(0))?
                .filter_map(Result::ok)
                .filter(|id| !valid_work_subscription_ids.contains(id))
                .collect();
            drop(stmt);

            for id in ids {
                tx.execute(
                    "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (
                        SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1
                    )",
                    params![id],
                )?;
                let c = tx.execute(
                    "DELETE FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1",
                    params![id],
                )?;
                total_deleted += c;
            }
        }

        tx.commit()?;
        Ok(total_deleted as i64)
    }

    pub fn stream_subscription_download_history(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<SubscriptionDownloadHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT s.HistoryEntryId, s.ArtworkId, s.SerializeKey, s.Destination, s.WorkSubscriptionId, s.State, s.FormatToken, s.ErrorMessage, p.SerializedArtwork
             FROM SubscriptionDownloadHistoryEntry s
             LEFT JOIN ArtworkPayloadEntry p ON s.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             ORDER BY s.HistoryEntryId DESC
             LIMIT ?1 OFFSET ?2",
        )?;
        let rows = stmt.query_map(params![take, skip], |row| {
            let artwork_id: String = row.get(1)?;
            let state: u32 = row.get::<_, Option<u32>>(5)?.unwrap_or(0);
            let format_token: Option<String> = row.get(6)?;
            let error_message: Option<String> = row.get(7)?;
            Ok(SubscriptionDownloadHistoryRecord {
                history_entry_id: row.get(0)?,
                id: artwork_id.clone(),
                artwork_id,
                serialize_key: row.get(2)?,
                destination: row.get(3)?,
                state,
                format_token,
                error_message,
                work_subscription_id: row.get(4)?,
                payload_json: row.get(8)?,
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            let record = r?;
            if record.payload_json.is_some() {
                results.push(record);
            }
        }
        Ok(results)
    }

    pub fn clear_subscription_download_history(&self) -> Result<(), StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;
        tx.execute(
            "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry)",
            [],
        )?;
        tx.execute("DELETE FROM SubscriptionDownloadHistoryEntry", [])?;
        tx.commit()?;
        Ok(())
    }

    pub fn count_subscription_download_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM SubscriptionDownloadHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    // --- Subscriptions ---

    pub fn get_subscription_by_key(
        &self,
        id: i64,
        subscription_type: u32,
        work_kind: u32,
    ) -> Result<Option<WorkSubscriptionRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, Id, SubscriptionType, WorkKind, Name, Account, AvatarUrl
             FROM WorkSubscriptionEntry
             WHERE Id = ?1 AND SubscriptionType = ?2 AND WorkKind = ?3",
        )?;
        let record = stmt
            .query_row(params![id, subscription_type, work_kind], |row| {
                Ok(WorkSubscriptionRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    subscription_type: row.get(2)?,
                    work_kind: row.get(3)?,
                    title: row.get(4)?,
                    author: row.get(5)?,
                    avatar: row.get(6)?,
                    last_check_time: String::new(),
                    last_work_id: None,
                })
            })
            .optional()?;
        Ok(record)
    }

    pub fn get_subscription_by_history_id(
        &self,
        history_entry_id: i64,
    ) -> Result<Option<WorkSubscriptionRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, Id, SubscriptionType, WorkKind, Name, Account, AvatarUrl
             FROM WorkSubscriptionEntry
             WHERE HistoryEntryId = ?1",
        )?;
        let record = stmt
            .query_row(params![history_entry_id], |row| {
                Ok(WorkSubscriptionRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    subscription_type: row.get(2)?,
                    work_kind: row.get(3)?,
                    title: row.get(4)?,
                    author: row.get(5)?,
                    avatar: row.get(6)?,
                    last_check_time: String::new(),
                    last_work_id: None,
                })
            })
            .optional()?;
        Ok(record)
    }

    pub fn upsert_subscription(
        &self,
        id: i64,
        subscription_type: u32,
        work_kind: u32,
        title: String,
        author: String,
        avatar: String,
        last_check_time: String,
        last_work_id: Option<String>,
    ) -> Result<WorkSubscriptionRecord, StorageError> {
        let conn = self.conn.lock();
        let existing: Option<i64> = conn
            .query_row(
                "SELECT HistoryEntryId FROM WorkSubscriptionEntry WHERE Id = ?1 AND SubscriptionType = ?2 AND WorkKind = ?3",
                params![id, subscription_type, work_kind],
                |row| row.get(0),
            )
            .optional()?;

        let history_entry_id = if let Some(existing_id) = existing {
            conn.execute(
                "UPDATE WorkSubscriptionEntry
                 SET Name = ?1, Account = ?2, AvatarUrl = ?3
                 WHERE HistoryEntryId = ?4",
                params![title, author, avatar, existing_id],
            )?;
            existing_id
        } else {
            conn.execute(
                "INSERT INTO WorkSubscriptionEntry (Id, SubscriptionType, WorkKind, Name, Account, AvatarUrl)
                 VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
                params![id, subscription_type, work_kind, title, author, avatar],
            )?;
            conn.last_insert_rowid()
        };

        Ok(WorkSubscriptionRecord {
            history_entry_id,
            id,
            subscription_type,
            work_kind,
            title,
            author,
            avatar,
            last_check_time,
            last_work_id,
        })
    }

    pub fn delete_subscription(&self, history_entry_id: i64) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let count = conn.execute(
            "DELETE FROM WorkSubscriptionEntry WHERE HistoryEntryId = ?1",
            params![history_entry_id],
        )?;
        Ok(count > 0)
    }

    pub fn get_all_subscriptions(&self) -> Result<Vec<WorkSubscriptionRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, Id, SubscriptionType, WorkKind, Name, Account, AvatarUrl
             FROM WorkSubscriptionEntry ORDER BY HistoryEntryId DESC",
        )?;
        let rows = stmt.query_map([], |row| {
            Ok(WorkSubscriptionRecord {
                history_entry_id: row.get(0)?,
                id: row.get(1)?,
                subscription_type: row.get(2)?,
                work_kind: row.get(3)?,
                title: row.get(4)?,
                author: row.get(5)?,
                avatar: row.get(6)?,
                last_check_time: String::new(),
                last_work_id: None,
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            results.push(r?);
        }
        Ok(results)
    }

    pub fn get_all_subscription_history_ids(&self) -> Result<Vec<i64>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare("SELECT HistoryEntryId FROM WorkSubscriptionEntry")?;
        let rows = stmt.query_map([], |row| row.get(0))?;
        let mut results = Vec::new();
        for r in rows {
            results.push(r?);
        }
        Ok(results)
    }

    pub fn stream_subscriptions(&self, skip: u32, take: u32) -> Result<Vec<WorkSubscriptionRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, Id, SubscriptionType, WorkKind, Name, Account, AvatarUrl
             FROM WorkSubscriptionEntry ORDER BY HistoryEntryId DESC LIMIT ?1 OFFSET ?2",
        )?;
        let rows = stmt.query_map(params![take, skip], |row| {
            Ok(WorkSubscriptionRecord {
                history_entry_id: row.get(0)?,
                id: row.get(1)?,
                subscription_type: row.get(2)?,
                work_kind: row.get(3)?,
                title: row.get(4)?,
                author: row.get(5)?,
                avatar: row.get(6)?,
                last_check_time: String::new(),
                last_work_id: None,
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            results.push(r?);
        }
        Ok(results)
    }

    pub fn count_subscriptions(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM WorkSubscriptionEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    // --- Blocked Users ---

    pub fn add_or_update_blocked_user(
        &self,
        id: i64,
        user_name: String,
        avatar_url: String,
        account: String,
    ) -> Result<BlockedUserRecord, StorageError> {
        let conn = self.conn.lock();
        conn.execute("DELETE FROM BlockedUserEntry WHERE Id = ?1", params![id])?;
        conn.execute(
            "INSERT INTO BlockedUserEntry (Id, Name, AvatarUrl, Account) VALUES (?1, ?2, ?3, ?4)",
            params![id, user_name, avatar_url, account],
        )?;
        let history_id = conn.last_insert_rowid();
        Ok(BlockedUserRecord {
            history_entry_id: history_id,
            id,
            user_name,
            avatar_url,
            account,
        })
    }

    pub fn try_delete_blocked_user(&self, id: i64) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let count = conn.execute("DELETE FROM BlockedUserEntry WHERE Id = ?1", params![id])?;
        Ok(count > 0)
    }

    pub fn is_user_blocked(&self, id: i64) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row(
            "SELECT COUNT(*) FROM BlockedUserEntry WHERE Id = ?1",
            params![id],
            |row| row.get(0),
        )?;
        Ok(count > 0)
    }

    pub fn get_all_blocked_users(&self) -> Result<Vec<BlockedUserRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare("SELECT HistoryEntryId, Id, Name, AvatarUrl, Account FROM BlockedUserEntry ORDER BY HistoryEntryId DESC")?;
        let rows = stmt.query_map([], |row| {
            Ok(BlockedUserRecord {
                history_entry_id: row.get(0)?,
                id: row.get(1)?,
                user_name: row.get(2)?,
                avatar_url: row.get::<_, Option<String>>(3)?.unwrap_or_default(),
                account: row.get::<_, Option<String>>(4)?.unwrap_or_default(),
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            results.push(r?);
        }
        Ok(results)
    }

    pub fn count_blocked_users(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM BlockedUserEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    // --- Login Users ---

    pub fn upsert_login_user(&self, record: LoginUserRecord) -> Result<LoginUserRecord, StorageError> {
        let conn = self.conn.lock();
        // Clear conflicting records with same RefreshToken but different UserId to avoid unique constraint failure
        conn.execute(
            "DELETE FROM LoginUserEntry WHERE RefreshToken = ?1 AND UserId != ?2",
            params![record.refresh_token, record.user_id],
        )?;

        let existing: Option<i64> = conn
            .query_row(
                "SELECT HistoryEntryId FROM LoginUserEntry WHERE UserId = ?1",
                params![record.user_id],
                |row| row.get(0),
            )
            .optional()?;

        let history_entry_id = if let Some(existing_id) = existing {
            conn.execute(
                "UPDATE LoginUserEntry
                 SET RefreshToken = ?1, Name = ?2, Account = ?3, MailAddress = ?4, IsPremium = ?5,
                     XRestrict = ?6, IsMailAuthorized = ?7, RequirePolicyAgreement = ?8,
                     Avatar16Url = ?9, Avatar50Url = ?10, Avatar170Url = ?11
                 WHERE HistoryEntryId = ?12",
                params![
                    record.refresh_token,
                    record.name,
                    record.account,
                    record.mail_address,
                    record.is_premium as i32,
                    record.x_restrict,
                    record.is_mail_authorized as i32,
                    record.require_policy_agreement as i32,
                    record.avatar16_url,
                    record.avatar50_url,
                    record.avatar170_url,
                    existing_id
                ],
            )?;
            existing_id
        } else {
            conn.execute(
                "INSERT INTO LoginUserEntry (
                    UserId, RefreshToken, Name, Account, MailAddress, IsPremium,
                    XRestrict, IsMailAuthorized, RequirePolicyAgreement,
                    Avatar16Url, Avatar50Url, Avatar170Url
                 ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12)",
                params![
                    record.user_id,
                    record.refresh_token,
                    record.name,
                    record.account,
                    record.mail_address,
                    record.is_premium as i32,
                    record.x_restrict,
                    record.is_mail_authorized as i32,
                    record.require_policy_agreement as i32,
                    record.avatar16_url,
                    record.avatar50_url,
                    record.avatar170_url,
                ],
            )?;
            conn.last_insert_rowid()
        };

        let mut res = record;
        res.history_entry_id = history_entry_id;
        Ok(res)
    }

    pub fn get_login_user_by_key(&self, history_entry_id: i64) -> Result<Option<LoginUserRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, UserId, RefreshToken, Name, Account, MailAddress, IsPremium,
                    XRestrict, IsMailAuthorized, RequirePolicyAgreement, Avatar16Url, Avatar50Url, Avatar170Url
             FROM LoginUserEntry WHERE HistoryEntryId = ?1",
        )?;
        let record = stmt
            .query_row(params![history_entry_id], |row| {
                let is_premium_int: i32 = row.get(6)?;
                let is_mail_auth_int: i32 = row.get(8)?;
                let req_policy_int: i32 = row.get(9)?;
                Ok(LoginUserRecord {
                    history_entry_id: row.get(0)?,
                    user_id: row.get(1)?,
                    refresh_token: row.get(2)?,
                    name: row.get(3)?,
                    account: row.get(4)?,
                    mail_address: row.get(5)?,
                    is_premium: is_premium_int != 0,
                    x_restrict: row.get(7)?,
                    is_mail_authorized: is_mail_auth_int != 0,
                    require_policy_agreement: req_policy_int != 0,
                    avatar16_url: row.get(10)?,
                    avatar50_url: row.get(11)?,
                    avatar170_url: row.get(12)?,
                })
            })
            .optional()?;
        Ok(record)
    }

    pub fn get_all_login_users(&self) -> Result<Vec<LoginUserRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT HistoryEntryId, UserId, RefreshToken, Name, Account, MailAddress, IsPremium,
                    XRestrict, IsMailAuthorized, RequirePolicyAgreement, Avatar16Url, Avatar50Url, Avatar170Url
             FROM LoginUserEntry ORDER BY HistoryEntryId DESC",
        )?;
        let rows = stmt.query_map([], |row| {
            let is_premium_int: i32 = row.get(6)?;
            let is_mail_auth_int: i32 = row.get(8)?;
            let req_policy_int: i32 = row.get(9)?;
            Ok(LoginUserRecord {
                history_entry_id: row.get(0)?,
                user_id: row.get(1)?,
                refresh_token: row.get(2)?,
                name: row.get(3)?,
                account: row.get(4)?,
                mail_address: row.get(5)?,
                is_premium: is_premium_int != 0,
                x_restrict: row.get(7)?,
                is_mail_authorized: is_mail_auth_int != 0,
                require_policy_agreement: req_policy_int != 0,
                avatar16_url: row.get(10)?,
                avatar50_url: row.get(11)?,
                avatar170_url: row.get(12)?,
            })
        })?;
        let mut results = Vec::new();
        for r in rows {
            results.push(r?);
        }
        Ok(results)
    }

    pub fn delete_login_user(&self, history_entry_id: i64) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let count = conn.execute(
            "DELETE FROM LoginUserEntry WHERE HistoryEntryId = ?1",
            params![history_entry_id],
        )?;
        Ok(count > 0)
    }

    pub fn clear_blocked_users(&self) -> Result<(), StorageError> {
        let conn = self.conn.lock();
        conn.execute("DELETE FROM BlockedUserEntry", [])?;
        Ok(())
    }

    pub fn clear_login_users(&self) -> Result<(), StorageError> {
        let conn = self.conn.lock();
        conn.execute("DELETE FROM LoginUserEntry", [])?;
        Ok(())
    }

    pub fn clear_subscriptions(&self) -> Result<(), StorageError> {
        let conn = self.conn.lock();
        conn.execute("DELETE FROM WorkSubscriptionEntry", [])?;
        Ok(())
    }
}
