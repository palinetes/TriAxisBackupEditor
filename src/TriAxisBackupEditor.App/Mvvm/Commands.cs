using System.Windows.Input;

namespace TriAxisBackupEditor.App.Mvvm;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        try
        {
            _execute();
        }
        catch (Exception ex)
        {
            AsyncRelayCommand.ReportError(ex);
        }
    }
}

/// <summary>Comando com parâmetro (ex.: o pedal clicado num menu de contexto).</summary>
public sealed class RelayCommandWithParameter<T> : ICommand
{
    private readonly Action<T?> _execute;

    public RelayCommandWithParameter(Action<T?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        try
        {
            _execute(parameter is T value ? value : default);
        }
        catch (Exception ex)
        {
            AsyncRelayCommand.ReportError(ex);
        }
    }
}

/// <summary>Comando assíncrono: fica desabilitado enquanto executa e nunca deixa exceção escapar sem aviso.</summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>Configurado em App.xaml.cs para registrar no log e mostrar mensagem ao usuário.</summary>
    public static Action<Exception>? UnhandledError { get; set; }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    internal static void ReportError(Exception ex)
    {
        if (UnhandledError is { } handler)
        {
            handler(ex);
        }
        else
        {
            System.Windows.MessageBox.Show(ex.Message, "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
