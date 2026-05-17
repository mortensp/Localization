using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace String.Localization
{
    /// <summary>
    /// LocalizedString holder en reference til Lex-egenskaben og notificerer ændringer
    /// når kulturen skifter
    /// </summary>
    public class LexString : INotifyPropertyChanged
    {
        static int cnt=1    ;

        private  Func<string> _valueGetter;
        private string _cachedValue{ get; set; }
        private string _propertyName; // Tracke hvilket property dette hører til
        private int Id=cnt++;
        public LexString([CallerMemberName] string propertyName = null)
        {
            _propertyName = propertyName;
        }

   

        public string PropertyName => _propertyName;
        public Func<string> ValueGetter=>_valueGetter;

        public string Value
        {
            get => _cachedValue;
            private set
            {
                if (_cachedValue != value)
                {
                    _cachedValue = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        /// <summary>Opdater valueGetter og refresh værdien</summary>
        internal void UpdateGetter(Func<string> newValueGetter)
        {
            if (newValueGetter == null)
                throw new ArgumentNullException(nameof(newValueGetter));

            _valueGetter = newValueGetter;
            Value        = _valueGetter();
        }

        /// <summary>Opdater værdien når kulturen ændres</summary>
        public void Refresh()
        {
            Value = _valueGetter();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString() => Value;
    }
}
