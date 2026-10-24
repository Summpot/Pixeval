package io.github.pixeval.mobile

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.staggeredgrid.LazyVerticalStaggeredGrid
import androidx.compose.foundation.lazy.staggeredgrid.StaggeredGridCells
import androidx.compose.foundation.lazy.staggeredgrid.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import uniffi.pixeval_mako.*

enum class UserProfileTab(val label: String) {
    WORKS("作品"),
    FOLLOWING("关注"),
    FOLLOWERS("粉丝"),
    RELATED("相关作者")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun UserProfileSheet(
    controller: AppController,
    images: ImageRepository,
    detail: SingleUserResponse,
    onDismiss: () -> Unit,
) {
    val user = detail.user
    var currentTab by remember { mutableStateOf(UserProfileTab.WORKS) }
    var works by remember { mutableStateOf<List<WorkEntry>>(emptyList()) }
    var followingUsers by remember { mutableStateOf<List<User>>(emptyList()) }
    var followerUsers by remember { mutableStateOf<List<User>>(emptyList()) }
    var relatedUsers by remember { mutableStateOf<List<User>>(emptyList()) }
    var loadingTab by remember { mutableStateOf(false) }

    // Load data for active tab
    LaunchedEffect(currentTab, user.id) {
        loadingTab = true
        withContext(Dispatchers.IO) {
            try {
                when (currentTab) {
                    UserProfileTab.WORKS -> {
                        if (works.isEmpty()) {
                            val feed = controller.client.workPostedUnified(controller.state.value.kind, user.id)
                            val page = feed.nextPage()
                            works = page.items
                        }
                    }
                    UserProfileTab.FOLLOWING -> {
                        if (followingUsers.isEmpty()) {
                            val feed = controller.client.userFollowingUnified(user.id, PrivacyRestrict.PUBLIC)
                            val page = feed.nextPage()
                            followingUsers = page.items
                        }
                    }
                    UserProfileTab.FOLLOWERS -> {
                        if (followerUsers.isEmpty()) {
                            val feed = controller.client.userFollower(user.id)
                            val page = feed.nextPage()
                            followerUsers = page.items
                        }
                    }
                    UserProfileTab.RELATED -> {
                        if (relatedUsers.isEmpty()) {
                            val feed = controller.client.userRelated(user.id)
                            val page = feed.nextPage()
                            relatedUsers = page.items
                        }
                    }
                }
            } catch (e: Exception) {
                // Ignore or log tab loading error
            } finally {
                loadingTab = false
            }
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                val avatarUrl = user.profileImageUrls.medium
                    ?: user.profileImageUrls.px170x170
                    ?: user.profileImageUrls.px50x50
                    ?: ""
                if (avatarUrl.isNotEmpty()) {
                    RustImage(
                        repository = images,
                        url = avatarUrl,
                        description = user.name,
                        modifier = Modifier.size(48.dp).clip(CircleShape)
                    )
                }
                Column(Modifier.weight(1f)) {
                    Text(user.name, style = MaterialTheme.typography.titleMedium, maxLines = 1)
                    Text("@${user.account}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 360.dp, max = 500.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                // User Bio and Actions
                if (!user.comment.isNullOrBlank()) {
                    Text(
                        text = user.comment.orEmpty(),
                        style = MaterialTheme.typography.bodySmall,
                        maxLines = 3
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    FilledTonalButton(
                        onClick = { controller.follow(user) },
                        modifier = Modifier.weight(1f)
                    ) {
                        Text(if (user.isFollowed) "已关注" else "关注")
                    }
                    OutlinedButton(
                        onClick = { controller.subscribe(user) },
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("追更订阅")
                    }
                }

                // Tab Selector
                TabRow(selectedTabIndex = currentTab.ordinal) {
                    UserProfileTab.entries.forEach { tab ->
                        Tab(
                            selected = currentTab == tab,
                            onClick = { currentTab = tab },
                            text = { Text(tab.label, style = MaterialTheme.typography.labelMedium) }
                        )
                    }
                }

                if (loadingTab) {
                    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(Modifier.size(32.dp))
                    }
                } else {
                    when (currentTab) {
                        UserProfileTab.WORKS -> {
                            if (works.isEmpty()) {
                                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                                    Text("暂无作品", style = MaterialTheme.typography.bodyMedium)
                                }
                            } else {
                                LazyVerticalStaggeredGrid(
                                    columns = StaggeredGridCells.Adaptive(120.dp),
                                    contentPadding = PaddingValues(4.dp),
                                    verticalItemSpacing = 4.dp,
                                    horizontalArrangement = Arrangement.spacedBy(4.dp),
                                    modifier = Modifier.weight(1f)
                                ) {
                                    items(works, key = { "${it.simpleKind}:${it.id}" }) { work ->
                                        Card(onClick = { onDismiss(); controller.open(work) }) {
                                            RustImage(
                                                repository = images,
                                                url = work.imageUrl,
                                                description = work.title,
                                                modifier = Modifier.fillMaxWidth().height(100.dp)
                                            )
                                            Text(work.title, style = MaterialTheme.typography.labelSmall, maxLines = 1, modifier = Modifier.padding(4.dp))
                                        }
                                    }
                                }
                            }
                        }
                        UserProfileTab.FOLLOWING -> UserList(images, followingUsers, onUserClick = { clicked ->
                            controller.openUser(clicked)
                        })
                        UserProfileTab.FOLLOWERS -> UserList(images, followerUsers, onUserClick = { clicked ->
                            controller.openUser(clicked)
                        })
                        UserProfileTab.RELATED -> UserList(images, relatedUsers, onUserClick = { clicked ->
                            controller.openUser(clicked)
                        })
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = {
                onDismiss()
                controller.userWorks(user)
            }) {
                Text("全部作品")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("关闭")
            }
        }
    )
}

@Composable
private fun UserList(
    images: ImageRepository,
    users: List<User>,
    onUserClick: (User) -> Unit,
) {
    if (users.isEmpty()) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            Text("无相关用户", style = MaterialTheme.typography.bodyMedium)
        }
    } else {
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            items(users, key = { it.id }) { u ->
                Card(
                    onClick = { onUserClick(u) },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier.padding(8.dp).fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        val avatarUrl = u.profileImageUrls.medium ?: u.profileImageUrls.px170x170 ?: u.profileImageUrls.px50x50 ?: ""
                        if (avatarUrl.isNotEmpty()) {
                            RustImage(
                                repository = images,
                                url = avatarUrl,
                                description = u.name,
                                modifier = Modifier.size(36.dp).clip(CircleShape)
                            )
                        }
                        Column(Modifier.weight(1f)) {
                            Text(u.name, style = MaterialTheme.typography.bodyMedium, maxLines = 1)
                            Text("@${u.account}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        if (u.isFollowed) {
                            Surface(
                                color = MaterialTheme.colorScheme.secondaryContainer,
                                shape = MaterialTheme.shapes.extraSmall
                            ) {
                                Text(
                                    text = "已关注",
                                    style = MaterialTheme.typography.labelSmall,
                                    modifier = Modifier.padding(horizontal = 4.dp, vertical = 2.dp)
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}
