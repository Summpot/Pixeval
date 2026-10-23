package io.github.pixeval.mobile

import io.github.pixeval.mobile.localization.Messages
import kotlin.test.*

class LocalizationTest {
    @Test fun preservesExistingLocalesAndFallsBackToChinese() {
        assertTrue(Messages.locales.containsAll(listOf("zh-Hans", "en-US", "ru-RU", "fr-FR")))
        assertEquals("Home", Messages.text("MainPage.Tab-Home", "en-GB"))
        assertEquals(Messages.text("MainPage.Tab-Home", "zh-Hans"), Messages.text("MainPage.Tab-Home", "ja-JP"))
    }
    @Test fun variablesAndLiteralWhitespaceKeepTheirMeaning() {
        assertEquals("flowers - Search", Messages.text("MainPage.SearchResultFormatted", "en-US", mapOf("searchText" to "flowers")))
        assertFailsWith<IllegalStateException> { Messages.text("MainPage.SearchResultFormatted", "en-US") }
    }
}
