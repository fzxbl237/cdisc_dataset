using System.Globalization;
using System.Resources;

namespace P21.Validator.Core;

public static class Text
{
    private const string BundleName = "validator-core-text";
    private const string DefaultLang = "en";
    private static bool _attempted;
    private static ResourceManager? _resourceManager;

    public static string Get(string key)
    {
        if (_attempted && _resourceManager == null)
        {
            return '!' + key + '!';
        }

        if (_resourceManager == null)
        {
            try
            {
                var culture = new CultureInfo(DefaultLang);
                _resourceManager = new ResourceManager(BundleName, typeof(Text).Assembly);
                return _resourceManager.GetString(key, culture) ?? '!' + key + '!';
            }
            catch (MissingManifestResourceException)
            {
                _attempted = true;
                return Get(key);
            }
        }

        try
        {
            return _resourceManager.GetString(key, CultureInfo.GetCultureInfo(DefaultLang)) ?? '!' + key + '!';
        }
        catch (MissingManifestResourceException)
        {
            return '!' + key + '!';
        }
    }
}
