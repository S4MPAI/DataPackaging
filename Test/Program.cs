﻿// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using DataPackaging.Logic.Streams;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

byte byte1 = 255;
byte1 = (byte)((byte)(byte1 << 3) >> 3);

var encoding = Encoding.GetEncoding(1251);

var b = encoding.GetBytes("КРАСНАЯ КРАСКА");
var decompressedStream = new MemoryStream(b);
var compressedStream = new MemoryStream();
var newDecompressedStream = new MemoryStream();

var lzw = new LzssStream(compressedStream, decompressedStream);
lzw.Encode();
lzw.ChangeDecompressedStream(newDecompressedStream);
lzw.Decode();


Console.WriteLine(b);