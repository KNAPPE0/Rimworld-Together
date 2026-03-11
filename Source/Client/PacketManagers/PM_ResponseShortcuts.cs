using GameClient.Dialogs;
using GameClient.Misc;
using Shared;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameClient.PacketManagers
{
    public class PM_ResponseShortcuts : PM_Base
    {
        [HandlesPacket(PacketHeader.ResponseShortcutManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            ResponseShortcutData data = Serializer.ConvertBytesToObject<ResponseShortcutData>(bytes);

            switch (data._stepMode)
            {
                case CommonEnumerators.ResponseStepMode.IllegalAction:
                    DLG_Wait.Instance.Close();
                    DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "Kicked for ilegal actions!" }));
                    break;

                case CommonEnumerators.ResponseStepMode.UserUnavailable:
                    DLG_Wait.Instance.Close();
                    DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "Player is not currently available!" }));
                    break;

                case CommonEnumerators.ResponseStepMode.NoPower:
                    DLG_Wait.Instance.Close();
                    DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "You don't have enough power for this action!" }));
                    break;

                case CommonEnumerators.ResponseStepMode.Pop:
                    DLG_Wait.Instance.Close();
                    break;
            }
        }
    }
}