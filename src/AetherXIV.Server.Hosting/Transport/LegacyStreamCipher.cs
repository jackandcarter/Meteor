namespace AetherXIV.Server.Hosting;

/// <summary>
/// Applies the client transport transform for the original FFXIV 1.23b wire format.
///
/// The transform is frame-aware because the 1.23b lobby stream keeps base/subpacket
/// headers readable and encrypts each subpacket body after its 0x10-byte subpacket header.
/// The first secure-start control frame is also plaintext and is used to establish the
/// per-connection Blowfish session key.
/// </summary>
public interface ILegacyStreamCipher
{
    string Name { get; }

    /// <summary>
    /// Allows a cipher to consume a plaintext control frame such as the lobby secure-start
    /// handshake. Returning true means the frame was handled and must not be dispatched as a
    /// normal decoded game-message frame.
    /// </summary>
    bool TryHandleControlFrame(ReadOnlySpan<byte> frame, out byte[]? immediateResponse);

    /// <summary>Decrypts an entire inbound frame in place.</summary>
    void TransformInboundFrame(Span<byte> frame);

    /// <summary>Encrypts an entire outbound frame in place.</summary>
    void TransformOutboundFrame(Span<byte> frame);
}

public interface ILegacyStreamCipherFactory
{
    ILegacyStreamCipher CreateCipher(LegacyConnectionContext context);
}

public sealed class LegacyNoOpStreamCipher : ILegacyStreamCipher
{
    public static LegacyNoOpStreamCipher Instance { get; } = new();

    private LegacyNoOpStreamCipher()
    {
    }

    public string Name => "noop-decrypted";

    public bool TryHandleControlFrame(ReadOnlySpan<byte> frame, out byte[]? immediateResponse)
    {
        immediateResponse = null;
        return false;
    }

    public void TransformInboundFrame(Span<byte> frame)
    {
    }

    public void TransformOutboundFrame(Span<byte> frame)
    {
    }
}

public sealed class LegacyNoOpStreamCipherFactory : ILegacyStreamCipherFactory
{
    public static LegacyNoOpStreamCipherFactory Instance { get; } = new();

    private LegacyNoOpStreamCipherFactory()
    {
    }

    public ILegacyStreamCipher CreateCipher(LegacyConnectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LegacyNoOpStreamCipher.Instance;
    }
}

public sealed class LegacyMissingCipher : ILegacyStreamCipher
{
    private readonly string missingReason;

    public LegacyMissingCipher(string missingReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missingReason);
        this.missingReason = missingReason;
    }

    public string Name => "missing-live-cipher";

    public bool TryHandleControlFrame(ReadOnlySpan<byte> frame, out byte[]? immediateResponse)
    {
        immediateResponse = null;
        ThrowMissing();
        return false;
    }

    public void TransformInboundFrame(Span<byte> frame)
    {
        ThrowMissing();
    }

    public void TransformOutboundFrame(Span<byte> frame)
    {
        ThrowMissing();
    }

    private void ThrowMissing()
    {
        throw new NotSupportedException($"Legacy live cipher is not configured: {missingReason}");
    }
}
