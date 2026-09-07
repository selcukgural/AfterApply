namespace AfterApply.Infrastructure.Documents;

public enum FileStorageProvider
{
    /// <summary>A directory on the local disk. For development and the test suite only — Cloud
    /// Run's filesystem is in-memory and per-instance, so anything written there is lost on the
    /// next revision and invisible to every other instance. Startup refuses this in Production
    /// rather than letting uploads quietly evaporate (see AddDocumentStorage).</summary>
    FileSystem = 0,

    GoogleCloudStorage = 1
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public FileStorageProvider Provider { get; init; } = FileStorageProvider.FileSystem;

    /// <summary>The GCS bucket holding CV files. Not a secret — it is an infrastructure identifier,
    /// and the bucket denies public access at the bucket level rather than relying on its name
    /// being unguessable — so it ships as a plain environment variable, not through Secret Manager.</summary>
    public string BucketName { get; init; } = string.Empty;

    /// <summary>Points the GCS client at a fake server instead of Google's. Set only by the
    /// integration suite; unset everywhere else, which is what makes the real client authenticate
    /// with Application Default Credentials.</summary>
    public string? EmulatorBaseUri { get; init; }

    /// <summary>Where <see cref="FileStorageProvider.FileSystem"/> keeps its files. Defaults under
    /// the OS temp directory so a fresh clone runs with no configuration at all.</summary>
    public string LocalRootPath { get; init; } = Path.Combine(Path.GetTempPath(), "afterapply-cv-storage");

    /// <summary>Per-file cap. 10 MB is generous for a CV (a text-heavy PDF is well under 1 MB) and
    /// far below Cloud Run's 32 MiB request limit, so the request never fails at the platform
    /// boundary with an error the app cannot phrase.</summary>
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
}
