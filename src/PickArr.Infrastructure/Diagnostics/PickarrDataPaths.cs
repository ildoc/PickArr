namespace Pickarr.Infrastructure.Diagnostics;

/// <summary>Resolved paths under <c>Pickarr:DataPath</c> for diagnostics and storage accounting.</summary>
public sealed class PickarrDataPaths
{
    public PickarrDataPaths(string root, string? jobsDatabaseFile = null, string? modelsDirectory = null)
    {
        Root = root;
        DatabaseFile = Path.Combine(root, "Pickarr.db");
        JobsDatabaseFile = jobsDatabaseFile;
        LogsDirectory = Path.Combine(root, "logs");
        KeysDirectory = Path.Combine(root, "keys");
        ModelsDirectory = string.IsNullOrWhiteSpace(modelsDirectory)
            ? Path.Combine(root, "models")
            : modelsDirectory;
    }

    public string Root { get; }
    public string DatabaseFile { get; }
    public string? JobsDatabaseFile { get; }
    public string LogsDirectory { get; }
    public string KeysDirectory { get; }
    public string ModelsDirectory { get; }
}
