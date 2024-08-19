using Shared;
using System.Net.Sockets;

namespace GameServer
{
    // Class that handles all incoming and outgoing packet instructions
    public class Listener
    {
        // TCP variables needed for the Listener to communicate
        public TcpClient connection;
        public NetworkStream networkStream;

        // Stream tools used to read and write the connection stream
        private StreamWriter streamWriter;
        private StreamReader streamReader;

        // Upload and download classes to send/receive files
        public UploadManager uploadManager = new UploadManager();
        public DownloadManager downloadManager = new DownloadManager();

        // Data queue used to hold packets that are to be sent through the connection
        private readonly Queue<Packet> dataQueue = new Queue<Packet>();
        private readonly object queueLock = new object();

        // Useful variables to handle connection status
        public bool disconnectFlag = false;
        public bool KAFlag = false;

        // Reference to the ServerClient instance of this Listener
        private readonly ServerClient targetClient;

        public Listener(ServerClient clientToUse, TcpClient connection)
        {
            targetClient = clientToUse ?? throw new ArgumentNullException(nameof(clientToUse));
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            networkStream = connection.GetStream();
            streamWriter = new StreamWriter(networkStream);
            streamReader = new StreamReader(networkStream);
        }

        // Enqueues a new packet into the data queue if needed
        public void EnqueuePacket(Packet packet)
        {
            if (disconnectFlag) return;

            lock (queueLock)
            {
                dataQueue.Enqueue(packet);
            }
        }

        // Runs in a separate thread and sends all queued packets through the connection
        public void SendData()
        {
            try
            {
                while (!disconnectFlag)
                {
                    Packet packet = null;

                    lock (queueLock)
                    {
                        if (dataQueue.Count > 0)
                        {
                            packet = dataQueue.Dequeue();
                        }
                    }

                    if (packet != null)
                    {
                        streamWriter.WriteLine(Serializer.SerializeToString(packet));
                        streamWriter.Flush();
                    }

                    Thread.Sleep(10); // Adjust sleep time to reduce CPU usage
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Listener] > Error in SendData: {ex.Message}");
                disconnectFlag = true;
            }
        }

        // Runs in a separate thread and listens for any kind of information being sent through the connection
        public void Listen()
        {
            try
            {
                while (!disconnectFlag)
                {
                    string data = streamReader.ReadLine();
                    if (string.IsNullOrEmpty(data)) continue;

                    Packet receivedPacket = Serializer.SerializeFromString<Packet>(data);
                    PacketHandler.HandlePacket(targetClient, receivedPacket);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Listener] > Error in Listen: {ex.Message}");
                disconnectFlag = true;
            }
        }

        // Runs in a separate thread and checks if the connection should still be up
        public void CheckConnectionHealth()
        {
            try
            {
                while (!disconnectFlag)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Listener] > Error in CheckConnectionHealth: {ex.Message}");
            }
            finally
            {
                Network.KickClient(targetClient);
            }
        }

        // Runs in a separate thread and checks if the connection is still alive
        public void CheckKAFlag()
        {
            KAFlag = false;

            try
            {
                while (!disconnectFlag)
                {
                    Thread.Sleep(int.Parse(Master.serverConfig.MaxTimeoutInMS));

                    if (KAFlag) KAFlag = false;
                    else break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Listener] > Error in CheckKAFlag: {ex.Message}");
            }
            finally
            {
                disconnectFlag = true;
            }
        }

        // Forcefully ends the connection with the client and any important process associated with it
        public void DestroyConnection()
        {
            try
            {
                connection.Close();
                uploadManager?.Dispose();
                downloadManager?.Dispose();
                if (targetClient.InVisitWith != null)
                {
                    OnlineActivityManager.SendVisitStop(targetClient);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Listener] > Error in DestroyConnection: {ex.Message}");
            }
        }
    }
}