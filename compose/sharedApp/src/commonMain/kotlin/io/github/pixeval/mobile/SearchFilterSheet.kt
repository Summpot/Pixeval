package io.github.pixeval.mobile

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import uniffi.pixeval_mako.*

data class SearchFilterState(
    val illustSearchTarget: IllustSearchTarget = IllustSearchTarget.PARTIAL_MATCH_FOR_TAGS,
    val novelSearchTarget: NovelSearchTarget = NovelSearchTarget.PARTIAL_MATCH_FOR_TAGS,
    val sort: SortOrder = SortOrder.DATE_DESC,
    val aiType: Int? = null, // null: All, 0: Hide AI, 1: Only AI
    val contentType: IllustContentType? = null,
    val startYear: Int? = null,
    val startMonth: Int? = null,
    val startDay: Int? = null,
    val endYear: Int? = null,
    val endMonth: Int? = null,
    val endDay: Int? = null,
) {
    val startDate: QueryDate?
        get() = if (startYear != null && startMonth != null && startDay != null) {
            QueryDate(startYear, startMonth, startDay)
        } else null

    val endDate: QueryDate?
        get() = if (endYear != null && endMonth != null && endDay != null) {
            QueryDate(endYear, endMonth, endDay)
        } else null
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SearchFilterSheet(
    isNovel: Boolean,
    initialFilter: SearchFilterState,
    onDismiss: () -> Unit,
    onApply: (SearchFilterState) -> Unit,
) {
    var filter by remember { mutableStateOf(initialFilter) }
    var startDateText by remember {
        mutableStateOf(
            if (initialFilter.startYear != null) "${initialFilter.startYear}-${initialFilter.startMonth?.toString()?.padStart(2, '0')}-${initialFilter.startDay?.toString()?.padStart(2, '0')}"
            else ""
        )
    }
    var endDateText by remember {
        mutableStateOf(
            if (initialFilter.endYear != null) "${initialFilter.endYear}-${initialFilter.endMonth?.toString()?.padStart(2, '0')}-${initialFilter.endDay?.toString()?.padStart(2, '0')}"
            else ""
        )
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (isNovel) "小说高级搜索筛选" else "插画高级搜索筛选") },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 480.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Match Target
                Text("匹配目标", style = MaterialTheme.typography.titleSmall)
                if (isNovel) {
                    val novelTargets = listOf(
                        NovelSearchTarget.PARTIAL_MATCH_FOR_TAGS to "标签（部分一致）",
                        NovelSearchTarget.EXACT_MATCH_FOR_TAGS to "标签（完全一致）",
                        NovelSearchTarget.TEXT to "正文",
                        NovelSearchTarget.KEYWORD to "关键字"
                    )
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        novelTargets.forEach { (target, label) ->
                            FilterChip(
                                selected = filter.novelSearchTarget == target,
                                onClick = { filter = filter.copy(novelSearchTarget = target) },
                                label = { Text(label, style = MaterialTheme.typography.bodySmall) }
                            )
                        }
                    }
                } else {
                    val illustTargets = listOf(
                        IllustSearchTarget.PARTIAL_MATCH_FOR_TAGS to "标签部分一致",
                        IllustSearchTarget.EXACT_MATCH_FOR_TAGS to "标签完全一致",
                        IllustSearchTarget.TITLE_AND_CAPTION to "标题与说明文字",
                        IllustSearchTarget.KEYWORD to "关键字"
                    )
                    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        illustTargets.chunked(2).forEach { rowTargets ->
                            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                rowTargets.forEach { (target, label) ->
                                    FilterChip(
                                        selected = filter.illustSearchTarget == target,
                                        onClick = { filter = filter.copy(illustSearchTarget = target) },
                                        label = { Text(label, style = MaterialTheme.typography.bodySmall) }
                                    )
                                }
                            }
                        }
                    }
                }

                // Sort Order
                Text("排序方式", style = MaterialTheme.typography.titleSmall)
                val sortOrders = listOf(
                    SortOrder.DATE_DESC to "最新发布",
                    SortOrder.DATE_ASC to "最早发布",
                    SortOrder.POPULAR_DESC to "按热度(会员)"
                )
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    sortOrders.forEach { (order, label) ->
                        FilterChip(
                            selected = filter.sort == order,
                            onClick = { filter = filter.copy(sort = order) },
                            label = { Text(label, style = MaterialTheme.typography.bodySmall) }
                        )
                    }
                }

                // Content Type (Illust only)
                if (!isNovel) {
                    Text("作品类型", style = MaterialTheme.typography.titleSmall)
                    val contentTypes = listOf(
                        null to "全部",
                        IllustContentType.ILLUSTRATION to "插画",
                        IllustContentType.MANGA to "漫画",
                        IllustContentType.UGOIRA to "动图"
                    )
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        contentTypes.forEach { (type, label) ->
                            FilterChip(
                                selected = filter.contentType == type,
                                onClick = { filter = filter.copy(contentType = type) },
                                label = { Text(label, style = MaterialTheme.typography.bodySmall) }
                            )
                        }
                    }
                }

                // AI Filter
                Text("AI 作品过滤", style = MaterialTheme.typography.titleSmall)
                val aiOptions = listOf(
                    null to "全部显示",
                    0 to "隐藏 AI",
                    1 to "仅 AI 作品"
                )
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    aiOptions.forEach { (aiVal, label) ->
                        FilterChip(
                            selected = filter.aiType == aiVal,
                            onClick = { filter = filter.copy(aiType = aiVal) },
                            label = { Text(label, style = MaterialTheme.typography.bodySmall) }
                        )
                    }
                }

                // Date Range (YYYY-MM-DD)
                Text("日期区间 (YYYY-MM-DD)", style = MaterialTheme.typography.titleSmall)
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = startDateText,
                        onValueChange = { text ->
                            startDateText = text
                            val parts = text.split("-")
                            if (parts.size == 3) {
                                val y = parts[0].toIntOrNull()
                                val m = parts[1].toIntOrNull()
                                val d = parts[2].toIntOrNull()
                                filter = filter.copy(startYear = y, startMonth = m, startDay = d)
                            } else if (text.isEmpty()) {
                                filter = filter.copy(startYear = null, startMonth = null, startDay = null)
                            }
                        },
                        modifier = Modifier.weight(1f),
                        singleLine = true,
                        label = { Text("起始日期") },
                        placeholder = { Text("2024-01-01") }
                    )
                    OutlinedTextField(
                        value = endDateText,
                        onValueChange = { text ->
                            endDateText = text
                            val parts = text.split("-")
                            if (parts.size == 3) {
                                val y = parts[0].toIntOrNull()
                                val m = parts[1].toIntOrNull()
                                val d = parts[2].toIntOrNull()
                                filter = filter.copy(endYear = y, endMonth = m, endDay = d)
                            } else if (text.isEmpty()) {
                                filter = filter.copy(endYear = null, endMonth = null, endDay = null)
                            }
                        },
                        modifier = Modifier.weight(1f),
                        singleLine = true,
                        label = { Text("结束日期") },
                        placeholder = { Text("2024-12-31") }
                    )
                }
            }
        },
        confirmButton = {
            Button(onClick = { onApply(filter); onDismiss() }) {
                Text("应用筛选")
            }
        },
        dismissButton = {
            TextButton(onClick = {
                filter = SearchFilterState()
                startDateText = ""
                endDateText = ""
                onApply(filter)
                onDismiss()
            }) {
                Text("重置")
            }
        }
    )
}
