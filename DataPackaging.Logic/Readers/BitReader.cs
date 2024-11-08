namespace DataPackaging.Logic.Readers;

public class BitReader
{
    private readonly Stream stream;
    private byte currentByte;
    private byte remainingBitsInByte;
    
    public BitReader(Stream stream)
    {
        this.stream = stream;
        stream.Seek(0, SeekOrigin.Begin);
    }
    
    public List<int> ReadBits(byte bitsLength, int length)
    {
        if (length == 0 || bitsLength == 0)
            return new();
        
        var result = new List<int>();
        var bufferLength = (int)Math.Ceiling(bitsLength * length / 8.0);
        var buffer = new byte[bufferLength];
        var count = stream.Read(buffer, 0, bufferLength);
        var currentBufferIndex = 0;
        
        if (count == 0)
            return result;

        if (remainingBitsInByte == 0)
        {
            currentByte = buffer[currentBufferIndex];
            currentBufferIndex++;
            remainingBitsInByte = 8;
        }


        for (var i = 0; i < length; i++)
        {
            var bits = GetBits(buffer, count, ref currentBufferIndex, bitsLength);
            
            if (bits == 0)
                break;
            result.Add(bits);
        }
        
        return result;
    }

    private int GetBits(byte[] buffer, int count, ref int currentBufferIndex, byte bitsCount)
    {
        var bits = 0;
        
        while (bitsCount >= remainingBitsInByte && currentBufferIndex <= count)
        {
            bits |= currentByte;
            bitsCount -= remainingBitsInByte;
            bits <<= bitsCount;
            remainingBitsInByte = 8;
            currentByte = buffer[currentBufferIndex];
            currentBufferIndex++;
        }

        if (currentBufferIndex <= count)
        {
            remainingBitsInByte -= bitsCount;
            bits |= currentByte >> remainingBitsInByte;
            var shift = 8 - remainingBitsInByte;
            currentByte = (byte)((byte)(currentByte << shift) >> shift);
        }

        return bits;
    }
}