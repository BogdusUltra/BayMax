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
    public class NodeManager
    {
        public List<CustomPythonNode> AvailableNodes { get; set; } = new List<CustomPythonNode>();

        public void LoadNodes()
        {
            // Теперь мы четко просим найти файл parser.py внутри папки Agent
            var (pythonPath, scriptPath) = BayMaxCore.ResolvePythonPaths("parser.py");

            if (pythonPath == null || scriptPath == null)
            {
                LoggerService.Log("Не удалось найти папку Agent или Питон!", LogLevel.Error);
                return;
            }

            // Базовая папка, где лежит твой .exe и CustomNodes
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Запускаем Питон
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = baseDir, // КРИТИЧЕСКИ ВАЖНО: Парсер должен работать в папке с .exe!
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

            // 2. Читаем полученный JSON
            string jsonPath = Path.Combine(baseDir, "nodes_schema.json");
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);

                // КРИТИЧЕСКИ ВАЖНО: Игнорируем регистр букв при парсинге
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                AvailableNodes = System.Text.Json.JsonSerializer.Deserialize<List<CustomPythonNode>>(json, options);
                LoggerService.Log($"Синхронизировано нод: {AvailableNodes?.Count ?? 0}", LogLevel.Success);
            }
            else
            {
                LoggerService.Log("Файл nodes_schema.json не был создан!", LogLevel.Warning);
            }
        }
    }
}
