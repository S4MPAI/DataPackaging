using System.Text;
using DataPackaging.Logic.Readers;
using DataPackaging.Logic.Writers;

namespace DataPackaging.Logic.Streams;

public class LzssStream : IPackagingStream
{
    private Stream decompressedStream;
    private Stream compressedStream;
    
    private const int bufferSize = 81920;
    private const int stringBufferSize = 16;
    private static readonly Encoding encoding = Encoding.GetEncoding(1251);
    
    public LzssStream(Stream compressedStream, Stream decompressedStream)
    {
        this.decompressedStream = decompressedStream;
        this.compressedStream = compressedStream;
    }

    public void ChangeDecompressedStream(Stream decompressedStream)
    {
        this.decompressedStream = decompressedStream;
    }
    
    public void Encode()
    {
        var bitWriter = new BitWriter(compressedStream);
        var stringBuffer = GenerateNewStringBuffer();
        var currentSubstring = new StringBuilder();
        var substringLength = 0;
        var dictionary = new string(stringBuffer);
        var length = 0;
        var currentIndex = -1;
        var offset = -1;

        
        while (true)
        {
            var buffer = new byte[bufferSize];
            var count = decompressedStream.Read(buffer, 0, bufferSize);

            if (count == 0)
                break;

            for (int i = 0; i < count; i++)
            {
                var localIndex = i;
                do
                {
                    offset = currentIndex;
                    length = currentSubstring.Length;
                    currentSubstring.Append(GetCharFromBuffer(buffer, localIndex));
                    currentIndex = dictionary.IndexOf(currentSubstring.ToString(), StringComparison.Ordinal);
                    localIndex++;
                } while (currentIndex >= 0 && localIndex < count);
                i += length == 0 ? 0 : length - 1;
                
                if (i < count && currentIndex >= 0)
                    break;
                
                if (offset >= 0 && length > 1)
                {
                    bitWriter.WriteBit(true);
                    var bits = new List<int>{offset, length};
                    bitWriter.WriteBits(bits, FindGroupBitLength());
                    MoveStringBuffer(stringBuffer, currentSubstring.ToString());
                }
                else
                {
                    bitWriter.WriteBit(false);
                    var bits = new List<int>{ buffer[i] };
                    bitWriter.WriteBits(bits, 8);
                    MoveStringBuffer(stringBuffer, currentSubstring.ToString()[0]);
                }
                
                length = 0;
                currentIndex = -1;
                offset = -1;
                dictionary = new string(stringBuffer);
                currentSubstring.Clear();
            }
        }
    }

    public void Decode()
    {
        var bitReader = new BitReader(compressedStream);
    }

    private char[] GenerateNewStringBuffer()
    {
        var buffer = new char[stringBufferSize];

        for (int i = 0; i < stringBufferSize; i++)
            buffer[i] = '*';
        
        return buffer;
    }
    
    private void MoveStringBuffer(char[] stringBuffer, string substring)
    {
        var shift = substring.Length - 1;

        for (var i = shift; i < stringBufferSize; i++)
            stringBuffer[i - shift] = stringBuffer[i];

        var startPosForAdding = stringBufferSize - shift;
        for (var i = 0; i < shift; i++)
            stringBuffer[startPosForAdding + i] = substring[i];
    }

    private void MoveStringBuffer(char[] stringBuffer, char letter)
    {
        for (var i = 1; i < stringBufferSize; i++)
            stringBuffer[i - 1] = stringBuffer[i];

        stringBuffer[^1] = letter;
    }
    
    private byte FindGroupBitLength() => (byte)(Math.Ceiling(Math.Log2(stringBufferSize)) - 1);

    private static string GetCharFromBuffer(byte[] buffer, int start) => encoding.GetString(new[]{buffer[start]});
}