using GameClient.Dialogs;
using GameClient.Misc;
using TCPNetwork.Packets;
using Shared;
using static Shared.CommonEnumerators;

namespace GameClient.PacketManagers
{
    public static class PM_ResponseShortcuts
    {
        [HandlesPacket(PacketHeader.ResponseShortcutManager)]
        private static void ParsePacket(byte[] bytes)
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