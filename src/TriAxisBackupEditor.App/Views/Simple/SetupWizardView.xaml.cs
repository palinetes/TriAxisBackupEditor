using System.Windows;
using System.Windows.Controls;
using TriAxisBackupEditor.App.ViewModels.Simple;

namespace TriAxisBackupEditor.App.Views.Simple;

public partial class SetupWizardView : UserControl
{
    public SetupWizardView()
    {
        InitializeComponent();
    }

    private void ChannelDown_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SetupWizardViewModel vm)
        {
            vm.Channel--;
        }
    }

    private void ChannelUp_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SetupWizardViewModel vm)
        {
            vm.Channel++;
        }
    }
}
