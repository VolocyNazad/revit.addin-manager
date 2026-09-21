using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Проверяет, что неудачный <see cref="AddinManager.Core.Abstractions.Storage.IAddinStore.SetEnabled"/> (например,
/// <see cref="UnauthorizedAccessException"/> для файла в области Machine — реальный случай,
/// когда приложение запущено без прав администратора) не оставляет чекбокс строки в состоянии,
/// которого на самом деле нет на диске, и что пользователь видит понятную причину, а не падает
/// необработанное исключение (см. <c>AddinFileRowViewModel.OnIsEnabledChanged</c>: метод приватный, cref из другой сборки неразрешим).
/// </summary>
public sealed class AddinFileRowViewModelTests
{
    private static readonly IAddinManifestParser Parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void IsEnabled_SetEnabledThrowsUnauthorizedAccess_RevertsCheckboxAndSetsErrorMessage()
    {
        TestCulture.RunIn("ru", () =>
        {
            var store = new FakeAddinStore();
            var file = NewFile("Autodesk.eTransmitApplication.addin", AddinScope.Machine, "2023", enabled: true);
            var row = NewRow(store, file);
            store.ThrowOnNextSetEnabled = new IOException(
                "Не удалось перенести файл в disabled",
                new UnauthorizedAccessException("Access to the path is denied."));

            row.IsEnabled = false;

            Assert.True(row.IsEnabled, "После неудачи чекбокс должен откатиться к реальному состоянию файла на диске.");
            Assert.NotNull(row.ErrorMessage);
            Assert.Contains(file.FileName, row.ErrorMessage);
            Assert.Contains("администратора", row.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, store.SetEnabledCallCount); // откат не должен снова дёргать SetEnabled
        });
    }

    [Fact]
    public void IsEnabled_SetEnabledThrowsGenericIOException_RevertsAndSurfacesOriginalMessage()
    {
        var store = new FakeAddinStore();
        var file = NewFile("Locked.addin", AddinScope.User, "2024", enabled: false);
        var row = NewRow(store, file);
        store.ThrowOnNextSetEnabled = new IOException("The process cannot access the file because it is in use.");

        row.IsEnabled = true;

        Assert.False(row.IsEnabled, "Откат должен вернуть чекбокс в исходное (выключенное) состояние.");
        Assert.Equal("The process cannot access the file because it is in use.", row.ErrorMessage);
        Assert.Equal(1, store.SetEnabledCallCount);
    }

    [Fact]
    public void IsEnabled_SetEnabledSucceeds_ClearsPriorErrorMessage()
    {
        var store = new FakeAddinStore();
        var file = NewFile("Normal.addin", AddinScope.User, "2024", enabled: true);
        var row = NewRow(store, file);
        store.ThrowOnNextSetEnabled = new IOException("boom");
        row.IsEnabled = false; // проваливается, ErrorMessage выставлен, чекбокс откатился на true
        Assert.NotNull(row.ErrorMessage);

        row.IsEnabled = false; // на этот раз store.ThrowOnNextSetEnabled уже сброшен в null — проходит успешно

        Assert.False(row.IsEnabled);
        Assert.Null(row.ErrorMessage);
        Assert.Equal(2, store.SetEnabledCallCount);
    }

    /// <summary>
    /// Часть плотного логирования "сервисов обновления данных аддинов": успешный тоггл — на
    /// уровне Information (и до, и после SetEnabled — оба факта важны отдельно: намерение
    /// пользователя и реальный результат).
    /// </summary>
    [Fact]
    public void IsEnabled_SetEnabledSucceeds_LogsInformationBeforeAndAfter()
    {
        var store = new FakeAddinStore();
        var file = NewFile("Normal.addin", AddinScope.User, "2024", enabled: true);
        var logger = new RecordingLogger<AddinFileRowViewModel>();
        _ = new AddinFileRowViewModel(store, file, logger, TestLocalization.For<AddinFileRowViewModel>())
        {
            IsEnabled = false
        };

        Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Information));
        Assert.True(logger.HasEntry(LogLevel.Information, "пользователь выставил"));
        Assert.True(logger.HasEntry(LogLevel.Information, "успешно"));
    }

    [Fact]
    public void IsEnabled_SetEnabledThrows_LogsErrorAndDebugOnRevert()
    {
        var store = new FakeAddinStore();
        var file = NewFile("Autodesk.eTransmitApplication.addin", AddinScope.Machine, "2023", enabled: true);
        var logger = new RecordingLogger<AddinFileRowViewModel>();
        var row = new AddinFileRowViewModel(store, file, logger, TestLocalization.For<AddinFileRowViewModel>());
        store.ThrowOnNextSetEnabled = new IOException(
            "boom", new UnauthorizedAccessException("Access to the path is denied."));

        row.IsEnabled = false;

        Assert.True(logger.HasEntry(LogLevel.Error, "не удалось выставить"));
        Assert.True(
            logger.HasEntry(LogLevel.Debug, "программный откат"),
            "Подавленный повторный заход в OnIsEnabledChanged при откате должен быть виден на уровне Debug.");
        // Ни одна Information-запись про "успешно" не появляется — переключение реально не удалось.
        Assert.False(logger.HasEntry(LogLevel.Information, "успешно"));
    }

    /// <summary>
    /// Сторонний путь записи при живом Revit (<see cref="RevitRunningException"/>
    /// из стора, другая сборка) — тот же откат чекбокса и понятное сообщение, что при ошибке диска.
    /// </summary>
    [Fact]
    public void IsEnabled_StoreThrowsRevitRunning_RevertsAndShowsMessage()
    {
        var store = new FakeAddinStore();
        var file = NewFile("Blocked.addin", AddinScope.User, "2024", enabled: true);
        var row = NewRow(store, file);
        store.ThrowOnNextSetEnabled = new RevitRunningException("Revit запущен — изменение файлов заблокировано.");

        row.IsEnabled = false;

        Assert.True(row.IsEnabled, "После запрета чекбокс должен откатиться к реальному состоянию файла на диске.");
        Assert.Equal("Revit запущен — изменение файлов заблокировано.", row.ErrorMessage);
        Assert.Equal(1, store.SetEnabledCallCount);
    }

    /// <summary>
    /// Крестик строки только просит: само удаление (подтверждение, стор, обновление) делает
    /// список в обработчике события. Пока жив Revit — команда недоступна.
    /// </summary>
    [Fact]
    public void DeleteCommand_RaisesDeleteRequested()
    {
        var row = NewRow(new FakeAddinStore(), NewFile("A.addin", AddinScope.User, "2025", enabled: true));
        var raised = 0;
        row.DeleteRequested += (_, _) => raised++;
        Assert.True(row.DeleteCommand.CanExecute(null));

        row.DeleteCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void DeleteCommand_Locked_CannotExecute()
    {
        var row = NewRow(new FakeAddinStore(), NewFile("A.addin", AddinScope.User, "2025", enabled: true));

        row.IsLocked = true;

        Assert.False(row.DeleteCommand.CanExecute(null));
    }

    private static AddinFileRowViewModel NewRow(FakeAddinStore store, AddinFile file) =>
        new(store, file, NullLogger<AddinFileRowViewModel>.Instance, TestLocalization.For<AddinFileRowViewModel>());

    /// <summary>
    /// Счётчик записей в <see cref="AddinFileRowViewModel.MetaLine"/> плюрализуется под культуру
    /// (английскому хватает two forms, русскому нужны три) — культуру выставляет сам тест.
    /// </summary>
    [Fact]
    public void MetaLine_EntryCount_PluralizesPerCulture()
    {
        var row2 = NewRow(new FakeAddinStore(), NewFileMany("Two.addin", 2));
        var row5 = NewRow(new FakeAddinStore(), NewFileMany("Five.addin", 5));

        TestCulture.RunIn("en", () =>
        {
            Assert.Contains("2 entries", row2.MetaLine);
            Assert.Contains("5 entries", row5.MetaLine);
        });
        TestCulture.RunIn("ru", () =>
        {
            Assert.Contains("2 записи", row2.MetaLine);
            Assert.Contains("5 записей", row5.MetaLine);
        });
    }

    private static AddinFile NewFileMany(string fileName, int entryCount) =>
        new(fileName, AddinScope.User, "2025", Enabled: true,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: Parser.Parse(MultiEntryXml(entryCount)));

    private static string MultiEntryXml(int entryCount) =>
        "<RevitAddIns>" + string.Concat(
            Enumerable.Range(1, entryCount).Select(i => $"""
                <AddIn Type="Application">
                  <Name>Entry{i}</Name>
                  <Assembly>a.dll</Assembly>
                  <AddInId>{Guid.NewGuid()}</AddInId>
                  <FullClassName>A.App{i}</FullClassName>
                </AddIn>
                """)) + "</RevitAddIns>";

    private static AddinFile NewFile(string fileName, AddinScope scope, string version, bool enabled) =>
        new(fileName, scope, version, enabled,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: Parser.Parse(ValidAddinXml(fileName)));

    private static string ValidAddinXml(string name) => $"""
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>{name}</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;
}
