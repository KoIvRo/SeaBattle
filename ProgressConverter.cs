using System.Globalization;

namespace SeaBattle
{
    public class ProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string progressText)
            {
                var match = System.Text.RegularExpressions.Regex.Match(progressText, @"(\d+)/(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int current) &&
                    int.TryParse(match.Groups[2].Value, out int total))
                {
                    return (double)current / total;
                }
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}