using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TriAxisBackupEditor.App.ViewModels;

namespace TriAxisBackupEditor.App.Views;

/// <summary>Teclado como pedaleira: as teclas só agem com esta aba aberta e fora de campos de texto.</summary>
public partial class ControlView : UserControl
{
    public ControlView()
    {
        InitializeComponent();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBoxBase or ComboBox or ComboBoxItem)
        {
            return;
        }

        if (DataContext is ControlViewModel vm && vm.HandleKey(e.Key))
        {
            e.Handled = true;
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Dispatcher.BeginInvoke(() => Focus());
        }
    }
}
