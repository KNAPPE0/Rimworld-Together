using System;
using Shared.Files.Configs.Mods;

namespace TCPNetwork.Packets
{
    public class PKT_ModConfig : PKT_Base
    {
        public ModConfigStepMode _stepMode { get; set; } = ModConfigStepMode.Send;

        public ModConfigFile _configFile { get; set; } = new ModConfigFile();

        public enum ModConfigStepMode { Send, Ask }

        // KMH: Options profile enforcement fields
        public bool _requestOptionsProfile { get; set; } = false;
        public bool _uploadOptionsProfile { get; set; } = false;

        public bool _isOptionsProfileChunk { get; set; } = false;
        public bool _noOptionsProfileAvailable { get; set; } = false;

        public string _optionsProfileHash { get; set; } = string.Empty;
        public long _optionsProfileUpdatedUtcTicks { get; set; } = 0;

        public int _chunkIndex { get; set; } = 0;
        public int _chunkCount { get; set; } = 0;
        public byte[] _chunkBytes { get; set; } = Array.Empty<byte>();
    }
}