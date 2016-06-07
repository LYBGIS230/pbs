using PBS.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PBS.DataSource
{
    public class TaskDirectory
    {
        private int startRow;
        private int endRow;
        private int startCol;
        private int endCol;
        private int level;
        private byte[][] buffer;
        private int[] offsets;
        private int dataLength;
        private List<int> wrotePages;
        private int rowCount;
        private int colCount;
        private int lastSegment;
        private int validCount;
        public TaskDirectory(int level, int minRow, int maxRow, int minCol, int maxCol)
        {
            buffer = new byte[2][];
            buffer[0] = new byte[4096];
            buffer[1] = new byte[4096];
            offsets = new int[2];
            offsets[0] = 0;
            offsets[1] = -1;
            validCount = 0;
            startRow = minRow;
            endRow = maxRow;
            startCol = minCol;
            endCol = maxCol;
            this.level = level;
            rowCount = maxRow - minRow + 1;
            colCount = maxCol - minCol + 1;
            int totalCount = rowCount * colCount;
            int tableLength = totalCount % 8 == 0 ? totalCount / 8 : totalCount / 8 + 1;
            dataLength = tableLength + 28;
            lastSegment = dataLength / 4096 * 4096;
            buffer[0][0] = 0xFF;
            buffer[0][1] = (byte)level;
            Array.Copy(IOProxy.getInst().l2b(dataLength, 4), 0, buffer[0], 2, 4);
            Array.Copy(IOProxy.getInst().l2b(minRow, 4), 0, buffer[0], 6, 4);
            Array.Copy(IOProxy.getInst().l2b(maxRow, 4), 0, buffer[0], 10, 4);
            Array.Copy(IOProxy.getInst().l2b(minCol, 4), 0, buffer[0], 14, 4);
            Array.Copy(IOProxy.getInst().l2b(maxCol, 4), 0, buffer[0], 18, 4);
            int fileOffset = IOProxy.getInst().recommendOffset();
            IOProxy.getInst().setLevelOffset(level, fileOffset, dataLength);
            wrotePages = new List<int>();
        }
        public void setValid(int row, int col)
        {
            if(row < startRow || row > endRow || col < startCol || col > endCol)
            {
                throw new Exception("invalid row col param");
            }
            int offsetRow = row - startRow;
            int offsetCount = offsetRow * colCount + (col - startCol);
            setBit(true, offsetCount);
        }
        public void Finish()
        {
            LoadSegment(0);
            int currentBuffer = offsets[0] >= 0 ? 0 : 1;
            Array.Copy(IOProxy.getInst().l2b(validCount, 4), 0, buffer[currentBuffer], 22, 4);
            WriteSegment(currentBuffer);
        }
        private void WriteSegment(int currentBuffer)
        {
            if (lastSegment == offsets[currentBuffer])
            {
                buffer[currentBuffer][dataLength - lastSegment - 2] = 0xFF;
                buffer[currentBuffer][dataLength - lastSegment - 1] = 0xFE;
                IOProxy.getInst().WriteSegment(level, offsets[currentBuffer], buffer[currentBuffer], dataLength - lastSegment);
            }
            else
            {
                IOProxy.getInst().WriteSegment(level, offsets[currentBuffer], buffer[currentBuffer], 4096);
            }
            if (!wrotePages.Contains(offsets[currentBuffer]))
            {
                wrotePages.Add(offsets[currentBuffer]);
            }
            offsets[currentBuffer] = -1;
        }
        private void LoadSegment(int offset)
        {
            int currentBuffer = offsets[0] >= 0 ? 0 : 1;
            if (offset - offsets[currentBuffer] < 0 || offset - offsets[currentBuffer] >= 4096)
            {
                int segmentStart = offset / 4096 * 4096;
                if (wrotePages.Contains(segmentStart))
                {
                    if (lastSegment == segmentStart)
                    {
                        IOProxy.getInst().LoadSegment(level, segmentStart, buffer[1 - currentBuffer], dataLength - lastSegment);
                    }
                    else
                    {
                        IOProxy.getInst().LoadSegment(level, segmentStart, buffer[1 - currentBuffer], 4096);
                    }
                }
                else
                {
                    Array.Clear(buffer[1 - currentBuffer], 0, 4096);
                }
                offsets[1 - currentBuffer] = segmentStart;
                if (offsets[currentBuffer] >= 0)
                {
                    WriteSegment(currentBuffer);
                }
            }
        }
        public void setBit(bool value, int offset)
        {
            int bitOffset = offset % 8;
            int absoluteOffset = offset / 8 + 26;
            LoadSegment(absoluteOffset);
            int currentBuffer = offsets[0] >= 0 ? 0 : 1;
            byte orginValue = buffer[currentBuffer][absoluteOffset - offsets[currentBuffer]];
            byte modifier = (byte)(0x1 << (7 - bitOffset));
            if (value)
            {
                orginValue = (byte)(orginValue | modifier);
                validCount++;
            }
            else
            {
                orginValue = (byte)(orginValue & (~modifier));
            }
            buffer[currentBuffer][absoluteOffset - offsets[currentBuffer]] = orginValue;
        }
    }
    public class IOProxy
    {
        private int allocatableOffset = 100;
        public int recommendOffset()
        {
            return allocatableOffset;
        }
        public void setLevelOffset(int level, int offset, int levelLength)
        {
            WriteInt(writer, level * 4, offset, 4);
            allocatableOffset += levelLength;
        }
        public int b2I(byte[] b)
        {
            int byteSize = b.Length;
            int result = (int)b[0];
            for (int i = 0; i < byteSize; i++)
            {
                result = result | (b[i] << (i * 8));
            }
            return result;
        }
        public byte[] l2b(long num, int byteLengh)
        {
            byte[] result = new byte[byteLengh];
            for (int i = 0; i < byteLengh; i++)
            {
                result[i] = (byte)(num >> (i * 8));
            }
            return result;
        }
        private int ReadInt(BinaryReader stream, int offset, int byteSize)
        {
            int result = 0;
            byte[] b = new byte[byteSize];
            stream.BaseStream.Seek(offset, SeekOrigin.Begin);
            stream.Read(b, 0, byteSize);
            result = b2I(b);
            return result;
        }
        private void WriteInt(BinaryWriter stream, int offset, int toWriteVale, int byteSize)
        {
            byte[] b = l2b(toWriteVale, byteSize);
            stream.BaseStream.Seek(offset, SeekOrigin.Begin);
            stream.Write(b, 0, byteSize);
        }
        FileStream stream;
        BinaryReader reader;
        BinaryWriter writer;
        public void LoadSegment(int level, int offset, byte[] buffer, int length)
        {
            int fileOffset = ReadInt(reader, level * 4, 4);
            fileOffset = fileOffset + offset;
            reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
            reader.Read(buffer, 0, length);
        }
        public void WriteSegment(int level, int offset, byte[] buffer, int length)
        {
            int fileOffset = ReadInt(reader, level * 4, 4);
            fileOffset = fileOffset + offset;
            writer.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
            writer.Write(buffer, 0, length);
            
        }
        private IOProxy()
        {
            stream = File.Create("task.bin");
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        public void Dispose()
        {
            reader.Close();
            reader.Dispose();
            writer.Close();
            writer.Dispose();
            stream.Close();
            stream.Dispose();
        }
        private static IOProxy _inst;
        public static IOProxy getInst()
        {
            if(_inst == null)
            {
                _inst = new IOProxy();
            }
            return _inst;
        }
    }
}
