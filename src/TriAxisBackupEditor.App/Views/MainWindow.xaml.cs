using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TriAxisBackupEditor.App.ViewModels;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TriAxisBackupEditor.App.Services.UiTranslator.Attach(this);
        DataContextChanged += OnDataContextChanged;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (ViewModel?.State.BusyOperation == ControlViewModel.OperationName)
        {
            // O modo controle só mantém portas abertas: pode ser desligado com segurança ao fechar.
            ViewModel.Control.Deactivate();
        }

        if (ViewModel?.State.CanInterrupt == true)
        {
            // Backup ou teste: só leem/esperam o TriAxis. Cancelar é seguro; fecha sem perguntar.
            ViewModel.State.Interrupt();
        }
        else if (ViewModel?.State.IsBusy == true)
        {
            // Só o envio ao TriAxis chega aqui: fechar no meio deixaria o aparelho com dados parciais.
            MessageBox.Show(this,
                Tr("O programa está mandando dados para o TriAxis agora. Espere alguns segundos até terminar e feche de novo."),
                Tr("Envio em andamento"), MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }

        if (ViewModel?.Editor.ConfirmDiscard() == false)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel vm)
        {
            vm.Log.Entries.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[^1]);
        }
    }

    private void LibraryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel?.Library.SetSelection(LibraryGrid.SelectedItems.OfType<BackupRow>());
    }

    private void LibraryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var command = ViewModel?.Library.ViewCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}
