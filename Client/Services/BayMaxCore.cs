using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Xml.Serialization;
using BayMax.Models;

namespace BayMax.Services
{
    public class BayMaxCore
    {
        private readonly DiscoveryService _discovery;
        private readonly LocalAgentManager _localAgent;
        private readonly SecurityService _security;

        private readonly Dictionary<string, BayMaxClient> _activeConnections = new Dictionary<string, BayMaxClient>();

        public ObservableCollection<Device> AvailableDevices { get; } = new ObservableCollection<Device>();
        public string AppPublicKey => Convert.ToBase64String(_security.Certificate.PublicKey);

        public event Action<string> OnDeviceReady;

        public BayMaxCore()
        {
            _security = new SecurityService();
            _discovery = new DiscoveryService();
            _localAgent = new LocalAgentManager();

            _discovery.OnDeviceDiscovered += (beacon, ip) => {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    if (!AvailableDevices.Any(d => d.Ip == ip))
                    {
                        AvailableDevices.Add(new Device
                        {
                            Name = beacon.agent_name,
                            Ip = ip,
                            Port = beacon.zmq_port,
                            PublicKey = beacon.public_key
                        });
                    }
                });
            };
        }

        public void StartLocalAgent()
        {
            LoggerService.Log("Запуск локального агента...");

            var (pythonPath, scriptPath) = ResolvePythonPaths();

            _localAgent.StartAgent(pythonPath, scriptPath, 5000, AppPublicKey);
        }

        private (string pythonExe, string agentScript) ResolvePythonPaths()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetDir = null;

            while (currentDir != null)
            {
                string potentialPythonDir = System.IO.Path.Combine(currentDir, "Agent");

                if (System.IO.Directory.Exists(potentialPythonDir))
                {
                    targetDir = potentialPythonDir;
                    break;
                }

                currentDir = System.IO.Directory.GetParent(currentDir)?.FullName;
            }

            if (targetDir == null)
            {
                LoggerService.Log("Не удалось найти корневую папку 'python' с агентом!");
            }

            string pythonPath = System.IO.Path.Combine(targetDir, ".venv", "Scripts", "python.exe");
            string scriptPath = System.IO.Path.Combine(targetDir, "agent.py");

            return (pythonPath, scriptPath);
        }

        public async Task ScanNetworkAsync(double timeoutSeconds = 0.6) 
        {
            LoggerService.Log($"Начинаю поиск устройств ({timeoutSeconds} сек)...");
            AvailableDevices.Clear();

            _discovery.Start();

            await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

            _discovery.Stop();
            LoggerService.Log($"Поиск завершен. Найдено устройств: {AvailableDevices.Count}");
        }

        private BayMaxClient EstablishConnection(string ip, int port, string serverPublicKey)
        {
            if (_activeConnections.ContainsKey(ip))
            {
                return _activeConnections[ip];
            }

            var client = new BayMaxClient(_security.Certificate);
            client.Connect(ip, port, serverPublicKey);

            _activeConnections[ip] = client;
            LoggerService.Log($"[ZMQ] Транспорт до {ip} установлен.");

            return client;
        }

        public async Task<bool> IsAlreadyAuthorizedAsync(Device device)
        {
            return await Task.Run(() =>
            {
                try
                {
                    BayMaxClient client = EstablishConnection(device.Ip, device.Port, device.PublicKey);

                    var req = new { type = "status_request", client_public_key = AppPublicKey };
                    string response = client.SendCommand(req);

                    using (JsonDocument doc = JsonDocument.Parse(response))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("error_code", out JsonElement code) && code.GetInt32() == 401)
                        {
                            return false;
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"Ошибка проверки статуса: {ex.Message}", LogLevel.Warning);
                    return false;
                }
            });
        }

        public async Task<string> PairAsync(Device device, string pin)
        {
            return await Task.Run(() =>
            {
                BayMaxClient client = EstablishConnection(device.Ip, device.Port, device.PublicKey);

                var req = new
                {
                    type = "pair_request",
                    pin_hash = BayMaxClient.HashPin(pin),
                    client_public_key = AppPublicKey
                };

                string response = client.SendCommand(req);

                if (response.Contains("success")) device.IsConnected = true;

                return response;
            });
        }

        public void Shutdown()
        {
            _discovery.Stop();
            _localAgent.StopAgent();
            foreach (var client in _activeConnections.Values)
            {
                client.Dispose();
            }
        }
    }
}
