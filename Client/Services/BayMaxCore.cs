using System.Windows;
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
        private readonly SecurityService _security;
        private readonly DiscoveryService _discovery;

        public NetBridge _netBridge;

        private readonly Dictionary<string, BayMaxConnection> _connections = new Dictionary<string, BayMaxConnection>();

        public ObservableCollection<Device> AvailableDevices { get; } = new ObservableCollection<Device>();
        public ObservableCollection<Device> ProjectDevices { get; } = new ObservableCollection<Device>();

        public string AppPublicKey => _security.PublicKeyBase64;

        //public event Action<string> OnDeviceReady;   

        public BayMaxCore()
        {
            _security = new SecurityService();
            _discovery = new DiscoveryService();
            _netBridge = new NetBridge();

            _discovery.OnScanComplete += HandleDiscoveredDevices;
            _discovery.StartPeriodicScan();
        }

        public BayMaxConnection GetOrCreateConnection(Device device)
        {
            if (!_connections.ContainsKey(device.Ip))
            {
                _connections[device.Ip] = new BayMaxConnection(device.Ip, device.Port, device.PublicKey, _security);
            }
            return _connections[device.Ip];
        }

        private void HandleDiscoveredDevices(List<DiscoveryService.AgentBeacon> beacons)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentIps = beacons.Select(b => b.ip_address).ToList();

                var lostDevices = AvailableDevices.Where(d => !currentIps.Contains(d.Ip)).ToList();
                foreach (var device in lostDevices)
                {
                    AvailableDevices.Remove(device);
                    ProjectDevices.Remove(device);
                    DisconnectDevice(device.Ip);
                }

                foreach (var beacon in beacons)
                {
                    var device = AvailableDevices.FirstOrDefault(d => d.Ip == beacon.ip_address);

                    if (device == null)
                    {
                        device = new Device { Name = beacon.agent_name, Ip = beacon.ip_address, Port = beacon.zmq_port, PublicKey = beacon.public_key };
                        AvailableDevices.Add(device);
                    }

                    // 3. Проверяем авторизацию через BayMaxConnection
                    var connection = GetOrCreateConnection(device);
                    var status = connection.SendCheckAuthorizationCommand();

                    device.IsAuthorized = (status == ConnectionStatus.Authorized);
                }

                SortDevices();
            });
        }

        public async Task<ConnectionStatus> CheckAutorizationAsync(Device device)
        {
            return await Task.Run(() =>
            {
                var conn = GetOrCreateConnection(device);
                return conn.SendCheckAuthorizationCommand();
            });
        }

        public async Task<bool> PairAsync(Device device, string pin)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var conn = GetOrCreateConnection(device);

                    bool isSuccess = conn.SendPairCommand(pin);

                    if (isSuccess) device.IsAuthorized = true;

                    return isSuccess;
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"Ошибка авторизации с {device.Ip}: {ex.Message}", LogLevel.Error);
                    return false;
                }
            });
        }



        public async Task<List<int>> GetAvailablePortsAsync(Device device, int count)
        {
            return await Task.Run(() =>
            {
                var conn = GetOrCreateConnection(device);
                return conn.SendGetAvailablePortsCommand(count);
            });
        }

        public async Task<bool> DeployAsync(Device device, object deployPayload)
        {
            return await Task.Run(() =>
            {
                var conn = GetOrCreateConnection(device);
                return conn.SendDeployCommand(deployPayload);
            });
        }

        public async Task<JsonElement> CheckNodesAsync(Device device, List<string> types)
        {
            return await Task.Run(() =>
            {
                var conn = GetOrCreateConnection(device);
                return conn.SendCheckNodesCommand(types);
            });
        }

        public async Task<bool> UploadNodeAsync(Device device, string nodeName, string code)
        {
            return await Task.Run(() =>
            {
                var conn = GetOrCreateConnection(device);      
                return conn.SendUploadNodeCommand(nodeName, code);
            });
        }

        public void DisconnectDevice(string ip)
        {
            if (_connections.TryGetValue(ip, out var conn))
            {
                conn.Dispose();
                _connections.Remove(ip);
            }
        }

        public void SortDevices()
        {
            var sorted = AvailableDevices.OrderByDescending(d => d.IsInProject).ThenByDescending(d => d.IsAuthorized).ThenBy(d => d.Name).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                int oldIndex = AvailableDevices.IndexOf(sorted[i]);
                if (oldIndex != i) AvailableDevices.Move(oldIndex, i);
            }
        }

        public void StartNetBridge() => _netBridge.Start();
        public void StopNetBridge() => _netBridge.Stop();
        public int GetBridgePort(string pinId) => _netBridge.GetPublisherPort(pinId);
        public void SendBridgeData(string pinId, string data) => _netBridge.SendData(pinId, data);
        public void ConnectBridgeToAgent(string pinId, string addr, Action<string> callback) => _netBridge.ConnectToAgent(pinId, addr, callback);

        public void Shutdown()
        {
            _netBridge.Stop();
            _discovery.Stop();
            foreach (var conn in _connections.Values) conn.Dispose();
            _connections.Clear();
        }

        //private void OnAgentDiscovered(DiscoveryService.AgentBeacon beacon, string ip)
        //{
        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        var device = AvailableDevices.FirstOrDefault(d => d.Ip == ip);

        //        if (device != null)
        //        {
        //            device.LastSeen = DateTime.Now; 

        //            if (device.PublicKey != beacon.public_key || device.Port != beacon.zmq_port)
        //            {
        //                LoggerService.Log($"[БЕЗОПАСНОСТЬ] Агент {device.Name} сменил ключи или порт!", LogLevel.Warning);

        //                device.PublicKey = beacon.public_key;
        //                device.Port = beacon.zmq_port;
        //                device.IsAuthorized = false;

        //                if (device.IsInProject)
        //                {
        //                    device.IsInProject = false;
        //                    ProjectDevices.Remove(device);
        //                }

        //                DisconnectDevice(ip);
        //                SortAvailableDevices();
        //            }
        //        }
        //        else
        //        {
        //            device = new Device
        //            {
        //                Name = beacon.agent_name,
        //                Ip = ip,
        //                Port = beacon.zmq_port,
        //                PublicKey = beacon.public_key
        //            };

        //            AvailableDevices.Add(device);

        //            Task.Run(async () =>
        //            {
        //                var status = await CheckConnectionStatusAsync(device);

        //                System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //                {
        //                    if (status == ConnectionStatus.Offline)
        //                    {
        //                        AvailableDevices.Remove(device);
        //                        DisconnectDevice(device.Ip);
        //                    }
        //                    else
        //                    {
        //                        device.IsAuthorized = (status == ConnectionStatus.Authorized);
        //                        SortAvailableDevices();
        //                    }
        //                });
        //            });
        //        }
        //    });
        //}

        //private async Task HeartbeatLoop()
        //{
        //    while (_isRunning)
        //    {
        //        await Task.Delay(5000);

        //        var deadDevices = AvailableDevices.Where(d => (DateTime.Now - d.LastSeen).TotalSeconds > 6).ToList();

        //        foreach (var dead in deadDevices)
        //        {
        //            System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                AvailableDevices.Remove(dead);
        //                ProjectDevices.Remove(dead);
        //            });

        //            if (_activeConnections.ContainsKey(dead.Ip))
        //            {
        //                _activeConnections[dead.Ip].Dispose();
        //                _activeConnections.Remove(dead.Ip);
        //                LoggerService.Log($"[ZMQ] Соединение с {dead.Ip} разорвано (таймаут).", LogLevel.Warning);
        //            }

        //            SortAvailableDevices();
        //        }

        //        var aliveDevices = AvailableDevices.ToList();
        //        foreach (var device in aliveDevices)
        //        {
        //            var status = await CheckConnectionStatusAsync(device);

        //            System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                if (status == ConnectionStatus.Offline)
        //                {
        //                    AvailableDevices.Remove(device);
        //                    ProjectDevices.Remove(device);
        //                    DisconnectDevice(device.Ip);
        //                }
        //                else
        //                {
        //                    bool isAuth = (status == ConnectionStatus.Authorized);
        //                    if (device.IsAuthorized != isAuth)
        //                    {
        //                        device.IsAuthorized = isAuth;
        //                        if (!isAuth && device.IsInProject)
        //                        {
        //                            device.IsInProject = false;
        //                            ProjectDevices.Remove(device);
        //                        }
        //                        SortAvailableDevices();
        //                    }
        //                }
        //            });
        //        }
        //    }
        //}

        //public void StartLocalAgent()
        //{
        //    LoggerService.Log("Запуск локального агента...");

        //    var (pythonPath, scriptPath) = ResolvePythonPaths("аgent.py");

        //    _localAgent.StartAgent(pythonPath, scriptPath, 5000, AppPublicKey);
        //}

        //public static (string pythonExe, string agentScript) ResolvePythonPaths(string scriptName)
        //{
        //    string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        //    string targetDir = null;

        //    // 1. Ищем саму папку Agent
        //    while (currentDir != null)
        //    {
        //        string potentialAgentDir = System.IO.Path.Combine(currentDir, "Agent");

        //        if (System.IO.Directory.Exists(potentialAgentDir))
        //        {
        //            targetDir = potentialAgentDir;
        //            break;
        //        }

        //        currentDir = System.IO.Directory.GetParent(currentDir)?.FullName;
        //    }

        //    if (targetDir == null) return (null, null);

        //    // 2. Формируем пути
        //    string pythonPath = System.IO.Path.Combine(targetDir, ".venv", "Scripts", "python.exe");
        //    string scriptPath = System.IO.Path.Combine(targetDir, scriptName); // Сюда подставится "parser.py" или "agent.py"

        //    return (pythonPath, scriptPath);
        //}

        //public async Task ScanNetworkAsync(double timeoutSeconds = 0.6) 
        //{
        //    LoggerService.Log($"Начинаю поиск устройств ({timeoutSeconds} сек)...");
        //    AvailableDevices.Clear();

        //    _discovery.Start();

        //    await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

        //    _discovery.Stop();
        //    LoggerService.Log($"Поиск завершен. Найдено устройств: {AvailableDevices.Count}");
        //}

        //private BayMaxClient EstablishConnection(string ip, int port, string serverPublicKey)
        //{
        //    if (_activeConnections.ContainsKey(ip))
        //    {
        //        return _activeConnections[ip];
        //    }

        //    var client = new BayMaxClient(_security.Certificate);
        //    client.Connect(ip, port, serverPublicKey);

        //    _activeConnections[ip] = client;
        //    LoggerService.Log($"[ZMQ] Транспорт до {ip} установлен.");

        //    return client;
        //}

        //public async Task<ConnectionStatus> CheckConnectionStatusAsync(Device device)
        //{
        //    return await Task.Run(() =>
        //    {
        //        try
        //        {
        //            BayMaxClient client = EstablishConnection(device.Ip, device.Port, device.PublicKey);

        //            var req = new { type = "status_request", client_public_key = AppPublicKey };
        //            string response = client.SendCommand(req);

        //            using (JsonDocument doc = JsonDocument.Parse(response))
        //            {
        //                JsonElement root = doc.RootElement;
        //                if (root.TryGetProperty("error_code", out JsonElement code))
        //                {
        //                    int errCode = code.GetInt32();
        //                    if (errCode == 401 || errCode == 403) return ConnectionStatus.Unauthorized;
        //                    if (errCode == 504) return ConnectionStatus.Offline;
        //                }
        //                return ConnectionStatus.Authorized;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            LoggerService.Log($"Ошибка связи с {device.Ip}: {ex.Message}", LogLevel.Warning);
        //            return ConnectionStatus.Offline;
        //        }
        //    });
        //}

        //public void DisconnectDevice(string ip)
        //{
        //    if (_activeConnections.ContainsKey(ip))
        //    {
        //        _activeConnections[ip].Dispose();
        //        _activeConnections.Remove(ip);
        //        LoggerService.Log($"[ZMQ] Память соединения {ip} очищена.");
        //    }
        //}

        //public async Task<string> PairAsync(Device device, string pin)
        //{
        //    return await Task.Run(() =>
        //    {
        //        BayMaxClient client = EstablishConnection(device.Ip, device.Port, device.PublicKey);

        //        var req = new
        //        {
        //            type = "pair_request",
        //            pin_hash = BayMaxClient.HashPin(pin),
        //            client_public_key = AppPublicKey
        //        };

        //        string response = client.SendCommand(req);

        //        if (response.Contains("success")) device.IsAuthorized = true;

        //        return response;
        //    });
        //}

        //public async Task<List<int>> GetAvailablePortsAsync(Device device, int count)
        //{
        //    return await Task.Run(() =>
        //    {
        //        try
        //        {
        //            BayMaxClient client = EstablishConnection(device.Ip, device.Port, device.PublicKey);

        //            var req = new { type = "discover_request", count = count, client_public_key = AppPublicKey };
        //            string response = client.SendCommand(req);

        //            using (JsonDocument doc = JsonDocument.Parse(response))
        //            {
        //                var root = doc.RootElement;
        //                if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
        //                {
        //                    if (root.TryGetProperty("ports", out var portsArray))
        //                    {
        //                        return portsArray.EnumerateArray().Select(p => p.GetInt32()).ToList();
        //                    }
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            LoggerService.Log($"Ошибка запроса портов у {device.Name}: {ex.Message}", LogLevel.Error);
        //        }
        //        return new List<int>();
        //    });
        //}

        //public async Task<bool> SendDeployRequestAsync(Device device, object requestObj)
        //{
        //    return await Task.Run(() =>
        //    {
        //        try
        //        {
        //            BayMaxClient client = EstablishConnection(device.Ip, device.Port, device.PublicKey);

        //            string response = client.SendCommand(requestObj);

        //            if (response.Contains("\"status\": \"success\"") || response.Contains("\"success\""))
        //            {
        //                LoggerService.Log($"[ДЕПЛОЙ] Успешно загружен граф на {device.Name}", LogLevel.Success);
        //                return true;
        //            }

        //            LoggerService.Log($"[ДЕПЛОЙ] Ошибка от агента {device.Name}: {response}", LogLevel.Error);
        //            return false;
        //        }
        //        catch (Exception ex)
        //        {
        //            LoggerService.Log($"[ДЕПЛОЙ] Сетевая ошибка при отправке на {device.Name}: {ex.Message}", LogLevel.Error);
        //            return false;
        //        }
        //    });
        //}

        //public async Task<string> SendCommandAsync(Device device, object requestObj)
        //{
        //    return await Task.Run(() =>
        //    {
        //        try
        //        {
        //            BayMaxClient client = EstablishConnection(device.Ip, device.Port, device.PublicKey);
        //            return client.SendCommand(requestObj);
        //        }
        //        catch (Exception ex)
        //        {
        //            LoggerService.Log($"[ZMQ] Ошибка отправки команды на {device.Name}: {ex.Message}", LogLevel.Error);
        //            return "{\"type\":\"error_response\"}";
        //        }
        //    });
        //}

        //public void Shutdown()
        //{
        //    _isRunning = false;
        //    _discovery.Stop();
        //    _localAgent.StopAgent();
        //    foreach (var client in _activeConnections.Values)
        //    {
        //        client.Dispose();
        //    }
        //}

        //public void SortAvailableDevices()
        //{
        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        var sorted = AvailableDevices
        //            .OrderByDescending(d => d.IsInProject)
        //            .ThenByDescending(d => d.IsAuthorized)
        //            .ThenBy(d => d.Name)
        //            .ToList();

        //        for (int i = 0; i < sorted.Count; i++)
        //        {
        //            int oldIndex = AvailableDevices.IndexOf(sorted[i]);
        //            if (oldIndex != i)
        //            {
        //                AvailableDevices.Move(oldIndex, i);
        //            }
        //        }
        //    });
        //}


    }
}
