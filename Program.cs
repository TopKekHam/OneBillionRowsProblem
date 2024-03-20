using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace OneBillionRowsProblem;

struct Station
{
    public float sum;
    public float min, max;
    public int total;
}

class LineReader
{

    const int BUFFER_SIZE = 4096 * 4096;

    char[] buffer;
    int head, tail;

    StreamReader streamReader;

    public LineReader(Stream stream)
    {
        buffer = new char[BUFFER_SIZE];
        head = 0;
        streamReader = new StreamReader(stream);
        tail = streamReader.ReadBlock(buffer);
    }

    public bool ReadNextLine(ref ReadOnlySpan<char> line)
    {
        var span = buffer.AsSpan(head, tail - head);

        var idx = span.IndexOf('\n');

        if (idx >= 0)
        {
            line = buffer.AsSpan(head, idx);
            head += idx + 1;
            return true;
        }

        if (head != tail)
        {

            if (streamReader.EndOfStream)
            {
                line = buffer.AsSpan(head);
                head = tail;
                return true;
            }

            // coping the remaining chars
            Array.Copy(buffer, head, buffer, 0, tail - head);

            tail = tail - head;
            head = 0;

            int bytesRead = streamReader.ReadBlock(buffer, tail, BUFFER_SIZE - tail);
            tail += bytesRead;

            return ReadNextLine(ref line);
        }
        else if (streamReader.EndOfStream == false) // when all buffer used but data left in stream
        {
            tail = streamReader.ReadBlock(buffer);
            head = 0;

            return ReadNextLine(ref line);
        }

        return false;
    }

}

class RowReader
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    uint HashName(ReadOnlySpan<char> str)
    {
        uint hash = 0;

        for (int i = 0; i < str.Length; i++)
        {
            hash += str[i];
            hash += (hash << 10);
            hash ^= (hash >> 6);
        }

        hash += (hash << 3);
        hash ^= (hash >> 11);
        hash += (hash << 15);

        return hash;

    }

    float ParseFloat(ReadOnlySpan<char> str)
    {
        int number = 0;
        bool negative = false;

        for (int i = 0; i < str.Length; i++)
        {
            if (str[i] == '-')
            {
                negative = true;
            }

            int off = str[i] - '0';

            if (off >= 0 && off <= 9)
            {
                number = number * 10 + off;
            }
        }

        number *= negative ? -1 : 1;

        return number / 10.0f;
    }

    int bufferSize = 1024 * 1024;
    char[] arr;
    int charsLeft = 0;
    bool EOF = false;
    FileStream file;
    StreamReader reader;

    ReadOnlyMemory<char>? ReadLine()
    {
        if (EOF) return null;

        if (charsLeft == 0)
        {
            int charsRead = reader.ReadBlock(arr);

            if (charsRead < bufferSize)
            {
                EOF = true;
            }
        }
        else
        {
            int start = bufferSize - charsLeft;

            for (int i = start; i < bufferSize; i++)
            {
                if (arr[i] == '\n')
                {
                    ReadOnlyMemory<char> mem = new ReadOnlyMemory<char>(arr, start, i);
                    charsLeft = charsLeft - i - 1;
                }
            }
        }



        return null;
    }

    public void CalcAverage(string path)
    {
        arr = new char[bufferSize];
        charsLeft = 0;
        EOF = false;

        //Hashtable hashtable = new Hashtable();

        Dictionary<string, Station> stations = new Dictionary<string, Station>();

        file = File.OpenRead(path);
        //reader = new StreamReader(file);
        LineReader lineReader = new LineReader(file);

        ReadOnlySpan<char> line = ReadOnlySpan<char>.Empty;

        while (lineReader.ReadNextLine(ref line))
        {
            var delimiter = line.IndexOf(';');

            var name = line.Slice(0, delimiter).ToString();
            //var hash = HashName(name);
            var stringNum = line.Slice(delimiter + 1);

            float num = ParseFloat(stringNum);

            if (stations.TryGetValue(name, out Station stat) == false)
            {
                stat = new Station();
                stat.total = 0;
                stat.max = num;
                stat.min = num;
                stat.sum = num;
                stat.total = 1;
            }
            else
            {
                stat.max = Math.Max(stat.max, num);
                stat.min = Math.Max(stat.min, num);
                stat.total += 1;
                stat.sum += num;
            }

            stations[name] = stat;
        }

        Console.Write("{");

        foreach (var kv in stations)
        {
            var val = kv.Value;
            float mean = val.max / val.total;

            Console.Write(kv.Key);
            Console.Write("=");
            Console.Write(val.min);
            Console.Write("/");
            Console.Write(mean);
            Console.Write("/");
            Console.Write(val.max);
            Console.Write(", ");
        }

        Console.Write("}");

    }

}

class Program
{
    static string oneMill = "./measurements_1_mil.txt";
    static string oneTenMill = "./measurements_10_mil.txt";
    static string oneHoundredMill = "./measurements_100_mil.txt";
    static string oneBill = "./measurements.txt";


    static void TestLineReader()
    {
        //{
        //    string str = "";
        //    int itrs = 10_000;
        //    for (int i = 0; i < itrs; i++)
        //    {
        //        str += $"line number: {i}";

        //        if (i != itrs - 1)
        //        {
        //            str += "\n";
        //        }
        //    }

        //    File.WriteAllText("./test.txt", str);
        //}

        {

            FileStream file = File.OpenRead(oneHoundredMill);
            
            Stopwatch sw = Stopwatch.StartNew();
            
            LineReader lineReader = new LineReader(file);

            ReadOnlySpan<char> str = ReadOnlySpan<char>.Empty;
            int lines = 0;

            while (lineReader.ReadNextLine(ref str))
            {
                lines++;
            }

            sw.Stop();

            file.Close();

            var info = new FileInfo(oneHoundredMill);
            var size = info.Length / 1024.0f / 1024.0f;
            var speed = size / sw.ElapsedMilliseconds * 1000.0f;

            Console.WriteLine($"lines count: {lines} | time: {sw.ElapsedMilliseconds} ms | speed: {speed} MBs");
        }
    }

    static void Run(string path)
    {
        Stopwatch sw = Stopwatch.StartNew();

        RowReader reader = new();

        reader.CalcAverage(oneBill);

        sw.Stop();

        var info = new FileInfo(oneHoundredMill);
        var size = info.Length / 1024.0f / 1024.0f;
        var speed = size / sw.ElapsedMilliseconds * 1000.0f;

        Console.WriteLine($"time: {sw.ElapsedMilliseconds} ms | speed: {speed} MBs");
    }

    static void Main(string[] args)
    {
        Run(oneMill);

        //Console.WriteLine($"\ndone: {sw.ElapsedMilliseconds} ms");

        //TestLineReader();
    }
}
