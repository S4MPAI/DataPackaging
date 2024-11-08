// See https://aka.ms/new-console-template for more information

using System.IO.Compression;
using System.Text;
using DataPackaging.Logic.Streams;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

byte byte1 = 255;
byte1 = (byte)((byte)(byte1 << 3) >> 3);

var encoding = Encoding.GetEncoding(1251);

var b = encoding.GetBytes("ауауауаккууцуцувв");
var decompressedStream = new MemoryStream(b);
var compressedStream = new MemoryStream();
var newDecompressedStream = new MemoryStream();

var lzw = new LzwStream(compressedStream, decompressedStream, CompressionMode.Compress);
lzw.Encode();
lzw.ChangeDecompressedStream(newDecompressedStream);
lzw.Decode();


Console.WriteLine(b);