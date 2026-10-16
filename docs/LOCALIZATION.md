# Localization

Pixeval uses the pinned ZeroI18n 1.1.0 source generator. Text is compiled into
`Pixeval.Localization.Strings`; no language files or language plugins are loaded
from disk. The default locale is `zh-Hans`.

## Messages

Keep each feature in `src/Pixeval/messages/<Feature>/<locale>.ftl`. Use the original
property-path segments joined by `-`, preserving case:

```fluent
# messages/MainPage/en-US.ftl
Tab-Settings = Settings
SearchResultFormatted = { $searchText } - Search
```

Call `Strings.MainPageSearchResultFormatted(searchText: query)` in C#. Use generated
method names with `nameof` for settings and enum metadata. Dates and numbers that
need a specific display format are formatted using
`AppLocalization.Instance.CurrentCulture` before passing them as message arguments.
Do not reintroduce positional placeholders or `string.Format` for messages.

XAML uses compiled indexer bindings:

```xml
xmlns:loc="using:Pixeval.Localization"
Text="{CompiledBinding [MainPage-Tab-Settings], Source={x:Static loc:LocalizationManager.Instance}}"
```

Use Fluent string expressions for significant whitespace, newlines and literal
braces. Avoid adding selectors or plural rules without checking generator support
for every affected locale. Existing translation coverage is intentionally preserved;
missing translations fall back to Chinese.

`ZERO005` reports unused messages in Release builds. The generated `MessageKeys`
table intentionally retains parameterless messages addressable by user navigation
YAML. Do not suppress the analyzer globally or add wildcard allow lists.

## Language changes

Change languages only through `AppLocalization.Instance.SetLanguage(cultureName)`;
an empty string follows the system culture captured at startup. Language changes
run on the UI thread. `ViewModelBase` invalidates computed properties automatically;
stateful text must retain its state/arguments rather than an already translated
string. `Subscribe` returns an owner-retained, disposable token and uses weak
references so the singleton cannot keep closed pages alive.

Refresh presentation without reloading data, replacing pages, resetting selection,
or changing user-entered text. Existing dialogs, transient notifications, and native
extension instances retain their original text. Newly created content uses the
current language.

Settings entries use the framework-independent localization refresh API in the
`Summpot/AutoSettingsPage` fork. All changes to that submodule must be pushed to
`https://github.com/Summpot/AutoSettingsPage.git`, never to the Pixeval upstream.

## Help and compatibility

Markdown is maintained in `src/Pixeval/Assets/Help/<locale>/*.md` and embedded as
Avalonia resources. `LocalizedHelp` caches by locale and document and falls back
to Chinese. It raises binding notifications on language changes.

Persisted navigation YAML continues to accept `$Feature.Property.Path` and `$$`
escaping. `MessageKeys` contains explicit aliases and validates existence without
comparing translated strings against resource keys. Do not rewrite user YAML when
switching languages.

The one-shot migration script is `scripts/i18n/migrate.py`; it operates on the old
resource tree and is not a build dependency. Its checked-in `mapping.json` records
the conversion. `LocalizationMigrationTest` checks the 2439 original messages,
formatting, fallback, navigation compatibility and live controls. When deliberately
changing messages, update the corresponding regression expectations.
