package io.github.pixeval.mobile

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import uniffi.pixeval_config.*

enum class YamlConfigType(val label: String) {
    HOME_CARDS("首页卡片布局 (Cards)"),
    NAVIGATION("导航菜单配置 (Navigation)")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun YamlEditor(
    controller: AppController,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
) {
    var selectedType by remember { mutableStateOf(YamlConfigType.HOME_CARDS) }
    var text by remember {
        mutableStateOf(
            try {
                formatHomePageCardsYaml(createDefaultCards())
            } catch (e: Exception) {
                "# Pixeval Home Cards Configuration\n"
            }
        )
    }
    var validationError by remember { mutableStateOf<String?>(null) }
    var notice by remember { mutableStateOf<String?>(null) }

    fun validate(currentText: String) {
        try {
            validateAndNormalizeYaml(currentText)
            when (selectedType) {
                YamlConfigType.HOME_CARDS -> {
                    parseHomePageCardsYaml(currentText)
                }
                YamlConfigType.NAVIGATION -> {
                    val result = parseNavigationYaml(
                        currentText,
                        listOf("browse", "downloads", "settings", "subscriptions", "spotlight"),
                        listOf("home", "star", "search", "user", "settings"),
                        null
                    )
                    if (!result.isValid) {
                        val firstDiag = result.diagnostics.firstOrNull()
                        throw Exception(firstDiag?.message ?: "Navigation YAML is invalid")
                    }
                }
            }
            validationError = null
        } catch (e: Exception) {
            validationError = e.message ?: e.toString()
        }
    }

    LaunchedEffect(selectedType) {
        text = when (selectedType) {
            YamlConfigType.HOME_CARDS -> {
                try { formatHomePageCardsYaml(createDefaultCards()) } catch (e: Exception) { "" }
            }
            YamlConfigType.NAVIGATION -> {
                "# Pixeval Navigation Configuration\nitems:\n  - page: browse\n    icon: home\n    title: 首页\n  - page: downloads\n    icon: star\n    title: 下载\n  - page: spotlight\n    icon: search\n    title: 特辑\n"
            }
        }
        validate(text)
    }

    Column(modifier = modifier.fillMaxSize().padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            YamlConfigType.entries.forEach { type ->
                FilterChip(
                    selected = type == selectedType,
                    onClick = { selectedType = type },
                    label = { Text(type.label) }
                )
            }
        }

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (validationError != null) {
                Surface(
                    color = MaterialTheme.colorScheme.errorContainer,
                    shape = MaterialTheme.shapes.small
                ) {
                    Text(
                        text = "⚠ $validationError",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onErrorContainer,
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
                        maxLines = 2
                    )
                }
            } else {
                Surface(
                    color = MaterialTheme.colorScheme.primaryContainer,
                    shape = MaterialTheme.shapes.small
                ) {
                    Text(
                        text = "✓ YAML 语法校验通过",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onPrimaryContainer,
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                    )
                }
            }

            Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                TextButton(onClick = {
                    try {
                        val formatted = validateAndNormalizeYaml(text)
                        text = formatted
                        validate(formatted)
                        notice = "已格式化"
                    } catch (e: Exception) {
                        validationError = e.message
                    }
                }) {
                    Text("格式化")
                }
                Button(
                    onClick = {
                        validate(text)
                        if (validationError == null) {
                            notice = "已保存配置"
                        }
                    },
                    enabled = validationError == null
                ) {
                    Text("保存")
                }
            }
        }

        notice?.let { msg ->
            Text(msg, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
        }

        OutlinedTextField(
            value = text,
            onValueChange = {
                text = it
                validate(it)
                notice = null
            },
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
            textStyle = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
            label = { Text("YAML 内容编辑") }
        )
    }
}
