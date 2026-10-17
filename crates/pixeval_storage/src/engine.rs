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

    pub fn query_subscription_author_suggestions(
        &self,
        pattern: String,
        limit: u32,
    ) -> Result<Vec<WorkSubscriptionRecord>, StorageError> {
        let conn = self.conn.lock();
        let mut results = Vec::new();
        let trimmed = pattern.trim();
        if trimmed.is_empty() {
            let mut stmt = conn.prepare(
                "SELECT HistoryEntryId, Id, SubscriptionType, WorkKind, Name, Account, AvatarUrl
                 FROM WorkSubscriptionEntry
                 WHERE Name != ''
                 GROUP BY Name, Account
                 ORDER BY HistoryEntryId DESC
                 LIMIT ?1",
            )?;
            let rows = stmt.query_map(params![limit], |row| {
                Ok(WorkSubscriptionRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    subscription_type: row.get(2)?,
                    work_kind: row.get(3)?,
                    title: row.get(4)?,
                    author: row.get(5)?,
                    avatar: row.get(6)?,


                })
            })?;
            for r in rows {
                results.push(r?);
            }
        } else {
            let like_pattern = format!("%{}%", trimmed);
            let prefix_pattern = format!("{}%", trimmed);
            let mut stmt = conn.prepare(
                "SELECT HistoryEntryId, Id, SubscriptionType, WorkKind, Name, Account, AvatarUrl
                 FROM WorkSubscriptionEntry
                 WHERE Name != '' AND (Name LIKE ?1 OR Account LIKE ?1)
                 GROUP BY Name, Account
                 ORDER BY (CASE WHEN Name LIKE ?2 OR Account LIKE ?2 THEN 0 ELSE 1 END), HistoryEntryId DESC
                 LIMIT ?3",
            )?;
            let rows = stmt.query_map(params![like_pattern, prefix_pattern, limit], |row| {
                Ok(WorkSubscriptionRecord {
                    history_entry_id: row.get(0)?,
                    id: row.get(1)?,
                    subscription_type: row.get(2)?,
                    work_kind: row.get(3)?,
                    title: row.get(4)?,
                    author: row.get(5)?,
                    avatar: row.get(6)?,


                })
            })?;
            for r in rows {
                results.push(r?);
            }
        }
        Ok(results)
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
