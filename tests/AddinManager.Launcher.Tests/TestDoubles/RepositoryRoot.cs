using System.IO;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Находит корень репозитория по <c>Revit.AddinManager.slnx</c>, чтобы тест мог прочитать
/// документацию/XAML, которые не копируются в test output. Общий хелпер на несколько
/// проверок структуры (design-данные, словарь иконок).
/// </summary>
internal static class RepositoryRoot
{
    internal static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Revit.AddinManager.slnx")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Корень репозитория (Revit.AddinManager.slnx) не найден выше каталога тестов.");
        return directory.FullName;
    }
}
