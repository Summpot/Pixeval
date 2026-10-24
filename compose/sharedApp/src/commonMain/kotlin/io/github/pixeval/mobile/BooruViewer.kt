package io.github.pixeval.mobile

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.staggeredgrid.LazyVerticalStaggeredGrid
import androidx.compose.foundation.lazy.staggeredgrid.StaggeredGridCells
import androidx.compose.foundation.lazy.staggeredgrid.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import uniffi.pixeval_booru.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun BooruViewer(
    controller: AppController,
    images: ImageRepository,
    modifier: Modifier = Modifier,
) {
    val scope = rememberCoroutineScope()
    var platform by remember { mutableStateOf<BooruPlatform>(BooruPlatform.YANDERE) }
    var tagQuery by remember { mutableStateOf("rating:safe") }
    var pageIndex by remember { mutableStateOf(1u) }
    var posts by remember { mutableStateOf<List<BooruPostPreview>>(emptyList()) }
    var loading by remember { mutableStateOf(false) }
    var hasMore by remember { mutableStateOf(true) }
    var errorMsg by remember { mutableStateOf<String?>(null) }
    var selectedPost by remember { mutableStateOf<BooruPost?>(null) }
    var postLoading by remember { mutableStateOf(false) }

    val booruClient = remember { BooruClient(null) }
    DisposableEffect(booruClient) {
        onDispose { booruClient.destroy() }
    }

    fun searchPosts(page: UInt) {
        loading = true
        errorMsg = null
        scope.launch {
            try {
                val res = withContext(Dispatchers.IO) {
                    booruClient.search(platform, tagQuery, page)
                }
                posts = if (page == 1u) res.results else posts + res.results
                pageIndex = page
                hasMore = res.results.size >= 10
            } catch (e: Exception) {
                errorMsg = e.message ?: e.toString()
            } finally {
                loading = false
            }
        }
    }

    LaunchedEffect(platform) {
        searchPosts(1u)
    }

    Column(modifier = modifier.fillMaxSize().padding(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        // Platform Chips
        Row(
            modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            BooruPlatform.entries.forEach { p ->
                FilterChip(
                    selected = p == platform,
                    onClick = { platform = p },
                    label = { Text(p.name) }
                )
            }
        }

        // Tag Search Bar
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            OutlinedTextField(
                value = tagQuery,
                onValueChange = { tagQuery = it },
                modifier = Modifier.weight(1f),
                singleLine = true,
                label = { Text("Booru 标签搜索") }
            )
            Spacer(Modifier.width(8.dp))
            Button(onClick = { searchPosts(1u) }) {
                Text("搜索")
            }
        }

        errorMsg?.let { err ->
            Surface(color = MaterialTheme.colorScheme.errorContainer, shape = MaterialTheme.shapes.small) {
                Text(err, color = MaterialTheme.colorScheme.onErrorContainer, modifier = Modifier.padding(8.dp))
            }
        }

        // Posts Grid
        LazyVerticalStaggeredGrid(
            columns = StaggeredGridCells.Adaptive(160.dp),
            contentPadding = PaddingValues(4.dp),
            verticalItemSpacing = 6.dp,
            horizontalArrangement = Arrangement.spacedBy(6.dp),
            modifier = Modifier.weight(1f)
        ) {
            items(posts, key = { "${platform.name}:${it.id}" }) { preview ->
                Card(
                    onClick = {
                        postLoading = true
                        scope.launch {
                            try {
                                val fullPost = withContext(Dispatchers.IO) {
                                    booruClient.getPost(platform, preview.id)
                                }
                                selectedPost = fullPost
                            } catch (e: Exception) {
                                errorMsg = e.message
                            } finally {
                                postLoading = false
                            }
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(Modifier.padding(4.dp)) {
                        Text("ID: ${preview.id}", style = MaterialTheme.typography.labelSmall)
                        Text(
                            text = preview.tagString,
                            style = MaterialTheme.typography.bodySmall,
                            maxLines = 3,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }

            item {
                Box(Modifier.fillMaxWidth().padding(16.dp), contentAlignment = Alignment.Center) {
                    if (loading) {
                        CircularProgressIndicator(Modifier.size(24.dp))
                    } else if (hasMore && posts.isNotEmpty()) {
                        Button(onClick = { searchPosts(pageIndex + 1u) }) {
                            Text("下一页")
                        }
                    }
                }
            }
        }
    }

    selectedPost?.let { post ->
        val uriHandler = LocalUriHandler.current
        AlertDialog(
            onDismissRequest = { selectedPost = null },
            title = { Text("Post #${post.id} (${post.platform.name})") },
            text = {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 450.dp)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    val imgUrl = post.sampleUrl ?: post.originalUrl
                    RustImage(
                        repository = images,
                        url = imgUrl,
                        description = post.id,
                        modifier = Modifier.fillMaxWidth().heightIn(max = 240.dp)
                    )
                    Text("尺寸: ${post.width} × ${post.height} (${post.fileExt.uppercase()})")
                    Text("评分: ${post.score}  |  分级: ${post.rating}")
                    if (post.uploaderName.isNotEmpty()) {
                        Text("上传者: ${post.uploaderName}")
                    }
                    Text("标签:", style = MaterialTheme.typography.titleSmall)
                    Text(post.tags.joinToString(" ") { it.name }, style = MaterialTheme.typography.bodySmall)
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    uriHandler.openUri(post.originalUrl)
                }) {
                    Text("在浏览器中查看原图")
                }
            },
            dismissButton = {
                TextButton(onClick = { selectedPost = null }) {
                    Text("关闭")
                }
            }
        )
    }
}
