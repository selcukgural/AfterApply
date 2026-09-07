using System.Net;
using AfterApply.Application.Documents;
using Google;
using Google.Apis.Storage.v1;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Documents;

/// <summary>
/// Cloud Storage, authenticated with Application Default Credentials — on Cloud Run that is the
/// service's own runtime identity, so there is no key file anywhere and nothing to rotate.
/// </summary>
/// <remarks>
/// No signed URLs, deliberately. The runtime service account has no private key, so a V4 signature
/// would have to go through the IAM Credentials API's signBlob (an extra role and an extra network
/// hop on every download), and the URL it produced would then be a bearer token for the file: valid
/// for anyone holding it, for its whole lifetime, with no way to revoke it. Proxying the bytes
/// through the API instead keeps every download behind the same authentication and the same
/// ownership check as every other endpoint, and the bucket can stay closed to the internet.
/// </remarks>
internal sealed class GoogleCloudStorageFileStorage(StorageClient client, IOptions<StorageOptions> options)
    : IFileStorage
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task SaveAsync(string objectName, Stream content, string contentType,
        CancellationToken cancellationToken)
    {
        await client.UploadObjectAsync(_bucket, objectName, contentType, content,
            cancellationToken: cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string objectName, CancellationToken cancellationToken)
    {
        // Streamed rather than downloaded into a buffer: DownloadObjectAsync wants a destination
        // stream, which for a proxied download would mean holding the whole file in memory (or in
        // Cloud Run's in-memory temp directory, which is the same thing) before the first byte
        // reaches the client. At the 5 MB cap and Cloud Run's default concurrency that is still a
        // real amplification vector, so the response body is piped straight from GCS instead.
        var request = client.Service.Objects.Get(_bucket, objectName);
        request.Alt = ObjectsResource.GetRequest.AltEnum.Media;

        try
        {
            return await request.ExecuteAsStreamAsync(cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string objectName, CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteObjectAsync(_bucket, objectName, cancellationToken: cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Already gone. Deletion is retried from background cleanup, so it has to be safe to
            // run twice — see IFileStorage.DeleteAsync.
        }
    }
}
