namespace ClipVault.Infrastructure.Persistence;

public sealed class AppDataPaths
{
    private const string AppFolderName = "ClipVault";

    public AppDataPaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);
        DatabasePath = Path.Combine(RootDirectory, "clipvault.db");
        ImagesDirectory = Path.Combine(RootDirectory, "images");
    }

    public string RootDirectory { get; }
    public string DatabasePath { get; }
    public string ImagesDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ImagesDirectory);
    }
}
