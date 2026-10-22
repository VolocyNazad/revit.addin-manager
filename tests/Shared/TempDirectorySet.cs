using System.IO;

namespace AddinManager.Testing;

/// <summary>
/// Registers temporary directories created by a test and deletes them on dispose, so every test
/// project cleans up the same way. Shared through the linked sources in <c>tests/Shared</c>.
/// </summary>
public sealed class TempDirectorySet : IDisposable
{
    private readonly List<string> _roots = [];

    /// <summary>Creates a unique temporary directory path and registers it for cleanup.</summary>
    /// <returns>The directory path; the caller creates the directory when it is actually needed.</returns>
    public string Create()
    {
        string root = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());
        _roots.Add(root);
        return root;
    }

    /// <summary>Deletes every registered directory; a directory that cannot be deleted is not a test failure.</summary>
    public void Dispose()
    {
        foreach (string root in _roots)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Temporary test folder — not critical if it could not be removed.
            }
            catch (UnauthorizedAccessException)
            {
                // Temporary test folder — not critical if it could not be removed.
            }
        }

        _roots.Clear();
    }
}
