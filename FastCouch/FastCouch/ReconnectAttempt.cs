using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net.Sockets;

namespace FastCouch
{
    internal class ReconnectAttempt
    {
        public TcpClient TcpClient { get; private set; }
        public bool HasBeenCanceled { get; private set; }
        public Server Server { get; set; }
        private Timer _timer;
        public Timer Timer
        {
            get { return _timer; }
            set
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                }

                _timer = value;
            }
        }

        public ReconnectAttempt(TcpClient tcpClient, Server server)
        {
            TcpClient = tcpClient;
            Server = server;
        }

        public void Cancel()
        {
            if (!this.HasBeenCanceled)
            {
                this.HasBeenCanceled = true;

                this.Timer.Dispose();
                this.TcpClient.SafeClose();
            }
        }
    }
}
