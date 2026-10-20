using System.Globalization;
using AddinManager.Core.Search;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Состояние поиска по тексту разметки: запрос, параметры сопоставления, найденные срезы и текущая
/// позиция в списке совпадений.
/// </summary>
/// <remarks>
/// Состояние — чистая презентационная модель: редактор оно не трогает. Строка поиска видна всегда,
/// пока открыт файл; <see cref="MarkupViewModel"/> передаёт сюда полный текст через
/// <see cref="SetDocumentText"/>, а вид реагирует на <see cref="CurrentMatch"/> и выделяет срез.
/// Каждое изменение запроса, параметра или документа пересчитывает совпадения синхронно через
/// <see cref="TextSearchEngine"/> и удерживает позицию внутри нового результата.
/// </remarks>
public sealed partial class MarkupSearchViewModel : ObservableObject
{
    private readonly IStringLocalizer<MarkupSearchViewModel> _localizer;
    private readonly ILogger<MarkupSearchViewModel> _logger;
    private IReadOnlyList<TextSearchMatch> _matches = [];
    private TextSearchError _error;
    private int _currentIndex = -1;
    private string? _documentText;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _wholeWords;

    [ObservableProperty]
    private bool _useRegex;

    /// <summary>Создаёт состояние поиска по разметке.</summary>
    /// <param name="localizer">Строки строки поиска и её статуса.</param>
    /// <param name="logger">Логгер поиска — неверный паттерн пишется на уровне Debug.</param>
    public MarkupSearchViewModel(
        IStringLocalizer<MarkupSearchViewModel> localizer,
        ILogger<MarkupSearchViewModel> logger)
    {
        _localizer = localizer;
        _logger = logger;

        NextCommand = new RelayCommand(Next);
        PreviousCommand = new RelayCommand(Previous);
    }

    /// <summary>Команда перехода к следующему совпадению.</summary>
    public IRelayCommand NextCommand { get; }

    /// <summary>Команда перехода к предыдущему совпадению.</summary>
    public IRelayCommand PreviousCommand { get; }

    /// <summary>Совпадения текущего запроса в текущем документе, в порядке текста.</summary>
    public IReadOnlyList<TextSearchMatch> Matches => _matches;

    /// <summary>Совпадение, которое редактор должен выделить, или <see langword="null"/>, если его нет.</summary>
    public TextSearchMatch? CurrentMatch =>
        _currentIndex >= 0 && _currentIndex < _matches.Count ? _matches[_currentIndex] : null;

    /// <summary>Нулевая позиция <see cref="CurrentMatch"/> в <see cref="Matches"/>; <c>-1</c>, если совпадений нет.</summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>Есть ли у текущего запроса хотя бы одно совпадение.</summary>
    public bool HasMatches => _matches.Count > 0;

    /// <summary>Запрос пустой или из одних пробелов — тогда показывается подсказка в поле.</summary>
    public bool IsQueryEmpty => string.IsNullOrWhiteSpace(Query);

    /// <summary>Есть ли что показать в <see cref="StatusText"/>.</summary>
    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    /// <summary>Локализованная строка статуса: счётчик совпадений, их отсутствие или ошибка.</summary>
    /// <remarks>
    /// Пустой запрос ничего не показывает. Иначе неверный паттерн и таймаут важнее сообщения об
    /// отсутствии совпадений: пустой результат вызван ошибкой, а не документом.
    /// </remarks>
    public string StatusText
    {
        get
        {
            if (IsQueryEmpty)
                return string.Empty;

            return _error switch
            {
                TextSearchError.InvalidPattern => _localizer["MarkupSearchInvalidPattern"].Value,
                TextSearchError.Timeout => _localizer["MarkupSearchTimeout"].Value,
                _ when _matches.Count == 0 => _localizer["MarkupSearchNoMatches"].Value,
                _ => _localizer[
                    "MarkupSearchMatchStatus",
                    (_currentIndex + 1).ToString(CultureInfo.InvariantCulture),
                    _matches.Count.ToString(CultureInfo.InvariantCulture)].Value,
            };
        }
    }

    /// <summary>Подсказка в пустом поле поиска.</summary>
    public string Placeholder => _localizer["MarkupSearchPlaceholder"].Value;

    /// <summary>Тултип кнопки следующего совпадения.</summary>
    public string NextTooltip => _localizer["MarkupSearchNextTooltip"].Value;

    /// <summary>Тултип кнопки предыдущего совпадения.</summary>
    public string PreviousTooltip => _localizer["MarkupSearchPreviousTooltip"].Value;

    /// <summary>Тултип опции "учитывать регистр".</summary>
    public string MatchCaseTooltip => _localizer["MarkupSearchMatchCaseTooltip"].Value;

    /// <summary>Тултип опции "только целые слова".</summary>
    public string WholeWordsTooltip => _localizer["MarkupSearchWholeWordsTooltip"].Value;

    /// <summary>Тултип опции "регулярное выражение".</summary>
    public string UseRegexTooltip => _localizer["MarkupSearchUseRegexTooltip"].Value;

    /// <summary>Сбрасывает запрос и его результаты.</summary>
    /// <remarks>Запрос сбрасывается обычным путём изменения, поэтому совпадения, статус и текущий срез чистятся вместе с ним.</remarks>
    public void Clear() => Query = string.Empty;

    /// <summary>Заменяет искомый текст документа и пересчитывает совпадения.</summary>
    /// <param name="text">Полный текст документа или <see langword="null"/>, если документ не загружен.</param>
    /// <remarks>Текущая позиция сохраняется, пока существует в новом результате, иначе зажимается в границы.</remarks>
    public void SetDocumentText(string? text)
    {
        _documentText = text;
        Recalculate();
    }

    /// <summary>Переходит к следующему совпадению с переходом с последнего на первое.</summary>
    public void Next()
    {
        if (_matches.Count == 0)
            return;
        Select((_currentIndex + 1) % _matches.Count);
    }

    /// <summary>Переходит к предыдущему совпадению с переходом с первого на последнее.</summary>
    public void Previous()
    {
        if (_matches.Count == 0)
            return;
        Select(_currentIndex <= 0 ? _matches.Count - 1 : _currentIndex - 1);
    }

    /// <summary>Поднимает уведомления для всех локализованных строк после смены языка интерфейса.</summary>
    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(NextTooltip));
        OnPropertyChanged(nameof(PreviousTooltip));
        OnPropertyChanged(nameof(MatchCaseTooltip));
        OnPropertyChanged(nameof(WholeWordsTooltip));
        OnPropertyChanged(nameof(UseRegexTooltip));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
    }

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(IsQueryEmpty));
        Recalculate();
    }

    partial void OnMatchCaseChanged(bool value) => Recalculate();

    partial void OnWholeWordsChanged(bool value) => Recalculate();

    partial void OnUseRegexChanged(bool value) => Recalculate();

    private void Recalculate()
    {
        var previousIndex = _currentIndex;
        var result = TextSearchEngine.Find(
            _documentText,
            Query,
            new TextSearchOptions(MatchCase, WholeWords, UseRegex));

        _matches = result.Matches;
        _error = result.Error;
        if (_error == TextSearchError.InvalidPattern)
        {
            _logger.LogDebug("Неверный паттерн поиска по разметке {Query}: {Error}", Query, _error);
        }

        _currentIndex = _matches.Count == 0
            ? -1
            : Math.Clamp(previousIndex, 0, _matches.Count - 1);

        NotifyResultChanged();
    }

    private void Select(int index)
    {
        if (index == _currentIndex)
            return;
        _currentIndex = index;
        NotifyResultChanged();
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(Matches));
        OnPropertyChanged(nameof(CurrentMatch));
        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"MarkupSearch(Query={Query}, Matches={_matches.Count}, Index={_currentIndex}, Error={_error})";
}
