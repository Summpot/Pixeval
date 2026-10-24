package io.github.pixeval.mobile

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp

enum class HelpTopic(val id: String, val label: String) {
    FILTERS("filters", "高级筛选语法"),
    DOWNLOAD("download", "下载路径宏"),
    SUBSCRIPTIONS("subscriptions", "作品追更订阅"),
    MCP("mcp", "MCP AI 协议")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun HelpViewer(
    controller: AppController,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
) {
    var selectedTopic by remember { mutableStateOf(HelpTopic.FILTERS) }
    val content = remember(selectedTopic, controller.state.collectAsState().value.locale) {
        controller.runtime.helpDocument(selectedTopic.id)
    }

    Column(modifier = modifier.fillMaxSize().padding(12.dp)) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .horizontalScroll(rememberScrollState())
                .padding(bottom = 8.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            HelpTopic.entries.forEach { topic ->
                FilterChip(
                    selected = topic == selectedTopic,
                    onClick = { selectedTopic = topic },
                    label = { Text(topic.label) }
                )
            }
        }

        Surface(
            modifier = Modifier.fillMaxWidth().weight(1f),
            color = MaterialTheme.colorScheme.surfaceVariant,
            shape = MaterialTheme.shapes.medium
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(16.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text(
                    text = selectedTopic.label,
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.primary
                )
                HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                Text(
                    text = content,
                    style = MaterialTheme.typography.bodyMedium,
                    fontFamily = FontFamily.Monospace
                )
            }
        }
    }
}
