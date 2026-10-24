package io.github.pixeval.mobile

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import uniffi.pixeval_saucenao.*
import uniffi.pixeval_cache.CachePreviewCallback
import uniffi.pixeval_cache.DecodedPreviewFrame

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun SauceNaoViewer(
    controller: AppController,
    images: ImageRepository,
    modifier: Modifier = Modifier,
) {
    val scope = rememberCoroutineScope()
    val uriHandler = LocalUriHandler.current
    var imageUrl by remember { mutableStateOf("") }
    var apiKey by remember { mutableStateOf("") }
    var items by remember { mutableStateOf<List<SauceNaoItem>>(emptyList()) }
    var loading by remember { mutableStateOf(false) }
    var errorMsg by remember { mutableStateOf<String?>(null) }
    var quotaInfo by remember { mutableStateOf<String?>(null) }

    fun search() {
        val url = imageUrl.trim()
        if (url.isEmpty()) return
        loading = true
        errorMsg = null
        scope.launch {
            try {
                val bytes = withContext(Dispatchers.IO) {
                    val callback = object : CachePreviewCallback {
                        override fun onPreviewFrame(frame: DecodedPreviewFrame) {}
                        override fun onProgress(downloadedBytes: ULong, totalBytes: ULong) {}
                    }
                    controller.cache.getOrFetch(url, null, callback)
                }
                val client = SauceNaoClient(apiKey.trim(), null)
                try {
                    val result = withContext(Dispatchers.IO) {
                        client.searchDetailed(bytes)
                    }
                    items = result.results
                    quotaInfo = "短时剩余配额: ${result.remainingShort} / 长期剩余配额: ${result.remainingLong}"
                } finally {
                    client.destroy()
                }
            } catch (e: Exception) {
                errorMsg = e.message ?: e.toString()
            } finally {
                loading = false
            }
        }
    }

    Column(modifier = modifier.fillMaxSize().padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text("SauceNAO 以图搜图", style = MaterialTheme.typography.titleMedium)

        OutlinedTextField(
            value = imageUrl,
            onValueChange = { imageUrl = it },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            label = { Text("图片网络 URL 地址") },
            placeholder = { Text("https://example.com/sample.jpg") }
        )

        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            OutlinedTextField(
                value = apiKey,
                onValueChange = { apiKey = it },
                modifier = Modifier.weight(1f),
                singleLine = true,
                label = { Text("SauceNAO API Key (可选)") }
            )
            Spacer(Modifier.width(8.dp))
            Button(onClick = { search() }, enabled = !loading && imageUrl.isNotBlank()) {
                if (loading) CircularProgressIndicator(Modifier.size(16.dp))
                else Text("搜索原图")
            }
        }

        quotaInfo?.let {
            Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.primary)
        }

        errorMsg?.let { err ->
            Surface(color = MaterialTheme.colorScheme.errorContainer, shape = MaterialTheme.shapes.small) {
                Text(err, color = MaterialTheme.colorScheme.onErrorContainer, modifier = Modifier.padding(8.dp))
            }
        }

        LazyColumn(
            modifier = Modifier.fillMaxWidth().weight(1f),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(items, key = { "${it.indexId}:${it.artworkId}:${it.title}" }) { item ->
                Card(
                    onClick = {
                        val link = item.extUrls.firstOrNull() ?: item.sourceUrl
                        if (!link.isNullOrEmpty()) {
                            uriHandler.openUri(link)
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier.padding(8.dp).fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        RustImage(
                            repository = images,
                            url = item.thumbnailUrl,
                            description = item.title,
                            modifier = Modifier.size(80.dp)
                        )
                        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Surface(
                                    color = if (item.similarity >= 80.0) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.secondaryContainer,
                                    shape = MaterialTheme.shapes.extraSmall
                                ) {
                                    Text(
                                        text = "${item.similarity.toInt()}% 匹配度",
                                        style = MaterialTheme.typography.labelSmall,
                                        modifier = Modifier.padding(horizontal = 4.dp, vertical = 2.dp)
                                    )
                                }
                                Text(
                                    text = item.platform,
                                    style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            if (item.title.isNotEmpty()) {
                                Text(item.title, style = MaterialTheme.typography.titleSmall, maxLines = 1)
                            }
                            if (item.authorName.isNotEmpty()) {
                                Text("作者: ${item.authorName}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            item.artworkId?.let { id ->
                                Text("作品 ID: $id", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }
                }
            }
        }
    }
}
