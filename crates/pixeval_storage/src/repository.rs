// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

use std::collections::HashSet;
use std::sync::Arc;
use parking_lot::Mutex;
use rusqlite::{params, Connection, OptionalExtension};

use crate::error::StorageError;
use crate::models::*;

pub(crate) fn notify_observers<F>(observers: &Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>, f: F)
where
    F: Fn(&dyn StorageObserver),
{
    let list = {
        observers.lock().clone()
    };
    for o in list {
        f(o.as_ref());
    }
}

#[derive(uniffi::Object)]
pub struct HistoryRepository {
    conn: Arc<Mutex<Connection>>,
    observers: Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>,
}

impl HistoryRepository {
    pub fn new(
        conn: Arc<Mutex<Connection>>,
        observers: Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>,
    ) -> Self {
        Self { conn, observers }
    }
}

#[uniffi::export]
impl HistoryRepository {
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

        tx.execute(
            "INSERT INTO ArtworkPayloadEntry (SerializedArtwork) VALUES (?1)",
            params![payload_json],
        )?;
        let payload_id = tx.last_insert_rowid();

        tx.execute(
            "INSERT INTO BrowseHistoryEntry (ArtworkPayloadEntryId, SerializeKey, WorkKey, Id) VALUES (?1, ?2, ?3, ?4)",
            params![payload_id, serialize_key, work_key, id],
        )?;
        let history_id = tx.last_insert_rowid();
        tx.commit()?;
        drop(conn);

        notify_observers(&self.observers, |o| o.on_browse_history_changed());

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

    pub fn stream_browse_history_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<BrowseHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut results = Vec::new();
        let mut broken_ids = Vec::new();

        if let Some(cid) = cursor_id {
            let mut stmt = conn.prepare(
                "SELECT b.HistoryEntryId, b.Id, b.SerializeKey, b.WorkKey, p.SerializedArtwork
                 FROM BrowseHistoryEntry b
                 LEFT JOIN ArtworkPayloadEntry p ON b.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 WHERE b.HistoryEntryId < ?1
                 ORDER BY b.HistoryEntryId DESC
                 LIMIT ?2",
            )?;
            let rows = stmt.query_map(params![cid, take], |row| {
                Ok(BrowseHistoryRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    serialize_key: row.get(2)?,
                    work_key: row.get(3)?,
                    payload_json: row.get(4)?,
                })
            })?;
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        } else {
            let mut stmt = conn.prepare(
                "SELECT b.HistoryEntryId, b.Id, b.SerializeKey, b.WorkKey, p.SerializedArtwork
                 FROM BrowseHistoryEntry b
                 LEFT JOIN ArtworkPayloadEntry p ON b.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 ORDER BY b.HistoryEntryId DESC
                 LIMIT ?1",
            )?;
            let rows = stmt.query_map(params![take], |row| {
                Ok(BrowseHistoryRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    serialize_key: row.get(2)?,
                    work_key: row.get(3)?,
                    payload_json: row.get(4)?,
                })
            })?;
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        }

        if !broken_ids.is_empty() {
            for id in broken_ids {
                let _ = conn.execute("DELETE FROM BrowseHistoryEntry WHERE HistoryEntryId = ?1", params![id]);
            }
        }

        Ok(results)
    }

    pub fn try_delete_browse_history_by_work_key(&self, work_key: String) -> Result<bool, StorageError> {
        let deleted = {
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
                true
            } else {
                false
            }
        };

        if deleted {
            notify_observers(&self.observers, |o| o.on_browse_history_changed());
        }

        Ok(deleted)
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
        drop(conn);

        notify_observers(&self.observers, |o| o.on_browse_history_changed());
        Ok(())
    }

    pub fn count_browse_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM BrowseHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
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
        drop(conn);

        notify_observers(&self.observers, |o| o.on_search_history_changed());

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
        drop(conn);

        notify_observers(&self.observers, |o| o.on_search_history_changed());

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

    pub fn stream_search_histories_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<SearchHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut results = Vec::new();

        if let Some(cid) = cursor_id {
            let mut stmt = conn.prepare(
                "SELECT HistoryEntryId, Value, TranslatedName, CAST(Time AS TEXT) FROM SearchHistoryEntry WHERE HistoryEntryId < ?1 ORDER BY HistoryEntryId DESC LIMIT ?2",
            )?;
            let rows = stmt.query_map(params![cid, take], |row| {
                Ok(SearchHistoryRecord {
                    history_entry_id: row.get(0)?,
                    value: row.get(1)?,
                    translated_name: row.get(2)?,
                    time: row.get(3)?,
                })
            })?;
            for r in rows {
                results.push(r?);
            }
        } else {
            let mut stmt = conn.prepare(
                "SELECT HistoryEntryId, Value, TranslatedName, CAST(Time AS TEXT) FROM SearchHistoryEntry ORDER BY HistoryEntryId DESC LIMIT ?1",
            )?;
            let rows = stmt.query_map(params![take], |row| {
                Ok(SearchHistoryRecord {
                    history_entry_id: row.get(0)?,
                    value: row.get(1)?,
                    translated_name: row.get(2)?,
                    time: row.get(3)?,
                })
            })?;
            for r in rows {
                results.push(r?);
            }
        }

        Ok(results)
    }

    pub fn query_search_history_suggestions(
        &self,
        pattern: &str,
        limit: u32,
    ) -> Result<Vec<SearchHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut results = Vec::new();
        let trimmed = pattern.trim();
        if trimmed.is_empty() {
            let mut stmt = conn.prepare(
                "SELECT HistoryEntryId, Value, TranslatedName, CAST(Time AS TEXT) FROM SearchHistoryEntry ORDER BY HistoryEntryId DESC LIMIT ?1",
            )?;
            let rows = stmt.query_map(params![limit], |row| {
                Ok(SearchHistoryRecord {
                    history_entry_id: row.get(0)?,
                    value: row.get(1)?,
                    translated_name: row.get(2)?,
                    time: row.get(3)?,
                })
            })?;
            for r in rows {
                results.push(r?);
            }
        } else {
            let like_pattern = format!("%{}%", trimmed);
            let prefix_pattern = format!("{}%", trimmed);
            let mut stmt = conn.prepare(
                "SELECT HistoryEntryId, Value, TranslatedName, CAST(Time AS TEXT) FROM SearchHistoryEntry WHERE Value LIKE ?1 OR (TranslatedName IS NOT NULL AND TranslatedName LIKE ?1) ORDER BY (CASE WHEN Value LIKE ?2 THEN 0 ELSE 1 END), HistoryEntryId DESC LIMIT ?3",
            )?;
            let rows = stmt.query_map(params![like_pattern, prefix_pattern, limit], |row| {
                Ok(SearchHistoryRecord {
                    history_entry_id: row.get(0)?,
                    value: row.get(1)?,
                    translated_name: row.get(2)?,
                    time: row.get(3)?,
                })
            })?;
            for r in rows {
                results.push(r?);
            }
        }
        Ok(results)
    }

    pub fn try_delete_search_history_by_value(&self, value: String) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let affected = conn.execute(
            "DELETE FROM SearchHistoryEntry WHERE Value = ?1",
            params![value],
        )?;
        drop(conn);

        if affected > 0 {
            notify_observers(&self.observers, |o| o.on_search_history_changed());
        }
        Ok(affected > 0)
    }

    pub fn clear_search_history(&self) -> Result<(), StorageError> {
        let conn = self.conn.lock();
        conn.execute("DELETE FROM SearchHistoryEntry", [])?;
        drop(conn);

        notify_observers(&self.observers, |o| o.on_search_history_changed());
        Ok(())
    }

    pub fn count_search_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM SearchHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
    }
}

#[derive(uniffi::Object)]
pub struct WatchLaterRepository {
    conn: Arc<Mutex<Connection>>,
    cached_keys: Arc<Mutex<HashSet<String>>>,
    observers: Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>,
}

impl WatchLaterRepository {
    pub fn new(
        conn: Arc<Mutex<Connection>>,
        observers: Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>,
    ) -> Self {
        let cached_keys = Arc::new(Mutex::new(HashSet::new()));
        {
            let conn_guard = conn.lock();
            if let Ok(mut stmt) = conn_guard.prepare("SELECT WorkKey FROM WatchLaterEntry") {
                if let Ok(rows) = stmt.query_map([], |row| row.get::<_, String>(0)) {
                    let mut keys = cached_keys.lock();
                    for r in rows.flatten() {
                        keys.insert(r);
                    }
                }
            }
        }

        Self {
            conn,
            cached_keys,
            observers,
        }
    }
}

#[uniffi::export]
impl WatchLaterRepository {
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
        drop(conn);

        self.cached_keys.lock().insert(work_key.clone());
        notify_observers(&self.observers, |o| o.on_watch_later_changed());

        Ok(WatchLaterRecord {
            history_entry_id: history_id,
            id,
            serialize_key,
            work_key,
            payload_json: Some(payload_json),
        })
    }

    pub fn contains_watch_later(&self, work_key: String) -> Result<bool, StorageError> {
        if self.cached_keys.lock().contains(&work_key) {
            return Ok(true);
        }

        let conn = self.conn.lock();
        let exists: Option<i64> = conn
            .query_row(
                "SELECT HistoryEntryId FROM WatchLaterEntry WHERE WorkKey = ?1",
                params![work_key],
                |row| row.get(0),
            )
            .optional()?;

        if exists.is_some() {
            self.cached_keys.lock().insert(work_key);
            Ok(true)
        } else {
            Ok(false)
        }
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
                self.cached_keys.lock().remove(&work_key);
                return Ok(None);
            }
        }

        Ok(record)
    }

    pub fn remove_watch_later(&self, work_key: String) -> Result<bool, StorageError> {
        let removed = {
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
                true
            } else {
                false
            }
        };

        if removed {
            self.cached_keys.lock().remove(&work_key);
            notify_observers(&self.observers, |o| o.on_watch_later_changed());
        }

        Ok(removed)
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
        drop(conn);

        self.cached_keys.lock().clear();
        notify_observers(&self.observers, |o| o.on_watch_later_changed());
        Ok(())
    }

    pub fn stream_watch_later(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<WatchLaterRecord>, StorageError> {
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
                let _ = conn.execute("DELETE FROM WatchLaterEntry WHERE HistoryEntryId = ?1", params![id]);
            }
        }

        Ok(results)
    }

    pub fn stream_watch_later_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<WatchLaterRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut results = Vec::new();
        let mut broken_ids = Vec::new();

        if let Some(cid) = cursor_id {
            let mut stmt = conn.prepare(
                "SELECT w.HistoryEntryId, w.Id, w.SerializeKey, w.WorkKey, p.SerializedArtwork
                 FROM WatchLaterEntry w
                 LEFT JOIN ArtworkPayloadEntry p ON w.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 WHERE w.HistoryEntryId < ?1
                 ORDER BY w.HistoryEntryId DESC
                 LIMIT ?2",
            )?;
            let rows = stmt.query_map(params![cid, take], |row| {
                Ok(WatchLaterRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    serialize_key: row.get(2)?,
                    work_key: row.get(3)?,
                    payload_json: row.get(4)?,
                })
            })?;
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        } else {
            let mut stmt = conn.prepare(
                "SELECT w.HistoryEntryId, w.Id, w.SerializeKey, w.WorkKey, p.SerializedArtwork
                 FROM WatchLaterEntry w
                 LEFT JOIN ArtworkPayloadEntry p ON w.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 ORDER BY w.HistoryEntryId DESC
                 LIMIT ?1",
            )?;
            let rows = stmt.query_map(params![take], |row| {
                Ok(WatchLaterRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    serialize_key: row.get(2)?,
                    work_key: row.get(3)?,
                    payload_json: row.get(4)?,
                })
            })?;
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        }

        if !broken_ids.is_empty() {
            for id in broken_ids {
                let _ = conn.execute("DELETE FROM WatchLaterEntry WHERE HistoryEntryId = ?1", params![id]);
            }
        }

        Ok(results)
    }

    pub fn count_watch_later(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM WatchLaterEntry", [], |row| row.get(0))?;
        Ok(count)
    }
}

#[derive(uniffi::Object)]
pub struct DownloadRepository {
    conn: Arc<Mutex<Connection>>,
    observers: Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>,
}

impl DownloadRepository {
    pub fn new(
        conn: Arc<Mutex<Connection>>,
        observers: Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>,
    ) -> Self {
        Self { conn, observers }
    }
}

#[uniffi::export]
impl DownloadRepository {
    // --- Regular Download History ---

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
        drop(conn);

        notify_observers(&self.observers, |o| o.on_download_history_changed());

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

    pub fn update_download_history_state(
        &self,
        destination: String,
        state: u32,
        error_message: Option<String>,
    ) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let affected = conn.execute(
            "UPDATE DownloadHistoryEntry SET State = ?1, ErrorMessage = ?2 WHERE Destination = ?3",
            params![state, error_message, destination],
        )?;
        drop(conn);

        if affected > 0 {
            notify_observers(&self.observers, |o| o.on_download_history_changed());
        }
        Ok(affected > 0)
    }

    pub fn try_delete_download_history_by_destination(&self, destination: String) -> Result<bool, StorageError> {
        let deleted = {
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
                true
            } else {
                false
            }
        };

        if deleted {
            notify_observers(&self.observers, |o| o.on_download_history_changed());
        }

        Ok(deleted)
    }

    pub fn stream_download_history(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<DownloadHistoryRecord>, StorageError> {
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
                let _ = conn.execute("DELETE FROM DownloadHistoryEntry WHERE HistoryEntryId = ?1", params![id]);
            }
        }

        Ok(results)
    }

    pub fn stream_download_history_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<DownloadHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut results = Vec::new();
        let mut broken_ids = Vec::new();

        if let Some(cid) = cursor_id {
            let mut stmt = conn.prepare(
                "SELECT d.HistoryEntryId, d.SerializeKey, d.Destination, d.State, d.FormatToken, d.ErrorMessage, p.SerializedArtwork
                 FROM DownloadHistoryEntry d
                 LEFT JOIN ArtworkPayloadEntry p ON d.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 WHERE d.HistoryEntryId < ?1
                 ORDER BY d.HistoryEntryId DESC
                 LIMIT ?2",
            )?;
            let rows = stmt.query_map(params![cid, take], |row| {
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
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        } else {
            let mut stmt = conn.prepare(
                "SELECT d.HistoryEntryId, d.SerializeKey, d.Destination, d.State, d.FormatToken, d.ErrorMessage, p.SerializedArtwork
                 FROM DownloadHistoryEntry d
                 LEFT JOIN ArtworkPayloadEntry p ON d.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 ORDER BY d.HistoryEntryId DESC
                 LIMIT ?1",
            )?;
            let rows = stmt.query_map(params![take], |row| {
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
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        }

        if !broken_ids.is_empty() {
            for id in broken_ids {
                let _ = conn.execute("DELETE FROM DownloadHistoryEntry WHERE HistoryEntryId = ?1", params![id]);
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
        drop(conn);

        notify_observers(&self.observers, |o| o.on_download_history_changed());
        Ok(())
    }

    pub fn count_download_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM DownloadHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
    }

    pub fn get_download_history_by_destination(
        &self,
        destination: String,
    ) -> Result<Option<DownloadHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT d.HistoryEntryId, d.SerializeKey, d.Destination, d.State, d.FormatToken, d.ErrorMessage, p.SerializedArtwork
             FROM DownloadHistoryEntry d
             LEFT JOIN ArtworkPayloadEntry p ON d.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             WHERE d.Destination = ?1",
        )?;
        let record = stmt
            .query_row(params![destination], |row| {
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
            })
            .optional()?;

        if let Some(ref r) = record {
            if r.payload_json.is_none() {
                drop(stmt);
                conn.execute(
                    "DELETE FROM DownloadHistoryEntry WHERE HistoryEntryId = ?1",
                    params![r.history_entry_id],
                )?;
                return Ok(None);
            }
        }

        Ok(record)
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
            "INSERT INTO SubscriptionDownloadHistoryEntry (
                ArtworkPayloadEntryId, SerializeKey, Destination, WorkSubscriptionId, ArtworkId, State, FormatToken, ErrorMessage
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
            params![payload_id, serialize_key, destination, work_subscription_id, artwork_id, state, format_token, error_message],
        )?;
        let history_id = tx.last_insert_rowid();
        tx.commit()?;
        drop(conn);

        notify_observers(&self.observers, |o| o.on_download_history_changed());

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
        if entries.is_empty() {
            return Ok(());
        }

        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        for entry in &entries {
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

            let payload_str = entry.payload_json.as_deref().unwrap_or("{}");
            tx.execute(
                "INSERT INTO ArtworkPayloadEntry (SerializedArtwork) VALUES (?1)",
                params![payload_str],
            )?;
            let payload_id = tx.last_insert_rowid();

            tx.execute(
                "INSERT INTO SubscriptionDownloadHistoryEntry (
                    ArtworkPayloadEntryId, SerializeKey, Destination, WorkSubscriptionId, ArtworkId, State, FormatToken, ErrorMessage
                 ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
                params![payload_id, entry.serialize_key, entry.destination, entry.work_subscription_id, entry.artwork_id, entry.state, entry.format_token, entry.error_message],
            )?;
        }

        tx.commit()?;
        drop(conn);

        notify_observers(&self.observers, |o| o.on_download_history_changed());
        Ok(())
    }

    pub fn update_subscription_download_history_state(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
        state: u32,
        error_message: Option<String>,
    ) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let affected = conn.execute(
            "UPDATE SubscriptionDownloadHistoryEntry SET State = ?1, ErrorMessage = ?2
             WHERE WorkSubscriptionId = ?3 AND ArtworkId = ?4 AND Destination = ?5",
            params![state, error_message, work_subscription_id, artwork_id, destination],
        )?;
        drop(conn);

        if affected > 0 {
            notify_observers(&self.observers, |o| o.on_download_history_changed());
        }
        Ok(affected > 0)
    }

    pub fn contains_subscription_download_identity(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
    ) -> Result<bool, StorageError> {
        let conn = self.conn.lock();
        let exists: Option<i64> = conn
            .query_row(
                "SELECT HistoryEntryId FROM SubscriptionDownloadHistoryEntry
                 WHERE WorkSubscriptionId = ?1 AND ArtworkId = ?2 AND Destination = ?3",
                params![work_subscription_id, artwork_id, destination],
                |row| row.get(0),
            )
            .optional()?;
        Ok(exists.is_some())
    }

    pub fn try_delete_subscription_download_by_identity(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
    ) -> Result<bool, StorageError> {
        let deleted = {
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
                true
            } else {
                false
            }
        };

        if deleted {
            notify_observers(&self.observers, |o| o.on_download_history_changed());
        }

        Ok(deleted)
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
        let affected = tx.execute(
            "DELETE FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId = ?1",
            params![work_subscription_id],
        )?;
        tx.commit()?;
        drop(conn);

        if affected > 0 {
            notify_observers(&self.observers, |o| o.on_download_history_changed());
        }

        Ok(affected as i64)
    }

    pub fn delete_orphan_subscription_downloads(
        &self,
        valid_subscription_ids: Vec<i64>,
    ) -> Result<i64, StorageError> {
        let mut conn = self.conn.lock();
        let tx = conn.transaction()?;

        let affected = if valid_subscription_ids.is_empty() {
            tx.execute(
                "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (
                    SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry
                 )",
                [],
            )?;
            tx.execute("DELETE FROM SubscriptionDownloadHistoryEntry", [])?
        } else {
            let id_list = valid_subscription_ids
                .iter()
                .map(|id| id.to_string())
                .collect::<Vec<_>>()
                .join(",");
            let sql_payload = format!(
                "DELETE FROM ArtworkPayloadEntry WHERE ArtworkPayloadEntryId IN (
                    SELECT ArtworkPayloadEntryId FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId NOT IN ({id_list})
                 )"
            );
            tx.execute(&sql_payload, [])?;
            let sql = format!("DELETE FROM SubscriptionDownloadHistoryEntry WHERE WorkSubscriptionId NOT IN ({id_list})");
            tx.execute(&sql, [])?
        };

        tx.commit()?;
        drop(conn);

        if affected > 0 {
            notify_observers(&self.observers, |o| o.on_download_history_changed());
        }

        Ok(affected as i64)
    }

    pub fn stream_subscription_download_history(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<SubscriptionDownloadHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut stmt = conn.prepare(
            "SELECT s.HistoryEntryId, s.ArtworkId, s.WorkSubscriptionId, s.SerializeKey, s.Destination, s.State, s.FormatToken, s.ErrorMessage, p.SerializedArtwork
             FROM SubscriptionDownloadHistoryEntry s
             LEFT JOIN ArtworkPayloadEntry p ON s.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
             ORDER BY s.HistoryEntryId DESC
             LIMIT ?1 OFFSET ?2",
        )?;
        let rows = stmt.query_map(params![take, skip], |row| {
            Ok(SubscriptionDownloadHistoryRecord {
                history_entry_id: row.get(0)?,
                id: row.get(1)?,
                artwork_id: row.get(1)?,
                work_subscription_id: row.get(2)?,
                serialize_key: row.get(3)?,
                destination: row.get(4)?,
                state: row.get(5)?,
                format_token: row.get(6)?,
                error_message: row.get(7)?,
                payload_json: row.get(8)?,
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
                let _ = conn.execute("DELETE FROM SubscriptionDownloadHistoryEntry WHERE HistoryEntryId = ?1", params![id]);
            }
        }

        Ok(results)
    }

    pub fn stream_subscription_download_history_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<SubscriptionDownloadHistoryRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut results = Vec::new();
        let mut broken_ids = Vec::new();

        if let Some(cid) = cursor_id {
            let mut stmt = conn.prepare(
                "SELECT s.HistoryEntryId, s.ArtworkId, s.WorkSubscriptionId, s.SerializeKey, s.Destination, s.State, s.FormatToken, s.ErrorMessage, p.SerializedArtwork
                 FROM SubscriptionDownloadHistoryEntry s
                 LEFT JOIN ArtworkPayloadEntry p ON s.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 WHERE s.HistoryEntryId < ?1
                 ORDER BY s.HistoryEntryId DESC
                 LIMIT ?2",
            )?;
            let rows = stmt.query_map(params![cid, take], |row| {
                Ok(SubscriptionDownloadHistoryRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    artwork_id: row.get(1)?,
                    work_subscription_id: row.get(2)?,
                    serialize_key: row.get(3)?,
                    destination: row.get(4)?,
                    state: row.get(5)?,
                    format_token: row.get(6)?,
                    error_message: row.get(7)?,
                    payload_json: row.get(8)?,
                })
            })?;
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        } else {
            let mut stmt = conn.prepare(
                "SELECT s.HistoryEntryId, s.ArtworkId, s.WorkSubscriptionId, s.SerializeKey, s.Destination, s.State, s.FormatToken, s.ErrorMessage, p.SerializedArtwork
                 FROM SubscriptionDownloadHistoryEntry s
                 LEFT JOIN ArtworkPayloadEntry p ON s.ArtworkPayloadEntryId = p.ArtworkPayloadEntryId
                 ORDER BY s.HistoryEntryId DESC
                 LIMIT ?1",
            )?;
            let rows = stmt.query_map(params![take], |row| {
                Ok(SubscriptionDownloadHistoryRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    artwork_id: row.get(1)?,
                    work_subscription_id: row.get(2)?,
                    serialize_key: row.get(3)?,
                    destination: row.get(4)?,
                    state: row.get(5)?,
                    format_token: row.get(6)?,
                    error_message: row.get(7)?,
                    payload_json: row.get(8)?,
                })
            })?;
            for r in rows {
                let record = r?;
                if record.payload_json.is_none() {
                    broken_ids.push(record.history_entry_id);
                } else {
                    results.push(record);
                }
            }
        }

        if !broken_ids.is_empty() {
            for id in broken_ids {
                let _ = conn.execute("DELETE FROM SubscriptionDownloadHistoryEntry WHERE HistoryEntryId = ?1", params![id]);
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
        drop(conn);

        notify_observers(&self.observers, |o| o.on_download_history_changed());
        Ok(())
    }

    pub fn count_subscription_download_history(&self) -> Result<i64, StorageError> {
        let conn = self.conn.lock();
        let count: i64 = conn.query_row("SELECT COUNT(*) FROM SubscriptionDownloadHistoryEntry", [], |row| row.get(0))?;
        Ok(count)
    }
}
