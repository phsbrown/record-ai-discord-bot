using System.Buffers.Binary;
using record_ai_discord_bot.Domain.ValueObjects;

namespace record_ai_discord_bot.Infrastructure.Persistence;

internal sealed class WavFileWriter : IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly PcmAudioFormat _format;
    private long _dataBytesWritten;
    private bool _isDisposed;

    public WavFileWriter(string filePath, PcmAudioFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _format = format ?? throw new ArgumentNullException(nameof(format));

        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        _stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        WriteHeaderPlaceholder();
    }

    public PcmAudioFormat Format => _format;

    public async ValueTask WritePcmAsync(ReadOnlyMemory<byte> pcmData, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (pcmData.IsEmpty)
        {
            return;
        }

        if (pcmData.Length % _format.BlockAlign != 0)
        {
            throw new InvalidOperationException(
                $"PCM frame payload length ({pcmData.Length}) must be divisible by block align ({_format.BlockAlign}).");
        }

        if (_dataBytesWritten + pcmData.Length > uint.MaxValue)
        {
            throw new InvalidOperationException("WAV data chunk exceeded 4 GiB limit.");
        }

        await _stream.WriteAsync(pcmData, cancellationToken).ConfigureAwait(false);
        _dataBytesWritten += pcmData.Length;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        await FinalizeHeaderAsync().ConfigureAwait(false);
        await _stream.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void WriteHeaderPlaceholder()
    {
        var header = new byte[44];

        WriteAscii(header, 0, "RIFF");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), 36);
        WriteAscii(header, 8, "WAVE");
        WriteAscii(header, 12, "fmt ");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(22, 2), (ushort)_format.ChannelCount);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24, 4), (uint)_format.SampleRateHz);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28, 4), (uint)_format.BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(32, 2), (ushort)_format.BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34, 2), (ushort)_format.BitsPerSample);
        WriteAscii(header, 36, "data");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40, 4), 0);

        _stream.Write(header, 0, header.Length);
    }

    private async Task FinalizeHeaderAsync()
    {
        if (!_stream.CanSeek)
        {
            throw new InvalidOperationException("WAV stream must support seeking to finalize the header.");
        }

        var dataChunkSize = checked((uint)_dataBytesWritten);
        var riffChunkSize = checked(36 + dataChunkSize);

        var riffSizeBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(riffSizeBytes, riffChunkSize);

        var dataSizeBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(dataSizeBytes, dataChunkSize);

        _stream.Seek(4, SeekOrigin.Begin);
        await _stream.WriteAsync(riffSizeBytes).ConfigureAwait(false);

        _stream.Seek(40, SeekOrigin.Begin);
        await _stream.WriteAsync(dataSizeBytes).ConfigureAwait(false);

        await _stream.FlushAsync().ConfigureAwait(false);
    }

    private static void WriteAscii(Span<byte> destination, int offset, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            destination[offset + index] = (byte)value[index];
        }
    }
}
