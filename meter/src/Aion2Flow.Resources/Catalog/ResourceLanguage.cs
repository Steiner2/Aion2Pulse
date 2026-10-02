namespace Cloris.Aion2Flow.Resources.Catalog;

public static class ResourceLanguage
{
    public const string English = "en-US";

    public static bool IsSupported(string language) => string.Equals(language, English, StringComparison.Ordinal);
}
