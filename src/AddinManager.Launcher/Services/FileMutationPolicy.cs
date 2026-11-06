using System.Security.Principal;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Политика изменения файлов по области установки: машинная область меняется только приложением,
/// запущенным от имени администратора. Права на конкретную папку не опрашиваются — область и
/// повышение прав дают предсказуемый ответ, а попытка открыть каждый файл на запись при
/// сканировании была бы лишним обращением к диску.
/// </summary>
public sealed class FileMutationPolicy : IFileMutationPolicy
{
    private readonly bool _isElevated;

    /// <summary>Создаёт политику, определяющую повышение прав у текущего процесса.</summary>
    public FileMutationPolicy()
        : this(IsCurrentProcessElevated())
    {
    }

    /// <summary>Создаёт политику с явно заданным признаком прав администратора (точка для тестов).</summary>
    /// <param name="isElevated">Запущен ли процесс с правами администратора.</param>
    public FileMutationPolicy(bool isElevated) => _isElevated = isElevated;

    /// <inheritdoc />
    public bool IsReadOnly(AddinFile file) => file.Scope == AddinScope.Machine && !_isElevated;

    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
