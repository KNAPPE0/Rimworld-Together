namespace TCPNetwork.Packets
{
    public class PKT_Version : PKT_Base
    {
        public enum VersionStep { Ask, Pass }

        public VersionStep _step { get; set; } = VersionStep.Ask;

        public string _version { get; set; } = string.Empty;
    }
}