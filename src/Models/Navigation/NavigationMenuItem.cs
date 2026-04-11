using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CbsContractsDesktopClient.Models.Navigation
{
    public class NavigationMenuItem : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _glyph = string.Empty;
        private string _route = string.Empty;
        private string _sectionTitle = string.Empty;
        private bool _isSelected;
        private bool _isAction;
        private string _background = "Transparent";
        private string _foreground = "#FF3A3A3A";
        private string _iconForeground = "#FF616161";
        private int _fontWeightValue = 400;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public string Glyph
        {
            get => _glyph;
            set => SetField(ref _glyph, value);
        }

        public string Route
        {
            get => _route;
            set => SetField(ref _route, value);
        }

        public string SectionTitle
        {
            get => _sectionTitle;
            set => SetField(ref _sectionTitle, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public bool IsAction
        {
            get => _isAction;
            set => SetField(ref _isAction, value);
        }

        public string Background
        {
            get => _background;
            set => SetField(ref _background, value);
        }

        public string Foreground
        {
            get => _foreground;
            set => SetField(ref _foreground, value);
        }

        public string IconForeground
        {
            get => _iconForeground;
            set => SetField(ref _iconForeground, value);
        }

        public int FontWeightValue
        {
            get => _fontWeightValue;
            set => SetField(ref _fontWeightValue, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
