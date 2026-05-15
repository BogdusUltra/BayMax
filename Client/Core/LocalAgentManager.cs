using BayMax.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BayMax.Core
{
    public class LocalAgentManager
    {
        private Process _agentProcess;

        public void StartAgent(string pythonPath, string scriptPath, int zmqPort, string masterKey)
        {
            if (_agentProcess != null && !_agentProcess.HasExited)
                return;

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath, 
                Arguments = $"-u \"{scriptPath}\" --port {zmqPort} --mode local --master-key {masterKey}",
                WorkingDirectory=System.IO.Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };


            try
            {
                _agentProcess = new Process { StartInfo = startInfo };

                _agentProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        LoggerService.Global.Log($"[ПИТОН]: {e.Data}");
                };

                _agentProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LoggerService.Global.Log($"[ПИТОН КРАШ] {e.Data}", LogLevel.Error);
                    }
                };

                _agentProcess.Start();

                _agentProcess.BeginOutputReadLine();
                _agentProcess.BeginErrorReadLine();

                LoggerService.Global.Log("Процесс локального агента успешно запущен.", LogLevel.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Global.Log($"Сбой запуска процесса Питона: {ex.Message} (Путь: {pythonPath})", LogLevel.Error);
            }
        }

        public void StopAgent()
        {
            if (_agentProcess != null && !_agentProcess.HasExited)
            {
                _agentProcess.Kill();
                _agentProcess.Dispose();
                _agentProcess = null;
                LoggerService.Global.Log("Локальный агент принудительно остановлен.", LogLevel.Warning);
            }
        }
    }
}
