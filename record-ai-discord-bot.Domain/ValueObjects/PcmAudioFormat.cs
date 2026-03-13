namespace record_ai_discord_bot.Domain.ValueObjects;

public sealed record PcmAudioFormat
{
    public PcmAudioFormat(int sampleRateHz, short channelCount, short bitsPerSample)
    {
        if (sampleRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz), sampleRateHz, "Sample rate must be a positive value.");
        }

        if (channelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "Channel count must be a positive value.");
        }

        if (bitsPerSample is <= 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerSample), bitsPerSample, "Bits per sample must be between 1 and 32.");
        }

        if (bitsPerSample % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerSample), bitsPerSample, "Bits per sample must be a multiple of 8 for PCM WAV.");
        }

        SampleRateHz = sampleRateHz;
        ChannelCount = channelCount;
        BitsPerSample = bitsPerSample;
    }

    public int SampleRateHz { get; }

    public short ChannelCount { get; }

    public short BitsPerSample { get; }

    public short BlockAlign => checked((short)(ChannelCount * (BitsPerSample / 8)));

    public int BytesPerSecond => checked(SampleRateHz * BlockAlign);
}
