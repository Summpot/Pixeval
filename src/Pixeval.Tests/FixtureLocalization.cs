namespace Pixeval.Tests;

internal static class FixtureLocalization
{
    internal static string GetResource(object key) => LocalizationManager.Instance[key.ToString()!];
}
