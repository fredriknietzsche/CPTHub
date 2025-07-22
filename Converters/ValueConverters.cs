using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CPTHub.Converters
{
    public class BoolToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isHealthy)
            {
                return isHealthy ? "CheckCircle" : "AlertCircle";
            }
            return "Help";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isHealthy)
            {
                return isHealthy ? Brushes.Green : Brushes.Orange;
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToAIStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isHealthy)
            {
                return isHealthy ? "AI Assistant Available" : "AI Assistant Offline";
            }
            return "AI Status Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProgressToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress)
            {
                return progress switch
                {
                    >= 90 => Brushes.Green,
                    >= 70 => Brushes.LimeGreen,
                    >= 50 => Brushes.Orange,
                    >= 25 => Brushes.OrangeRed,
                    _ => Brushes.Red
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DifficultyToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.DifficultyLevel difficulty)
            {
                return difficulty switch
                {
                    Models.DifficultyLevel.Easy => Brushes.Green,
                    Models.DifficultyLevel.Medium => Brushes.Orange,
                    Models.DifficultyLevel.Hard => Brushes.Red,
                    Models.DifficultyLevel.Expert => Brushes.Purple,
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SessionTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.SessionType sessionType)
            {
                return sessionType switch
                {
                    Models.SessionType.Reading => "BookOpen",
                    Models.SessionType.Quiz => "QuizOutline",
                    Models.SessionType.Flashcards => "Cards",
                    Models.SessionType.Review => "Refresh",
                    Models.SessionType.AIChat => "Robot",
                    Models.SessionType.Practice => "Dumbbell",
                    _ => "Help"
                };
            }
            return "Help";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan.TotalDays >= 1)
                {
                    return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
                }
                else if (timeSpan.TotalHours >= 1)
                {
                    return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
                }
                else
                {
                    return $"{timeSpan.Minutes}m";
                }
            }
            return "0m";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PercentageToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                return $"{percentage:F1}%";
            }
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && str.EndsWith("%"))
            {
                var numberPart = str.Substring(0, str.Length - 1);
                if (double.TryParse(numberPart, out double result))
                {
                    return result;
                }
            }
            return 0.0;
        }
    }
}
