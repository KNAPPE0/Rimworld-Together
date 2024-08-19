using System;
using System.Threading.Tasks;

namespace GameClient
{
    public static class Threader
    {
        public enum Mode { Listener, Sender, Health, KASender, Chat }

        public static Task GenerateThread(Mode mode)
        {
            return mode switch
            {
                Mode.Listener => Task.Run(Network.Listener.Listen),
                Mode.Sender => Task.Run(Network.Listener.SendData),
                Mode.Health => Task.Run(Network.Listener.CheckConnectionHealth),
                Mode.KASender => Task.Run(Network.Listener.SendKAFlag),
                Mode.Chat => Task.Run(ChatManager.ChatClock),
                _ => throw new NotImplementedException()
            };
        }
    }
}