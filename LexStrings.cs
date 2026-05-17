using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;


using System;
using System.Collections.Generic;

namespace String.Localization;

public class LexStrings : ObservableCollection<LexString>
{
    private INotifyPropertyChanged _parentViewModel;

    /// <summary>
    /// Initialiser LexStrings med en reference til parent-viewmodel
    /// </summary>
    public LexStrings(INotifyPropertyChanged parentViewModel )
    {
        _parentViewModel = parentViewModel;
    }

    /// <summary>
    /// Registrer eller opdater en LexString baseret på property-navn (hentes automatisk).
    /// </summary>
    public void Set(LexString ls, Func<string> valueGetter)
    {
        if (ls == null)
            throw new ArgumentNullException(nameof(ls));

        if (string.IsNullOrWhiteSpace(ls.PropertyName))
            throw new ArgumentNullException(nameof(ls.PropertyName));

        // Find eksisterende entry med samme navn
        var existing = this.FirstOrDefault(e => e.PropertyName == ls.PropertyName);

        if (existing == null)
        {
            ls.UpdateGetter(valueGetter);
            this.Add(ls);
        }
        else
            existing.UpdateGetter(valueGetter);

        NotifyParent(ls.PropertyName);
    }

    /// <summary>Opdater alle LexString-objekter i samlingen og notificér parent</summary>
    public void RefreshAll()
    {
        foreach (var lexString in this)
        {
            lexString.Refresh();
            NotifyParent(lexString.PropertyName);
        }
    }

    /// <summary>Notificér parent-viewmodel om property-ændring</summary>
    private void NotifyParent(string propertyName)
    {
        if (_parentViewModel is Caliburn.Micro.Screen screen)
            screen.NotifyOfPropertyChange(propertyName);
    }
}
