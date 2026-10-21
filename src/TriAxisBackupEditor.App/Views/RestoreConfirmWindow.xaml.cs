using System.Globalization;
using System.Windows;
using TriAxisBackupEditor.App.Services;
using TriAxisBackupEditor.Application.Restore;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.App.Views;

/// <summary>
/// Confirmação em duas etapas. O botão de envio só habilita com a etapa 1 marcada, a palavra RESTAURAR digitada
/// e, se houver avisos, o reconhecimento deles. O serviço de restauração revalida tudo isso.
/// </summary>
public partial class RestoreConfirmWindow : Window
{
    private readonly bool _hasWarnings;

    public RestoreConfirmWindow(RestoreDialogContext context)
    {
        InitializeComponent();
        TriAxisBackupEditor.App.Services.UiTranslator.Attach(this);

        FileText.Text = context.FileName;
        SizeText.Text = context.SizeBytes.ToString("N0", new CultureInfo("pt-BR")) + Tr(" bytes");
        HashText.Text = context.Sha256;
        OutputText.Text = context.OutputDevice;

        _hasWarnings = context.Warnings.Count > 0;
        if (_hasWarnings)
        {
            WarningsPanel.Visibility = Visibility.Visible;
            WarningsText.Text = string.Join(Environment.NewLine, context.Warnings.Select(w => "• " + w));
        }

        if (context.SafetyBackupPath is not null)
        {
            SafetyDoneText.Text = Tr("✔ Backup de segurança salvo: ") + context.SafetyBackupPath;
            SafetyDoneText.Visibility = Visibility.Visible;
            SafetyButton.Content = Tr("Fazer outro backup de segurança");
        }

        if (!context.CanMakeSafetyBackup)
        {
            SafetyButton.IsEnabled = false;
            SafetyText.Text = Tr("Para fazer o backup de segurança, selecione também o MIDI IN na aba Conexão.");
        }
    }

    public RestoreDialogResult Result { get; private set; } = new(RestoreDialogOutcome.Cancelled, false, string.Empty, false);

    private void OnInputChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        var step1 = Step1Check.IsChecked == true;
        Step2Panel.IsEnabled = step1;
        var phraseOk = string.Equals(PhraseBox.Text.Trim(), RestoreConfirmation.RequiredPhrase, StringComparison.Ordinal);
        var warningsOk = !_hasWarnings || WarningsCheck.IsChecked == true;
        ConfirmButton.IsEnabled = step1 && phraseOk && warningsOk;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Result = new RestoreDialogResult(
            RestoreDialogOutcome.Confirmed,
            Step1Check.IsChecked == true,
            PhraseBox.Text,
            WarningsCheck.IsChecked == true);
        DialogResult = true;
    }

    private void SafetyButton_Click(object sender, RoutedEventArgs e)
    {
        Result = new RestoreDialogResult(RestoreDialogOutcome.SafetyBackupRequested, false, string.Empty, false);
        DialogResult = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = new RestoreDialogResult(RestoreDialogOutcome.Cancelled, false, string.Empty, false);
        DialogResult = false;
    }
}
