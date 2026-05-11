using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BayMax.Services
{
    public static class PythonEnv
    {
        public static (string Executable, string Script) GetPath(string scriptName)
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetDir = null;

            while (currentDir != null)
            {
                string potentialAgentDir = Path.Combine(currentDir, "Agent");
                if (Directory.Exists(potentialAgentDir))
                {
                    targetDir = potentialAgentDir;
                    break;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            if (targetDir == null) return (null, null);

            string pythonPath = Path.Combine(targetDir, ".venv", "Scripts", "python.exe");
            string scriptPath = Path.Combine(targetDir, scriptName);

            return (pythonPath, scriptPath);
        }
    }
}
