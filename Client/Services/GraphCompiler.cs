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
                    MessageBox.Show("На холсте нет ни одной вычислительной (зеленой) ноды! Нечего деплоить.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                foreach (var node in logicNodes)
                {
                    if (node.TargetDevice == null || !node.TargetDevice.IsAuthorized)
                    {
                        MessageBox.Show($"Ошибка в ноде '{node.NodeTitle.Text}': Не выбрано устройство!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }

                // =========================================================
                // ЗАПУСК СЕТЕВОГО МОСТА UI
                // =========================================================
                var mainWindow = Application.Current.MainWindow as MainWindow;
                var uiBridge = mainWindow?.UIBridge;
                uiBridge?.Start(); // Запускаем фоновый слушатель ZMQ

                var connectionsByInput = canvas.Connections
                    .GroupBy(c => c.EndPin)
                    .ToDictionary(g => g.Key, g => g.First());

                var groupedByDevice = logicNodes.GroupBy(n => n.TargetDevice);
                var availablePythonNodes = mainWindow?.NodeManager?.AvailableNodes ?? new List<CustomPythonNode>();

                bool allSuccess = true;

                // Словарь для запоминания портов Питона, чтобы C# знал, куда подключаться
                var globalLogicPublishers = new Dictionary<string, string>();

                foreach (var group in groupedByDevice)
                {
                    Device targetDevice = group.Key;

                    // Если Питон запущен локально, используем 127.0.0.1, иначе - IP компьютера в сети
                    string localIp = targetDevice.Ip == "127.0.0.1" ? "127.0.0.1" : GetLocalIPAddress();

                    // ПРОХОД 0: HANDSHAKE
                    var requiredTypes = group.Select(n => n.LogicNodeTypeName).Distinct().ToList();
                    var checkPayload = new { type = "check_nodes", required_nodes = requiredTypes, client_public_key = _core.AppPublicKey };

                    string checkResponse = await _core.SendCommandAsync(targetDevice, checkPayload);
                    using (JsonDocument doc = JsonDocument.Parse(checkResponse))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("type", out var resType) && resType.GetString() == "missing_nodes")
                        {
                            if (root.TryGetProperty("nodes", out var missingArray))
                            {
                                foreach (var missingNode in missingArray.EnumerateArray())
                                {
                                    string missingName = missingNode.GetString();
                                    var nodeMeta = availablePythonNodes.FirstOrDefault(n => n.Name == missingName);

                                    if (nodeMeta != null && !string.IsNullOrEmpty(nodeMeta.SourceCode))
                                    {
                                        var uploadPayload = new { type = "upload_node", node_name = nodeMeta.Name, code = nodeMeta.SourceCode, client_public_key = _core.AppPublicKey };
                                        string uploadRes = await _core.SendCommandAsync(targetDevice, uploadPayload);
                                        if (!uploadRes.Contains("\"success\"")) return false;
                                    }
                                    else return false;
                                }
                            }
                        }
                    }

                    // ПРОХОД 1: Сбор портов
                    var outPinPorts = new Dictionary<string, int>();
                    var outPinAddresses = new Dictionary<string, string>();

                    var deviceOutputPins = new List<NodePin>();
                    foreach (var node in group)
                        deviceOutputPins.AddRange(node.OutputPinsContainer.Children.OfType<NodePin>());

                    if (deviceOutputPins.Count > 0)
                    {
                        List<int> freePorts = await _core.GetAvailablePortsAsync(targetDevice, deviceOutputPins.Count);
                        if (freePorts.Count < deviceOutputPins.Count) return false;

                        for (int i = 0; i < deviceOutputPins.Count; i++)
                        {
                            outPinPorts[deviceOutputPins[i].Id] = freePorts[i];
                            string addr = $"tcp://{targetDevice.Ip}:{freePorts[i]}";
                            outPinAddresses[deviceOutputPins[i].Id] = addr;

                            // Сохраняем адрес для UI-моста
                            globalLogicPublishers[deviceOutputPins[i].Id] = addr;
                        }
                    }

                    // --- ПРОХОД 2: Сборка JSON ---
                    var deployPayload = new DeployRequest { ClientPublicKey = _core.AppPublicKey };

                    foreach (NodeBlock node in group)
                    {
                        var dNode = new DeployNode { Id = node.Id, Type = node.LogicNodeTypeName };

                        var nodeMeta = availablePythonNodes.FirstOrDefault(n => n.Name == node.LogicNodeTypeName);
                        if (nodeMeta == null) continue;

                        var outPins = node.OutputPinsContainer.Children.OfType<NodePin>().ToList();
                        for (int i = 0; i < outPins.Count; i++)
                        {
                            dNode.Publishers[nodeMeta.Outputs[i]] = outPinPorts[outPins[i].Id];
                        }

                        var inPins = node.InputPinsContainer.Children.OfType<NodePin>().ToList();
                        for (int i = 0; i < inPins.Count; i++)
                        {
                            var inPin = inPins[i];
                            if (connectionsByInput.ContainsKey(inPin))
                            {
                                var sourcePin = connectionsByInput[inPin].StartPin;
                                var sourceNode = sourcePin.FindParent<NodeBlock>();
                                string pythonInName = nodeMeta.Inputs[i];

                                if (sourceNode.Type == NodeType.UI)
                                {
                                    // Просим мост открыть порт конкретно для этого провода!
                                    int uiPort = uiBridge.GetPublisherPort(sourcePin.Id);
                                    dNode.Subscribers[pythonInName] = $"tcp://{localIp}:{uiPort}";
                                }
                                else if (outPinAddresses.ContainsKey(sourcePin.Id))
                                {
                                    dNode.Subscribers[pythonInName] = outPinAddresses[sourcePin.Id];
                                }
                            }
                        }
                        deployPayload.Nodes.Add(dNode);
                    }

                    bool success = await _core.SendDeployRequestAsync(targetDevice, deployPayload);
                    if (!success) allSuccess = false;
                }

                // =========================================================
                // ПРОХОД 3: ПРИВЯЗКА UI К СЕТИ
                // =========================================================
                if (allSuccess && uiBridge != null)
                {
                    var handledPins = new HashSet<string>(); // Чтобы не дублировать отправку

                    foreach (var conn in canvas.Connections)
                    {
                        var startNode = conn.StartPin.FindParent<NodeBlock>();
                        var endNode = conn.EndPin.FindParent<NodeBlock>();

                        // Если UI отправляет данные
                        if (startNode.Type == NodeType.UI && endNode.Type == NodeType.Logic)
                        {
                            if (!handledPins.Contains(conn.StartPin.Id))
                            {
                                handledPins.Add(conn.StartPin.Id);
                                conn.StartPin.ValueChanged += (val) =>
                                {
                                    if (canvas.IsDeployed && val != null)
                                    {
                                        // Отправляем данные в конкретный порт!
                                        uiBridge.SendData(conn.StartPin.Id, val.ToString());
                                    }
                                };
                            }
                        }

                        // Если UI принимает данные
                        if (startNode.Type == NodeType.Logic && endNode.Type == NodeType.UI)
                        {
                            if (globalLogicPublishers.ContainsKey(conn.StartPin.Id))
                            {
                                string address = globalLogicPublishers[conn.StartPin.Id];

                                // Подключаемся и выводим на экран
                                uiBridge.ConnectToAgent(conn.EndPin.Id, address, (msg) =>
                                {
                                    conn.EndPin.SetValue(msg);
                                });
                            }
                        }
                    }
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка сборки графа: {ex.Message}", "Сбой компилятора", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Вспомогательный метод: узнает IP компьютера в локальной сети
        private string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
