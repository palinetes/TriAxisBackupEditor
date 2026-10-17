using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TriAxisBackupEditor.App;

/// <summary>true → Collapsed; false → Visible.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility v && v != Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>null/vazio → Collapsed.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s)) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Liga um RadioButton a um valor de enum: ConverterParameter = nome do membro.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null && string.Equals(value.ToString(), parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true || parameter is not string name)
        {
            return Binding.DoNothing;
        }

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Enum.Parse(enumType, name);
    }
}

/// <summary>[número do passo atual, Tag do elemento] → true quando são iguais.</summary>
public sealed class StepEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is int step && int.TryParse(values[1]?.ToString(), out var tag) && step == tag;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>[valor de enum, Tag do elemento] → true quando o nome do enum é igual ao Tag.</summary>
public sealed class StepNameEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is not null && string.Equals(values[0].ToString(), values[1]?.ToString(), StringComparison.Ordinal);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>[número do passo atual, Tag do elemento] → true quando o passo já foi alcançado.</summary>
public sealed class StepReachedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is int step && int.TryParse(values[1]?.ToString(), out var tag) && step >= tag;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formata bytes: 2746 → "2.746 bytes".</summary>
public sealed class ByteSizeConverter : IValueConverter
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        long l => l.ToString("N0", PtBr) + " bytes",
        int i => i.ToString("N0", PtBr) + " bytes",
        _ => string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
