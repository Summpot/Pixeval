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
    observers: Arc<Mutex<Vec<Arc<dyn StorageObserver>>>>,
    history_repo: Arc<crate::repository::HistoryRepository>,
    watch_later_repo: Arc<crate::repository::WatchLaterRepository>,
    download_repo: Arc<crate::repository::DownloadRepository>,
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

        let conn_arc = Arc::new(Mutex::new(conn));
        let observers = Arc::new(Mutex::new(Vec::new()));
        let history_repo = Arc::new(crate::repository::HistoryRepository::new(conn_arc.clone(), observers.clone()));
        let watch_later_repo = Arc::new(crate::repository::WatchLaterRepository::new(conn_arc.clone(), observers.clone()));
        let download_repo = Arc::new(crate::repository::DownloadRepository::new(conn_arc.clone(), observers.clone()));

        Ok(Self {
            conn: conn_arc,
            observers,
            history_repo,
            watch_later_repo,
            download_repo,
        })
    }

    pub fn register_observer(&self, observer: Box<dyn StorageObserver>) {
        self.observers.lock().push(Arc::from(observer));
    }

    pub fn history(&self) -> Arc<crate::repository::HistoryRepository> {
        self.history_repo.clone()
    }

    pub fn watch_later(&self) -> Arc<crate::repository::WatchLaterRepository> {
        self.watch_later_repo.clone()
    }

    pub fn download(&self) -> Arc<crate::repository::DownloadRepository> {
        self.download_repo.clone()
    }

    // --- Search History ---

    pub fn insert_search_history(
        &self,
        value: String,
        translated_name: Option<String>,
        time: String,
    ) -> Result<SearchHistoryRecord, StorageError> {
        self.history_repo.insert_search_history(value, translated_name, time)
    }

    pub fn upsert_search_history(
        &self,
        value: String,
        translated_name: Option<String>,
        time: String,
    ) -> Result<SearchHistoryRecord, StorageError> {
        self.history_repo.upsert_search_history(value, translated_name, time)
    }

    pub fn get_search_history_by_value(
        &self,
        value: String,
    ) -> Result<Option<SearchHistoryRecord>, StorageError> {
        self.history_repo.get_search_history_by_value(value)
    }

    pub fn stream_search_histories(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<SearchHistoryRecord>, StorageError> {
        self.history_repo.stream_search_histories(skip, take)
    }

    pub fn stream_search_histories_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<SearchHistoryRecord>, StorageError> {
        self.history_repo.stream_search_histories_cursor(cursor_id, take)
    }

    pub fn try_delete_search_history_by_value(&self, value: String) -> Result<bool, StorageError> {
        self.history_repo.try_delete_search_history_by_value(value)
    }

    pub fn clear_search_history(&self) -> Result<(), StorageError> {
        self.history_repo.clear_search_history()
    }

    pub fn count_search_history(&self) -> Result<i64, StorageError> {
        self.history_repo.count_search_history()
    }

    // --- Browse History ---

    pub fn add_or_replace_browse_history(
        &self,
        id: String,
        serialize_key: Option<String>,
        work_key: String,
        payload_json: String,
    ) -> Result<BrowseHistoryRecord, StorageError> {
        self.history_repo.add_or_replace_browse_history(id, serialize_key, work_key, payload_json)
    }

    pub fn get_browse_history_by_work_key(
        &self,
        work_key: String,
    ) -> Result<Option<BrowseHistoryRecord>, StorageError> {
        self.history_repo.get_browse_history_by_work_key(work_key)
    }

    pub fn stream_browse_history(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<BrowseHistoryRecord>, StorageError> {
        self.history_repo.stream_browse_history(skip, take)
    }

    pub fn stream_browse_history_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<BrowseHistoryRecord>, StorageError> {
        self.history_repo.stream_browse_history_cursor(cursor_id, take)
    }

    pub fn try_delete_browse_history_by_work_key(&self, work_key: String) -> Result<bool, StorageError> {
        self.history_repo.try_delete_browse_history_by_work_key(work_key)
    }

    pub fn clear_browse_history(&self) -> Result<(), StorageError> {
        self.history_repo.clear_browse_history()
    }

    pub fn count_browse_history(&self) -> Result<i64, StorageError> {
        self.history_repo.count_browse_history()
    }

    // --- Watch Later ---

    pub fn add_or_replace_watch_later(
        &self,
        id: String,
        serialize_key: Option<String>,
        work_key: String,
        payload_json: String,
    ) -> Result<WatchLaterRecord, StorageError> {
        self.watch_later_repo.add_or_replace_watch_later(id, serialize_key, work_key, payload_json)
    }

    pub fn contains_watch_later(&self, work_key: String) -> Result<bool, StorageError> {
        self.watch_later_repo.contains_watch_later(work_key)
    }

    pub fn remove_watch_later(&self, work_key: String) -> Result<bool, StorageError> {
        self.watch_later_repo.remove_watch_later(work_key)
    }

    pub fn stream_watch_later(&self, skip: u32, take: u32) -> Result<Vec<WatchLaterRecord>, StorageError> {
        self.watch_later_repo.stream_watch_later(skip, take)
    }

    pub fn stream_watch_later_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<WatchLaterRecord>, StorageError> {
        self.watch_later_repo.stream_watch_later_cursor(cursor_id, take)
    }

    pub fn clear_watch_later(&self) -> Result<(), StorageError> {
        self.watch_later_repo.clear_watch_later()
    }

    pub fn count_watch_later(&self) -> Result<i64, StorageError> {
        self.watch_later_repo.count_watch_later()
    }

    pub fn get_watch_later_by_work_key(
        &self,
        work_key: String,
    ) -> Result<Option<WatchLaterRecord>, StorageError> {
        self.watch_later_repo.get_watch_later_by_work_key(work_key)
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
        self.download_repo.add_or_replace_download_history(
            id,
            serialize_key,
            destination,
            state,
            format_token,
            error_message,
            payload_json,
        )
    }

    pub fn update_download_history_state(
        &self,
        destination: String,
        state: u32,
        error_message: Option<String>,
    ) -> Result<bool, StorageError> {
        self.download_repo.update_download_history_state(destination, state, error_message)
    }

    pub fn try_delete_download_history_by_destination(&self, destination: String) -> Result<bool, StorageError> {
        self.download_repo.try_delete_download_history_by_destination(destination)
    }

    pub fn stream_download_history(&self, skip: u32, take: u32) -> Result<Vec<DownloadHistoryRecord>, StorageError> {
        self.download_repo.stream_download_history(skip, take)
    }

    pub fn stream_download_history_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<DownloadHistoryRecord>, StorageError> {
        self.download_repo.stream_download_history_cursor(cursor_id, take)
    }

    pub fn clear_download_history(&self) -> Result<(), StorageError> {
        self.download_repo.clear_download_history()
    }

    pub fn count_download_history(&self) -> Result<i64, StorageError> {
        self.download_repo.count_download_history()
    }

    pub fn get_download_history_by_destination(
        &self,
        destination: String,
    ) -> Result<Option<DownloadHistoryRecord>, StorageError> {
        self.download_repo.get_download_history_by_destination(destination)
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
        self.download_repo.add_or_replace_subscription_download_history(
            id,
            serialize_key,
            destination,
            state,
            format_token,
            error_message,
            work_subscription_id,
            artwork_id,
            payload_json,
        )
    }

    pub fn add_or_replace_subscription_download_history_batch(
        &self,
        entries: Vec<SubscriptionDownloadHistoryRecord>,
    ) -> Result<(), StorageError> {
        self.download_repo.add_or_replace_subscription_download_history_batch(entries)
    }

    pub fn update_subscription_download_history_state(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
        state: u32,
        error_message: Option<String>,
    ) -> Result<bool, StorageError> {
        self.download_repo.update_subscription_download_history_state(
            work_subscription_id,
            artwork_id,
            destination,
            state,
            error_message,
        )
    }

    pub fn contains_subscription_download_identity(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
    ) -> Result<bool, StorageError> {
        self.download_repo.contains_subscription_download_identity(work_subscription_id, artwork_id, destination)
    }

    pub fn try_delete_subscription_download_by_identity(
        &self,
        work_subscription_id: i64,
        artwork_id: String,
        destination: String,
    ) -> Result<bool, StorageError> {
        self.download_repo.try_delete_subscription_download_by_identity(
            work_subscription_id,
            artwork_id,
            destination,
        )
    }

    pub fn delete_subscription_downloads_by_work_subscription_id(
        &self,
        work_subscription_id: i64,
    ) -> Result<i64, StorageError> {
        self.download_repo.delete_subscription_downloads_by_work_subscription_id(work_subscription_id)
    }

    pub fn delete_orphan_subscription_downloads(
        &self,
        valid_work_subscription_ids: Vec<i64>,
    ) -> Result<i64, StorageError> {
        self.download_repo.delete_orphan_subscription_downloads(valid_work_subscription_ids)
    }

    pub fn stream_subscription_download_history(
        &self,
        skip: u32,
        take: u32,
    ) -> Result<Vec<SubscriptionDownloadHistoryRecord>, StorageError> {
        self.download_repo.stream_subscription_download_history(skip, take)
    }

    pub fn stream_subscription_download_history_cursor(
        &self,
        cursor_id: Option<i64>,
        take: u32,
    ) -> Result<Vec<SubscriptionDownloadHistoryRecord>, StorageError> {
        self.download_repo.stream_subscription_download_history_cursor(cursor_id, take)
    }

    pub fn clear_subscription_download_history(&self) -> Result<(), StorageError> {
        self.download_repo.clear_subscription_download_history()
    }

    pub fn count_subscription_download_history(&self) -> Result<i64, StorageError> {
        self.download_repo.count_subscription_download_history()
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

        let existing: Option<i64> = if record.history_entry_id > 0 {
            conn.query_row(
                "SELECT HistoryEntryId FROM LoginUserEntry WHERE HistoryEntryId = ?1 OR UserId = ?2 LIMIT 1",
                params![record.history_entry_id, record.user_id],
                |row| row.get(0),
            )
            .optional()?
        } else {
            conn.query_row(
                "SELECT HistoryEntryId FROM LoginUserEntry WHERE UserId = ?1",
                params![record.user_id],
                |row| row.get(0),
            )
            .optional()?
        };

        let history_entry_id = if let Some(existing_id) = existing {
            conn.execute(
                "UPDATE LoginUserEntry
                 SET UserId = ?1, RefreshToken = ?2, Name = ?3, Account = ?4, MailAddress = ?5, IsPremium = ?6,
                     XRestrict = ?7, IsMailAuthorized = ?8, RequirePolicyAgreement = ?9,
                     Avatar16Url = ?10, Avatar50Url = ?11, Avatar170Url = ?12
                 WHERE HistoryEntryId = ?13",
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
