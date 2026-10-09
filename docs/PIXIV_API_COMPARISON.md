# Pixeval vs. Pixiv Android 6.199.0 — Pixiv API 使用对照报告

- 对比对象 A：`C:\Users\Summp\Documents\GitHub\Pixeval`（当前工作区，Rust Core 迁移中）
- 对比对象 B：`C:\Users\Summp\Downloads\pixiv_6.199.0_APKPure.apk_Decompiler.com`（官方 Android 6.199.0 反编译产物）
- 生成时间：2026-10-09

---

## 0. 结论摘要

| 指标 | Pixeval | Pixiv Android 6.199.0 |
|---|---|---|
| app-api 端点（去重） | **67** | **150** |
| 双方共有 | **61** | **61** |
| 仅一方拥有 | 6 | 89 |

**差异确实存在，且是单向的**：Pixeval 实现了官方 150 个端点中的 61 个（约 **41%** 覆盖率）；另有 6 个端点官方 APK 中不存在。缺失的 89 个端点**按功能域高度集中**——几乎全部落在「社交互动、通知、举报、内容创作/上传、账号与商业化、站内信息流」这些**只读浏览客户端用不到**的能力上。

同时发现 **3 处 Pixeval 正在使用、但官方 App 已不再使用的端点**（协议退化风险，旧 C# Mako 库里还留着），详见 §4。

---

## 1. 当前 Pixeval 实际使用的 PixivAPI

### 1.1 架构现状

Pixeval 目前已**完成 Rust 化**，运行时真正的 Pixiv API 客户端是 `crates/pixeval_mako`，底层走自研 `crates/pixeval_maho` 传输层（TLS SNI 分片 / 域前置）。

| 层 | 位置 | 状态 |
|---|---|---|
| 运行时实际使用 | [client.rs](crates/pixeval_mako/src/client.rs) | ✅ 活跃 |
| 认证 | [auth.rs](crates/pixeval_mako/src/auth.rs) | ✅ 活跃 |
| 传输 | [pixeval_maho](crates/pixeval_maho/src/config.rs) | ✅ 活跃 |
| 旧 C# SDK | [src/lib/Mako](src/lib/Mako) | ❌ **死代码**：不在 `Pixeval.slnx` 中，仅被自身的 `Mako.Tests` 引用 |

### 1.2 主机（Base URL）

| 用途 | 主机 | 常量位置 |
|---|---|---|
| App API | `https://app-api.pixiv.net` | [client.rs:19](crates/pixeval_mako/src/client.rs#L19) |
| OAuth | `https://oauth.secure.pixiv.net/auth/token` | [auth.rs:9](crates/pixeval_mako/src/auth.rs#L9) |
| 图片 CDN | `i.pximg.net` / `s.pximg.net` | [config.rs:10-11](crates/pixeval_maho/src/config.rs#L10-L11) |
| Web API | `www.pixiv.net` | 仅用于域前置/Referer，**未实际调用其接口** |
| 账号 | `accounts.pixiv.net` | 仅常量声明，未调用 |

### 1.3 端点清单（67 个）

**认证（1）**
```
POST https://oauth.secure.pixiv.net/auth/token   (refresh_token / authorization_code)
```

**作品详情与系列（7）**
```
GET  /v1/illust/detail
GET  /v2/novel/detail
GET  /v1/user/detail          ← 官方已移除该版本，只有 /v2/user/detail
GET  /v1/ugoira/metadata
GET  /v1/illust/series
GET  /v2/novel/series
GET  /v1/illust-series/illust
```

**推荐 / 排行 / 新作 / 关注（12）**
```
GET  /v1/illust/recommended
GET  /v1/novel/recommended
GET  /v1/illust/ranking
GET  /v1/novel/ranking
GET  /v1/illust/new
GET  /v1/novel/new
GET  /v2/illust/follow
GET  /v1/novel/follow
GET  /v1/spotlight/articles
GET  /v1/trending-tags/illust | /novel
GET  /v1/search/options
GET  /v2/search/autocomplete
```

**搜索（3）**
```
GET  /v1/search/illust
GET  /v1/search/novel
GET  /v1/search/user
```

**用户关系与作品列表（10）**
```
GET  /v1/user/illusts
GET  /v1/user/novels
GET  /v1/user/following
GET  /v1/user/follower
GET  /v1/user/mypixiv
GET  /v1/user/related
GET  /v1/user/recommended
GET  /v2/illust/mypixiv
GET  /v2/novel/mypixiv          ← 官方 APK 中不存在
GET  /v1/user/detail            ← 官方 APK 中不存在
```

**收藏 / 追更（12）**
```
GET  /v2/illust/bookmark/detail
GET  /v2/novel/bookmark/detail
POST /v2/illust/bookmark/add
POST /v2/novel/bookmark/add
POST /v1/illust/bookmark/delete
POST /v1/novel/bookmark/delete
GET  /v1/user/bookmarks/illust
GET  /v1/user/bookmarks/novel
GET  /v1/user/bookmark-tags/illust | /novel
GET  /v1/watchlist/manga | /novel
POST /v1/watchlist/manga|novel/add | /delete
```

**评论（9）**
```
GET  /v3/illust/comments
GET  /v3/novel/comments
GET  /v2/illust/comment/replies
GET  /v2/novel/comment/replies
POST /v1/illust/comment/add | /delete
POST /v1/novel/comment/add | /delete
```

**设置（4）**
```
GET  /v1/user/ai-show-settings
POST /v1/user/ai-show-settings/edit
GET  /v1/user/restricted-mode-settings
POST /v1/user/restricted-mode-settings
```

**小说正文（2）**
```
GET  /webview/v2/novel       ← 官方走「同一路径但参数不同」，非独立端点
GET  /v1/novel/text          ← 官方 APK 中不存在（旧版端点）
```

### 1.4 请求头（与官方对齐程度）

| Header | Pixeval | 官方 APK | 差异 |
|---|---|---|---|
| `User-Agent` | `PixivAndroidApp/6.140.2 (Android 15.0)` | `PixivAndroidApp/6.199.0 (Android <ver>; <model>)` | ⚠️ 版本落后 + 无设备信息 |
| `App-OS` | `android` | `android` | ✅ |
| `App-OS-Version` | `15.0`（硬编码） | `Build.VERSION.RELEASE` | ⚠️ 硬编码 |
| `App-Version` | `6.140.2` | `6.199.0` | ⚠️ 落后 |
| `Authorization` | `Bearer <token>` | ✅ | ✅ |
| `Accept-Language` | ❌ 未发送 | `Locale.getDefault()` | ❌ 缺失 |
| `app-accept-language` | ❌ 未发送 | ✅ 发送 | ❌ 缺失 |
| `X-Client-Time` | ❌ 未发送 | `yyyy-MM-dd'T'HH:mm:ssZZZZZ` | ❌ 缺失 |
| `X-Client-Hash` | ❌ 未发送 | `MD5(time + "28c1fdd170a5204386cb1313c7077b34f83e4aaf4aa829ce78c231e05b0bae2c")` | ❌ **缺失（风控指纹）** |
| `Referer` | 仅下载/图片链路带 | 全 API 请求带 `https://app-api.pixiv.net/` | ⚠️ 覆盖不全 |
| `Cookie` | 可选透传 | — | — |

**风险提示**：`X-Client-Hash` 是官方用来校验客户端真实性的指纹头（[h6c.java](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/h6c.java)、[sy7.java](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/sy7.java)）。Pixeval 完全不带，且 UA/App-Version 停在 6.140.2，是服务端限流/封禁的潜在触发点。

---

## 2. 官方 APK 使用的 PixivAPI

反编译产物包含 **48 个 Retrofit 接口**（`@o14` = GET，`@ro7` = POST），共 **150 个唯一端点路径**。

### 2.1 主机（Base URL）

| 主机 | 用途 | 证据 |
|---|---|---|
| `https://app-api.pixiv.net` | 主 API（3 个 Retrofit 实例：JSON 表单 / 带拦截器 / 固定 UA） | [c62.java:76,265,701](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/c62.java) |
| `https://oauth.secure.pixiv.net` | OAuth token | [c62.java:301](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/c62.java) |
| `https://accounts.pixiv.net` | 账号设置 / 退会 | [c62.java:884](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/c62.java) |
| `https://pixon.ads-pixiv.net` | 广告投放 | [c62.java:1415](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/c62.java) |
| `https://open-pixon.ads-pixiv.net/` | 广告 SSP | [c62.java:631](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/c62.java) |
| `https://www.pixiv.net` | 分享链接 / 规约 / 帮助（WebView） | 多处字符串 |
| `https://www.pixivision.net` | Pixivision | 1 处 |
| `https://touch.pixiv.net` | Premium 引导页 | 2 处 |
| `https://policies.pixiv.net` | 隐私政策（30 处引用，最高频） | 字符串资源 |
| `https://source.pixiv.net` | 默认头像等静态资源 | 3 处 |

> 注：官方 App **完全不调用** `www.pixiv.net` 的 `/ajax/*` 或 `/stacc/*` 私有 Web 接口，全部业务走 `app-api`。旧 C# Mako 中的 `FeedEngine`（`/stacc?mode=unify`）是一条**已失效的 Web 抓取路线**。

### 2.2 端点清单（150 个，按功能域）

<details>
<summary><b>作品 / 系列 / 推荐（展开）</b></summary>

```
GET  /v1/illust/detail?filter=for_android
GET  /v2/novel/detail
GET  /v2/user/detail?filter=for_android
GET  /v1/ugoira/metadata
GET  /v1/illust/series?filter=for_android
GET  /v1/illust-series/illust?filter=for_android
GET  /v2/novel/series
GET  /v2/illust/related?filter=for_android
GET  /v2/illust/follow
GET  /v1/illust/new?filter=for_android
GET  /v1/illust/ranking?filter=for_android
GET  /v1/illust/recommended?filter=for_android
GET  /v1/manga/recommended?filter=for_android
GET  /v1/novel/new
GET  /v1/novel/ranking
POST /v1/novel/recommended
POST /v1/novel/related
GET  /v1/novel/follow
POST /v1/illust/delete
POST /v1/novel/delete
```
</details>

<details>
<summary><b>搜索（展开）</b></summary>

```
GET  /v1/search/illust      (+include_translated_tag_results / merge_plain_keyword_results)
GET  /v1/search/novel
GET  /v1/search/user
GET  /v1/search/options
GET  /v1/search/popular-preview/illust
GET  /v1/search/popular-preview/novel
GET  /v2/search/autocomplete
GET  /v1/trending-tags/illust
GET  /v1/trending-tags/novel
GET  /v1/walkthrough/illusts
```
</details>

<details>
<summary><b>收藏 / 追更 / 浏览历史（展开）</b></summary>

```
GET  /v2/illust/bookmark/detail
GET  /v2/novel/bookmark/detail
POST /v2/illust/bookmark/add
POST /v2/novel/bookmark/add
POST /v1/illust/bookmark/delete
POST /v1/novel/bookmark/delete
GET  /v1/user/bookmarks/illust
GET  /v1/user/bookmarks/novel
GET  /v1/user/bookmark-tags/illust
GET  /v1/user/bookmark-tags/novel
GET  /v1/illust/bookmark/users
GET  /v1/novel/bookmark/users
GET  /v1/watchlist/manga          POST /v1/watchlist/manga/add   POST /v1/watchlist/manga/delete
GET  /v1/watchlist/novel          POST /v1/watchlist/novel/add   POST /v1/watchlist/novel/delete
GET  /v1/search/bookmark/illust
GET  /v1/search/bookmark/illust/bookmark-tag
GET  /v1/search/bookmark/illust/illust-tag
GET  /v1/search/bookmark/illust/period
GET  /v1/search/bookmark/novel
GET  /v1/search/bookmark/novel/bookmark-tag
GET  /v1/search/bookmark/novel/novel-tag
GET  /v1/search/bookmark/novel/period
GET  /v1/search/bookmark/sync-status
GET  /v1/user/browsing-history/illusts
GET  /v1/user/browsing-history/novels
POST /v2/user/browsing-history/illust/add
POST /v2/user/browsing-history/novel/add
```
</details>

<details>
<summary><b>用户关系 / 屏蔽 / 静音（展开）</b></summary>

```
GET  /v1/user/illusts?filter=for_android
GET  /v1/user/illust-series
GET  /v1/user/novels
GET  /v1/user/following?filter=for_android
GET  /v1/user/follower
GET  /v1/user/follow/detail
GET  /v1/user/mypixiv?filter=for_android
GET  /v2/illust/mypixiv
GET  /v1/novel/mypixiv
GET  /v1/user/related?filter=for_android
GET  /v1/user/recommended?filter=for_android
POST /v1/user/follow/add
POST /v1/user/follow/delete
GET  /v1/access-block/users
POST /v1/access-block/user/add
POST /v1/access-block/user/delete
GET  /v1/mute/list
POST /v1/mute/edit
GET  /v1/user/me/state
GET  /v1/user/me/audience-targeting
GET  /v1/user/profile/presets
POST /v2/user/profile/edit
POST /v1/user/workspace/edit
```
</details>

<details>
<summary><b>评论 / 表情 / 举报（展开）</b></summary>

```
GET  /v3/illust/comments            GET  /v3/novel/comments
GET  /v2/illust/comment/replies     GET  /v2/novel/comment/replies
POST /v1/illust/comment/add         POST /v1/novel/comment/add
POST /v1/illust/comment/delete      POST /v1/novel/comment/delete
GET  /v1/stamps                     GET  /v1/emoji
POST /v2/illust/report              POST /v2/novel/report         POST /v2/user/report
POST /v1/illust/comment/report      POST /v1/novel/comment/report
GET  /v1/illust/report/topic-list   GET  /v1/novel/report/topic-list
GET  /v1/illust/comment/report/topic-list  GET /v1/novel/comment/report/topic-list
GET  /v1/user/report/topic-list
POST /v1/feedback
```
</details>

<details>
<summary><b>通知（展开）</b></summary>

```
GET  /v1/notification/list
GET  /v1/notification/view-more
GET  /v1/notification/has-unread-notifications
GET  /v1/notification/new-from-following
POST /v1/notification/user/register
GET  /v2/notification/settings
POST /v2/notification/settings/edit
```
</details>

<details>
<summary><b>小说创作 / 上传（展开）</b></summary>

```
GET  /v1/user/novel/draft/detail
POST /v1/upload/novel/draft
POST /v1/edit/novel/draft
POST /v1/user/novel/draft/delete
GET  /v1/user/novel-draft-previews
GET  /v1/upload/novel/covers
POST /v2/upload/novel
GET  /v2/novel/markers
POST /v1/novel/marker/add
POST /v1/novel/marker/delete
POST /v1/novel/poll/answer
POST /v2/upload/illust
POST /v1/upload/status
```
</details>

<details>
<summary><b>信息流 / 广告 / 账号 / 商业化 / 设置（展开）</b></summary>

```
POST /v1/home/all
POST /v1/home/access
GET  /v1/home/user-popular-works
GET  /v1/app/show?os=and
GET  /show?format=json&os=and
GET  /v2/pixiv-info/android
GET  /v1/application-info/android
GET  /v1/info/latest
GET  /v1/info/list
GET  /v1/feature-configs
GET  /idp-urls
GET  /v1/privacy-policy/agreement      POST /v1/privacy-policy/agreement
POST /v1/terms/agree                   POST /v1/terms/agreement-status
POST /v1/mail-authentication/send
GET  /v1/premium/android/plans
GET  /v1/premium/android/landing-page-url
POST /v1/premium/android/register
GET  /v1/user/request-plans
GET  /v1/user/collection-citation-settings   POST 同路径
POST /v1/user/collection-citation/revoke-all
GET  /v1/user/ai-show-settings               POST /v1/user/ai-show-settings/edit
GET  /v1/user/restricted-mode-settings       POST /v1/user/restricted-mode-settings
[idp-urls 相关: /idp-urls]
```
</details>

---

## 3. 差异分析

### 3.1 Pixeval 独有（6 个）

| 端点 | 性质 | 说明 |
|---|---|---|
| `/v1/user/detail` | ⚠️ **已停用** | 官方 6.199.0 只保留 `/v2/user/detail`，`/v1` 版本全 APK 零引用。Pixeval [client.rs:227](crates/pixeval_mako/src/client.rs#L227) 在用，但 Rust 中 `get_user_detail` 仅被 MCP/订阅模块调用。 |
| `/v2/novel/mypixiv` | ⚠️ **不存在** | 官方只有 `/v1/novel/mypixiv`。Pixeval [client.rs:877](crates/pixeval_mako/src/client.rs#L877)。 |
| `/v1/novel/text` | ⚠️ **已移除** | 官方改用 `/webview/v2/novel` 的 HTML 内嵌 JSON。Pixeval 仅作 fallback（[client.rs:993](crates/pixeval_mako/src/client.rs#L993)），主路径已是 webview。 |
| `/webview/v2/novel` | ➖ 路径重合 | 官方用 `Uri.Builder().path("/webview/v2/novel")` 动态构造（[md7.java:64](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/md7.java)），**参数差异大**：官方传 `viewer_version=20260126_viewer_comments`、`use_block`、`restricted_mode`、`theme`、`font`、`light/dark` 等 15+ 参数；Pixeval 只传 `viewer_version=20221031_ai`。 |
| `{path}` / `{path}{id}` | ➖ 模板占位 | 分析脚本的占位符（bookmark add/delete 的动态选择），非真实端点。 |

**→ 实际需要修的是 3 个。**

### 3.2 官方独有（89 个）—— 按「Pixeval 是否需要」分级

#### 🟢 A 级：与 Pixeval 现有功能直接相关，属**功能缺口**

| 端点 | Pixeval 缺失影响 |
|---|---|
| `GET /v1/search/popular-preview/illust` `…/novel` | 搜索页无热门预览 |
| `GET /v1/illust/bookmark/users` `GET /v1/novel/bookmark/users` | 无法查看「谁收藏了这幅作品」 |
| `POST /v1/home/all` `POST /v1/home/access` `GET /v1/home/user-popular-works` | 首页无官方聚合信息流 |
| `GET /v1/user/browsing-history/illusts` `…/novels` | 无浏览历史 |
| `POST /v2/user/browsing-history/{illust,novel}/add` | 不向服务端上报阅读记录 |
| `GET /v1/user/follow/detail` | 关注状态无单点查询 |
| `GET /v1/illust/comment/report/topic-list` 等 6 个 report/topic-list | 举报功能完全缺失 |
| `POST /v2/illust/report` `POST /v2/novel/report` `POST /v2/user/report` | 同上 |
| `GET /v1/notification/list` + 6 个通知端点 | 无站内通知面板 |
| `GET /v1/stamps` `GET /v1/emoji` | 评论表情/贴纸资源枚举（Pixeval 目前用硬编码 URL，见 [CommentImageHelper.cs:26](src/Pixeval/Utilities/CommentImageHelper.cs#L26)） |
| `GET /v1/mute/list` `POST /v1/mute/edit` | 无官方静音（区别于本地屏蔽） |
| `GET /v1/access-block/users` + add/delete | 同上 |
| `GET /v1/search/bookmark/*`（9 个） | 收藏夹内搜索 |
| `GET /v1/user/me/state` `GET /v1/user/me/audience-targeting` | 账号状态 / 受众定向设置 |
| `GET /v1/novel/markers` + marker add/delete | 小说书签（阅读进度同步） |
| `POST /v1/novel/poll/answer` | 小说投票 |
| `GET /v1/feature-configs` `GET /v1/info/latest` `GET /v1/info/list` `GET /v2/pixiv-info/android` `GET /v1/application-info/android` | 服务端公告 / 功能开关 / 强制升级判定 |

#### 🟡 B 级：Pixeval 定位为「浏览/下载客户端」，可暂不实现

| 端点 | 说明 |
|---|---|
| `POST /v2/upload/illust` `POST /v1/upload/status` | 投稿插图 |
| `POST /v2/upload/novel` + 6 个 draft/covers 端点 | 投稿小说（含草稿） |
| `GET /v1/illust/delete` `POST /v1/novel/delete` | 删除自己的作品 |
| `GET /v1/user/novel-draft-previews` | 草稿预览 |
| `POST /v2/user/profile/edit` `POST /v1/user/workspace/edit` `GET /v1/user/profile/presets` | 资料编辑 |
| `GET /v1/premium/android/plans` `…/landing-page-url` `POST …/register` | Premium 订阅（受 Google Play 结算约束） |
| `GET /v1/user/request-plans` | 约稿 |
| `GET /v1/user/collection-citation-settings` + revoke-all | 合集引用授权 |
| `POST /v1/terms/agree` `POST /v1/terms/agreement-status` `POST /v1/privacy-policy/agreement` `POST /v1/mail-authentication/send` | 条款 / 邮箱验证 |
| `POST /v1/feedback` | 反馈 |
| `POST /v1/notification/user/register` | FCM 推送注册 |
| `GET /v1/walkthrough/illusts` | 新手引导素材 |
| `GET /v1/manga/recommended` | Pixeval 用 `/v1/illust/recommended` 覆盖了漫画 |
| `GET /v1/notification/new-from-following` | — |

#### ⚪ C 级：与 Pixeval 无关（第三方/商业化）

```
GET  /v1/app/show          GET /show          ← 广告（pixon.ads-pixiv.net）
GET  /idp-urls                                ← 第三方登录
GET  /v1/user/me/audience-targeting           ← 广告定向
GET  //pagead2.googlesyndication.com/pagead/gen_204  ← AdMob 埋点（误入本表）
```

### 3.3 同名端点的**参数/方法差异**（隐性差异）

这类差异脚本按路径判为「共有」，但语义已经不同：

| 端点 | 官方 6.199.0 | Pixeval | 影响 |
|---|---|---|---|
| `/v1/novel/recommended` | **POST**（body 带 `read_novel_ids[]`、`view_novel_ids[]`、`read_novel_datetimes[]`、`view_novel_datetimes[]`） | **GET**（只带 `include_ranking_novels`、`include_privacy_policy`） | 推荐质量下降：服务端拿不到阅读/浏览历史 |
| `/v1/novel/related` | **POST**（同上 4 组历史数组） | **GET**（只带 `novel_id`） | 关联推荐质量下降 |
| `/v1/user/restricted-mode-settings` | GET 读 / POST 写 | GET 读 / POST 写 | ✅ 一致 |
| 所有列表端点 | 普遍附加 `filter=for_android` | 通过 `target_filter` 注入 | ✅ 一致 |

---

## 4. 风险项与建议

### 4.1 高风险

1. **`X-Client-Hash` 缺失** — 官方每个 app-api 请求都带 `MD5(time + 固定盐)`。Pixeval 完全省略。建议在 [client.rs:1303-1315](crates/pixeval_mako/src/client.rs#L1303-L1315) / [1430-1441](crates/pixeval_mako/src/client.rs#L1430-L1441) 补齐 `X-Client-Time` / `X-Client-Hash`。

2. **弃用端点仍在使用** — `/v1/user/detail`、`/v2/novel/mypixiv` 已从官方 API 消失，随时可能返回 404。建议改为 `/v2/user/detail` 与 `/v1/novel/mypixiv`。

3. **UA / App-Version 停在 6.140.2** — 距 6.199.0 落后 59 个版本。建议统一提升，并复用官方的格式 `PixivAndroidApp/<ver> (Android <os>; <model>)`。

4. **旧 C# Mako 子模块残留** — `src/lib/Mako` 在索引中仍是 gitlink（mode `160000`，commit `445a5dbe`），但已从 `.gitmodules` 移除，导致它成为一个**无 URL 的孤儿 submodule**。该项目不在 `Pixeval.slnx` 中，仅 `Mako.Tests` 引用。其中的 `FeedEngine`（`/stacc?mode=unify`）与 `BookmarkUserEngine` 等是**官方 APK 中完全不存在、且已被 Pixiv 下线**的 Web 抓取路线。建议 `git rm --cached src/lib/Mako` 并删除目录与 `Mako.Tests`。

### 4.2 中风险

5. **`/v1/search/*` 参数不完整** — 官方固定带 `include_translated_tag_results=true&merge_plain_keyword_results=true`；Pixeval 仅 autocomplete 带了后者。

6. **`/webview/v2/novel` 参数严重简化** — 官方 `viewer_version=20260126_viewer_comments`（含评论渲染），Pixeval 用 `20221031_ai`（3 年前的版本），可能导致小说插图/评论/前后篇解析缺失（对应路线图中已记录的 H6 缺陷）。

7. **`Referer` 覆盖不全** — 官方所有 app-api 请求统一带 `Referer: https://app-api.pixiv.net/`（[by7.java](file:///C:/Users/Summp/Downloads/pixiv_6.199.0_APKPure.apk_Decompiler.com/sources/defpackage/by7.java)），Pixeval 只在下载链路带。建议在 `request_get` / `request_post_form` 统一注入。

### 4.3 优先级建议

```
P0（正确性）  : 修 /v1/user/detail → /v2/user/detail；/v2/novel/mypixiv → /v1/novel/mypixiv
              : 补 X-Client-Time / X-Client-Hash
P1（兼容性）  : 统一 UA = PixivAndroidApp/6.199.0；统一 Referer；补 Accept-Language
              : /v1/novel/recommended & /related 改 POST 并补阅读历史数组
P2（功能补齐）: A 级缺口按需选做 —— 通知面板 / 浏览历史 / 收藏夹搜索 / 热门预览 / 举报
P3（清理）    : 删除 src/lib/Mako 死代码与 Mako.Tests
```

---

## 附录：分析方法

- 官方侧：扫描 `sources/**/*.java`，按 Retrofit 注解提取 `@o14`(=GET) / `@ro7`(=POST) 的路径字面量（含 `Uri.Builder().path()` 动态构造），共 48 个接口类 / 150 个端点。
  - 注：该反编译产物 R8 混淆严重，`sources/` 与 `smali/` 均只有部分类（README 说明 34070 类中 >30000 时跳过 smali），因此 **150 是下界**，个别端点可能仍漏。
- Pixeval 侧：扫描 [crates/pixeval_mako/src/client.rs](crates/pixeval_mako/src/client.rs) 的 `APP_API_BASE_URL` 拼接字面量 + [auth.rs](crates/pixeval_mako/src/auth.rs)，共 67 个端点。
- 明细数据：[_final_diff.json](_final_diff.json)
