namespace GameServer
{
    /// <summary>
    /// Handles the creation and management of server and client threads.
    /// </summary>
    public static class Threader
    {
        public enum ServerMode 
        { 
            StartServer, 
            RunSiteManager, 
            RunCaravanManager, 
            ListenToConsole 
        }

        public enum ClientMode 
        { 
            ListenToClient, 
            SendDataToClient, 
            MonitorClientHealth, 
            MonitorKAFlag 
        }

        /// <summary>
        /// Generates and starts a task for the specified server mode.
        /// </summary>
        /// <param name="mode">The server mode to run.</param>
        /// <returns>A Task representing the operation.</returns>
        public static Task GenerateServerThread(ServerMode mode)
        {
            try
            {
                return mode switch
                {
                    ServerMode.StartServer => Task.Run(Network.ReadyServer),
                    ServerMode.RunSiteManager => Task.Run(SiteManager.StartSiteTicker),
                    ServerMode.RunCaravanManager => Task.Run(CaravanManager.StartCaravanTicker),
                    ServerMode.ListenToConsole => Task.Run(ServerCommandManager.ListenForServerCommands),
                    _ => throw new NotImplementedException($"The server mode {mode} is not implemented."),
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in GenerateServerThread: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates and starts a task for the specified client mode.
        /// </summary>
        /// <param name="Listener">The Listener object associated with the client.</param>
        /// <param name="mode">The client mode to run.</param>
        /// <returns>A Task representing the operation.</returns>
        public static Task GenerateClientThread(Listener Listener, ClientMode mode)
        {
            try
            {
                return mode switch
                {
                    ClientMode.ListenToClient => Task.Run(Listener.Listen),
                    ClientMode.SendDataToClient => Task.Run(Listener.SendData),
                    ClientMode.MonitorClientHealth => Task.Run(Listener.CheckConnectionHealth),
                    ClientMode.MonitorKAFlag => Task.Run(Listener.CheckKAFlag),
                    _ => throw new NotImplementedException($"The client mode {mode} is not implemented."),
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in GenerateClientThread: {ex.Message}");
                throw;
            }
        }

        internal static void GenerateClientThread(Listener listener, object sender)
        {
            throw new NotImplementedException();
        }

        public enum DiscordMode { Start, Console, Count }

        public static Task GenerateDiscordThread(DiscordMode mode)
        {
            return mode switch
            {
                DiscordMode.Start => Task.Run(DiscordManager.TryStartDiscordIntegration),
                DiscordMode.Console => Task.Run(DiscordManager.LoopMessagesToConsoleChannel),
                DiscordMode.Count => Task.Run(DiscordManager.LoopUpdatePlayerCount),
                _ => throw new NotImplementedException(),
            };
        }
    }
}