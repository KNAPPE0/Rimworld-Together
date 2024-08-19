using Mono.Nat;
using System.Threading.Tasks;

namespace GameServer
{
    // Class that handles UPnP forwarding between the server and the router
    public class UPnP
    {
        // Indicates if the port forwarding was successful
        public bool AutoPortForwardSuccessful { get; private set; } = false;

        public UPnP()
        {
            Logger.Warning($"[UPnP] > Attempting to forward port '{Network.port}'");

            // Subscribe to the DeviceFound event
            NatUtility.DeviceFound += DeviceFound;

            // Start the UPnP mapping process
            TryToMapPort();
        }

        // Attempts to map the port using UPnP
        public void TryToMapPort()
        {
            NatUtility.StartDiscovery();

            Task.Run(async () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(250); // Asynchronous wait for 250ms
                    if (AutoPortForwardSuccessful) break; // Exit loop if successful
                }

                if (!AutoPortForwardSuccessful)
                {
                    Logger.Error("[UPnP] > Could not enable UPnP. Possible causes:\n" +
                        "- The port is being used by another application\n" +
                        "- The router has UPnP disabled\n" +
                        "- The router/modem does not support UPnP or has no ports available");
                }
            });
        }

        // Event handler that gets triggered whenever a UPnP-capable device is found
        private void DeviceFound(object sender, DeviceEventArgs args)
        {
            try
            {
                INatDevice device = args.Device;
                device.CreatePortMap(new Mapping(Protocol.Tcp, Network.port, Network.port));

                if (!AutoPortForwardSuccessful)
                {
                    Logger.Warning("[UPnP] > Successfully port-forwarded the server.");
                    AutoPortForwardSuccessful = true;
                }

                Logger.Warning("[UPnP] > UPnP forward successful.");
            }
            catch (Exception e)
            {
                Logger.Error($"[UPnP] > Error occurred while attempting to forward port: {e.Message}");
            }
        }
    }
}