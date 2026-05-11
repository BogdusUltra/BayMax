using NetMQ;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BayMax.Services
{
    public class SecurityService
    {
        private readonly string _keyPath;
        public NetMQCertificate Certificate { get; private set; }
        public string PublicKeyBase64 => Convert.ToBase64String(Certificate.PublicKey);

        public SecurityService()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BayMax");
            Directory.CreateDirectory(folder);
            _keyPath = Path.Combine(folder, "client.key");

            LoadOrGenerateKeys();
        }

        private void LoadOrGenerateKeys()
        {
            if (File.Exists(_keyPath))
            {
                try
                {
                    string[] keys = File.ReadAllLines(_keyPath);
                    if (keys.Length >= 2)
                    {
                        Certificate = new NetMQCertificate(
                            Convert.FromBase64String(keys[0]),
                            Convert.FromBase64String(keys[1])
                        );
                        LoggerService.Log("Ключи безопасности загружены.", LogLevel.Success);
                        return;
                    }
                }
                catch {  }
            }

            LoggerService.Log("Генерация новой пары ключей...", LogLevel.Info);
            Certificate = new NetMQCertificate();
            File.WriteAllLines(_keyPath, new[] {
                Convert.ToBase64String(Certificate.SecretKey),
                Convert.ToBase64String(Certificate.PublicKey)
            });
        }
    }
}

