namespace TravelPlanner.Api.Logging;

internal sealed class LimitedResponseCapture(Stream response) : Stream
{
    private readonly MemoryStream captured = new();
    private bool overflow;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => response.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public System.Text.Json.Nodes.JsonNode? ReadJson() => overflow || captured.Length == 0
        ? null
        : ApiLogCapture.Parse(captured.GetBuffer().AsSpan(0, (int)captured.Length));

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        response.Write(buffer);
        Capture(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await response.WriteAsync(buffer, cancellationToken);
        Capture(buffer.Span);
    }

    public override void Flush() => response.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => response.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            captured.Dispose();
        base.Dispose(disposing);
    }

    private void Capture(ReadOnlySpan<byte> buffer)
    {
        if (captured.Length + buffer.Length > ApiLogCapture.BodyLimit)
            overflow = true;
        if (!overflow)
            captured.Write(buffer);
    }
}
