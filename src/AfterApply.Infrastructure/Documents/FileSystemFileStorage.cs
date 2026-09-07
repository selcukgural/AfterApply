using AfterApply.Application.Documents;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Documents;

/// <summary>
/// Stores CV files under a directory on the local disk. Development and tests only — see
/// <see cref="FileStorageProvider.FileSystem"/> for why Production refuses it.
/// </summary>
internal sealed class FileSystemFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalRootPath);

    public async Task SaveAsync(string objectName, Stream content, string contentType,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string objectName, CancellationToken cancellationToken)
    {
        var path = ResolvePath(objectName);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken)
    {
        // File.Delete is a no-op on a missing file, which is the idempotence IFileStorage asks for.
        File.Delete(ResolvePath(objectName));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps an object name onto a path under the root, and refuses anything that would land
    /// outside it. Callers only ever pass names built by CvDocument.BuildStorageObjectName, so this
    /// cannot fire today — it is here so that stays true if some later caller is less careful.
    /// </summary>
    private string ResolvePath(string objectName)
    {
        var path = Path.GetFullPath(Path.Combine(_root, objectName));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Object name '{objectName}' resolves outside the storage root.",
                nameof(objectName));
        }

        return path;
    }
}
