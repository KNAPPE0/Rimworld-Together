using System;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_ResponseShortcut : PKT_Base
    {
        public ResponseStepMode _stepMode { get; set; } = ResponseStepMode.IllegalAction;
    }
}