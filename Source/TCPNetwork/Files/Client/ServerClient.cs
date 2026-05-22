using Shared;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace TCPNetwork.Files.Client
{
    public class ServerClient
    {
        public string CurrentIP { get; set; } = string.Empty;

        public bool IsVerified { get; private set; } = false;

        public UserFile UserFile { get; set; } = null;

        public Listener Listener { get; set; } = null;

        public TcpClient Tcp { get; set; } = null;

        public NetworkRuleset Ruleset { get; set; } = null;

        public ServerClient(TcpClient tcp, NetworkRuleset ruleset, bool createListener = true)
        {
            if (tcp == null) return;
            Tcp = tcp;
            Ruleset = ruleset;
            CurrentIP = ((IPEndPoint)tcp.Client.RemoteEndPoint).Address.ToString();
            if (createListener) CreateListener();
        }

        public void CreateListener() { Listener = new Listener(this, Tcp, Ruleset); }

        public void DisposeTCP() { Tcp.Dispose(); }

        public void VerifyUser() { IsVerified = true; }

        public void LoadUserFromFile(ServerClient client)
        {
            // Case-insensitive match + per-file try/catch — a single corrupt file used to break login
            // for everyone (the deserialize throws and the loop dies before reaching the real user).
            string targetUsername = client.UserFile.Username;
            if (string.IsNullOrEmpty(targetUsername)) return;

            foreach (string userFile in Directory.GetFiles(CommonValues.ServerUsersPath))
            {
                UserFile file;
                try { file = Serializer.SerializeFromFile<UserFile>(userFile); }
                catch { continue; }

                if (file == null) continue;
                if (!string.Equals(file.Username, targetUsername, System.StringComparison.OrdinalIgnoreCase)) continue;

                UserFile = file;
                UserFile.UpdateIP(CurrentIP);
                return;
            }
        }
    }
}