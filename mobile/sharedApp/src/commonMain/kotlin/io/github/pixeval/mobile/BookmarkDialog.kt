package io.github.pixeval.mobile

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import uniffi.pixeval_mako.BookmarkDetail
import uniffi.pixeval_mako.PrivacyRestrict
import uniffi.pixeval_mako.WorkEntry

@OptIn(ExperimentalLayoutApi::class)
@Composable
internal fun BookmarkDialog(
    controller: AppController,
    work: WorkEntry,
    onDismiss: () -> Unit
) {
    var loading by remember { mutableStateOf(true) }
    var restrict by remember { mutableStateOf(PrivacyRestrict.PUBLIC) }
    var selectedTags by remember { mutableStateOf<Set<String>>(emptySet()) }
    var availableTags by remember { mutableStateOf<List<String>>(emptyList()) }
    var newTag by remember { mutableStateOf("") }
    var isBookmarked by remember { mutableStateOf(work.bookmarked) }

    LaunchedEffect(work.id) {
        loading = true
        try {
            val detail = withContext(Dispatchers.IO) {
                controller.client.getBookmarkDetailUnified(work.simpleKind, work.id)
            }
            restrict = detail.restrict
            isBookmarked = detail.isBookmarked
            selectedTags = detail.tags.filter { it.isRegistered }.map { it.name }.toSet()

            val userId = controller.state.value.user?.id?.toLongOrNull()
            if (userId != null) {
                val userTags = withContext(Dispatchers.IO) {
                    controller.client.workBookmarkTagsUnified(work.simpleKind, userId, detail.restrict)
                }
                availableTags = (detail.tags.map { it.name } + userTags.map { it.name }).distinct()
            } else {
                availableTags = detail.tags.map { it.name }
            }
        } catch (_: Exception) {
            // Fallback to basic state
        } finally {
            loading = false
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (isBookmarked) "Edit Bookmark" else "Add Bookmark") },
        text = {
            if (loading) {
                Box(Modifier.fillMaxWidth().height(120.dp), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
            } else {
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text("Privacy", style = MaterialTheme.typography.labelLarge)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        FilterChip(
                            selected = restrict == PrivacyRestrict.PUBLIC,
                            onClick = { restrict = PrivacyRestrict.PUBLIC },
                            label = { Text("Public") }
                        )
                        FilterChip(
                            selected = restrict == PrivacyRestrict.PRIVATE,
                            onClick = { restrict = PrivacyRestrict.PRIVATE },
                            label = { Text("Private") }
                        )
                    }

                    Text("Tags", style = MaterialTheme.typography.labelLarge)
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalArrangement = Arrangement.spacedBy(6.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        availableTags.forEach { tag ->
                            val selected = tag in selectedTags
                            FilterChip(
                                selected = selected,
                                onClick = {
                                    selectedTags = if (selected) selectedTags - tag else selectedTags + tag
                                },
                                label = { Text(tag) }
                            )
                        }
                    }

                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        OutlinedTextField(
                            value = newTag,
                            onValueChange = { newTag = it },
                            modifier = Modifier.weight(1f),
                            singleLine = true,
                            label = { Text("New tag") }
                        )
                        Spacer(Modifier.width(8.dp))
                        TextButton(
                            onClick = {
                                val trimmed = newTag.trim()
                                if (trimmed.isNotEmpty()) {
                                    selectedTags = selectedTags + trimmed
                                    if (trimmed !in availableTags) availableTags = availableTags + trimmed
                                    newTag = ""
                                }
                            },
                            enabled = newTag.isNotBlank()
                        ) {
                            Text("Add")
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    controller.bookmarkAdvanced(work, true, restrict, selectedTags.toList())
                    onDismiss()
                },
                enabled = !loading
            ) {
                Text("Save")
            }
        },
        dismissButton = {
            Row {
                if (isBookmarked) {
                    TextButton(
                        onClick = {
                            controller.bookmarkAdvanced(work, false, restrict, emptyList())
                            onDismiss()
                        },
                        colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error)
                    ) {
                        Text("Remove")
                    }
                }
                TextButton(onClick = onDismiss) {
                    Text("Cancel")
                }
            }
        }
    )
}
