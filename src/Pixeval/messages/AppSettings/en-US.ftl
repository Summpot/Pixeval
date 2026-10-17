SettingsGroup-Application-Header = Application

SettingsGroup-BrowsingExperience-Header = User Experiences

SettingsGroup-Network-Header = Network Settings

SettingsGroup-Download-Header = Download

SettingsGroup-Mcp-Header = MCP Settings

SettingsGroup-Search-Header = Search

SettingsGroup-Novel-Header = Novel Settings

RankOptionEntry-Description = Ranking parameters

RankOptionEntry-Header = Default ranking option

SimpleWorkTypeEntry-Description = Type of work to be shown by default

SimpleWorkTypeEntry-Header = Default work type

DownloadPathMacroEntry-Header = Default download location

DownloadPathMacroEntry-Description = See Download Macros instructions on the help page

EnableMcpServerEntry-Header = Enable MCP Server

EnableMcpServerEntry-Description = Allow local MCP clients to connect to the running Pixeval instance

McpPortEntry-Header = MCP Port

McpPortEntry-Description = The local port the MCP server listens on. Changing this will restart the MCP service; connected clients will need to reconnect.

EnableMcpWriteToolsEntry-Header = Enable MCP Write Tools

EnableMcpWriteToolsEntry-Description = Allow MCP clients to perform write operations such as posting comments, deleting comments, and adding to the download queue

McpMaxBinaryResourceMegabytesEntry-Header = MCP Binary Resource Size Limit (MB)

McpMaxBinaryResourceMegabytesEntry-Description = The Pixeval MCP server will refuse to return binary resources (such as thumbnails) exceeding this size to clients

HomePageRowsEntry-Description = Number of rows used in the custom home page layout

HomePageRowsEntry-Header = Home Page Layout Rows

HomePageColumnsEntry-Description = Number of columns used in the custom home page layout

HomePageColumnsEntry-Header = Home Page Layout Columns

HideHomePageToolbarEntry-Description = Hide the top toolbar on the home page, which contains edit and display option toggles

HideHomePageToolbarEntry-Header = Hide Home Page Toolbar

HideHomePageCardTitleEntry-Description = Hide the title bar at the bottom of home page cards

HideHomePageCardTitleEntry-Header = Hide Home Page Card Titles

EnablePixivDomainFrontingEntry-Description = Disabling this will cause most features to become unavailable for users in countries or regions where Pixiv is blocked

EnablePixivDomainFrontingEntry-Header = Enable Pixiv Domain Fronting

EnableSubscriptionDaemonEntry-Description = Automatically poll subscriptions in the background for new works and ingest them incrementally

EnableSubscriptionDaemonEntry-Header = Enable Subscription Daemon

SubscriptionDaemonIntervalMinutesEntry-Description = Interval in minutes for background checks of artist and series subscriptions

SubscriptionDaemonIntervalMinutesEntry-Header = Subscription Check Interval

PixivDomainFrontingTypeEntry-Description = Choose the bypass method for domain fronting requests. Different methods may work better under different network conditions.

PixivDomainFrontingTypeEntry-Header = Domain Fronting Method

EnableGitHubDomainFrontingEntry-Description = Disabling this will cause the About, Check for Updates, and Changelog features to become unavailable for users in countries or regions where GitHub is blocked

EnableGitHubDomainFrontingEntry-Header = Enable GitHub Domain Fronting

DownloadUpdateAutomaticallyEntry-Description = Metered networks excluded

DownloadUpdateAutomaticallyEntry-Header = Download update automatically

ThumbnailLayoutTypeEntry-Description = The grid layout constraints images in the same width and height, while flow layout only forces vertical alignments

ThumbnailLayoutTypeEntry-Header = Thumbnail View Mode

BrowseMode-Description = Browsing mode when viewing an image set with multiple images in the viewer page

BrowseMode-Header = Browse Mode

BrowseDirection-Description = Browsing direction when viewing an image set with multiple images in the viewer page

BrowseDirection-Header = Browse Direction

IllustrationViewerAutoPlayIntervalEntry-Description = Time interval before advancing to the next page during auto-play in the illustration viewer

IllustrationViewerAutoPlayIntervalEntry-Header = Auto-play Interval (seconds)

IllustrationViewerAutoPlayModeEntry-Description = Behavior when auto-play reaches the end in the illustration viewer

IllustrationViewerAutoPlayModeEntry-Header = Auto-play Mode

IllustrationViewerAutoPlayScopeEntry-Description = Page range for auto-play in the illustration viewer

IllustrationViewerAutoPlayScopeEntry-Header = Auto-play Scope

BlockedTagsEntry-Description = Blocked tags will not be displayed in Pixeval (Use comma to separate them)

BlockedTagsEntry-Header = Global blocked tags

BlockedTagsEntry-Placeholder = Blocked tags

ImageMirrorServerEntry-Description = Mirror host for downloading images, leave this empty if you don't understand

ImageMirrorServerEntry-Header = Host or the mirror site

ImageMirrorServerEntry-Placeholder = Hostname

MaxDownloadConcurrencyLevelEntry-Description = This setting entry sets the threshold of the maximum parallel download tasks, the max value will be the number of your CPU cores. Notice: A higher value may cause the system to be unresponsive on low-end PCs

MaxDownloadConcurrencyLevelEntry-Header = Maximum parallel download task

OverwriteDownloadedFileEntry-Description = The old file with same name will be overwritten once this setting is turned on

OverwriteDownloadedFileEntry-Header = Overwrite downloaded files

TargetAPIPlatformEntry-Description = Which API platform will be used during request? Keep default if you don't understand

TargetAPIPlatformEntry-Header = Target API platform

ProxyTypeEntry-Description = Choose the proxy method Pixeval uses for non-domain-fronting requests

ProxyTypeEntry-Header = Proxy Mode

ProxyTextBoxEntry-Description = Specify the proxy server address. If no protocol is specified, HTTP proxy is assumed.

ProxyTextBoxEntry-Header = Proxy server

LimitFileCacheSizeEntry-Description = When disabled, the file cache is kept indefinitely with no size limit. When enabled, old cache entries are cleaned up based on recent usage.

LimitFileCacheSizeEntry-Header = Limit File Cache Size

FileCacheSizeLimitInMegabytesEntry-Description = When the file cache exceeds this size, entries that haven't been used recently will be cleaned up first

FileCacheSizeLimitInMegabytesEntry-Header = File Cache Size Limit (MB)

WorkDownloadFormatEntry-Description = Set the format of the downloaded works

WorkDownloadFormatEntry-Header = Work format

WorkSubscriptionsSettingsEntry-Description = Add user bookmarks or posted works for automatic backup downloads

WorkSubscriptionsSettingsEntry-Header = Subscription Backup Downloads

WebCookieEntry-Description = Features such as the user feed require this cookie to function properly

WebCookieEntry-Header = Cookie

WebCookieEntry-Placeholder = Pixiv web cookie

SauceNaoApiKeyEntry-Header = SauceNAO API Key

SauceNaoApiKeyEntry-Description = You can acquire and API Key at here (requires sign up/login)

SauceNaoApiKeyEntry-Placeholder = API Key

OpenWorkInfoByDefaultEntry-Header = Open Work Info Panel by Default

OpenWorkInfoByDefaultEntry-Description = Automatically open the work info panel on the work details page

OpenUserInfoByDefaultEntry-Header = Open User Info Panel by Default

OpenUserInfoByDefaultEntry-Description = Automatically open the user info panel on the user details page

PixivNameResolverEntry-Header = Pixiv Name Resolver

PixivNameResolverEntry-Description = { "When Pixiv domain fronting is enabled, Pixiv domain names will be resolved to the following IP addresses\u000AEnter an IP and click \"+\" to add" }

GitHubNameResolverEntry-Header = GitHub Name Resolver

GitHubNameResolverEntry-Description = { "When GitHub domain fronting is enabled, GitHub domain names will be resolved to the following IP addresses\u000AEnter an IP and click \"+\" to add" }

AppLanguageEntry-Header = Language

AppLanguageEntry-Description = Open system language settings

AppLanguageEntry-Placeholder = Select language

ThemeEntry-Header = Application theme

ThemeEntry-Description = Open system theme settings

AppFontFamilyEntry-Header = Font settings (take effect after restart the app)

AppFontFamilyEntry-Description = When multiple fonts are selected, if a glyph is missing in the previous font, the next font will be used as a fallback

AppFontFamilyEntry-Placeholder = You can select a font from the dropdown

NovelSettingsFontFamilyEntry-Header = Font family

NovelSettingsFontColorEntry-Header = Font color

NovelSettingsFontColorEntry-Description = Body font color

NovelSettingsBackgroundEntry-Header = Background color

NovelSettingsBackgroundEntry-Description = Page background color

NovelSettingsFontWeightEntry-Header = Font weight

NovelSettingsFontWeightEntry-Description = Body font weight

NovelSettingsFontWeightEntry-Placeholder = Select font weight

NovelSettingsFontSizeEntry-Header = Font size

NovelSettingsFontSizeEntry-Description = Body font size

NovelSettingsLineHeightEntry-Header = Line height

NovelSettingsLineHeightEntry-Description = Body line spacing

NovelSettingsMaxWidthEntry-Header = Page width

NovelSettingsMaxWidthEntry-Description = Maximum page width
