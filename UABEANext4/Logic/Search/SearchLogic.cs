using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UABEANext4.Logic.Search;

internal class SearchLogic
{
    public static IEnumerable<long> FindAllSubstringsInStream(Stream fs, byte[] patternBytes, bool ignoreCase, Encoding encoding,string? patternStr = null)
    {
        if (!ignoreCase || string.IsNullOrEmpty(patternStr))
        {
            return FindBytesInStream(fs, patternBytes);
        }
        return FindTextInStream(fs, patternStr, encoding);
    }

    private static IEnumerable<long> FindTextInStream(Stream fs, string pattern, Encoding encoding)
    {
        int maxBytesPerChar = encoding.GetMaxByteCount(1);
        int overlapBytes = pattern.Length * maxBytesPerChar;

        const int bufferSize = 1024 * 512; // 512KB
        byte[] byteBuffer = new byte[bufferSize];

        fs.Position = 0;
        long lastFoundOffset = -1;

        while (true)
        {
            long chunkStartPos = fs.Position;
            int bytesRead = fs.Read(byteBuffer, 0, bufferSize);
            if (bytesRead <= 0) break;

            string chunkStr = encoding.GetString(byteBuffer, 0, bytesRead);

            int matchIdx = chunkStr.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            while (matchIdx != -1)
            {
                int byteOffsetInChunk = encoding.GetByteCount(chunkStr.Substring(0, matchIdx));
                long absoluteOffset = chunkStartPos + byteOffsetInChunk;

                if (absoluteOffset > lastFoundOffset)
                {
                    yield return absoluteOffset;
                    lastFoundOffset = absoluteOffset;
                }

                matchIdx = chunkStr.IndexOf(pattern, matchIdx + 1, StringComparison.OrdinalIgnoreCase);
            }

            if (bytesRead < bufferSize)
                break;

          
            long seekBack = Math.Min(overlapBytes, chunkStartPos + bytesRead);
            fs.Position = (chunkStartPos + bytesRead) - seekBack;

            if (fs.Position <= chunkStartPos && bytesRead > 0)
            {
                fs.Position = chunkStartPos + bytesRead;
            }
        }
    }
    private static IEnumerable<long> FindBytesInStream(Stream fs, byte[] pattern)
    {
        const int ChunkSize = 65536;

        byte[] buffer = new byte[ChunkSize];
        int patternLength = pattern.Length;
        int overlap = patternLength - 1;

        long currentPosition = 0;
        int bytesRead;

        fs.Position = 0;
        while ((bytesRead = fs.Read(buffer, 0, ChunkSize)) > 0)
        {
            int searchStart = 0;
            while (true)
            {
                int indexInChunk = IndexOfBytes(buffer, pattern, searchStart, bytesRead);
                if (indexInChunk == -1)
                {
                    break;
                }

                yield return currentPosition + indexInChunk;
                searchStart = indexInChunk + 1;
                if (searchStart > bytesRead - patternLength)
                {
                    break;
                }
            }

            if (bytesRead == ChunkSize)
            {
                fs.Seek(-overlap, SeekOrigin.Current);
                currentPosition += (bytesRead - overlap);
            }
            else
            {
                currentPosition += bytesRead;
            }
        }
    }

    public static int IndexOfBytes(byte[] buffer, byte[] pattern, int start, int length)
    {
        if (pattern.Length == 0)
        {
            return -1;
        }
        var source = buffer.AsSpan(start, length - start);
        int idx = source.IndexOf(pattern);
        return idx != -1 ? idx + start : -1;
    }

    public static int IndexOfBytes(byte[] buffer, byte[] pattern)
    {
       return IndexOfBytes(buffer, pattern, 0, buffer.Length);
    }
}