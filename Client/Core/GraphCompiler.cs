using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using BayMax.Models;
using BayMax.Services;
using BayMax.UI.Components;
using BayMax.Workspaces;

namespace BayMax.Core
{
    public class GraphCompiler
    {
        public void ValidateGraph(NodeCanvas canvas)
        {
            var allNodes = canvas.DrawArea.Children.OfType<NodeBlock>().ToList();
            var logicNodes = allNodes.Where(n => n.Type == NodeType.Logic).ToList();

            if (logicNodes.Count == 0)
                throw new Exception("На холсте нет вычислительных нод для деплоя.");

            var unassigned = logicNodes.Where(n => n.TargetDevice == null).ToList();
            if (unassigned.Count > 0)
                throw new Exception("На холсте есть ноды, которые не привязаны ни к одному устройству!");
        }

        public Dictionary<Device, List<NodeBlock>> GroupNodesByDevice(NodeCanvas canvas)
        {
            var allNodes = canvas.DrawArea.Children.OfType<NodeBlock>().ToList();
            var logicNodes = allNodes.Where(n => n.Type == NodeType.Logic).ToList();

            return logicNodes.GroupBy(n => n.TargetDevice).ToDictionary(g => g.Key, g => g.ToList());
        }

        public List<string> GetRequiredNodeTypes(IEnumerable<NodeBlock> nodes)
        {
            return nodes.Select(n => n.LogicNodeTypeName).Distinct().ToList();
        }

        public List<NodePin> GetOutputPins(IEnumerable<NodeBlock> nodes)
        {
            return nodes.SelectMany(n => n.OutputPinsContainer.Children.OfType<NodePin>()).ToList();
        }

        public DeployRequest BuildDeployPayload(
            IEnumerable<NodeBlock> deviceNodes,
            NodeCanvas canvas,
            string appPublicKey,
            string localIp,
            Dictionary<string, int> outPinPorts,
            Dictionary<string, string> outPinAddresses,
            Func<string, int> uiPortProvider,
            List<CustomPythonNode> availablePythonNodes)
        {
            var deployPayload = new DeployRequest { ClientPublicKey = appPublicKey };
            var connectionsByInput = canvas.Connections.ToDictionary(c => c.EndPin, c => c);

            foreach (NodeBlock node in deviceNodes)
            {
                node.OnSaveSettings?.Invoke();

                var deployNode = new DeployNode { Id = node.Id, Type = node.LogicNodeTypeName };
                var meta = availablePythonNodes.FirstOrDefault(n => n.Name == node.LogicNodeTypeName);
                if (meta == null) continue;

                if (meta.Parameters != null)
                {
                    foreach (var param in meta.Parameters)
                    {
                        if (node.Settings.TryGetValue(param.Name, out string val)) deployNode.Parameters[param.Name] = val;
                        else deployNode.Parameters[param.Name] = param.Default;
                    }
                }

                // Заполняем издателей (выходные порты)
                var outPins = node.OutputPinsContainer.Children.OfType<NodePin>().ToList();
                for (int i = 0; i < outPins.Count; i++)
                {
                    if (outPinPorts.TryGetValue(outPins[i].Id, out int port))
                        deployNode.Publishers[meta.Outputs[i].Name] = port;
                }

                // Заполняем подписчиков (кто откуда берет данные)
                var inPins = node.InputPinsContainer.Children.OfType<NodePin>().ToList();
                for (int i = 0; i < inPins.Count; i++)
                {
                    if (connectionsByInput.TryGetValue(inPins[i], out var conn))
                    {
                        var sourcePin = conn.StartPin;
                        var sourceNode = canvas.GetNodeByPin(sourcePin);

                        string resolvedAddr = null;

                        if (sourceNode.Type == NodeType.UI)
                        {
                            int uiPort = uiPortProvider(sourcePin.Id);
                            resolvedAddr = $"tcp://{localIp}:{uiPort}";

                            sourcePin.NetworkAddress = resolvedAddr;
                            sourcePin.UpdateTooltip(true);
                        }
                        else if (outPinAddresses.TryGetValue(sourcePin.Id, out var addr))
                        {
                            resolvedAddr = addr;
                        }

                        if (!string.IsNullOrEmpty(resolvedAddr))
                        {
                            deployNode.Subscribers[meta.Inputs[i].Name] = resolvedAddr;
                            inPins[i].NetworkAddress = resolvedAddr;
                            inPins[i].UpdateTooltip(true);
                        }
                    }
                }
                deployPayload.Nodes.Add(deployNode);
            }

            return deployPayload;
        }
    }
}
