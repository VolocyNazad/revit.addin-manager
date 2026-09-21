using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using AddinManager.Launcher.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Проверка обновлений через GitHub API: последний релиз <c>releases/latest</c>, версия из
/// <c>tag_name</c> (префикс <c>v</c> срезается), ссылка на скачивание — <c>html_url</c>.
/// </summary>
public sealed class GitHubUpdateChecker(HttpClient httpClient, ILogger<GitHubUpdateChecker> logger) : IUpdateChecker
{
    private const string Repository = "VolocyNazad/revit.addin-manager";

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = CurrentVersion;
        try
        {
            using var response = await httpClient.GetAsync(
                $"https://api.github.com/repos/{Repository}/releases/latest",
                cancellationToken);

            // 404 — релизов ещё нет: это не ошибка, а просто "обновлений нет".
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("CheckAsync: релизов нет (404)");
                return new UpdateCheckResult(false, null, null, current?.ToString(), null);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "CheckAsync: GitHub вернул {StatusCode}", (int)response.StatusCode);
                return new UpdateCheckResult(false, null, null, current?.ToString(), $"HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var url = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;

            if (tag is null || url is null)
            {
                logger.LogWarning("CheckAsync: в ответе нет tag_name/html_url");
                return new UpdateCheckResult(false, null, null, current?.ToString(), "Unexpected GitHub response.");
            }

            var latest = StripVersionPrefix(tag);
            return new UpdateCheckResult(IsNewerVersion(latest, current), latest, url, current?.ToString(), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "CheckAsync: проверка обновлений не удалась");
            return new UpdateCheckResult(false, null, null, current?.ToString(), ex.Message);
        }
    }

    /// <summary>Текущая версия сборки (1.0.0.0 по умолчанию, если не задана).</summary>
    private static Version? CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version;

    /// <summary>Срезает префикс <c>v</c>/<c>V</c> у тега релиза.</summary>
    internal static string StripVersionPrefix(string tag) =>
        tag.Length > 1 && (tag[0] is 'v' or 'V') ? tag[1..] : tag;

    /// <summary>Парсит версию из тега; нечисловые теги (например, <c>preview</c>) не считаются версией.</summary>
    internal static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (Version.TryParse(text.Trim(), out var parsed) && parsed is not null)
        {
            version = parsed;
            return true;
        }

        return false;
    }

    /// <summary>Есть ли версия новее текущей (нечитаемый тег или отсутствие версии — не обновление).</summary>
    internal static bool IsNewerVersion(string? latestTag, Version? current)
    {
        if (current is null || !TryParseVersion(latestTag, out var latest))
            return false;

        return latest > current;
    }
}
