package io.github.pixeval.mobile

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import uniffi.pixeval_mako.NovelContent
import uniffi.pixeval_novel.NovelDocument
import uniffi.pixeval_novel.NovelNode

/** Presentation uses the Rust parser's nodes and the original domain models. */
@Composable
internal fun NovelReader(controller: AppController, content: NovelContent, document: NovelDocument, images: ImageRepository) {
    if (document.pages.isEmpty()) return
    var page by rememberSaveable(content.id) { mutableIntStateOf(((content.marker?.page ?: 1) - 1).coerceIn(document.pages.indices)) }
    var chapters by remember { mutableStateOf(false) }
    fun go(index: Int) {
        page = index.coerceIn(document.pages.indices)
        controller.markNovelPage(content.id, page)
    }
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Row {
            TextButton(onClick = { go(page - 1) }, enabled = page > 0) { Text("←") }
            Box {
                TextButton(onClick = { chapters = true }) { Text("${page + 1} / ${document.pages.size}") }
                DropdownMenu(chapters, { chapters = false }) {
                    document.pages.forEachIndexed { index, section ->
                        val titles = section.nodes.filterIsInstance<NovelNode.Chapter>().joinToString(" · ") { it.title }
                        DropdownMenuItem(text = { Text("${index + 1} $titles") }, onClick = { chapters = false; go(index) })
                    }
                }
            }
            TextButton(onClick = { go(page + 1) }, enabled = page < document.pages.lastIndex) { Text("→") }
        }
        document.pages[page].nodes.forEach { node -> when(node) {
            is NovelNode.Text -> SelectionContainer { Text(node.content, style = MaterialTheme.typography.bodyLarge) }
            is NovelNode.Ruby -> Text("${node.kanji}（${node.rubyText}）", style = MaterialTheme.typography.bodyLarge)
            is NovelNode.Chapter -> Text(node.title, style = MaterialTheme.typography.headlineSmall)
            is NovelNode.JumpUri -> TextButton(onClick = { controller.openLink(node.uri) }) { Text(node.title) }
            is NovelNode.JumpPage -> TextButton(onClick = { go(node.page.toInt() - 1) }) { Text("→ ${node.page}") }
            is NovelNode.UploadImage -> {
                val image = content.images.firstOrNull { it.novelImageId == node.imageId }
                val url = image?.urls?.let { it.x1200.ifBlank { it.mw480.ifBlank { it.original } } }
                RustImage(images, url, node.imageId.toString(), Modifier.fillMaxWidth().height(400.dp), 1200)
            }
            is NovelNode.PixivImage -> {
                val image = content.illusts.firstOrNull { it.id == node.illustId && it.page == node.page }
                val url = image?.takeIf { it.visible }?.illust?.images?.let { it.original ?: it.medium }
                RustImage(images, url, image?.availableMessage ?: node.illustId.toString(), Modifier.fillMaxWidth().height(400.dp), 1200)
            }
        } }
    }
}
