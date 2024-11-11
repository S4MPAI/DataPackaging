using System.IO.Compression;
using System.Text;
using DataPackaging.Logic.Readers;
using DataPackaging.Logic.Writers;

namespace DataPackaging.Logic.Streams;

public class LzwStream : IPackagingStream
{
    private Dictionary<string, int> codingMap;
    private Dictionary<int, string> decodingMap;
    private readonly Dictionary<string, int> startSubstringsMap;
    private Stream decompressedStream;
    private Stream compressedStream;
    private readonly CompressionMode mode;
    
    private string wordInMap = string.Empty;
    
    private const int bufferSize = 81920;
    private static readonly Encoding encoding = Encoding.GetEncoding(1251);
    
    public LzwStream(Stream compressedStream, Stream decompressedStream, CompressionMode mode)
    {
        startSubstringsMap = GenerateStartSubstringMap(decompressedStream);
        this.decompressedStream = decompressedStream;
        this.compressedStream = compressedStream;
        this.mode = mode;
    }

    private static Dictionary<string, int> GenerateStartSubstringMap(Stream decompressedStream)
    {
        var substringMap = new Dictionary<string, int>();
        
        var buffer = new byte[bufferSize];
        var count = decompressedStream.Read(buffer, 0, bufferSize);
        while (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                var c = encoding.GetString(new[] { buffer[i] });

                if (!substringMap.ContainsKey(c))
                {
                    substringMap[c] = substringMap.Count + 1;
                }
            }
            
            count = decompressedStream.Read(buffer, 0, bufferSize);
        }
        
        decompressedStream.Seek(0, SeekOrigin.Begin);
        return substringMap;
    }

    public void ChangeDecompressedStream(Stream decompressedStream)
    {
        this.decompressedStream = decompressedStream;
    }

    public void Decode()
    {
        decompressedStream.Seek(0, SeekOrigin.Begin);
        var bitReader = new BitReader(compressedStream);
        decodingMap = startSubstringsMap.ToDictionary(k => k.Value, v => v.Key);
        var stringBuilder = new StringBuilder();
        var codedWord = "";
        
        var bitsLength = FindGroupBitLength(decodingMap.Count + 1);
        var length = (int)Math.Pow(2, bitsLength) - decodingMap.Count;

        var isStop = false;
        while (!isStop)
        {
            var bits = bitReader.ReadBits(bitsLength, length);
            
            if (bits.Count == 0)
                break;

            foreach (var wordNumber in bits)
            {
                if (wordNumber == 0)
                {
                    isStop = true;
                    break;
                }
                
                var isNotAdded = true;

                if (wordNumber == decodingMap.Count + 1)
                {
                    isNotAdded = false;
                    decodingMap[decodingMap.Count + 1] = codedWord + codedWord[0];
                }
                    
                
                var word = decodingMap[wordNumber];
                stringBuilder.Append(word);
                
                if (codedWord != "" && isNotAdded)
                {
                    codedWord += word[0];
                    decodingMap[decodingMap.Count + 1] = codedWord;
                }
                codedWord = word;

                if (stringBuilder.Length == bufferSize)
                {
                    var buffer = encoding.GetBytes(stringBuilder.ToString());
                    stringBuilder.Clear();
                    decompressedStream.Write(buffer, 0, buffer.Length);
                }
            }
            
            bitsLength = FindGroupBitLength(decodingMap.Count + 2);
            length = (int)Math.Pow(2, bitsLength) - decodingMap.Count - 1;
        }

        if (stringBuilder.Length > 0)
        {
            var buffer = encoding.GetBytes(stringBuilder.ToString());
            decompressedStream.Write(buffer, 0, buffer.Length);
        }
    }

    public void Encode()
    {
        var bitWriter = new BitWriter(compressedStream);
        codingMap = startSubstringsMap.ToDictionary();
        var buffer = new byte[bufferSize];
        var checkingStringBuilder = new StringBuilder();
        var groupBitLength = FindGroupBitLength(codingMap.Count);

        while (true)
        {
            var count = decompressedStream.Read(buffer, 0, bufferSize);
            if (count == 0)
                return;
            
            var writeBuffer = new List<int>();
        
            checkingStringBuilder.Append(GetCharFromBuffer(buffer, 0));
        
            for (var i = 1; i <= count; i++)
            {
                var str = checkingStringBuilder.ToString();
            
                if (codingMap.ContainsKey(str))
                {
                    if (i == count)
                        writeBuffer.Add(codingMap[str]);
                    
                    wordInMap = str;
                    var c = GetCharFromBuffer(buffer, i);
                    checkingStringBuilder.Append(c);
                    continue;
                }
                
                writeBuffer.Add(codingMap[wordInMap]);
                codingMap[str] = codingMap.Count + 1;
                checkingStringBuilder.Clear();
                checkingStringBuilder.Append(GetCharFromBuffer(buffer, --i));
                
                var newGroupBitLength = FindGroupBitLength(codingMap.Count + 1);
                if (newGroupBitLength != groupBitLength)
                {
                    bitWriter.WriteBits(writeBuffer, groupBitLength);
                    writeBuffer = new List<int>();
                }

                groupBitLength = newGroupBitLength;
            }
            
            bitWriter.WriteBits(writeBuffer, groupBitLength);
        }
    }

    private byte FindGroupBitLength(int mapLength)
    {
        return (byte)Math.Ceiling(Math.Log2(mapLength));
    }

    private static string GetCharFromBuffer(byte[] buffer, int start) => encoding.GetString(new[]{buffer[start]});
}