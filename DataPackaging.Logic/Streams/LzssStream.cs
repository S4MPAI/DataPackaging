using System.Text;
using DataPackaging.Logic.Readers;
using DataPackaging.Logic.Writers;

namespace DataPackaging.Logic.Streams;

public class LzssStream : IPackagingStream
{
    private Stream decompressedStream;
    private Stream compressedStream;
    
    private const int bufferSize = 81920;
    private const int stringBufferSize = 8;
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
                
                if (i == count - 1 && currentIndex >= 0)
                    break;
                
                if (offset >= 0 && (length > 1 || (length == 1 && stringBufferSize <= 8)))
                {
                    WriteSubstring(bitWriter, offset, stringBuffer, currentSubstring.ToString().Remove(currentSubstring.Length - 1));
                }
                else
                {
                    WriteLetter(bitWriter, buffer[i], stringBuffer);
                }
                
                length = 0;
                currentIndex = -1;
                offset = -1;
                dictionary = new string(stringBuffer);
                currentSubstring.Clear();
            }
        }
        
        if (currentSubstring.Length > 0)
            WriteSubstring(bitWriter, currentIndex, stringBuffer, currentSubstring.ToString());
    }

    private void WriteLetter(BitWriter bitWriter, byte letter, char[] stringBuffer)
    {
        var letterChar = encoding.GetString([letter]);
        bitWriter.WriteBit(false);
        var bits = new List<int>{ letter };
        bitWriter.WriteBits(bits, 8);
        MoveStringBuffer(stringBuffer, letterChar);
    }

    private void WriteSubstring(BitWriter bitWriter, int offset, char[] stringBuffer,
        string currentSubstring)
    {
        bitWriter.WriteBit(true);
        var bits = new List<int>{offset, currentSubstring.Length};
        bitWriter.WriteBits(bits, FindGroupBitLength());
        MoveStringBuffer(stringBuffer, currentSubstring);
    }

    public void Decode()
    {
        decompressedStream.Seek(0, SeekOrigin.Begin);
        var bitReader = new BitReader(compressedStream);
        var stringBuffer = GenerateNewStringBuffer();
        var bufferBuilder = new StringBuilder(bufferSize);

        while (true)
        {
            var symbolType = bitReader.ReadBit();
            
            if (symbolType < 0)
                break;

            if (symbolType == 1)
            {
                var bits = bitReader.ReadBits(FindGroupBitLength(), 2);
                var dictionary = new string(stringBuffer);
                var word = dictionary.Substring(bits[0], bits[1]);
                bufferBuilder.Append(word);
                MoveStringBuffer(stringBuffer, word);
            }
            else
            {
                var bits = bitReader.ReadBits(8, 1);
                
                if (bits.Count == 0)
                    break;
                
                var letter = encoding.GetString([(byte)bits[0]]);
                bufferBuilder.Append(letter);
                MoveStringBuffer(stringBuffer, letter[0]);
            }

            if (bufferBuilder.Length >= bufferSize)
                decompressedStream.Write(encoding.GetBytes(bufferBuilder.ToString()), 0, bufferBuilder.Length);
        }

        if (bufferBuilder.Length > 0)
            decompressedStream.Write(encoding.GetBytes(bufferBuilder.ToString()), 0, bufferBuilder.Length);
        decompressedStream.Close();
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
        var shift = substring.Length;

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
    
    private byte FindGroupBitLength() => (byte)(Math.Ceiling(Math.Log2(stringBufferSize + 1)) - 1);

    private static string GetCharFromBuffer(byte[] buffer, int start) => encoding.GetString([buffer[start]]);
}