using BayMax.Models;
using BayMax.UI.Controls;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace BayMax.Services
{
    public class GraphCompiler
    {
        private readonly BayMaxCore _core;

        public GraphCompiler(BayMaxCore core)
        {
            _core = core;
        }

        public async Task<bool> CompileAndDeployAsync(NodeCanvas canvas)
        {
            try
            {
                var allNodes = canvas.DrawArea.Children.OfType<NodeBlock>().ToList();
                var logicNodes = allNodes.Where(n => n.Type == NodeType.Logic).ToList();

                if (logicNodes.Count == 0)
                {
                    MessageBox.Show("На холсте нет вычислительных нод для деплоя.", "Инфо", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                foreach (var node in logicNodes)
                {
                    if (node.TargetDevice == null || !node.TargetDevice.IsAuthorized)
                    {
                        MessageBox.Show("Не все ноды привязаны к авторизованным устройствам!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }

                var mainWindow = Application.Current.MainWindow as MainWindow;
                _core.StartNetBridge();

                var connectionsByInput = canvas.Connections.GroupBy(c => c.EndPin).ToDictionary(g => g.Key, g => g.First());

                var groupedByDevice = logicNodes.GroupBy(n => n.TargetDevice);
                var availablePythonNodes = mainWindow?.NodeManager?.AvailableNodes ?? new List<CustomPythonNode>();

                bool allSuccess = true;

                var globalLogicPublishers = new Dictionary<string, string>();

                foreach (var group in groupedByDevice)
                {
                    Device targetDevice = group.Key;

                    // Если Питон запущен локально, используем 127.0.0.1, иначе - IP компьютера в сети
                    string localIp = GetLocalIPAddress(targetDevice.Ip);

                    // ПРОХОД 0: HANDSHAKE
                    var requiredTypes = group.Select(n => n.LogicNodeTypeName).Distinct().ToList();
                    var checkRes = await _core.CheckNodesAsync(targetDevice, requiredTypes);

                    if (checkRes.TryGetProperty("type", out var resType) && resType.GetString() == "missing_nodes")
                    {
                        foreach (var missing in checkRes.GetProperty("nodes").EnumerateArray())
                        {
                            var meta = availablePythonNodes.FirstOrDefault(n => n.Name == missing.GetString());
                            if (meta != null && !string.IsNullOrEmpty(meta.SourceCode))
                            {
                                if (!await _core.UploadNodeAsync(targetDevice, meta.Name, meta.SourceCode)) return false;
                            }
                        }
                    }

                    // ПРОХОД 1: Сбор портов
                    var deviceOutputPins = group.SelectMany(n => n.OutputPinsContainer.Children.OfType<NodePin>()).ToList();
                    var outPinPorts = new Dictionary<string, int>();
                    var outPinAddresses = new Dictionary<string, string>();

                    if (deviceOutputPins.Count > 0)
                    {
                        List<int> freePorts = await _core.GetAvailablePortsAsync(targetDevice, deviceOutputPins.Count);
                        if (freePorts == null || freePorts.Count < deviceOutputPins.Count) return false;

                        for (int i = 0; i < deviceOutputPins.Count; i++)
                        {
                            outPinPorts[deviceOutputPins[i].Id] = freePorts[i];
                            string addr = $"tcp://{targetDevice.Ip}:{freePorts[i]}";
                            outPinAddresses[deviceOutputPins[i].Id] = addr;

                            globalLogicPublishers[deviceOutputPins[i].Id] = addr;
                        }
                    }

                    // --- ПРОХОД 2: Сборка JSON ---
                    var deployPayload = new DeployRequest { ClientPublicKey = _core.AppPublicKey };

                    foreach (NodeBlock node in group)
                    {
                        var dNode = new DeployNode { Id = node.Id, Type = node.LogicNodeTypeName };

                        var meta = availablePythonNodes.FirstOrDefault(n => n.Name == node.LogicNodeTypeName);
                        if (meta == null) continue;

                        var outPins = node.OutputPinsContainer.Children.OfType<NodePin>().ToList();
                        for (int i = 0; i < outPins.Count; i++)
                        {
                            dNode.Publishers[meta.Outputs[i]] = outPinPorts[outPins[i].Id];
                        }

                        var inPins = node.InputPinsContainer.Children.OfType<NodePin>().ToList();
                        for (int i = 0; i < inPins.Count; i++)
                        {
                            if (connectionsByInput.TryGetValue(inPins[i], out var conn))
                            {
                                var sourcePin = conn.StartPin;
                                var sourceNode = sourcePin.FindParent<NodeBlock>();

                                if (sourceNode.Type == NodeType.UI)
                                {
                                    int uiPort = _core.GetBridgePort(sourcePin.Id);
                                    dNode.Subscribers[meta.Inputs[i]] = $"tcp://{localIp}:{uiPort}";
                                }
                                else if (outPinAddresses.TryGetValue(sourcePin.Id, out var addr))
                                {
                                    dNode.Subscribers[meta.Inputs[i]] = addr;
                                }
                            }
                        }
                        deployPayload.Nodes.Add(dNode);
                    }

                    if (!await _core.DeployAsync(targetDevice, deployPayload)) allSuccess = false;
                }

                if (allSuccess)
                {
                    ActivateUIConnections(canvas, globalLogicPublishers);
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка сборки графа: {ex.Message}", "Сбой компилятора", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ActivateUIConnections(NodeCanvas canvas, Dictionary<string, string> logicPubs)
        {
            var handledPins = new HashSet<string>();

            foreach (var conn in canvas.Connections)
            {
                var startNode = conn.StartPin.FindParent<NodeBlock>();
                var endNode = conn.EndPin.FindParent<NodeBlock>();

                if (startNode.Type == NodeType.UI && endNode.Type == NodeType.Logic)
                {
                    if (!handledPins.Contains(conn.StartPin.Id))
                    {
                        handledPins.Add(conn.StartPin.Id);

                        conn.StartPin.ValueChanged += (val) =>
                        {
                            if (canvas.IsDeployed && val != null)
                            {
                                _core.SendBridgeData(conn.StartPin.Id, val.ToString());
                            }
                        };

                        if (conn.StartPin.DataValue != null)
                        {
                            string initialData = conn.StartPin.DataValue.ToString();
                            string pinId = conn.StartPin.Id;

                            Task.Run(async () =>
                            {
                                // Ждем полсекунды, чтобы Питон 100% успел запустить все сокеты
                                await Task.Delay(500);
                                if (canvas.IsDeployed)
                                    _core.SendBridgeData(pinId, initialData);
                            });
                        }
                    }
                }

                if (startNode.Type == NodeType.Logic && endNode.Type == NodeType.UI)
                {
                    if (logicPubs.TryGetValue(conn.StartPin.Id, out var address))
                    {
                        _core.ConnectBridgeToAgent(conn.EndPin.Id, address, (msg) =>
                        {
                            conn.EndPin.SetValue(msg);
                        });
                    }
                }
            }
        }

        private string GetLocalIPAddress(string targetIp)
        {
            try
            {
                if (targetIp == "127.0.0.1") return "127.0.0.1";

                // Создаем "пустышку" UDP, чтобы ОС сама рассчитала правильный сетевой интерфейс
                using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
                {
                    socket.Connect(targetIp, 65530);
                    return (socket.LocalEndPoint as System.Net.IPEndPoint).Address.ToString();
                }
            }
            catch { return "127.0.0.1"; }
        }
    }
}
