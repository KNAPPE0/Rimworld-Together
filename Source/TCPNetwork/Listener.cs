using Shared;
using Shared.Misc;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.Misc.Printer;

namespace TCPNetwork
{
    public class Listener
    {
        public ServerClient TargetClient { get; private set; } = null;

        private TcpClient Connection { get; set; } = null;

        private NetworkStream Stream { get; set; } = null;

        private NetworkRuleset Ruleset { get; set; } = null;

        // Queue of fully-built outbound packets. The header is already inside PKT_Base.Header,
        // so we don't need a separate byte key (the previous KeyValuePair<byte, PKT_Base> wrapper
        // duplicated that data and allocated a kvp per Enqueue).
        private ConcurrentQueue<PKT_Base> PacketQueue { get; set; } = new ConcurrentQueue<PKT_Base>();

        // Per-listener reusable scratch for the header byte and the 4-byte length, so the
        // tight Read/Write loop doesn't allocate two arrays per packet just to hold 5 bytes.
        private readonly byte[] _readHeaderBuf = new byte[1];
        private readonly byte[] _readLenBuf = new byte[sizeof(int)];
        private readonly byte[] _writeHeaderBuf = new byte[1];
        private readonly byte[] _writeLenBuf = new byte[sizeof(int)];

        private bool IsDisconnecting { get; set; } = false;

        private DateTime LastKAReceivedPacket { get; set; } = DateTime.Now;

        private DateTime LastKASentPacket { get; set; } = DateTime.Now;

        public Listener(ServerClient clientToUse, TcpClient connection, NetworkRuleset ruleset)
        {
            this.Connection = connection;
            this.TargetClient = clientToUse;
            this.Stream = connection.GetStream();
            this.Ruleset = ruleset;

            Ruleset.OnConnect?.Invoke(clientToUse);
            Task.Run(RunAllListenerTasks);
        }

        private void RunAllListenerTasks()
        {
            while (true)
            {
                Thread.Sleep(1);

                try
                {
                    CheckKAFlag();
                    SendKAFlag();

                    Read();
                    Write();

                    if (IsDisconnecting) break;
                }

                catch (Exception ex)
                {
                    Printer.Warning(ex, LogImportanceMode.Extreme);
                    break;
                }
            }

            Disconnect();
        }

        public void EnqueuePacket(PacketHeader header, object obj)
        {
            if (IsDisconnecting) return;
            if (!obj.GetType().IsSubclassOf(typeof(PKT_Base))) { Printer.Error($"Malformed package {obj.GetType()}"); return; }

            PKT_Base packet = new PKT_Base();
            packet.Header = header;
            packet.MainThread = false;
            packet.Contents = Serializer.ConvertObjectToBytes(obj);

            PacketQueue.Enqueue(packet);
        }

        private void Read()
        {
            if (!Stream.DataAvailable) return;

            // 1) Header byte (PacketHeader is byte-sized).
            Stream.Read(_readHeaderBuf, 0, 1);
            PacketHeader header = (PacketHeader)_readHeaderBuf[0];

            // 2) Length prefix.
            Stream.Read(_readLenBuf, 0, sizeof(int));
            if (!Network.CheckForPacketSize(TargetClient, _readLenBuf)) return;

            // 3) Body. Previous code allocated a 4-byte buffer first and then threw it away.
            byte[] packetBuffer = new byte[BitConverter.ToInt32(_readLenBuf, 0)];
            Network.ReadFullPacket(Stream, packetBuffer);

            if (!Network.IgnoreLogPackets.Contains(header)) Printer.Message($"[Packet] > Received packet {header}", LogImportanceMode.Verbose);
            else Printer.Message($"[Packet] > Received packet {header}", LogImportanceMode.Extreme);

            if (Network.CheckIfPacketIsValidated(TargetClient, header)) Ruleset.OnRead?.Invoke(header, packetBuffer, TargetClient);
            else return;

            LastKAReceivedPacket = DateTime.Now;
        }

        private void Write()
        {
            while (PacketQueue.Count > 0)
            {
                if (!PacketQueue.TryDequeue(out PKT_Base packet)) return;

                _writeHeaderBuf[0] = (byte)packet.Header;
                Stream.Write(_writeHeaderBuf, 0, 1);

                // In-place length encoding — was a fresh BitConverter.GetBytes() each call.
                int len = packet.Contents.Length;
                _writeLenBuf[0] = (byte)(len & 0xFF);
                _writeLenBuf[1] = (byte)((len >> 8) & 0xFF);
                _writeLenBuf[2] = (byte)((len >> 16) & 0xFF);
                _writeLenBuf[3] = (byte)((len >> 24) & 0xFF);
                Stream.Write(_writeLenBuf, 0, sizeof(int));

                Stream.Write(packet.Contents, 0, packet.Contents.Length);

                if (!Network.IgnoreLogPackets.Contains(packet.Header)) Printer.Message($"[Packet] > Sent packet {packet.Header}", LogImportanceMode.Verbose);
                else Printer.Message($"[Packet] > Sent packet {packet.Header}", LogImportanceMode.Extreme);

                Ruleset.OnWrite?.Invoke(TargetClient);
            }
        }

        private void SendKAFlag()
        {
            if (!Ruleset.HandleKeepAlive) return;
            if (DateTime.Now - LastKASentPacket < Network.KeepAliveInterval) return;

            LastKASentPacket = DateTime.Now;
            EnqueuePacket(PacketHeader.KeepAliveManager, new PKT_KeepAlive());
        }

        private void CheckKAFlag()
        {
            // 6× the keepalive interval before we give up on the client.
            if (DateTime.Now - LastKAReceivedPacket >= TimeSpan.FromTicks(Network.KeepAliveInterval.Ticks * 6))
                MarkForDisconnect();
        }

        public void MarkForDisconnect(bool sendDisconnectPacket = true)
        {
            if (sendDisconnectPacket)
            {
                try
                {
                    PKT_Disconnect packet = new PKT_Disconnect();
                    EnqueuePacket(PacketHeader.DisconnectManager, packet);
                }
                catch (Exception ex) { Printer.Error(ex); }
            }

            IsDisconnecting = true;
        }

        private void Disconnect()
        {
            Stream.Dispose();
            Connection.Dispose();
            Ruleset.OnDisconnect?.Invoke(TargetClient);
        }
    }
}
