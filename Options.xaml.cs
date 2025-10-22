using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NINA.Plugin.Canon.EDSDK
{
    [Export(typeof(ResourceDictionary))]
    partial class Options : ResourceDictionary
    {
        public Options()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Converter to show/hide UI elements based on integer value
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public int TrueValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue == TrueValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
