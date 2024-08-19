using System;
using System.IO;

namespace Shared
{
    public class UploadManager : IDisposable
    {
        private FileStream? fileStream = null;  // Nullable type, to avoid null reference warnings
        private FileInfo? fileInfo = null;      // Nullable type, to avoid null reference warnings

        public string FilePath { get; private set; } = string.Empty;
        public string FileName { get; private set; } = string.Empty;
        public double FileSize { get; private set; } = 0;
        public double FileParts { get; private set; } = 0;

        private readonly double partSize = 262144;
        public bool IsLastPart { get; private set; } = false;

        public void PrepareUpload(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            FilePath = filePath;

            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            fileInfo = new FileInfo(filePath);

            FileName = Path.GetFileName(filePath);
            FileSize = fileInfo.Length;
            FileParts = Math.Ceiling(fileInfo.Length / partSize);
        }

        public byte[] ReadFilePart()
        {
            if (fileStream == null || fileInfo == null)
                throw new InvalidOperationException("FileStream or FileInfo is not initialized. Call PrepareUpload first.");

            double bytesToRead = fileStream.Position + partSize <= fileInfo.Length 
                ? partSize 
                : fileInfo.Length - fileStream.Position;

            IsLastPart = fileStream.Position + bytesToRead >= fileInfo.Length;

            byte[] toReturn = new byte[(int)bytesToRead];
            fileStream.Read(toReturn, 0, (int)bytesToRead);

            return toReturn;
        }

        public void FinishFileWrite()
        {
            fileStream?.Close();
            fileStream?.Dispose();
            fileStream = null;
            fileInfo = null; // Clear references to avoid potential null warnings
        }

        public void Dispose()
        {
            FinishFileWrite();
        }
    }
}