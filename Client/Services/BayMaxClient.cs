using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetMQ;
using NetMQ.Sockets;

namespace BayMax.Services
{
    public class BayMaxClient : IDisposable
    {
        private RequestSocket _socket;
        private readonly NetMQCertificate _clientCertificate;

        public string PublicKey => Convert.ToBase64String(_clientCertificate.PublicKey);

        private readonly object _socketLock = new object();

        public BayMaxClient(NetMQCertificate sharedCertificate)
        {
            _clientCertificate = sharedCertificate;
        }

        public static byte[] DecodeZ85(string z85)
        {
            string z85chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

            if (string.IsNullOrEmpty(z85) || z85.Length != 40)
                throw new ArgumentException("Ключ Z85 сервера должен состоять ровно из 40 символов!");

            byte[] decoded = new byte[32];
            int byteIdx = 0;
            uint value = 0;

            for (int i = 0; i < z85.Length; i++)
            {
                value = value * 85 + (uint)z85chars.IndexOf(z85[i]);
                if ((i + 1) % 5 == 0)
                {
                    decoded[byteIdx++] = (byte)((value >> 24) & 0xFF);
                    decoded[byteIdx++] = (byte)((value >> 16) & 0xFF);
                    decoded[byteIdx++] = (byte)((value >> 8) & 0xFF);
                    decoded[byteIdx++] = (byte)(value & 0xFF);
                    value = 0;
                }
            }
            return decoded;
        }

        public void Connect(string ip, int port, string serverPublicKeyZ85)
        {
            _socket?.Dispose();

            _socket = new RequestSocket();
            _socket.Options.CurveCertificate = _clientCertificate;
            _socket.Options.CurveServerKey = DecodeZ85(serverPublicKeyZ85);

            _socket.Connect($"tcp://{ip}:{port}");
            LoggerService.Log($"[ZMQ] Установлено соединение с {ip}:{port}", LogLevel.Debug);
        }

        public string SendCommand(object commandObj)
        {
            lock (_socketLock)
            {
                string json = JsonSerializer.Serialize(commandObj);

                _socket.SendFrame(json);

                if (_socket.TryReceiveFrameString(TimeSpan.FromSeconds(2), out string response))
                {
                    return response;
                }

                LoggerService.Log("[ZMQ] Таймаут ответа от агента.", LogLevel.Warning);
                return "{\"type\": \"error_response\", \"error_code\": 504, \"message\": \"Таймаут\"}";
            }
        }


        public static string HashPin(string pin)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(pin));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public void Dispose()
        {
            _socket?.Dispose();
            NetMQConfig.Cleanup();
        }
    }
}