namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>Показ файла в проводнике (за пределами приложения).</summary>
public interface IFolderOpener
{
    /// <summary>Открывает папку файла и выделяет в ней сам файл.</summary>
    /// <param name="fullPath">Полный путь к файлу.</param>
    void Reveal(string fullPath);
}
