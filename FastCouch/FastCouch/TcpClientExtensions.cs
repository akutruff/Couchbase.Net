using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace FastCouch
{
    public static class TcpClientExtensions
    {
        public static void SafeClose(this TcpClient tcpClient)
        {
            try
            {
                tcpClient.GetStream().Close();
            }
            catch
            {}

            try
            {
                tcpClient.Close();
            }
            catch
            {}
        }
    }
}
