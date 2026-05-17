using System.Globalization;

namespace String.Localization;

// Simple message for EventAggregator
public class LanguageChangedMessage
{
    public CultureInfo NewCulture { get; }
    public LanguageChangedMessage(CultureInfo newCulture) => NewCulture = newCulture;
}
