using AddinManager.Launcher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AddinManager.Launcher.Views;

/// <summary>
/// Вид подпанели разметки: AvalonEdit над <see cref="MarkupViewModel.RawXml"/>. Вся механика —
/// синхронизация текста, подсветка блока/диагностики, строка поиска и хоткеи — в behaviors
/// (<c>AvalonEditMarkupBehavior</c>, <c>AvalonEditSearchBehavior</c>), поэтому code-behind
/// остаётся только композицией вида и модели.
/// </summary>
public partial class MarkupView
{
    /// <summary>
    /// Создает вид (только для окна разметки: без модели, редактор пуст). Рантайм всегда идёт
    /// через конструктор с моделью ниже — какой брать, контейнеру подсказывает
    /// <c>ActivatorUtilitiesConstructor</c>, наличие конструктора без параметров его не сбивает.
    /// </summary>
    public MarkupView() => InitializeComponent();

    /// <summary>Создает вид.</summary>
    /// <param name="viewModel">ViewModel подпанели.</param>
    [ActivatorUtilitiesConstructor]
    public MarkupView(MarkupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
