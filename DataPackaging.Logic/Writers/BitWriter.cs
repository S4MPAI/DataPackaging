namespace DataPackaging.Logic.Writers;

public class BitWriter
{
    private Stream stream;
    private byte neededBitsCountForLastByte = 8;
    
    public BitWriter(Stream stream)
    {
        this.stream = stream;
        stream.Seek(0, SeekOrigin.Begin);
    }

    public void WriteBits(List<int> bits, byte bitsCount)
    {
        if (bits.Count == 0)
            return;
        
        var usedIntBits = (byte)0;
        var currentInt = 0;
        
        if (neededBitsCountForLastByte != 8)
        {
            stream.Position--;
            var lastByte = stream.ReadByte();
            stream.Position--;

            var neededBits = GetNewByteToAddFromBits(bits, bitsCount, ref currentInt, ref usedIntBits);

            var newLastByte = (byte)(lastByte | neededBits);
            stream.WriteByte(newLastByte);
        }
        
        var buffer = new byte[(int)Math.Ceiling((bitsCount * (bits.Count - currentInt - 1) + bitsCount - usedIntBits) / 8.0)];

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = GetNewByteToAddFromBits(bits, bitsCount, ref currentInt, ref usedIntBits);
        }
        
        stream.Write(buffer, 0, buffer.Length);
    }

    private byte GetNewByteToAddFromBits(List<int> bits, byte bitsCount, ref int currentInt, ref byte usedIntBits)
    {
        var neededBits = 0;
        while (neededBitsCountForLastByte > (bitsCount - usedIntBits) && currentInt < bits.Count)
        {
            var writedBitsCount = (byte)(bitsCount - usedIntBits);
            neededBits |= bits[currentInt] << (neededBitsCountForLastByte - writedBitsCount);
            neededBitsCountForLastByte -= writedBitsCount;
            currentInt++;
            usedIntBits = 0;
        }

        if (currentInt < bits.Count)
        {
            var remainingIntBits = (byte)(bitsCount - neededBitsCountForLastByte);
            neededBits |= bits[currentInt] >> remainingIntBits;
            usedIntBits = (byte)(bitsCount - remainingIntBits);
            neededBitsCountForLastByte = 8;

            if (remainingIntBits == 0)
            {
                currentInt++;
                usedIntBits = 0;
            }
        }

        return (byte)neededBits;
    }
}