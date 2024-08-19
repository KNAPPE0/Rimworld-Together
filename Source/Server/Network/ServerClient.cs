using System;
using System.Net;
using System.Net.Sockets;

namespace GameServer
{
    // Class object for the client connecting into the server. Contains all important data about it
    [Serializable]
    public class ServerClient
    {
        // PascalCase property
        public UserFile UserFile { get; private set; } = new UserFile();

        // camelCase property for backward compatibility
        public UserFile userFile
        {
            get => UserFile;
            set => UserFile = value;
        }

        [NonSerialized]
        public Listener Listener;

        [NonSerialized]
        public ServerClient InVisitWith;

        public ServerClient(TcpClient tcp)
        {
            if (tcp == null)
            {
                throw new ArgumentNullException(nameof(tcp), "TcpClient cannot be null");
            }
            else
            {
                UserFile.SavedIP = ((IPEndPoint)tcp.Client.RemoteEndPoint).Address.ToString();
            }
        }

        public void LoadFromUserFile()
        {
            UserFile = UserManager.GetUserFile(this) ?? throw new InvalidOperationException("Failed to load user file.");
        }
    }
}