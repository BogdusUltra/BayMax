using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BayMax.Services
{
    public enum ConnectionStatus
    {
        Authorized,
        Unauthorized,
        Offline
    }

    public class BayMaxConnection : IDisposable
    {
        private readonly RequestSocket _socket;
        private readonly string _clientPublicKey;
        private readonly object _socketLock = new object();
        public string IpAddress { get; }

        public BayMaxConnection(string ip, int port, string serverPublicKeyZ85, SecurityService security)
        {
            IpAddress = ip;
            _clientPublicKey = security.PublicKeyBase64;

            _socket = new RequestSocket();
            _socket.Options.CurveCertificate = security.Certificate;
            _socket.Options.CurveServerKey = DecodeZ85(serverPublicKeyZ85);
            _socket.Connect($"tcp://{ip}:{port}");
        }

        private JsonElement SendCommand(object payload)
        {
            lock (_socketLock)
            {
                string jsonReq = JsonSerializer.Serialize(payload);
                _socket.SendFrame(jsonReq);

                if (_socket.TryReceiveFrameString(TimeSpan.FromSeconds(0.5), out string response))
                {
                    return JsonDocument.Parse(response).RootElement;
                }

                return JsonDocument.Parse("{\"error_code\": 504}").RootElement; // Таймаут
            }
        }

        public ConnectionStatus SendCheckAuthorizationCommand()
        {
            try
            {
                var response = SendCommand(new { type = "status_request", client_public_key = _clientPublicKey });

                if (response.TryGetProperty("error_code", out JsonElement code))
                {
                    int err = code.GetInt32();
                    if (err == 401 || err == 403) return ConnectionStatus.Unauthorized;
                    if (err == 504) return ConnectionStatus.Offline;
                }
                return ConnectionStatus.Authorized;
            }
            catch { return ConnectionStatus.Offline; }
        }

        public bool SendPairCommand(string pin)
        {
            string pinHash = HashPin(pin);
            var response = SendCommand(new { type = "pair_request", pin_hash = pinHash, client_public_key = _clientPublicKey });

            return response.TryGetProperty("status", out var status) && status.GetString() == "success";
        }

        public List<int> SendGetAvailablePortsCommand(int count)
        {
            var req = new { type = "discover_request", count = count, client_public_key = _clientPublicKey };
            var res = SendCommand(req);

            if (res.TryGetProperty("status", out var st) && st.GetString() == "success")
            {
                if (res.TryGetProperty("ports", out var portsArray))
                    return portsArray.EnumerateArray().Select(p => p.GetInt32()).ToList();
            }
            return new List<int>();
        }

        public bool SendDeployCommand(object deployData)
        {
            var res = SendCommand(deployData);
            return res.TryGetProperty("status", out var st) && st.GetString() == "success";
        }

        public bool SendStopCommand()
        {
            var res = SendCommand(new { type = "stop_request", client_public_key = _clientPublicKey });
            return res.TryGetProperty("status", out var st) && st.GetString() == "success";
        }

        public JsonElement SendCheckNodesCommand(Dictionary<string, long> nodesData)
        {
            return SendCommand(new { type = "check_nodes", nodes = nodesData, client_public_key = _clientPublicKey });
        }

        public bool SendUploadNodeCommand(string nodeName, string code)
        {
            var res = SendCommand(new { type = "upload_node", node_name = nodeName, code = code, client_public_key = _clientPublicKey });
            return res.TryGetProperty("status", out var st) && st.GetString() == "success";
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
            //NetMQConfig.Cleanup();
        }
    }
}
