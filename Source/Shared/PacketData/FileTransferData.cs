using System;

namespace Shared
{
    [Serializable]
    public class FileTransferData
    {
        // Size of the file being transferred in bytes
        public double FileSize { get; set; } = 0.0; // Default to 0.0, representing an uninitialized file size

        // Number of parts the file is divided into
        public double FileParts { get; set; } = 0.0; // Default to 0.0, representing no parts

        // Byte array containing the file data
        public byte[] FileBytes { get; set; } = new byte[0]; // Initialize with an empty byte array

        // Indicator if this is the last part of the file
        public bool IsLastPart { get; set; } = false; // Default to false

        // Instructions or metadata associated with the file transfer
        public int Instructions { get; set; } = -1; // Default to -1, indicating no specific instructions
    }
}