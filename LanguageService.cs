using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;

using Caliburn.Micro;

using WPFLocalizeExtension.Engine;

namespace Localization;

public sealed class LanguageService : INotifyPropertyChanged
{
    private static readonly Lazy<LanguageService> _lazy = new(() => new LanguageService()
                                                             , LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly object                                                                    _initLock       = new();
    private readonly List<(Type ResxType, Assembly ResourceAssembly, ResourceManager Manager)> _registeredResx = new();
    private          CultureInfo                                                               _currentCulture = CultureInfo.InvariantCulture;
    private readonly IEventAggregator                                                          _events;

    #region Public Properties and Events
        public static LanguageService Instance => _lazy.Value;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<CultureInfo> LanguageChanged;
        public string DefaultCultureName { get; set; } = "en";

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
        /// Registrer multiple .resx types for synkronisering
        /// </summary>
        public void Initialize(params Type[] resxTypes)
        {
            Initialize(false, resxTypes);
        }

        /// <summary>
        /// Registrer multiple .resx types med force-flag
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

                // Sæt initial kultur efter alle er registreret
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
        /// Skift kultur på ALLE registrerede .resx filer
        /// </summary>
        public CultureInfo SetCulture(string cultureName, bool setCurrentUICulture = true)
        {
            lock (_initLock)
            {
                if (_registeredResx.Count == 0)
                    throw new InvalidOperationException("LanguageService.Initialize must be called before SetCulture.");

                CultureInfo requested         = string.IsNullOrWhiteSpace(cultureName)
                                              ? CultureInfo.InvariantCulture
                                              : new CultureInfo(cultureName);

                var         availableCultures = GetAvailableCultures();

                var finalCulture = availableCultures.FirstOrDefault(c => c.Name.Equals(requested.Name, StringComparison.OrdinalIgnoreCase))
                                ?? availableCultures.FirstOrDefault(c => c.TwoLetterISOLanguageName.Equals(requested.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                                ?? CultureInfo.InvariantCulture;

                InvokeOnUi(() =>
                {
                    if (setCurrentUICulture)
                    {
                        Thread.CurrentThread.CurrentUICulture = finalCulture;
                        Thread.CurrentThread.CurrentCulture   = finalCulture;
                    }
                    LocalizeDictionary.Instance.Culture = finalCulture;
                    // Sæt kultur direkte på hver ResourceManager
                    foreach (var (resxType, resourceAssembly, manager) in _registeredResx)
                    {
                        // Brug reflection til at sætte resourceCulture på .resx klassen
                        var cultureField = resxType.GetField("resourceCulture", BindingFlags.NonPublic | BindingFlags.Static);
                        if (cultureField != null)
                            cultureField.SetValue(null, finalCulture);
                    }
                    CurrentCulture = finalCulture;
                });

                return finalCulture;
            }
        }

        public IReadOnlyList<CultureInfo> GetAvailableCultures()
        {
            var result = new List<CultureInfo>();

            foreach (var (resxType, resourceAssembly, manager) in _registeredResx)
            {
                foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
                    try
                    {
                        var rs = manager.GetResourceSet(culture, true, false);

                        if (rs != null && !result.Any(c => c.Name == culture.Name))
                            result.Add(culture);
                    }

                    catch { }
            }

            if (!string.IsNullOrEmpty(DefaultCultureName)
            && !result.Any(c => c.Name == DefaultCultureName))
            {
                result.Add(new CultureInfo(DefaultCultureName));
                result.RemoveAll(c => string.IsNullOrEmpty(c.Name));
            }

            return result;
        }

        public LanguageService(IEventAggregator events) : this() => _events = events;

        private LanguageService()                                => ChangeCultureCommand = new RelayCommand<string>(s => SetCulture(s));

        private void registerInternal(Assembly resourceAssembly, Type resxType, bool force)
        {
            var rmProperty = resxType.GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Static);

            if (rmProperty == null)
                throw new ArgumentException($"Type '{resxType.Name}' does not have a ResourceManager property.", nameof(resxType));

            var rm = (ResourceManager)rmProperty.GetValue(null);

            // Tjek om denne type allerede er registreret
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
