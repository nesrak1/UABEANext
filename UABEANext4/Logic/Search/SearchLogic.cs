using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UABEANext4.Logic.Search;
internal class SearchLogic
{
    public static IEnumerable<long> FindAllSubstringsInStream(Stream fs, byte[] patternBytes)
    {
        const int ChunkSize = 65536;

        int patternLength = patternBytes.Length;
        int overlap = patternLength > 1 ? patternLength - 1 : 0;

        byte[] buffer = new byte[ChunkSize];
        long currentPosition = 0;
        int bytesRead;

        fs.Position = 0;
        while ((bytesRead = fs.Read(buffer, 0, ChunkSize)) > 0)
        {
            int indexInChunk;
            int searchStart = 0;
            while ((indexInChunk = IndexOfBytes(buffer, patternBytes, searchStart)) != -1)
            {
                long absolutePosition = currentPosition + indexInChunk;

                yield return absolutePosition;

                searchStart = indexInChunk + patternLength;

                if (searchStart >= bytesRead)
                {
                    break;
                }
            }

            if (bytesRead == ChunkSize && fs.Position < fs.Length)
            {
                fs.Seek(-overlap, SeekOrigin.Current);
            }

            currentPosition += bytesRead - overlap;
        }
    }

    public static int IndexOfBytes(byte[] buffer, byte[] pattern, int start = 0)
    {
        if (buffer == null || pattern == null || pattern.Length == 0) return -1;
        if (start < 0 || start > buffer.Length - pattern.Length) return -1;

        var span = buffer.AsSpan(start);
        for (int i = 0; i <= span.Length - pattern.Length; i++)
        {
            if (span.Slice(i, pattern.Length).SequenceEqual(pattern))
                return i + start;
        }
        return -1;
    }
}
