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

        public SecurityService()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BayMax");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _keyPath = Path.Combine(folder, "client.key");

            InitializeKeys();
        }

        private void InitializeKeys()
        {
            if (TryLoadKeys())
            {
                LoggerService.Log("Ключи безопасности успешно загружены из файла.", LogLevel.Success);
            }
            else
            {
                GenerateAndSaveNewKeys();
            }
        }

        private bool TryLoadKeys()
        {
            if (!File.Exists(_keyPath)) return false;

            try
            {
                string[] keys = File.ReadAllLines(_keyPath);

                if (keys.Length < 2) return false;

                byte[] secretKey = Convert.FromBase64String(keys[0]);
                byte[] publicKey = Convert.FromBase64String(keys[1]);

                Certificate = new NetMQCertificate(secretKey, publicKey);

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Ошибка при чтении ключей: {ex.Message}", LogLevel.Warning);
                return false;
            }
        }

        private void GenerateAndSaveNewKeys()
        {
            LoggerService.Log("Генерация новой пары ключей...", LogLevel.Info);

            Certificate = new NetMQCertificate();

            try
            {
                string[] keysToSave = 
                {
                    Convert.ToBase64String(Certificate.SecretKey),
                    Convert.ToBase64String(Certificate.PublicKey)
                };

                File.WriteAllLines(_keyPath, keysToSave);

                LoggerService.Log("Новые ключи созданы и защищены.", LogLevel.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Не удалось сохранить ключи на диск: {ex.Message}", LogLevel.Error);
            }
        }
    }
}

