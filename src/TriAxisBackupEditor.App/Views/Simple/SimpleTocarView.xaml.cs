using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TriAxisBackupEditor.App.ViewModels.Simple;

namespace TriAxisBackupEditor.App.Views.Simple;

/// <summary>Teclado como pedaleira enquanto a página "Tocar" está aberta.</summary>
public partial class SimpleTocarView : UserControl
{
    public SimpleTocarView()
    {
        InitializeComponent();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is SimpleTocarViewModel vm && vm.Control.HandleKey(e.Key))
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
