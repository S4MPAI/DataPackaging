namespace DataPackaging.Logic.Streams;

public interface IPackagingStream
{
    public void Encode();
    public void Decode();

    void ChangeDecompressedStream(Stream decompressedStream);
}