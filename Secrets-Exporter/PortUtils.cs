using System.Net;
using System.Net.Sockets;

namespace Secrets_Exporter;

public static class PortUtils
{
    public static int GetFirstAvailablePort(int startPort, int endPort)
    {
        for (var port = startPort; port <= endPort; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }
        
        throw new InvalidOperationException("No available ports in the specified range");
    }

    public static bool IsPortAvailable(int port)
    {
        try
        {
            using var tcpListener = new TcpListener(IPAddress.Loopback, port);
            
            tcpListener.Start();
            tcpListener.Stop();
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}