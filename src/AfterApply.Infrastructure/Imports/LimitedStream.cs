namespace AfterApply.Infrastructure.Imports;

/// <summary>
/// Wraps a stream and throws once cumulative bytes read exceed a configured cap. Closes the
/// zip-bomb gap where a ZIP entry's declared (metadata) Length is checked before opening, but
/// nothing bounds the actual decompressed byte count during the read itself — a maliciously
/// crafted entry whose metadata understates its real decompressed size would otherwise read
/// unbounded until it exhausts memory or coincidentally hits the row-count cap.
/// </summary>
public sealed class LimitedStream(Stream inner, long maxBytes) : Stream
{
    private long _totalRead;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        Track(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        Track(read);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Track(int bytesRead)
    {
        _totalRead += bytesRead;
        if (_totalRead > maxBytes)
        {
            throw new StreamLengthExceededException(maxBytes);
        }
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        await base.DisposeAsync();
    }
}

public sealed class StreamLengthExceededException(long maxBytes)
    : Exception($"Stream exceeded the {maxBytes} byte limit.")
{
    public long MaxBytes { get; } = maxBytes;
}
