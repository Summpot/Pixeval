package io.github.pixeval.mobile

import androidx.compose.foundation.*
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.staggeredgrid.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.saveable.rememberSaveableStateHolder
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.*
import io.github.pixeval.mobile.localization.Messages
import uniffi.pixeval_mako.*

@Composable
fun PixevalApp(platform: PlatformServices) {
    var controller by remember(platform) { mutableStateOf<AppController?>(null) }
    var startupError by remember { mutableStateOf<String?>(null) }
    var retry by remember { mutableIntStateOf(0) }
    LaunchedEffect(platform, retry) {
        startupError = null
        try { controller = AppController.open(platform) }
        catch (cancelled: CancellationException) { throw cancelled }
        catch (error: Exception) { startupError = error.message ?: error.toString() }
    }
    DisposableEffect(controller) {
        val owned = controller
        onDispose { if (owned != null) CoroutineScope(Dispatchers.Default).launch { owned.close() } }
    }
    val current = controller
    if (current == null) MaterialTheme {
        Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.Center) {
            Text("Pixeval", style = MaterialTheme.typography.headlineLarge)
            startupError?.let { Text(it); Button(onClick = { retry++ }) { Text(Messages.text("WorkContainer.RefreshButton-Label", platform.systemLocale)) } }
                ?: CircularProgressIndicator()
        }
    } else PixevalApp(current)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PixevalApp(controller: AppController) {
    val state by controller.state.collectAsState()
    val images = remember(controller, state.accountEpoch) { ImageRepository(controller.cache, controller.platform.imageMemoryBudgetBytes) }
    var page by rememberSaveable { mutableStateOf("browse") }
    var menu by remember { mutableStateOf(false) }
    val pages = key(state.accountEpoch) { rememberSaveableStateHolder() }
    val canGoBack = state.userDetail != null || state.selected != null || page != "browse"
    val goBack by rememberUpdatedState<() -> Unit>({
        when {
            state.userDetail != null -> controller.closeUser()
            state.selected != null -> controller.back()
            else -> page = "browse"
        }
    })
    DisposableEffect(controller, canGoBack) {
        controller.platform.setBackHandler(if (canGoBack) ({ goBack() }) else null)
        onDispose { controller.platform.setBackHandler(null) }
    }
    fun text(key: String) = Messages.text(key, state.locale)
    MaterialTheme(colorScheme = if(state.darkTheme) darkColorScheme() else lightColorScheme()) {
        Scaffold(
            topBar = {
                TopAppBar(title = { Text(state.selected?.title ?: "Pixeval") }, navigationIcon = {
                    if(canGoBack) TextButton(onClick = goBack) { Text("←") }
                    else TextButton(onClick = { menu = true }) { Text("☰") }
                }, actions = {
                    DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                        FeedSource.entries.forEach { source -> DropdownMenuItem(text = { Text(text(source.label)) }, onClick = {
                            page = "browse"; menu = false; controller.selectSource(source)
                        }) }
                        DropdownMenuItem(text = { Text(text("MainPage.Tab-Download")) }, onClick = { page = "downloads"; menu = false; controller.back(); controller.refreshDownloads() })
                        DropdownMenuItem(text = { Text(text("MainPage.Tab-Settings")) }, onClick = { page = "settings"; menu = false; controller.back() })
                        DropdownMenuItem(text = { Text(text("AppSettings.WorkSubscriptionsSettingsEntry-Header")) }, onClick = { page = "subscriptions"; menu = false; controller.back(); controller.refreshSubscriptions() })
                    }
                    if(state.user != null) TextButton(onClick = { controller.logout() }) { Text(state.user!!.name) }
                })
            },
        ) { padding ->
            Column(Modifier.fillMaxSize().padding(padding)) {
                state.error?.let { message -> Surface(color = MaterialTheme.colorScheme.errorContainer) {
                    Row(Modifier.fillMaxWidth().padding(12.dp)) { Text(message, Modifier.weight(1f)); TextButton(onClick = controller::dismissError) { Text("×") } }
                } }
                when {
                    page == "settings" -> Settings(controller, state, ::text)
                    state.user == null -> Login(controller, ::text)
                    page == "downloads" -> Downloads(controller, state, ::text)
                    page == "subscriptions" -> Subscriptions(controller, state, ::text)
                    else -> BoxWithConstraints(Modifier.fillMaxSize()) {
                        val wide = maxWidth >= 840.dp
                        Row(Modifier.fillMaxSize()) {
                            if(wide || state.selected == null) pages.SaveableStateProvider("gallery:${state.source}:${state.kind}") { Column(Modifier.weight(1f)) {
                                Row(Modifier.horizontalScroll(rememberScrollState()).padding(horizontal = 8.dp)) {
                                    WorkKind.entries.forEach { kind -> FilterChip(selected = kind == state.kind, onClick = { controller.selectKind(kind) }, label = { Text(kind.name) }) }
                                }
                                if(state.source == FeedSource.Search) Row(Modifier.padding(8.dp)) {
                                    OutlinedTextField(state.query, controller::setQuery, Modifier.weight(1f), singleLine = true, label = { Text(text("MainPage.Tab-Search")) })
                                    TextButton(onClick = { controller.refresh() }) { Text("→") }
                                }
                                OutlinedTextField(state.filter, controller::setFilter, Modifier.fillMaxWidth().padding(horizontal = 8.dp),
                                    singleLine = true, label = { Text(text("WorkContainer.FilterAutoSuggestBox-PlaceholderText")) })
                                if(state.filter.isNotEmpty() && state.completions.isNotEmpty()) Row(Modifier.horizontalScroll(rememberScrollState())) {
                                    state.completions.take(8).forEach { completion -> TextButton(onClick = { controller.setFilter(completion.completedText) }) { Text(completion.displayText) } }
                                }
                                if(state.source == FeedSource.Ranking) Row(Modifier.horizontalScroll(rememberScrollState()).padding(horizontal = 8.dp)) {
                                    RankMode.entries.forEach { mode -> FilterChip(selected = mode == state.rankMode, onClick = { controller.selectRank(mode) }, label = { Text(mode.name) }) }
                                }
                                Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                                    Text(text(state.source.label), style = MaterialTheme.typography.titleMedium)
                                    TextButton(onClick = { controller.refresh() }) { Text(text("WorkContainer.RefreshButton-Label")) }
                                }
                                val gridState = rememberLazyStaggeredGridState()
                                LazyVerticalStaggeredGrid(columns = StaggeredGridCells.Adaptive(160.dp), state = gridState,
                                    contentPadding = PaddingValues(8.dp), verticalItemSpacing = 8.dp, horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.weight(1f)) {
                                    items(state.works, key = { "${it.simpleKind}:${it.id}" }) { work ->
                                        Card(onClick = { controller.open(work) }) {
                                            val ratio = if(work is WorkEntry.Illust && work.illustration.height > 0) work.illustration.width.toFloat() / work.illustration.height else .75f
                                            RustImage(images, work.imageUrl, work.title, Modifier.fillMaxWidth().aspectRatio(ratio.coerceIn(.4f, 2f)))
                                            Text(work.title, Modifier.padding(8.dp), maxLines = 2)
                                            Text(work.author.name, Modifier.padding(horizontal = 8.dp), style = MaterialTheme.typography.bodySmall)
                                            Row { TextButton(onClick = { controller.bookmark(work) }) { Text(if(work.bookmarked) "♥" else "♡") }; TextButton(onClick = { controller.download(work) }) { Text("↓") } }
                                        }
                                    }
                                    item { if(state.loading) CircularProgressIndicator() else if(!state.exhausted) TextButton(onClick = controller::loadMore) { Text("＋") } }
                                }
                            } }
                            state.selected?.let { selected ->
                                if(wide) VerticalDivider()
                                Detail(controller, state, selected, images, Modifier.weight(1f), ::text)
                            }
                        }
                    }
                }
            }
        }
        state.userDetail?.let { detail -> AlertDialog(
            onDismissRequest = controller::closeUser,
            title = { Text(detail.user.name) },
            text = { Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(detail.user.comment.orEmpty())
                TextButton(onClick = { controller.follow(detail.user) }) { Text(text(if(detail.user.isFollowed) "UserViewerPage.Unfollow" else "UserViewerPage.Follow")) }
                TextButton(onClick = { controller.subscribe(detail.user) }) { Text(text("WorkSubscriptionsSettingsExpander.AddSubscriptionButton-Label")) }
            } },
            confirmButton = { TextButton(onClick = { controller.userWorks(detail.user) }) { Text(text("MainPage.Tab-WorkPosts")) } },
            dismissButton = { TextButton(onClick = controller::closeUser) { Text(text("ContentDialog.CancelButtonContent")) } },
        ) }
    }
}

@Composable
private fun Login(controller: AppController, text: (String) -> String) {
    var token by rememberSaveable { mutableStateOf("") }
    var code by rememberSaveable { mutableStateOf("") }
    Column(Modifier.fillMaxWidth().padding(24.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
        Text(text("LoginPage.LoginHeader"), style = MaterialTheme.typography.headlineMedium)
        OutlinedTextField(token, { token = it }, Modifier.fillMaxWidth(), visualTransformation = PasswordVisualTransformation(), label = { Text(text("LoginPage.RefreshTokenBox-PlaceholderText")) })
        Button(onClick = { controller.login(token); token = "" }, enabled = token.isNotBlank()) { Text(text("LoginPage.RefreshTokenButton-Content")) }
        OutlinedButton(onClick = { controller.beginOAuth() }) { Text("OAuth") }
        OutlinedTextField(code, { code = it }, Modifier.fillMaxWidth(), label = { Text("OAuth code") })
        Button(onClick = { controller.completeOAuth(code); code = "" }, enabled = code.isNotBlank()) { Text(text("LoginPage.LoginHeader")) }
    }
}

@Composable
private fun Detail(controller: AppController, state: AppState, work: WorkEntry, images: ImageRepository, modifier: Modifier, text: (String) -> String) {
    LazyColumn(modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        item { Text(work.title, style = MaterialTheme.typography.headlineSmall); TextButton(onClick = { controller.openUser(work.author) }) { Text(work.author.name) } }
        item { Row {
            TextButton(onClick = { controller.bookmark(work) }) { Text(if(work.bookmarked) "♥" else "♡") }
            TextButton(onClick = { controller.download(work) }) { Text(text("EntryItem.Save")) }
            TextButton(onClick = { controller.platform.shareText("https://www.pixiv.net/${if(work is WorkEntry.NovelWork) "novel/show.php?id=" else "artworks/"}${work.id}") }) { Text(text("EntryItem.GenerateWebLink")) }
        } }
        when(work) {
            is WorkEntry.Illust -> {
                val urls = work.illustration.metaPages.mapNotNull { it.imageUrls.large ?: it.imageUrls.original }.ifEmpty { listOfNotNull(work.illustration.imageUrls.large ?: work.imageUrl) }
                items(urls) { url ->
                    var zoom by remember(url) { mutableFloatStateOf(1f) }
                    RustImage(images, url, work.title, Modifier.fillMaxWidth().height(500.dp).pointerInput(url) {
                        detectTransformGestures { _, _, factor, _ -> zoom = (zoom * factor).coerceIn(1f, 5f) }
                    }.graphicsLayer { scaleX = zoom; scaleY = zoom }, width = 1440)
                }
                item { Text(work.illustration.tags.joinToString("  ") { "#${it.name}" }) }
            }
            is WorkEntry.NovelWork -> item {
                val content = state.novelContent
                val document = state.novelDocument
                if (content != null && document != null) NovelReader(controller, content, document, images)
                else CircularProgressIndicator()
            }
        }
        item { TextButton(onClick = { controller.loadComments(work) }) { Text(text("MainPage.Tab-Comments")) } }
        items(state.comments, key = { it.id }) { comment ->
            Text(comment.user.name, style = MaterialTheme.typography.labelLarge)
            Text(comment.comment)
        }
        item {
            var comment by rememberSaveable(work.id) { mutableStateOf("") }
            OutlinedTextField(comment, { comment = it }, Modifier.fillMaxWidth(), label = { Text(text("MainPage.Tab-Comments")) })
            TextButton(onClick = { controller.postComment(work, comment) }, enabled = comment.isNotBlank()) { Text("→") }
        }
    }
}

@Composable
private fun Settings(controller: AppController, state: AppState, text: (String) -> String) {
    val prefs = state.preferences ?: return
    Column(Modifier.verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(text("AppSettings.AppLanguageEntry-Header"), style = MaterialTheme.typography.titleLarge)
        Messages.locales.forEach { locale -> FilterChip(selected = state.locale == locale, onClick = { controller.setLocale(locale) }, label = { Text(locale) }) }
        Row { Text(text("MainPage.Tab-Settings"), Modifier.weight(1f)); Switch(state.darkTheme, controller::setDarkTheme) }
        HorizontalDivider()
        Text(text("AppSettings.SettingsGroup-Network-Header"), style = MaterialTheme.typography.titleLarge)
        var proxy by rememberSaveable { mutableStateOf(prefs.proxyUrl.orEmpty()) }
        var fronting by rememberSaveable { mutableStateOf(prefs.domainFrontingEnabled) }
        var mirror by rememberSaveable { mutableStateOf(prefs.mirrorHost.orEmpty()) }
        OutlinedTextField(proxy, { proxy = it }, Modifier.fillMaxWidth(), label = { Text(text("AppSettings.ProxyTextBoxEntry-Header")) })
        OutlinedTextField(mirror, { mirror = it }, Modifier.fillMaxWidth(), label = { Text(text("AppSettings.ImageMirrorServerEntry-Header")) })
        Row { Text(text("AppSettings.EnablePixivDomainFrontingEntry-Header"), Modifier.weight(1f)); Switch(fronting, { fronting = it }) }
        Button(onClick = { controller.changePreferences { it.copy(proxyUrl = proxy.trim().ifEmpty { null }, mirrorHost = mirror.trim().ifEmpty { null }, domainFrontingEnabled = fronting) } }) { Text(text("EntryItem.Save")) }
        HorizontalDivider()
        Text(text("AppSettings.SettingsGroup-Download-Header"), style = MaterialTheme.typography.titleLarge)
        var macro by rememberSaveable { mutableStateOf(prefs.downloadPathMacro) }
        OutlinedTextField(macro, { macro = it }, Modifier.fillMaxWidth(), label = { Text(text("AppSettings.DownloadPathMacroEntry-Header")) })
        Button(onClick = { controller.changePreferences { it.copy(downloadPathMacro = macro) } }) { Text(text("EntryItem.Save")) }
        Row(Modifier.horizontalScroll(rememberScrollState())) {
            (listOf("Original", "Gif", "Apng", "Webp") + if(controller.platform.videoEncoder != null || controller.runtime.capabilities().mp4Export) listOf("Mp4") else emptyList()).forEach { format -> FilterChip(selected = prefs.ugoiraFormat == format,
                onClick = { controller.changePreferences { it.copy(ugoiraFormat = format) } }, label = { Text(format) }) }
        }
        Row(Modifier.horizontalScroll(rememberScrollState())) {
            listOf("OriginalTxt", "Md", "Html", "Epub").forEach { format -> FilterChip(selected = prefs.novelFormat == format,
                onClick = { controller.changePreferences { it.copy(novelFormat = format) } }, label = { Text(format) }) }
        }
        HorizontalDivider()
        Text(text("AppSettings.SettingsGroup-Mcp-Header"), style = MaterialTheme.typography.titleLarge)
        Row { Text(text("AppSettings.EnableMcpWriteToolsEntry-Header"), Modifier.weight(1f)); Switch(prefs.mcpWriteTools, { enabled -> controller.changePreferences { it.copy(mcpWriteTools = enabled) } }) }
        Row { Text(text("AppSettings.EnableMcpServerEntry-Header"), Modifier.weight(1f)); Switch(state.mcpRunning, { controller.setMcpEnabled(it) }) }
        Text(controller.runtime.directories().data, style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun Subscriptions(controller: AppController, state: AppState, text: (String) -> String) {
    LazyColumn(Modifier.fillMaxSize().padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        item { Button(onClick = { controller.syncSubscriptions() }) { Text(text("WorkSubscriptionsSettingsExpander.SyncAllSubscriptionsButton-Label")) } }
        items(state.subscriptions, key = { it.historyEntryId }) { row ->
            Card { Column(Modifier.padding(12.dp)) {
                Text(row.title, style = MaterialTheme.typography.titleMedium)
                Text(row.author)
                Row {
                    TextButton(onClick = { controller.syncSubscriptions(row.historyEntryId) }) { Text("↻") }
                    TextButton(onClick = { controller.removeSubscription(row) }) { Text("×") }
                }
            } }
        }
    }
}

@Composable
private fun Downloads(controller: AppController, state: AppState, text: (String) -> String) {
    LaunchedEffect(controller) { controller.refreshDownloads().join() }
    LazyColumn(Modifier.fillMaxSize().padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        items(state.downloads?.ordinaryItems.orEmpty(), key = { "ordinary:${it.key.destination}" }) { item ->
            DownloadRow(controller, item, text)
        }
        state.downloads?.folders.orEmpty().forEach { folder ->
            item("folder:${folder.subscriptionId}") {
                Column(Modifier.fillMaxWidth().padding(top = 16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(folder.displayName, style = MaterialTheme.typography.titleLarge)
                    Text("${folder.completedCount} / ${folder.totalCount} · ${folder.progressPercentage.toInt()}%")
                    if (folder.isFetching) LinearProgressIndicator(Modifier.fillMaxWidth())
                    HorizontalDivider()
                }
            }
            items(folder.items, key = { "folder:${folder.subscriptionId}:${it.key.destination}" }) { item ->
                DownloadRow(controller, item, text)
            }
        }
    }
}

@Composable
private fun DownloadRow(controller: AppController, item: uniffi.pixeval_download.DownloadItemSnapshot, text: (String) -> String) {
    val active = item.state == uniffi.pixeval_download.DownloadState.QUEUED || item.state == uniffi.pixeval_download.DownloadState.RUNNING
    Card { Column(Modifier.padding(12.dp)) {
        Text(item.title)
        Text("${item.state} · ${item.progressPercentage.toInt()}%")
        item.errorMessage?.let { Text(it) }
        Row(Modifier.horizontalScroll(rememberScrollState())) {
            TextButton(onClick = { controller.changeDownload(item.key, "pause") }, enabled = active) { Text(text("Misc.Pause")) }
            TextButton(onClick = { controller.changeDownload(item.key, "resume") }, enabled = item.state == uniffi.pixeval_download.DownloadState.PAUSED) { Text(text("Misc.Play")) }
            TextButton(onClick = { controller.changeDownload(item.key, "cancel") }, enabled = active || item.state == uniffi.pixeval_download.DownloadState.PAUSED) { Text(text("ContentDialog.CancelButtonContent")) }
            TextButton(onClick = { controller.changeDownload(item.key, "retry") }, enabled = item.state == uniffi.pixeval_download.DownloadState.ERROR || item.state == uniffi.pixeval_download.DownloadState.CANCELLED) { Text(text("WorkContainer.RefreshButton-Label")) }
        }
        if (item.state == uniffi.pixeval_download.DownloadState.COMPLETED) Row {
            TextButton(onClick = { controller.exportDownload(item) }) { Text(text("Mobile.ExportFile")) }
            TextButton(onClick = { controller.exportDownload(item, true) }) { Text(text("Mobile.ShareFile")) }
        }
    } }
}
