package io.github.pixeval.mobile

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.staggeredgrid.LazyVerticalStaggeredGrid
import androidx.compose.foundation.lazy.staggeredgrid.StaggeredGridCells
import androidx.compose.foundation.lazy.staggeredgrid.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.horizontalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.unit.dp
import uniffi.pixeval_mako.SpotlightArticle
import uniffi.pixeval_mako.SpotlightCategory

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun SpotlightView(
    controller: AppController,
    images: ImageRepository,
    articles: List<SpotlightArticle>,
    loading: Boolean,
    exhausted: Boolean,
    currentCategory: SpotlightCategory,
    onSelectCategory: (SpotlightCategory) -> Unit,
    onLoadMore: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val uriHandler = LocalUriHandler.current
    val categories = listOf(
        SpotlightCategory.ALL to "全部",
        SpotlightCategory.SPOTLIGHT to "特辑",
        SpotlightCategory.TUTORIAL to "教程",
        SpotlightCategory.INSPIRATION to "灵感"
    )

    Column(modifier = modifier.fillMaxSize()) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .horizontalScroll(rememberScrollState())
                .padding(horizontal = 8.dp, vertical = 4.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            categories.forEach { (category, label) ->
                FilterChip(
                    selected = category == currentCategory,
                    onClick = { onSelectCategory(category) },
                    label = { Text(label) }
                )
            }
        }

        LazyVerticalStaggeredGrid(
            columns = StaggeredGridCells.Adaptive(180.dp),
            contentPadding = PaddingValues(8.dp),
            verticalItemSpacing = 8.dp,
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.weight(1f)
        ) {
            items(articles, key = { it.id }) { article ->
                Card(
                    onClick = {
                        if (article.articleUrl.isNotEmpty()) {
                            uriHandler.openUri(article.articleUrl)
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column {
                        RustImage(
                            repository = images,
                            url = article.thumbnail,
                            description = article.title,
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(120.dp)
                        )
                        Column(Modifier.padding(8.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            if (article.subcategoryLabel.isNotEmpty()) {
                                Surface(
                                    color = MaterialTheme.colorScheme.primaryContainer,
                                    shape = MaterialTheme.shapes.extraSmall
                                ) {
                                    Text(
                                        text = article.subcategoryLabel,
                                        style = MaterialTheme.typography.labelSmall,
                                        modifier = Modifier.padding(horizontal = 4.dp, vertical = 2.dp),
                                        color = MaterialTheme.colorScheme.onPrimaryContainer
                                    )
                                }
                            }
                            Text(
                                text = article.pureTitle ?: article.title,
                                style = MaterialTheme.typography.titleSmall,
                                maxLines = 2
                            )
                            if (article.publishDate.isNotEmpty()) {
                                Text(
                                    text = article.publishDate,
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                }
            }

            item {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    contentAlignment = Alignment.Center
                ) {
                    if (loading) {
                        CircularProgressIndicator(modifier = Modifier.size(24.dp))
                    } else if (!exhausted && articles.isNotEmpty()) {
                        Button(onClick = onLoadMore) {
                            Text("加载更多")
                        }
                    }
                }
            }
        }
    }
}
