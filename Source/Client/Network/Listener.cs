using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace GameClient
{
    // Class that handles all incoming and outgoing packet instructions
    public class Listener : IDisposable
    {
        // TCP variables needed for the Listener to communicate
        public TcpClient Connection { get; private set; }
        public NetworkStream NetworkStream { get; private set; }

        // Stream tools used to read and write the connection stream
        private StreamWriter _streamWriter;
        private StreamReader _streamReader;

        // Upload and download managers to send/receive files
        public UploadManager UploadManager { get; private set; }
        public DownloadManager DownloadManager { get; private set; }

        // Data queue used to hold packets that are to be sent through the connection
        private readonly Queue<Packet> _dataQueue = new Queue<Packet>();

        // Useful variables to handle connection status
        public bool DisconnectFlag { get; private set; }

        public Listener(TcpClient connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            NetworkStream = Connection.GetStream();
            _streamWriter = new StreamWriter(NetworkStream) { AutoFlush = true };
            _streamReader = new StreamReader(NetworkStream);
        }

        // Method to set the UploadManager
        public void SetUploadManager(UploadManager uploadManager)
        {
            UploadManager = uploadManager;
        }

        // Method to set the DownloadManager
        public void SetDownloadManager(DownloadManager downloadManager)
        {
            DownloadManager = downloadManager;
        }

        // Enqueues a new packet into the data queue if needed
        public void EnqueuePacket(Packet packet)
        {
            if (!DisconnectFlag)
            {
                lock (_dataQueue)
                {
                    _dataQueue.Enqueue(packet);
                }
            }
        }

        // Runs in a separate thread and sends all queued packets through the connection
        public void SendData()
        {
            try
            {
                while (!DisconnectFlag)
                {
                    Thread.Sleep(1);

                    if (_dataQueue.Count > 0)
                    {
                        Packet packet;
                        lock (_dataQueue)
                        {
                            packet = _dataQueue.Dequeue();
                        }
                        _streamWriter.WriteLine(Serializer.SerializeToString(packet));
                    }
                }
            }
            catch
            {
                DisconnectFlag = true;
            }
        }

        // Runs in a separate thread and listens for any kind of information being sent through the connection
        public void Listen()
        {
            try
            {
                while (!DisconnectFlag)
                {
                    Thread.Sleep(1);

                    string data = _streamReader.ReadLine();
                    if (data != null)
                    {
                        Packet receivedPacket = Serializer.SerializeFromString<Packet>(data);
                        PacketHandler.HandlePacket(receivedPacket);
                    }
                }
            }
            catch (Exception e)
            {
                if (ClientValues.verboseBool)
                {
                    Logger.Warning($"{e}");
                }

                DisconnectFlag = true;
            }
        }

        // Runs in a separate thread and checks if the connection should still be up
        public void CheckConnectionHealth()
        {
            try
            {
                while (!DisconnectFlag)
                {
                    Thread.Sleep(1);
                }
            }
            catch
            {
                // Handle exception if necessary
            }

            Thread.Sleep(1000);

            Master.threadDispatcher.Enqueue(() => Network.DisconnectFromServer());
        }

        // Runs in a separate thread and sends alive pings towards the server
        public void SendKAFlag()
        {
            try
            {
                while (!DisconnectFlag)
                {
                    Thread.Sleep(1000);

                    KeepAliveData keepAliveData = new KeepAliveData();
                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.KeepAlivePacket), keepAliveData);
                    EnqueuePacket(packet);
                }
            }
            catch
            {
                // Handle exception if necessary
            }
        }

        // Forcefully ends the connection with the server and any important process associated with it
        public void DestroyConnection()
        {
            DisconnectFlag = true;
            UploadManager?.Dispose();
            DownloadManager?.Dispose();
            Connection.Close();
        }

        // Properly dispose of resources
        public void Dispose()
        {
            DestroyConnection();
            _streamWriter?.Dispose();
            _streamReader?.Dispose();
            NetworkStream?.Dispose();
            Connection?.Dispose();
        }

        internal void SetDisconnectFlag(bool v)
        {
            throw new NotImplementedException();
        }
    }
}