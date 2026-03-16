using System;
using Shared.Files.Configs.Mods;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_ModConfig : PKT_Base
    {
        public ModConfigStepMode _stepMode { get; set; } = ModConfigStepMode.Send;

        public ModsConfigFile _configFile { get; set; } = new ModsConfigFile();

        public bool _requestOptionsProfile { get; set; } = false;
        public bool _uploadOptionsProfile { get; set; } = false;

        public bool _isOptionsProfileChunk { get; set; } = false;
        public bool _noOptionsProfileAvailable { get; set; } = false;

        public string _optionsProfileHash { get; set; } = string.Empty;
        public long _optionsProfileUpdatedUtcTicks { get; set; } = 0;

        public int _chunkIndex { get; set; } = 0;
        public int _chunkCount { get; set; } = 0;
        public byte[] _chunkBytes { get; set; } = Array.Empty<byte>();

        public override string ToString()
        {
            return $"PKT_ModConfig|{_stepMode}|Mods:{_configFile?.ModConfigs?.Count}|ReqProfile:{_requestOptionsProfile}|UploadProfile:{_uploadOptionsProfile}|Chunk:{_isOptionsProfileChunk} {(_chunkIndex + 1)}/{_chunkCount}";
        }
    }
}