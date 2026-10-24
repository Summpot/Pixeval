package io.github.pixeval.mobile

import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import uniffi.pixeval_mako.*
import uniffi.pixeval_download.DownloadPageSnapshot
import uniffi.pixeval_download.DownloadFormatEncoder
import uniffi.pixeval_download.DownloadTaskKey
import uniffi.pixeval_download.DownloadItemSnapshot
import uniffi.pixeval_download.DownloadPageCallback
import uniffi.pixeval_native.*
import uniffi.pixeval_storage.WorkSubscriptionRecord
import uniffi.pixeval_filters.*
import uniffi.pixeval_mcp.*
import kotlin.time.Instant
import uniffi.pixeval_novel.NovelDocument
import uniffi.pixeval_novel.parseNovel
import uniffi.pixeval_subscription.SubscriptionSyncConfig
import uniffi.pixeval_storage.ArtworkPayload
import uniffi.pixeval_storage.StorageEngine
import uniffi.pixeval_storage.SearchHistoryRecord
import uniffi.pixeval_storage.WatchLaterRecord

enum class FeedSource(val label: String) {
    Recommended("MainPage.Tab-WorkRecommended"), Ranking("MainPage.Tab-WorkRanking"),
    Following("MainPage.Tab-WorkFollowing"), Bookmarks("MainPage.Tab-WorkBookmarks"),
    New("MainPage.Tab-WorkNew"), MyPixiv("MainPage.Tab-WorkMyPixiv"),
    History("MainPage.Tab-BrowsingHistory"), Search("MainPage.Tab-Search"),
    WatchLater("MainPage.Tab-WatchLater")
}

data class TagSuggestion(
    val name: String,
    val translatedName: String?,
    val isHistory: Boolean
)

data class FeedPageResult(val items: List<WorkEntry>, val exhausted: Boolean)

interface PagedFeed {
    suspend fun nextPage(): FeedPageResult
    fun cancel()
    fun destroy()
}

class WorkFeedWrapper(private val feed: WorkFeed) : PagedFeed {
    override suspend fun nextPage(): FeedPageResult {
        val page = feed.nextPage()
        return FeedPageResult(page.items, page.exhausted)
    }
    override fun cancel() = feed.cancel()
    override fun destroy() = feed.destroy()
}

class IllustFeedWrapper(private val feed: IllustrationFeed) : PagedFeed {
    override suspend fun nextPage(): FeedPageResult {
        val page = feed.nextPage()
        return FeedPageResult(page.items.map { WorkEntry.Illust(it) }, page.exhausted)
    }
    override fun cancel() = feed.cancel()
    override fun destroy() = feed.destroy()
}

class NovelFeedWrapper(private val feed: NovelFeed) : PagedFeed {
    override suspend fun nextPage(): FeedPageResult {
        val page = feed.nextPage()
        return FeedPageResult(page.items.map { WorkEntry.NovelWork(it) }, page.exhausted)
    }
    override fun cancel() = feed.cancel()
    override fun destroy() = feed.destroy()
}

class WatchLaterFeed(private val storage: StorageEngine) : PagedFeed {
    private var offset = 0u
    override suspend fun nextPage(): FeedPageResult {
        val records = withContext(Dispatchers.IO) {
            storage.watchLater().streamWatchLater(offset, 30u)
        }
        offset += records.size.toUInt()
        val works = records.mapNotNull { record ->
            when (val payload = record.payload) {
                is ArtworkPayload.Work -> payload.entry
                else -> null
            }
        }
        return FeedPageResult(works, records.size < 30)
    }
    override fun cancel() {}
    override fun destroy() {}
}

data class AppState(
    val user: TokenUser? = null,
    val source: FeedSource = FeedSource.Recommended,
    val kind: WorkKind = WorkKind.ILLUSTRATION,
    val works: List<WorkEntry> = emptyList(),
    val selected: WorkEntry? = null,
    val query: String = "",
    val loading: Boolean = false,
    val exhausted: Boolean = false,
    val error: String? = null,
    val notice: String? = null,
    val locale: String = "zh-Hans",
    val darkTheme: Boolean = false,
    val downloads: DownloadPageSnapshot? = null,
    val novelText: String? = null,
    val novelContent: NovelContent? = null,
    val novelDocument: NovelDocument? = null,
    val preferences: MobilePreferences? = null,
    val subscriptions: List<WorkSubscriptionRecord> = emptyList(),
    val comments: List<CommentRecord> = emptyList(),
    val commentsExhausted: Boolean = false,
    val relatedWorks: List<WorkEntry> = emptyList(),
    val seriesWorks: List<WorkEntry> = emptyList(),
    val seriesQuery: WorkSeriesQuery? = null,
    val seriesOpen: Boolean = false,
    val filter: String = "",
    val completions: List<FilterCompletionItem> = emptyList(),
    val rankMode: RankMode = RankMode.DAY,
    val userDetail: SingleUserResponse? = null,
    val mcpRunning: Boolean = false,
    val accountEpoch: Long = 0,
    val isWatchLater: Boolean = false,
    val searchFilter: SearchFilterState = SearchFilterState(),
    val searchSuggestions: List<TagSuggestion> = emptyList(),
    val spotlightArticles: List<SpotlightArticle> = emptyList(),
    val spotlightCategory: SpotlightCategory = SpotlightCategory.ALL,
    val spotlightLoading: Boolean = false,
    val spotlightExhausted: Boolean = false,
)

val WorkEntry.id: Long get() = when(this) {
    is WorkEntry.Illust -> illustration.id
    is WorkEntry.NovelWork -> novel.id
}
val WorkEntry.title: String get() = when(this) {
    is WorkEntry.Illust -> illustration.title
    is WorkEntry.NovelWork -> novel.title
}
val WorkEntry.author: User get() = when(this) {
    is WorkEntry.Illust -> illustration.user
    is WorkEntry.NovelWork -> novel.user
}
val WorkEntry.imageUrl: String? get() = when(this) {
    is WorkEntry.Illust -> illustration.imageUrls.medium ?: illustration.imageUrls.large
    is WorkEntry.NovelWork -> novel.imageUrls.medium ?: novel.imageUrls.large
}
val WorkEntry.bookmarked: Boolean get() = when(this) {
    is WorkEntry.Illust -> illustration.isBookmarked
    is WorkEntry.NovelWork -> novel.isBookmarked
}
val WorkEntry.isUgoira: Boolean get() = this is WorkEntry.Illust && illustration.illustType == IllustContentType.UGOIRA
val WorkEntry.series: Series? get() = when(this) {
    is WorkEntry.Illust -> illustration.series
    is WorkEntry.NovelWork -> novel.series
}
val WorkEntry.simpleKind: SimpleWorkKind get() = if (this is WorkEntry.NovelWork) SimpleWorkKind.NOVEL else SimpleWorkKind.ILLUSTRATION
fun WorkEntry.workKey(): String = when (this) {
    is WorkEntry.Illust -> "Mako.Model.Illustration:${illustration.id}"
    is WorkEntry.NovelWork -> "Mako.Model.Novel:${novel.id}"
}
fun WorkEntry.toArtworkPayload(): ArtworkPayload = ArtworkPayload.Work(this)

class AppController private constructor(val runtime: MobileRuntime, val platform: PlatformServices) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var accountJob = SupervisorJob(scope.coroutineContext[Job])
    private var accountScope = CoroutineScope(scope.coroutineContext + accountJob)
    private val authLock = Mutex()
    private val preferencesLock = Mutex()
    private val _state = MutableStateFlow(AppState(locale = platform.systemLocale))
    val state: StateFlow<AppState> = _state.asStateFlow()
    val client = runtime.client()
    val storage = runtime.storage()
    val downloads = runtime.downloads()
    val cache = runtime.cache()
    val subscriptions = runtime.subscriptions()
    private val downloadEvents = MutableStateFlow<DownloadPageSnapshot?>(null)
    private val downloadCallback = object : DownloadPageCallback {
        override fun onPageSnapshot(snapshot: DownloadPageSnapshot) { downloadEvents.value = snapshot }
    }
    private val filterStorage = object : FilterStorageProvider {
        override fun querySearchHistoryTags(pattern: String, limit: UInt): List<TagCandidate> =
            storage.history().use { repo -> repo.querySearchHistorySuggestions(pattern, limit).map { TagCandidate(it.value, it.translatedName) } }
        override fun querySubscriptionAuthors(pattern: String, limit: UInt): List<AuthorCandidate> =
            storage.querySubscriptionAuthorSuggestions(pattern, limit).map { AuthorCandidate(it.title, it.author) }
    }
    private val filters = FilterSession.withProvider(filterStorage, null)
    private val formatEncoder = object : DownloadFormatEncoder {
        override fun encodeStaticImage(sourcePath: String, destinationPath: String, extension: String) = "No encoder for .$extension"
        override fun encodeAnimated(framePaths: List<String>, delaysMs: List<UInt>, destinationPath: String, extension: String): String {
            if(!extension.equals("mp4", true)) return "No encoder for .$extension"
            return platform.videoEncoder?.encode(framePaths, delaysMs.map { it.toLong() }, destinationPath) ?: "MP4 encoding is unavailable"
        }
        override fun encodeNovel(text: String, imagePaths: List<String>, destinationPath: String, extension: String) = "No encoder for .$extension"
    }
    private val sessionEvents = runtime.sessionEvents()
    private var feed: PagedFeed? = null
    private var relatedFeed: WorkFeed? = null
    private var commentsFeed: CommentFeed? = null
    private var spotlightFeed: SpotlightFeed? = null
    private var feedJob: Job? = null
    private var filterJob: Job? = null
    private var allWorks: List<WorkEntry> = emptyList()
    private val pageLock = Mutex()
    private val navigationLock = Mutex()
    private var epoch = 0
    private var pkceVerifier: String? = null
    private var mcp: McpServer? = null
    private val mcpLock = Mutex()
    private var foreground = true

    init {
        downloads.setPageCallback(downloadCallback)
        if(platform.videoEncoder != null) downloads.setFormatEncoder(formatEncoder)
    }

    private val mcpBridge = object : McpSessionBridge {
        override fun getCurrentUser(): McpSessionUserInfo? = state.value.user?.let { McpSessionUserInfo(it.id, it.name, it.account) }
        override fun getHelpDocument(topic: String?): String = runtime.helpDocument(topic)
        override fun onDownloadMacroChanged(macroText: String) { changePreferences { it.copy(downloadPathMacro = macroText) } }
        override fun logEvent(level: String, message: String) { if (level == "error") _state.update { it.copy(error = message) } }
    }

    private fun operation(block: suspend () -> Unit): Job = scope.launch {
        try { block() }
        catch (cancelled: CancellationException) { throw cancelled }
        catch (error: Exception) { _state.update { it.copy(error = error.message ?: error.toString(), loading = false) } }
    }
    private fun accountOperation(block: suspend () -> Unit): Job = accountScope.launch {
        try { block() }
        catch (cancelled: CancellationException) { throw cancelled }
        catch (error: Exception) { _state.update { it.copy(error = error.message ?: error.toString(), loading = false) } }
    }
    private suspend fun clearAccountState() {
        accountJob.cancelAndJoin()
        resetFeed()
        accountJob = SupervisorJob(scope.coroutineContext[Job])
        accountScope = CoroutineScope(scope.coroutineContext + accountJob)
        _state.update { AppState(locale = it.locale, darkTheme = it.darkTheme,
            preferences = it.preferences, accountEpoch = it.accountEpoch + 1) }
    }

    private fun start() {
        val prefs = runtime.preferences()
        _state.update { it.copy(locale = prefs.language.ifEmpty { platform.systemLocale }, darkTheme = prefs.darkTheme, preferences = prefs) }
        operation { downloadEvents.filterNotNull().collect { snapshot -> _state.update { it.copy(downloads = snapshot) } } }
        operation {
            val token = withContext(Dispatchers.IO) { storage.getMobileRefreshToken() }
            if (token != null) authLock.withLock { loginNow(token) }
        }
        operation { combine(platform.foreground, platform.backgroundDownloads) { visible, downloading -> visible to downloading }.collect { (visible, downloading) ->
            foreground = visible
            if (!visible) {
                stopMcp()
                if (!downloading) withContext(Dispatchers.IO) { runtime.suspend() }
            }
        } }
        operation { platform.incomingLinks.collect { acceptLink(it) } }
        operation {
            sessionEvents.asFlow().collect { event ->
                when(event) {
                    is SessionEvent.AuthInvalidated -> {
                        authLock.withLock {
                            stopMcp()
                            clearAccountState()
                            _state.update { it.copy(error = event.reason) }
                        }
                    }
                    is SessionEvent.RateLimited -> _state.update { it.copy(error = "HTTP 429 (${event.retryAfterSeconds}s)") }
                }
            }
        }
    }

    private val pendingLinks = ArrayDeque<String>()
    private suspend fun acceptLink(value: String) {
        mobileOauthCode(value)?.let { completeOAuth(it).join(); return }
        if (state.value.user == null) {
            if (value !in pendingLinks) pendingLinks.addLast(value)
            return
        }
        when(val link = parsePixivUrl(value)) {
            is ParsedPixivLink.Artwork -> open(WorkEntry.Illust(client.getIllustration(link.id)))
            is ParsedPixivLink.Novel -> open(WorkEntry.NovelWork(client.getNovel(link.id)))
            else -> Unit
        }
    }

    private suspend fun loginNow(token: String) {
        stopMcp()
        clearAccountState()
        withContext(Dispatchers.IO) { runtime.suspend() }
        _state.update { it.copy(loading = true, error = null) }
        withContext(Dispatchers.IO) { client.clearToken(); client.setRefreshToken(token) }
        val response = client.refreshToken()
        _state.update { it.copy(user = response.user, loading = false) }
        refresh()
        while (pendingLinks.isNotEmpty()) acceptLink(pendingLinks.removeFirst())
    }
    fun login(token: String) = operation { authLock.withLock { loginNow(token.trim()) } }
    fun beginOAuth() = operation {
        val params = createPkceAuthParams()
        pkceVerifier = params.codeVerifier
        platform.openUrl(params.authUrl)
    }
    fun completeOAuth(code: String) = operation { authLock.withLock {
        val verifier = pkceVerifier ?: error("OAuth session has expired")
        pkceVerifier = null
        stopMcp()
        clearAccountState()
        withContext(Dispatchers.IO) { runtime.suspend(); client.clearToken() }
        val response = client.exchangeCode(code.trim(), verifier)
        _state.update { it.copy(user = response.user, error = null) }
        refresh()
        while (pendingLinks.isNotEmpty()) acceptLink(pendingLinks.removeFirst())
    } }
    fun logout() = operation { authLock.withLock {
        stopMcp()
        clearAccountState()
        withContext(Dispatchers.IO) {
            runtime.suspend()
            client.clearToken()
            storage.setMobileRefreshToken(null)
        }
    } }
    fun dismissError() { _state.update { it.copy(error = null, notice = null) } }
    fun savePreferences(prefs: MobilePreferences) = changePreferences { prefs }
    fun changePreferences(change: (MobilePreferences) -> MobilePreferences) = operation { preferencesLock.withLock {
        stopMcp()
        val prefs = change(withContext(Dispatchers.IO) { runtime.preferences() })
        withContext(Dispatchers.IO) { runtime.updatePreferences(prefs) }
        _state.update { it.copy(preferences = prefs, locale = prefs.language.ifEmpty { platform.systemLocale }, darkTheme = prefs.darkTheme) }
    } }
    fun setMcpEnabled(enabled: Boolean) = operation { mcpLock.withLock {
        mcp?.stop(); mcp?.destroy(); mcp = null
        _state.update { it.copy(mcpRunning = false) }
        if(enabled) {
            check(foreground) { "MCP requires the application to be in the foreground" }
            val prefs = runtime.preferences()
            val server = McpServer(McpServerConfig(prefs.mcpPort, prefs.mcpWriteTools, 16, "1.0.0", "for_ios"),
                mcpBridge, client, storage, downloads, cache, subscriptions, null)
            try { server.start(); mcp = server; _state.update { it.copy(mcpRunning = true) } }
            catch(error: Exception) { server.destroy(); throw error }
        }
    } }
    private suspend fun stopMcp() = mcpLock.withLock {
        mcp?.stop(); mcp?.destroy(); mcp = null
        _state.update { it.copy(mcpRunning = false) }
    }
    fun setLocale(locale: String) = changePreferences { it.copy(language = locale) }
    fun setDarkTheme(dark: Boolean) = changePreferences { it.copy(darkTheme = dark) }
    fun setQuery(query: String) {
        _state.update { it.copy(query = query) }
        updateSearchSuggestions(query)
    }
    fun updateSearchSuggestions(keyword: String) {
        scope.launch(Dispatchers.IO) {
            try {
                val history = storage.history().querySearchHistorySuggestions(keyword, 6u)
                val historySuggestions = history.map { TagSuggestion(it.value, it.translatedName, isHistory = true) }
                val remoteSuggestions = if (keyword.isNotBlank()) {
                    try {
                        client.searchAutocomplete(keyword).map { TagSuggestion(it.name, it.translatedName, isHistory = false) }
                    } catch (_: Exception) { emptyList() }
                } else emptyList()
                val combined = (historySuggestions + remoteSuggestions).distinctBy { it.name }
                _state.update { it.copy(searchSuggestions = combined) }
            } catch (_: Exception) {}
        }
    }
    fun deleteSearchHistory(value: String) {
        scope.launch(Dispatchers.IO) {
            try {
                storage.history().tryDeleteSearchHistoryByValue(value)
                updateSearchSuggestions(state.value.query)
            } catch (_: Exception) {}
        }
    }
    fun clearSearchHistory() {
        scope.launch(Dispatchers.IO) {
            try {
                storage.history().clearSearchHistory()
                updateSearchSuggestions(state.value.query)
            } catch (_: Exception) {}
        }
    }
    fun applySearchFilter(filter: SearchFilterState) {
        _state.update { it.copy(searchFilter = filter) }
        if (state.value.source == FeedSource.Search) refresh()
    }
    fun selectSource(source: FeedSource) { _state.update { it.copy(source = source, selected = null) }; refresh() }
    fun selectKind(kind: WorkKind) { _state.update { it.copy(kind = kind, selected = null) }; refresh() }
    fun selectRank(mode: RankMode) { _state.update { it.copy(rankMode = mode) }; refresh() }
    fun setFilter(query: String) {
        _state.update { it.copy(filter = query) }
        filterJob?.cancel()
        filterJob = accountOperation {
            val analysis = withContext(Dispatchers.IO) { filters.analyze(query, query.length) }
            try {
                if(state.value.filter == query) {
                    _state.update { it.copy(completions = analysis.completions) }
                    if(analysis.isSuccess) applyFilter()
                }
            } finally { analysis.queryHandle?.destroy() }
        }
    }

    private suspend fun applyFilter() {
        val query = state.value.filter
        val works = allWorks
        val version = epoch
        val mask = withContext(Dispatchers.IO) {
            val metadata = works.map { it.metadata() }
            filters.setSessionArtworks(metadata)
            if(query.isBlank()) List(works.size) { true } else filters.filterArtworks(query, metadata)
        }
        if(query == state.value.filter && version == epoch && works === allWorks) {
            _state.update { it.copy(works = works.filterIndexed { index, _ -> mask.getOrElse(index) { false } }) }
        }
    }

    private suspend fun resetFeed() {
        epoch++
        feed?.cancel()
        feedJob?.cancelAndJoin()
        feed?.destroy()
        feed = null
        allWorks = emptyList()
    }
    fun refresh() = accountOperation { navigationLock.withLock {
        resetFeed()
        val snapshot = state.value
        _state.update { it.copy(works = emptyList(), loading = true, exhausted = false, error = null) }
        val kind = if (snapshot.kind == WorkKind.NOVEL) SimpleWorkKind.NOVEL else SimpleWorkKind.ILLUSTRATION
        feed = withContext(Dispatchers.IO) {
            when(snapshot.source) {
                FeedSource.Recommended -> WorkFeedWrapper(client.workRecommendedUnified(snapshot.kind))
                FeedSource.Ranking -> WorkFeedWrapper(client.workRankingUnified(kind, snapshot.rankMode, null))
                FeedSource.Following -> WorkFeedWrapper(client.workFollowingUnified(kind, PrivacyRestrict.PUBLIC))
                FeedSource.Bookmarks -> WorkFeedWrapper(client.workBookmarksUnified(kind, snapshot.user?.id?.toLongOrNull() ?: error("Login required"), PrivacyRestrict.PUBLIC, null))
                FeedSource.New -> WorkFeedWrapper(client.workNewUnified(snapshot.kind))
                FeedSource.MyPixiv -> WorkFeedWrapper(client.workMypixivUnified(kind))
                FeedSource.History -> WorkFeedWrapper(client.browsingHistoryUnified(kind))
                FeedSource.WatchLater -> WatchLaterFeed(storage)
                FeedSource.Search -> {
                    val q = snapshot.query.trim().ifEmpty { error("Enter a search query") }
                    try { storage.history().upsertSearchHistory(q, null, "2026-10-24T00:00:00Z") } catch (_: Exception) {}
                    val filter = snapshot.searchFilter
                    if (snapshot.kind == WorkKind.NOVEL) {
                        val params = NovelSearchParams(
                            word = q,
                            searchTarget = filter.novelSearchTarget,
                            sort = filter.sort,
                            searchAiType = filter.aiType,
                            lang = null,
                            contentLengthOption = null,
                            contentLengthMin = null,
                            contentLengthMax = null,
                            isOriginalOnly = null,
                            genre = null,
                            isReplaceableOnly = null,
                            mergePlainKeywordResults = null,
                            includeTranslatedTagResults = null,
                            includePotentialViolationWorks = null,
                            startDate = filter.startDate,
                            endDate = filter.endDate
                        )
                        NovelFeedWrapper(client.novelSearchAdvanced(params))
                    } else {
                        val params = IllustrationSearchParams(
                            word = q,
                            searchTarget = filter.illustSearchTarget,
                            sort = filter.sort,
                            searchAiType = filter.aiType,
                            contentType = filter.contentType,
                            ratioPattern = null,
                            mergePlainKeywordResults = null,
                            includeTranslatedTagResults = null,
                            includePotentialViolationWorks = null,
                            startDate = filter.startDate,
                            endDate = filter.endDate,
                            widthMin = null,
                            widthMax = null,
                            heightMin = null,
                            heightMax = null,
                            tool = null
                        )
                        IllustFeedWrapper(client.illustrationSearchAdvanced(params))
                    }
                }
            }
        }
        loadMore()
    } }
    fun loadMore() {
        if (feedJob?.isActive == true || state.value.exhausted) return
        val currentFeed = feed ?: return
        val currentEpoch = epoch
        feedJob = accountOperation {
            pageLock.withLock {
                _state.update { it.copy(loading = true) }
                val page = currentFeed.nextPage()
                if (currentEpoch == epoch) {
                    allWorks = (allWorks + page.items).distinctBy { work -> "${work.simpleKind}:${work.id}" }
                    applyFilter()
                    _state.update { it.copy(loading = false, exhausted = page.exhausted) }
                }
            }
        }
    }
    fun open(work: WorkEntry) = accountOperation {
        val inWl = withContext(Dispatchers.IO) {
            try { storage.watchLater().containsWatchLater(work.workKey()) } catch (_: Exception) { false }
        }
        _state.update { it.copy(selected = work, novelText = null, novelContent = null, novelDocument = null, comments = emptyList(), commentsExhausted = false, relatedWorks = emptyList(), isWatchLater = inWl) }
        withContext(Dispatchers.IO) { runtime.recordBrowse(work) }
        loadRelated(work)
        if (work is WorkEntry.NovelWork) {
            val content = client.getNovelContentStructured(work.id)
            val document = withContext(Dispatchers.IO) { parseNovel(content.text) }
            if (state.value.selected?.let { it.id == work.id && it.simpleKind == work.simpleKind } == true)
                _state.update { it.copy(novelText = content.text, novelContent = content, novelDocument = document) }
        }
    }
    fun toggleWatchLater(work: WorkEntry) = accountOperation {
        val key = work.workKey()
        val nowInWl = withContext(Dispatchers.IO) {
            val exists = storage.watchLater().containsWatchLater(key)
            if (exists) {
                storage.watchLater().removeWatchLater(key)
                false
            } else {
                storage.watchLater().addWatchLater(work.toArtworkPayload())
                true
            }
        }
        if (state.value.selected?.let { it.id == work.id && it.simpleKind == work.simpleKind } == true) {
            _state.update { it.copy(isWatchLater = nowInWl) }
        }
        if (state.value.source == FeedSource.WatchLater) {
            refresh()
        }
    }
    fun selectSpotlightCategory(category: SpotlightCategory) = accountOperation {
        _state.update { it.copy(spotlightCategory = category) }
        refreshSpotlight()
    }
    fun refreshSpotlight() = accountOperation {
        spotlightFeed?.cancel(); spotlightFeed?.destroy(); spotlightFeed = null
        _state.update { it.copy(spotlightArticles = emptyList(), spotlightLoading = true, spotlightExhausted = false) }
        val f = withContext(Dispatchers.IO) {
            client.spotlight(state.value.spotlightCategory)
        }
        spotlightFeed = f
        val page = withContext(Dispatchers.IO) { f.nextPage() }
        _state.update { it.copy(spotlightArticles = page.items, spotlightLoading = false, spotlightExhausted = page.exhausted) }
    }
    fun loadMoreSpotlight() = accountOperation {
        val f = spotlightFeed ?: return@accountOperation
        if (state.value.spotlightExhausted || state.value.spotlightLoading) return@accountOperation
        _state.update { it.copy(spotlightLoading = true) }
        val page = withContext(Dispatchers.IO) { f.nextPage() }
        _state.update {
            it.copy(
                spotlightArticles = (it.spotlightArticles + page.items).distinctBy { a -> a.id },
                spotlightLoading = false,
                spotlightExhausted = page.exhausted
            )
        }
    }
    fun loadRelated(work: WorkEntry) = accountOperation {
        val f = withContext(Dispatchers.IO) { client.workRelatedUnified(work.simpleKind, work.id) }
        relatedFeed?.cancel(); relatedFeed?.destroy()
        relatedFeed = f
        val page = withContext(Dispatchers.IO) { f.nextPage() }
        if (state.value.selected?.id == work.id) {
            _state.update { it.copy(relatedWorks = page.items) }
        }
    }
    fun openSeries(kind: SimpleWorkKind, seriesId: Long) = accountOperation {
        val query = withContext(Dispatchers.IO) { client.getWorkSeries(kind, seriesId) }
        val firstPage = withContext(Dispatchers.IO) { query.engine().nextPage() }
        _state.update { it.copy(seriesQuery = query, seriesWorks = firstPage.items, seriesOpen = true) }
    }
    fun closeSeries() {
        _state.update { it.copy(seriesOpen = false, seriesQuery = null, seriesWorks = emptyList()) }
    }
    fun toggleSeriesWatchlist(kind: SimpleWorkKind, seriesId: Long, currentWatched: Boolean) = accountOperation {
        withContext(Dispatchers.IO) { client.setSeriesWatchlistUnified(kind, seriesId, !currentWatched) }
    }
    fun markNovelPage(id: Long, page: Int) = accountOperation { client.addNovelMarker(id, page + 1) }
    fun openLink(url: String) = accountOperation {
        if (parsePixivUrl(url) != null) acceptLink(url)
        else if (url.startsWith("https://", true) || url.startsWith("http://", true)) platform.openUrl(url)
    }
    fun loadComments(work: WorkEntry) = accountOperation {
        val feed = withContext(Dispatchers.IO) { client.workComments(work.simpleKind, work.id) }
        commentsFeed?.cancel(); commentsFeed?.destroy()
        commentsFeed = feed
        val page = withContext(Dispatchers.IO) { feed.nextPage() }
        if (state.value.selected?.id == work.id) {
            _state.update { it.copy(comments = page.items, commentsExhausted = page.exhausted) }
        }
    }
    fun loadMoreComments(work: WorkEntry) = accountOperation {
        val feed = commentsFeed ?: return@accountOperation
        if (state.value.commentsExhausted) return@accountOperation
        val page = withContext(Dispatchers.IO) { feed.nextPage() }
        if (state.value.selected?.id == work.id) {
            _state.update { it.copy(comments = (it.comments + page.items).distinctBy { c -> c.id }, commentsExhausted = page.exhausted) }
        }
    }
    fun postComment(work: WorkEntry, comment: String, parent: Long? = null) = accountOperation {
        client.addWorkCommentUnified(work.simpleKind, work.id, comment, parent, null)
        loadComments(work)
    }
    fun deleteComment(work: WorkEntry, commentId: Long) = accountOperation {
        client.deleteWorkCommentUnified(work.simpleKind, commentId)
        loadComments(work)
    }
    fun openUser(user: User) = accountOperation {
        val detail = client.getUserDetail(user.id)
        _state.update { it.copy(userDetail = detail) }
    }
    fun closeUser() { _state.update { it.copy(userDetail = null) } }
    fun follow(user: User) = accountOperation {
        client.setFollow(user.id, !user.isFollowed, PrivacyRestrict.PUBLIC)
        _state.update { it.copy(userDetail = it.userDetail?.copy(user = user.copy(isFollowed = !user.isFollowed))) }
    }
    fun userWorks(user: User) = accountOperation { navigationLock.withLock {
        resetFeed()
        _state.update { it.copy(userDetail = null, selected = null, works = emptyList(), exhausted = false) }
        feed = WorkFeedWrapper(client.workPostedUnified(state.value.kind, user.id))
        loadMore()
    } }
    fun refreshSubscriptions() = accountOperation {
        val rows = withContext(Dispatchers.IO) { storage.getAllSubscriptions() }
        _state.update { it.copy(subscriptions = rows) }
    }
    fun subscribe(user: User, bookmarks: Boolean = false) = accountOperation {
        withContext(Dispatchers.IO) {
            storage.upsertSubscription(user.id, if(bookmarks) 0u else 1u, if(state.value.kind == WorkKind.NOVEL) 1u else 0u,
                user.name, user.account, user.profileImageUrls.medium ?: user.profileImageUrls.px170x170 ?: "")
        }
        refreshSubscriptions()
    }
    fun removeSubscription(row: WorkSubscriptionRecord) = accountOperation {
        withContext(Dispatchers.IO) { subscriptions.removePendingSubscription(row.historyEntryId); storage.deleteSubscription(row.historyEntryId) }
        refreshSubscriptions()
    }
    fun syncSubscriptions(id: Long? = null) = accountOperation {
        platform.prepareDownload()
        try { withContext(Dispatchers.IO) {
            val prefs = runtime.preferences()
            subscriptions.updateConfig(SubscriptionSyncConfig(prefs.downloadPathMacro, runtime.directories().downloads,
                prefs.overwrite, 5u, state.value.user?.id?.toLongOrNull()))
            if (id == null) subscriptions.queueSyncAll() else subscriptions.queueSyncSubscription(id)
        } } finally { platform.downloadQueued() }
    }
    fun back() { _state.update { it.copy(selected = null, novelText = null, novelContent = null, novelDocument = null, relatedWorks = emptyList()) } }
    fun bookmark(work: WorkEntry) = accountOperation {
        client.setBookmark(work.simpleKind, work.id, !work.bookmarked, PrivacyRestrict.PUBLIC, null)
        fun changed(value: WorkEntry) = if(value.simpleKind == work.simpleKind && value.id == work.id) when(value) {
            is WorkEntry.Illust -> WorkEntry.Illust(value.illustration.copy(isBookmarked = !work.bookmarked))
            is WorkEntry.NovelWork -> WorkEntry.NovelWork(value.novel.copy(isBookmarked = !work.bookmarked))
        } else value
        allWorks = allWorks.map(::changed)
        _state.update { it.copy(works = it.works.map(::changed), selected = it.selected?.let(::changed)) }
    }
    fun bookmarkAdvanced(work: WorkEntry, favorite: Boolean, restrict: PrivacyRestrict, tags: List<String>) = accountOperation {
        client.setBookmark(work.simpleKind, work.id, favorite, restrict, if (tags.isEmpty()) null else tags)
        fun changed(value: WorkEntry) = if(value.simpleKind == work.simpleKind && value.id == work.id) when(value) {
            is WorkEntry.Illust -> WorkEntry.Illust(value.illustration.copy(isBookmarked = favorite))
            is WorkEntry.NovelWork -> WorkEntry.NovelWork(value.novel.copy(isBookmarked = favorite))
        } else value
        allWorks = allWorks.map(::changed)
        _state.update { it.copy(works = it.works.map(::changed), selected = it.selected?.let(::changed)) }
    }
    fun download(work: WorkEntry) = accountOperation {
        platform.prepareDownload()
        try {
            val key = withContext(Dispatchers.IO) {
                when(work) {
                    is WorkEntry.Illust -> downloads.enqueueIllustration(work.illustration, -1, runtime.preferences().downloadPathMacro, runtime.directories().downloads, 0, "")
                    is WorkEntry.NovelWork -> downloads.enqueueNovel(work.novel, runtime.preferences().downloadPathMacro, runtime.directories().downloads, 0, "")
                }
            }
            check(key.isNotEmpty()) { "The download could not be queued" }
            refreshDownloads()
        } finally { platform.downloadQueued() }
    }
    fun changeDownload(key: DownloadTaskKey, action: String) = accountOperation {
        val starting = action == "resume" || action == "retry"
        if (starting) platform.prepareDownload()
        try {
            withContext(Dispatchers.IO) {
                when(action) {
                    "pause" -> downloads.pauseWork(key)
                    "resume" -> downloads.resumeWork(key)
                    "cancel" -> downloads.cancelWork(key)
                    "retry" -> downloads.resetWork(key)
                }
            }
            refreshDownloads().join()
        } finally { if (starting) platform.downloadQueued() }
    }
    fun exportDownload(item: DownloadItemSnapshot, share: Boolean = false, gallery: Boolean = false) = accountOperation {
        check(item.state == uniffi.pixeval_download.DownloadState.COMPLETED) { "Download has not completed" }
        when {
            gallery -> platform.saveToGallery(item.openDestination)
            share -> platform.shareFile(item.openDestination)
            else -> platform.exportFile(item.openDestination)
        }
    }
    fun refreshDownloads() = operation {
        val snapshot = withContext(Dispatchers.IO) { downloads.currentPageSnapshot() }
        _state.update { it.copy(downloads = snapshot) }
    }
    fun suspendApp() = operation { withContext(Dispatchers.IO) { runtime.suspend() } }
    suspend fun close() {
        sessionEvents.cancel()
        feed?.cancel()
        relatedFeed?.cancel()
        commentsFeed?.cancel()
        scope.coroutineContext[Job]?.cancelAndJoin()
        stopMcp()
        withContext(Dispatchers.IO) {
            runtime.suspend()
            downloads.setFormatEncoder(null)
            downloads.setPageCallback(null)
            filters.destroy(); subscriptions.destroy()
            feed?.destroy()
            relatedFeed?.destroy()
            commentsFeed?.destroy()
            sessionEvents.destroy()
            downloads.destroy(); cache.destroy(); storage.destroy(); client.destroy(); runtime.destroy()
        }
    }

    companion object {
        suspend fun open(platform: PlatformServices): AppController {
            var acquired: MobileRuntime? = null
            try {
                val runtime = withContext(Dispatchers.IO) { MobileRuntime.open(platform.directories).also { acquired = it } }
                return AppController(runtime, platform).also { it.start() }
            } catch (failure: Throwable) {
                withContext(NonCancellable + Dispatchers.IO) { acquired?.destroy() }
                throw failure
            }
        }
    }
}

private fun WorkEntry.metadata(): ArtworkMetadata {
    fun timestamp(value: String) = runCatching { Instant.parse(value).epochSeconds }.getOrDefault(0L)
    return when(this) {
        is WorkEntry.Illust -> illustration.let { value -> ArtworkMetadata(value.id.toString(), value.title, value.user.name, value.user.account,
            value.tags.map { ArtworkTag(it.name, it.translatedName) }, value.totalBookmarks, timestamp(value.createDate), value.width, value.height,
            value.xRestrict.value, value.illustAiType, value.illustType.value) }
        is WorkEntry.NovelWork -> novel.let { value -> ArtworkMetadata(value.id.toString(), value.title, value.user.name, value.user.account,
            value.tags.map { ArtworkTag(it.name, it.translatedName) }, value.totalBookmarks, timestamp(value.createDate), 0, 0, value.xRestrict.value,
            value.novelAiType, 3) }
    }
}

internal fun SessionEventReceiver.asFlow(): Flow<SessionEvent> = flow {
    try { while (true) emit(next() ?: break) }
    finally { this@asFlow.cancel() }
}
