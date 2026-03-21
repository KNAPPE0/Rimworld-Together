using GameClient.Core.Configs;
using GameClient.Dialogs;
using GameClient.Files;
using GameClient.Managers;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using TCPNetwork;
using TCPNetwork.Files.Client;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Hooks.TCPNetwork
{
    public class ClientNetwork
    {
        private Action<PacketHeader, byte[], ServerClient> OnReadPacket { get; set; } = delegate (PacketHeader header, byte[] buffer, ServerClient client)
        {
            Thread.Sleep(250 * (int)ModConfigGetter.CurrentSimulatedLag);

            bool bypassReady =
                Network.BypassReadyPackets.Contains(header) ||
                header == PacketHeader.ModManager;

            if (!SessionHandler.IsReadyToPlay && !bypassReady) return;

            MainThreadHandler.Instance.Enqueue(delegate
            {
                MethodInfo method = (MethodInfo)MethodGatherer.ClientMethodDictionary[header][1];
                method.Invoke(MethodGatherer.ClientMethodDictionary[header][0], new object[] { client, buffer, header });
            });
        };

        private Action<ServerClient> OnWritePacket { get; set; } = delegate (ServerClient client) { };

        private Action<ServerClient> OnConnect { get; set; } = delegate
        {
            MainThreadHandler.Instance.Enqueue(delegate
            {
                HarmonyHandler.EnableMainPatches();
                OptionsProfileSessionManager.RequestServerOptionsProfile(isManual: false);
            });
        };

        private Action<ServerClient> OnDisconnect { get; set; } = delegate
        {
            MainThreadHandler.Instance.Enqueue(delegate
            {
                OptionsProfileSessionManager.TryRestoreOnDisconnect();

                DisconnectionManager.HandleDisconnect();
                MainThreadHandler.Instance.DoOnEndMethods();
                SessionHandler.CurrentNetworkState = ClientNetworkState.Disconnected;
                Printer.Warning("Disconnecting from server", LogImportanceMode.Verbose);
            });
        };

        public ClientNetwork()
        {
            Start();
        }

        private void Start()
        {
            if (TryConnect())
            {
                SessionHandler.CurrentNetworkState = ClientNetworkState.Connected;

                PersistentSettings settings = PersistentSettings.Load();
                settings.ServerSettings.Set(Network.Ip, Network.Port);
                settings.Save();

                Printer.Message("Connected to server");
            }
            else
            {
                DLG_Wait.Instance.Close();
                DLG_Message d1 = new DLG_Message("ERROR", new string[] { "The server did not respond in time" });
                DLG_Base.PushNewDialog(d1);
                OnDisconnect.Invoke(null);
            }
        }

        private bool TryConnect()
        {
            if (SessionHandler.CurrentNetworkState != ClientNetworkState.Disconnected)
                return false;

            try
            {
                TcpClient tcpClient = new TcpClient(Network.Ip, Network.Port);
                NetworkRuleset ruleset = new NetworkRuleset(OnConnect, OnDisconnect, OnReadPacket, OnWritePacket);
                Network.ServerEndpoint = new Listener(null, tcpClient, ruleset, Listener.ListenerMode.Client);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}