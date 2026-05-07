using BayMax.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BayMax
{
    public class DiscoveryService
    {
        private UdpClient _udpClient;
        private readonly int _port;

        public event Action<AgentBeacon, string> OnDeviceDiscovered;

        public class AgentBeacon
        {
            public string type { get; set; }
            public string agent_name { get; set; }
            public int zmq_port { get; set; }
            public string public_key { get; set; }
        }

        public DiscoveryService(int port = 5001)
        {
            _port = port;
        }

        public void Start()
        {
            _udpClient = new UdpClient(_port);
            _udpClient.EnableBroadcast = true;
            _udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            LoggerService.Log($"[РАДАР] Запущен. Слушаю порт {_port}...", LogLevel.Info);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, _port);
                byte[] bytes = _udpClient.EndReceive(ar, ref ipEndPoint);
                string jsonMessage = Encoding.UTF8.GetString(bytes);

                var beacon = JsonSerializer.Deserialize<AgentBeacon>(jsonMessage);

                if (beacon != null && beacon.type == "beacon")
                {
                    OnDeviceDiscovered?.Invoke(beacon, ipEndPoint.Address.ToString());
                }

                _udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            }

            catch (ObjectDisposedException) { }

            catch (Exception ex)
            {
                LoggerService.Log($"[РАДАР] Ошибка парсинга пакета: {ex.Message}", LogLevel.Warning);
                _udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            }
        }

        public void Stop()
        {
            _udpClient?.Close();
            LoggerService.Log("[РАДАР] Остановлен.", LogLevel.Info);
        }
    }
}
