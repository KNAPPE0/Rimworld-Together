using System;
using System.IO;

namespace Shared
{
    public class DownloadManager : IDisposable
    {
        private FileStream fileStream = new FileStream(Path.GetTempFileName(), FileMode.Create); // Initialized to avoid null
        public string FilePath { get; private set; } = string.Empty; // Non-nullable, initialized to an empty string
        public double FileSize { get; private set; } = 0;
        public double FileParts { get; private set; } = 1; // Default to 1 to avoid division by zero
        public bool IsLastPart { get; private set; } = false;

        public void PrepareDownload(string filePath, double fileParts)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (fileParts <= 0)
                throw new ArgumentOutOfRangeException(nameof(fileParts));

            FilePath = filePath;
            FileParts = fileParts;
            fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        }

        public void WriteFilePart(byte[] partBytes)
        {
            if (partBytes.Length == 0)
                throw new ArgumentException("The byte array cannot be empty.", nameof(partBytes));

            fileStream.Write(partBytes, 0, partBytes.Length);
            fileStream.Flush();
        }

        public void FinishFileWrite()
        {
            fileStream.Close();
            fileStream.Dispose();
        }

        public void Dispose()
        {
            FinishFileWrite();
        }
    }
}