using BayMax.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BayMax.Models;
using System.Linq;
using System;

namespace BayMax.Services
{
    public class NodeRegistryMetadata
    {
        public Dictionary<string, DateTime> LastRegisteredChanges { get; set; } = new Dictionary<string, DateTime>();
    }
    public class NodeManager
    {
        public List<CustomPythonNode> AvailableNodes { get; set; } = new List<CustomPythonNode>();

        private readonly string _metadataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nodes_metadata.json");
        private NodeRegistryMetadata _metadata;

        public NodeManager()
        {
            LoadMetadata();
        }
        private void LoadMetadata()
        {
            try
            {
                if (File.Exists(_metadataPath))
                {
                    string json = File.ReadAllText(_metadataPath);
                    _metadata = JsonSerializer.Deserialize<NodeRegistryMetadata>(json) ?? new NodeRegistryMetadata();
                }
                else _metadata = new NodeRegistryMetadata();
            }
            catch { _metadata = new NodeRegistryMetadata(); }
        }

        public void SaveCurrentMetadata()
        {
            try
            {
                foreach (var node in AvailableNodes)
                {
                    _metadata.LastRegisteredChanges[node.Name] = node.LastDiskChange;
                    node.IsModified = false;
                }

                string json = JsonSerializer.Serialize(_metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataPath, json);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Ошибка сохранения метаданных: {ex.Message}", LogLevel.Error);
            }
        }

        public void LoadNodes()
        {
            var (pythonPath, scriptPath) = PythonEnv.GetPath("parser.py");

            if (pythonPath == null || scriptPath == null)
            {
                LoggerService.Log("Не удалось найти папку Agent или Питон!", LogLevel.Error);
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = baseDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                process?.WaitForExit();
                if (process?.ExitCode != 0)
                {
                    string err = process?.StandardError.ReadToEnd();
                    LoggerService.Log($"Ошибка парсера Python: {err}", LogLevel.Error);
                    return;
                }
            }

            string jsonPath = Path.Combine(baseDir, "nodes_schema.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedNodes = JsonSerializer.Deserialize<List<CustomPythonNode>>(json, options);

                string customNodesDir = Path.Combine(baseDir, "CustomNodes");
                var validNodes = new List<CustomPythonNode>();

                if (parsedNodes != null)
                {
                    foreach (var node in parsedNodes)
                    {
                        string pyFilePath = Path.Combine(customNodesDir, $"{node.Name}.py");

                        if (!File.Exists(pyFilePath))
                        {
                            continue;
                        }

                        node.LastDiskChange = File.GetLastWriteTime(pyFilePath);

                        if (_metadata.LastRegisteredChanges.TryGetValue(node.Name, out DateTime registeredTime))
                        {
                            if ((node.LastDiskChange - registeredTime).TotalMilliseconds > 100)
                            {
                                node.IsModified = true;
                            }
                        }
                        else
                        {
                            node.IsModified = true;
                        }

                        validNodes.Add(node);
                    }
                }

                AvailableNodes = validNodes;
                LoggerService.Log($"Синхронизировано нод: {AvailableNodes?.Count ?? 0}", LogLevel.Success);
            }
            else
            {
                LoggerService.Log("Файл nodes_schema.json не был создан!", LogLevel.Warning);
            }
        }
    }
}
