using System.Collections.Generic;
using System.Globalization;
using Pixeval.Localization;
namespace Pixeval.Common;
public static class LanguageHelper
{
    public static IReadOnlyList<CultureInfo> AvailableLanguages => AppLocalization.Instance.AvailableLanguages;
    public static readonly CultureInfo DefaultLanguage = new("zh-Hans");
}
