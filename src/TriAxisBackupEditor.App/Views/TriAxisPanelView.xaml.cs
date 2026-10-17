using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TriAxisBackupEditor.App.ViewModels;

namespace TriAxisBackupEditor.App.Views;

/// <summary>Painel do TriAxis: roda do mouse e menu de valores nos displays (não cabem bem em bindings).</summary>
public partial class TriAxisPanelView : UserControl
{
    public TriAxisPanelView()
    {
        InitializeComponent();
    }

    private EditorViewModel? ViewModel => DataContext as EditorViewModel;

    private void KnobDisplay_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: KnobViewModel knob } && ViewModel?.HasDump == true)
        {
            knob.Step(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    /// <summary>Clique no display: menu com todos os valores válidos.</summary>
    private void KnobDisplay_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: KnobViewModel knob } element || ViewModel?.HasDump != true)
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = element,
            Placement = PlacementMode.Bottom,
            Background = (Brush)FindResource("Panel2Brush"),
            Foreground = (Brush)FindResource("TextBrush"),
            BorderBrush = (Brush)FindResource("AccentBrush")
        };

        for (var i = 0; i < knob.Options.Count; i++)
        {
            var index = i;
            var item = new MenuItem
            {
                Header = knob.Options[i],
                IsChecked = knob.Options[i] == knob.Display,
                FontFamily = (FontFamily)FindResource("MonoFont"),
                Foreground = (Brush)FindResource("TextBrush")
            };
            item.Click += (_, _) => knob.SetIndex(index);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void PresetDisplay_MouseWheel(object sender, MouseWheelEventArgs e) =>
        Execute(e.Delta > 0 ? ViewModel?.PresetNextCommand : ViewModel?.PresetPrevCommand, e);

    private void ProgramDisplay_MouseWheel(object sender, MouseWheelEventArgs e) =>
        Execute(e.Delta > 0 ? ViewModel?.ProgramNextCommand : ViewModel?.ProgramPrevCommand, e);

    private static void Execute(ICommand? command, MouseWheelEventArgs e)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }

        e.Handled = true;
    }
}
