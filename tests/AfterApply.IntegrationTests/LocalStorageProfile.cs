using Microsoft.AspNetCore.Hosting;

namespace AfterApply.IntegrationTests;

/// <summary>
/// A host whose document storage is a scratch directory of the class's own. Emptied before every
/// test, so one test's files can never be mistaken for another's and the assertions about what is
/// on disk mean what they say; deleted with the fixture.
/// </summary>
public class LocalStorageProfile(string purpose) : IHostProfile
{
    public LocalStorageProfile() : this("tests")
    {
    }

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"afterapply-{purpose}", Guid.CreateVersion7().ToString("N"));

    public virtual void Configure(IWebHostBuilder builder) => builder.UseSetting("Storage:LocalRootPath", StorageRoot);

    public virtual void Reset() => DeleteRoot();

    public virtual ValueTask DisposeAsync()
    {
        DeleteRoot();
        return default;
    }

    private void DeleteRoot()
    {
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }
}
