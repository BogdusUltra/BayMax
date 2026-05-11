using BayMax.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace BayMax.Services
{
    public class DiscoveryService
    {
        //private UdpClient _udpClient;
        private readonly int _port;
        private CancellationTokenSource _cts;

        public class AgentBeacon
        {
            //public string type { get; set; }
            public string agent_name { get; set; }
            public int zmq_port { get; set; }
            public string public_key { get; set; }
            public string ip_address { get; set; }
        }

        public event Action<List<AgentBeacon>> OnScanComplete;

        public DiscoveryService(int port = 5001)
        {
            _port = port;
        }

        public void StartPeriodicScan(int intervalSeconds = 5)
        {
            Stop();
            _cts = new CancellationTokenSource();
            Task.Run(() => ScanLoop(intervalSeconds, _cts.Token));
            LoggerService.Log($"[РАДАР] Запущен. Интервал: {intervalSeconds} сек.", LogLevel.Info);
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task ScanLoop(int intervalSeconds, CancellationToken token)
        {
            using var udpClient = new UdpClient(_port);
            udpClient.EnableBroadcast = true;

            while (!token.IsCancellationRequested)
            {
                var discoveredBeacons = new Dictionary<string, AgentBeacon>();

                var listenTask = ListenForBeaconsAsync(udpClient, discoveredBeacons, token);
                await Task.WhenAny(listenTask, Task.Delay(1500, token));

                if (discoveredBeacons.Count > 0)
                {
                    OnScanComplete?.Invoke(new List<AgentBeacon>(discoveredBeacons.Values));
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token);
            }
        }

        private async Task ListenForBeaconsAsync(UdpClient udpClient, Dictionary<string, AgentBeacon> beacons, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await udpClient.ReceiveAsync();
                    string json = Encoding.UTF8.GetString(result.Buffer);

                    var beacon = JsonSerializer.Deserialize<AgentBeacon>(json);
                    if (beacon != null)
                    {
                        beacon.ip_address = result.RemoteEndPoint.Address.ToString();

                        beacons[beacon.ip_address] = beacon;
                    }
                }
            }
            catch { /* Игнорируем ошибки таймаута сокетов */ }
        }
    }
}
