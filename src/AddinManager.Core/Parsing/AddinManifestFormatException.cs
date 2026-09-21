namespace AddinManager.Core.Parsing;

/// <summary>Манифест нельзя распарсить: битый XML, нет корня, нет обязательных полей.</summary>
public sealed class AddinManifestFormatException(string message, Exception? inner = null)
    : Exception(message, inner);
