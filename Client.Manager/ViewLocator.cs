using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Common.Client.UI.ViewModels;

namespace Client.Manager;

/// <summary>
/// Связывает ViewModel с соответствующим View по конвенции наименования:
/// заменяет пространство имён «.ViewModels.» на «.Views.» и суффикс класса
/// «ViewModel» на «View». Поиск типа ведётся по всем загруженным сборкам.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        // Преобразуем полное имя типа ViewModel в имя типа View
        var name = param.GetType().FullName!
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
        if (name.EndsWith("ViewModel", StringComparison.Ordinal))
            name = string.Concat(name.AsSpan(0, name.Length - "ViewModel".Length), "View");

        // Type.GetType() ищет только в текущей сборке и системных сборках.
        // Сканируем все загруженные сборки, чтобы находить View из других проектов.
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name))
            .FirstOrDefault(t => t != null);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
