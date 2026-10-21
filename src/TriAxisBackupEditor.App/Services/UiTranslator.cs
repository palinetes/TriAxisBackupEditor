using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using TriAxisBackupEditor.Localization;

namespace TriAxisBackupEditor.App.Services;

/// <summary>
/// Traduz os textos fixos das telas (XAML) para o inglês. O texto-fonte do XAML continua em português; só o que não vem
/// de binding é traduzido (os textos dinâmicos passam por Tr/TrF nos ViewModels). Em português não faz nada.
/// Cada janela chama <see cref="Attach"/> ao ser criada: a árvore é traduzida na hora e de novo
/// depois de cada mudança de layout (modelos de item, abas e páginas só ganham elementos mais tarde).
/// </summary>
public static class UiTranslator
{
    private static readonly HashSet<Window> Watched = new();

    /// <summary>Quantos elementos já passaram pelo tradutor (diagnóstico).</summary>
    public static int HandledCount { get; private set; }

    public static void Attach(Window window)
    {
        if (!Localizer.IsEnglish || !Watched.Add(window))
        {
            return;
        }

        TranslateTree(window);

        // Depois de cada passada de layout, uma única passada de tradução (agrupa várias de uma vez). Roda na mesma
        // prioridade do layout, então o texto em português não chega a ser desenhado. Só troca textos que mudam de fato,
        // então repetir a passada não provoca novo layout (sem laço).
        var scheduled = false;
        window.LayoutUpdated += (_, _) =>
        {
            if (scheduled)
            {
                return;
            }

            scheduled = true;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                scheduled = false;
                TranslateTree(window);
            }));
        };
        window.Closed += (_, _) => Watched.Remove(window);
    }

    /// <summary>Traduz a árvore visual a partir de <paramref name="root"/> (e os menus de contexto encontrados).</summary>
    public static void TranslateTree(DependencyObject root)
    {
        if (!Localizer.IsEnglish)
        {
            return;
        }

        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is FrameworkElement element)
            {
                HandledCount++;
                Translate(element);
                if (element.ContextMenu is { } menu)
                {
                    foreach (var item in menu.Items.OfType<DependencyObject>())
                    {
                        stack.Push(item);
                    }
                }
            }

            if (node is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                var count = VisualTreeHelper.GetChildrenCount(node);
                for (var i = 0; i < count; i++)
                {
                    stack.Push(VisualTreeHelper.GetChild(node, i));
                }
            }
        }
    }

    internal static void Translate(FrameworkElement element)
    {
        switch (element)
        {
            case TextBlock textBlock:
                TranslateTextBlock(textBlock);
                break;

            case Window window when !BindingOperations.IsDataBound(window, Window.TitleProperty):
                SetIfChanged(window.Title, Localizer.TranslateUiText(window.Title), v => window.Title = v);
                break;

            case DataGrid grid:
                foreach (var column in grid.Columns)
                {
                    if (column.Header is string header)
                    {
                        SetIfChanged(header, Localizer.TranslateUiText(header), v => column.Header = v);
                    }
                }

                break;

            case ListView listView when listView.View is GridView gridView:
                foreach (var column in gridView.Columns)
                {
                    if (column.Header is string header)
                    {
                        SetIfChanged(header, Localizer.TranslateUiText(header), v => column.Header = v);
                    }
                }

                break;
        }

        if (element is ContentControl contentControl
            && contentControl.Content is string content
            && !BindingOperations.IsDataBound(contentControl, ContentControl.ContentProperty))
        {
            SetIfChanged(content, Localizer.TranslateUiText(content), v => contentControl.Content = v);
        }

        if (element is HeaderedContentControl headered
            && headered.Header is string header1
            && !BindingOperations.IsDataBound(headered, HeaderedContentControl.HeaderProperty))
        {
            SetIfChanged(header1, Localizer.TranslateUiText(header1), v => headered.Header = v);
        }
        else if (element is HeaderedItemsControl headeredItems
                 && headeredItems.Header is string header2
                 && !BindingOperations.IsDataBound(headeredItems, HeaderedItemsControl.HeaderProperty))
        {
            SetIfChanged(header2, Localizer.TranslateUiText(header2), v => headeredItems.Header = v);
        }

        if (element.ToolTip is string tip && !BindingOperations.IsDataBound(element, FrameworkElement.ToolTipProperty))
        {
            SetIfChanged(tip, Localizer.TranslateUiText(tip), v => element.ToolTip = v);
        }
        else if (element.ToolTip is ToolTip tipControl && tipControl.Content is string tipContent)
        {
            SetIfChanged(tipContent, Localizer.TranslateUiText(tipContent), v => tipControl.Content = v);
        }
    }

    private static void SetIfChanged(string current, string translated, Action<string> assign)
    {
        if (!string.Equals(current, translated, StringComparison.Ordinal))
        {
            assign(translated);
        }
    }

    private static void TranslateTextBlock(TextBlock textBlock)
    {
        if (BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty))
        {
            return;
        }

        var inlines = textBlock.Inlines;
        if (inlines.Count <= 1 && inlines.FirstInline is null or Run)
        {
            // Texto simples. Se foi escrito direto no elemento (Text="..."), troca a propriedade Text. Um texto vindo de
            // Style/Trigger não é mexido aqui: um valor local passaria por cima do gatilho e o impediria de funcionar.
            if (DependencyPropertyHelper.GetValueSource(textBlock, TextBlock.TextProperty).BaseValueSource == BaseValueSource.Local)
            {
                SetIfChanged(textBlock.Text, Localizer.TranslateUiText(textBlock.Text), v => textBlock.Text = v);
            }
            else if (inlines.FirstInline is Run run && !BindingOperations.IsDataBound(run, Run.TextProperty))
            {
                SetIfChanged(run.Text, Localizer.TranslateUiText(run.Text), v => run.Text = v);
            }

            return;
        }

        foreach (var inline in inlines.ToList())
        {
            TranslateInline(inline);
        }
    }

    private static void TranslateInline(Inline inline)
    {
        switch (inline)
        {
            case Run run when !BindingOperations.IsDataBound(run, Run.TextProperty):
                SetIfChanged(run.Text, Localizer.TranslateUiText(run.Text), v => run.Text = v);
                break;

            case Span span:
                foreach (var child in span.Inlines.ToList())
                {
                    TranslateInline(child);
                }

                break;
        }
    }
}
