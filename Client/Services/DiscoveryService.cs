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
            public ConnectionStatus Status { get; set; }
        }

        public event Action<List<AgentBeacon>> OnScanComplete;

        private readonly object _lock = new object();

        private readonly Dictionary<string, AgentBeacon> _discoveredBeacons = new Dictionary<string, AgentBeacon>();

        public DiscoveryService(int port = 5001)
        {
            _port = port;
        }

        public void StartPeriodicScan(int intervalSeconds = 2)
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

        public void RequestImmediateUpdate()
        {
            List<AgentBeacon> snapshot;
            lock (_lock)
            {
                snapshot = new List<AgentBeacon>(_discoveredBeacons.Values);
                lock (_lock) { _discoveredBeacons.Clear(); }
            }
            OnScanComplete?.Invoke(snapshot);
        }

        private async Task ScanLoop(int intervalSeconds, CancellationToken token)
        {
            using var udpClient = new UdpClient(_port);
            udpClient.EnableBroadcast = true;

            var listenTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {   
                        var result = await udpClient.ReceiveAsync();
                        string json = Encoding.UTF8.GetString(result.Buffer);

                        var beacon = JsonSerializer.Deserialize<AgentBeacon>(json);
                        if (beacon != null)
                        {
                            beacon.ip_address = result.RemoteEndPoint.Address.ToString();

                            lock (_lock)
                            {
                                _discoveredBeacons[beacon.ip_address] = beacon;
                            }
                        }
                    }
                    catch { }
                }
                
            }, token);

            try
            {
                while (!token.IsCancellationRequested)
                {  
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token);
                    RequestImmediateUpdate();
                }
            }
            catch (TaskCanceledException) { }

            udpClient.Close();
        
        }
    }
}
