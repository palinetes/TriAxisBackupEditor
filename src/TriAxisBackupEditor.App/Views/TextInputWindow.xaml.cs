using System.Windows;

namespace TriAxisBackupEditor.App.Views;

public partial class TextInputWindow : Window
{
    public TextInputWindow(string title, string label, string initialValue)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        ValueBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public string Value => ValueBox.Text;

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
