using System.Globalization;

namespace Localization;

// Simple message for EventAggregator
public class LanguageChangedMessage
{
    public CultureInfo NewCulture { get; }
    public LanguageChangedMessage(CultureInfo newCulture) => NewCulture = newCulture;
}
