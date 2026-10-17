using TriAxisBackupEditor.App.Mvvm;

namespace TriAxisBackupEditor.App.ViewModels.Simple;

/// <summary>Uma página do modo simples. O shell chama OnEnter/OnLeave ao navegar.</summary>
public abstract class SimplePage : ObservableObject
{
    protected SimplePage(SimpleShellViewModel shell)
    {
        Shell = shell;
    }

    public SimpleShellViewModel Shell { get; }

    /// <summary>Título curto exibido no topo da página.</summary>
    public abstract string Title { get; }

    /// <summary>Falso enquanto algo não pode ser interrompido (ex.: envio ao TriAxis).</summary>
    public virtual bool CanLeave => true;

    public virtual void OnEnter()
    {
    }

    public virtual void OnLeave()
    {
    }
}
