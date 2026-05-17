using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using Caliburn.Micro;

using WPFLocalizeExtension.Engine;

namespace String.Localization;

public sealed class LanguageService : INotifyPropertyChanged
{
    #region Private Fields & Properties
        private static readonly Lazy<LanguageService> _lazy = 
                                                      new(() => new LanguageService()
                                                         , LazyThreadSafetyMode.ExecutionAndPublication);

        //
        private readonly List<(Type ResxType, Assembly ResourceAssembly, ResourceManager Manager)> _registeredResx = new();
        //
        private          CultureInfo      _currentCulture = CultureInfo.InvariantCulture;
        private readonly IEventAggregator _events;
        private readonly object           _initLock       = new();
    #endregion

    #region Public Properties and Events
        public static LanguageService Instance => _lazy.Value;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<CultureInfo> LanguageChanged;
        public string DefaultCultureName { get; set; } = "en-Us";

        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            private set
            {
                if (_currentCulture.Equals(value))
                    return;

                _currentCulture = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
                LanguageChanged?.Invoke(this, value);
                _events?.PublishOnUIThreadAsync(new LanguageChangedMessage(value));
            }
        }

        public RelayCommand<string> ChangeCultureCommand { get; }
    #endregion

    #region Public Methods
        /// <summary>
        /// Register multiple .resx types for syncronization
        /// </summary>
        public void Initialize(params Type[] resxTypes) => Initialize(false, resxTypes);

        /// <summary>
        /// Register multiple .resx types with force-flag
        /// </summary>
        public void Initialize(bool force, params Type[] resxTypes)
        {
            if (resxTypes == null)
                throw new ArgumentNullException(nameof(resxTypes));

            if (resxTypes.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(resxTypes));

            lock (_initLock)
            {
                foreach (var resxType in resxTypes)
                {
                    var resourceAssembly = resxType.Assembly;
                    registerInternal(resourceAssembly, resxType, force);
                }

                // Initialize Culture after all has been registered
                if (_registeredResx.Count >  0)
                {
                    var initial  = CultureInfo.CurrentUICulture;
                    var selected = SetCulture(initial.Name,false);

                    InvokeOnUi(() =>
                    {
                        LocalizeDictionary.Instance.Culture = selected;
                        CurrentCulture                      = selected;
                    });
                }
            }
        }

        /// <summary>
        /// Change culture on all registrated .resx files
        /// </summary>
        public CultureInfo SetCulture(string cultureName, bool setCurrentUICulture = true)
        {
            lock (_initLock)
            {
                if (_registeredResx.Count == 0)
                    throw new InvalidOperationException("LanguageService.Initialize must be called before SetCulture.");

                CultureInfo requested = string.IsNullOrWhiteSpace(cultureName)
                                      ? new CultureInfo(DefaultCultureName)
                                      : new CultureInfo(cultureName);

                var availableCultures = GetAvailableCultures();

                var finalCulture = availableCultures.FirstOrDefault(c => c.Name.Equals(requested.Name, StringComparison.OrdinalIgnoreCase))
                                ?? availableCultures.FirstOrDefault(c => c.TwoLetterISOLanguageName.Equals(requested.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                                ?? requested;

                InvokeOnUi(() =>
                {
                    if (setCurrentUICulture)
                    {
                        Thread.CurrentThread.CurrentUICulture = finalCulture;
                        Thread.CurrentThread.CurrentCulture   = finalCulture;
                    }
                    LocalizeDictionary.Instance.Culture = finalCulture;
                    // Set Culture directly for each ResourceManager
                    foreach (var (resxType, resourceAssembly, manager) in _registeredResx)
                    {
                        // Use reflection to set resourceCulture on the .resx class
                        var cultureField = resxType.GetField("resourceCulture", BindingFlags.NonPublic | BindingFlags.Static);
                        if (cultureField != null)
                            cultureField.SetValue(null, finalCulture);
                    }
                    CurrentCulture = finalCulture;
                });

                return finalCulture;
            }
        }

        public IReadOnlyList<CultureInfo> GetAvailableCultures(string defaultCultureName = null)
        {
            var cultures = new List<CultureInfo>();

            foreach (var (resxType, resourceAssembly, manager) in _registeredResx)
            {
                foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
                    try
                    {
                        var rs = manager.GetResourceSet(culture, true, false);

                        if (rs != null && !cultures.Any(c => c.Name == culture.Name))
                            cultures.Add(culture);
                    }
                    catch { }
            }

            if (!string.IsNullOrEmpty(defaultCultureName))
                if (cultures.Any(c => string.IsNullOrEmpty(c.Name))
                && !cultures.Any(c => c.Name == defaultCultureName))
                {
                    cultures.Add(new CultureInfo(defaultCultureName));
                    cultures.RemoveAll(c => string.IsNullOrEmpty(c.Name));
                }

            return cultures;
        }

        public LanguageService(IEventAggregator events) : this() => _events = events;

        #region Public Static Methods
            /// <summary>
            /// Gets all translations for a given resource key across available cultures.
            /// </summary>
            /// <param name="type"></param>
            /// <param name="key"></param>
            /// <returns></returns>
            /// <exception cref="ArgumentNullException"></exception>
            /// <exception cref="InvalidOperationException"></exception>
            /// <usages>
            ///   <example>
            ///     var translations = LanguageService.GetTranslationsFor(typeof(Lex), nameof(Lex.Red));
            ///   </example>
            /// </usages>
            public static IDictionary<CultureInfo, string> GetTranslationsFor(Type type, string key)
            {
                if (type == null)
                    throw new ArgumentNullException(nameof(type));

                if (string.IsNullOrEmpty(key))
                    throw new ArgumentNullException(nameof(key));

                var rmProp = type.GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Static);

                if (rmProp == null)
                    throw new InvalidOperationException("ResourceManager property not found on resource type.");

                var rm = (ResourceManager)rmProp.GetValue(null);

                var cultures = LanguageService.Instance?.GetAvailableCultures()
                            ?? CultureInfo.GetCultures(CultureTypes.SpecificCultures);

                var result = new Dictionary<CultureInfo, string>();

                foreach (var culture in cultures)
                    try
                    {
                        var value = rm.GetString(key, culture);

                        if (!string.IsNullOrEmpty(value))
                            result[culture] = value;
                    }
                    catch { /* ignore cultures that fail */ }

                return result;
            }

            public static string Translate(Type type, string key)
            {
                if (string.IsNullOrEmpty(key))
                    return string.Empty;

                var rmProp = type.GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Static);

                if (rmProp != null)
                    try
                    {
                        var rm   = (ResourceManager)rmProp.GetValue(null);
                        var text = rm?.GetString(key, Instance.CurrentCulture);

                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                    catch { }

                return "Key: " + key;
            }

            /// <summary>
            /// Gets all translations for a given resource expression across available cultures.
            /// </summary>
            /// <param name="resourceExpression"></param>
            /// <returns></returns>
            /// <exception cref="ArgumentNullException"></exception>
            /// <exception cref="ArgumentException"></exception>
            /// <usages>
            ///   <example>
            ///     var translations = LanguageService.GetTranslationsFor(() => Lex.Red);
            ///   </example>
            /// </usages>
            public static IDictionary<CultureInfo, string> GetTranslationsFor(Expression<Func<string>> resourceExpression)
            {
                if (resourceExpression == null)
                    throw new ArgumentNullException(nameof(resourceExpression));

                if (resourceExpression.Body is not MemberExpression member)
                    throw new ArgumentException("Expression must be a member access (e.g. () => Lex.Red).", nameof(resourceExpression));

                var memberName    = member.Member.Name;
                var declaringType = member.Member.DeclaringType ?? throw new InvalidOperationException("Declaring type not found for member.");

                return GetTranslationsFor(declaringType, memberName);
            }

            /// <summary>
            ///  Finds the resource key corresponding to a given translated value and retrieves all translations for that key across available cultures.
            ///  Note: value-to-key mapping can be ambiguous — prefer the expression overload.
            /// </summary>
            /// <param name="resourceType"></param>
            /// <param name="translatedValue"></param>
            /// <returns></returns>
            /// <exception cref="ArgumentNullException"></exception>
            /// <exception cref="InvalidOperationException"></exception>
            public static IDictionary<CultureInfo, string> GetTranslationsForValue(Type resourceType, string translatedValue)
            {
                if (resourceType == null)
                    throw new ArgumentNullException(nameof(resourceType));

                if (translatedValue == null)
                    throw new ArgumentNullException(nameof(translatedValue));

                var rmProp = resourceType.GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Static);

                if (rmProp == null)
                    throw new InvalidOperationException("ResourceManager property not found on resource type.");

                var rm = (ResourceManager)rmProp.GetValue(null);

                var cultures = LanguageService.Instance?.GetAvailableCultures()
                            ?? CultureInfo.GetCultures(CultureTypes.SpecificCultures);

                string foundKey = null;

                foreach (var culture in cultures)
                {
                    try
                    {
                        var rs = rm.GetResourceSet(culture, true, false);

                        if (rs == null)
                            continue;

                        foreach (DictionaryEntry entry in rs)
                        {
                            if (entry.Value is string s && s == translatedValue)
                            {
                                foundKey = entry.Key as string;
                                break;
                            }
                        }
                    }
                    catch { }

                    if (foundKey != null)
                        break;
                }

                if (foundKey == null)
                    throw new InvalidOperationException("Could not locate resource key for the provided translated value.");

                return GetTranslationsFor(resourceType, foundKey);
            }
        #endregion
    #endregion

    #region Private Methods
        private LanguageService() => ChangeCultureCommand = new RelayCommand<string>(s => SetCulture(s));

        private void registerInternal(Assembly resourceAssembly, Type resxType, bool force)
        {
            var rmProperty = resxType.GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Static);

            if (rmProperty == null)
                throw new ArgumentException($"Type '{resxType.Name}' does not have a ResourceManager property.", nameof(resxType));

            var rm = (ResourceManager)rmProperty.GetValue(null);

            // Check for duplicate registrations
            if (_registeredResx.Any(x => x.ResxType == resxType))
            {
                if (!force)
                    return;

                _registeredResx.RemoveAll(x => x.ResxType == resxType);
            }

            _registeredResx.Add((resxType, resourceAssembly, rm));
        }

        private static void InvokeOnUi(System.Action action)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(action);
            else
                action();
        }
    #endregion
}
