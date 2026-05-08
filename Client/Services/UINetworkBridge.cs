using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BayMax.Services
{
    public class UINetworkBridge
    {
        // Словари для хранения сокетов под КАЖДЫЙ пин интерфейса
        private readonly Dictionary<string, PublisherSocket> _publishers = new Dictionary<string, PublisherSocket>();
        private readonly Dictionary<string, int> _publisherPorts = new Dictionary<string, int>();

        private readonly Dictionary<string, SubscriberSocket> _subscribers = new Dictionary<string, SubscriberSocket>();
        private readonly Dictionary<string, Action<string>> _uiCallbacks = new Dictionary<string, Action<string>>();

        private CancellationTokenSource _cts;

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            Task.Run(() => ReceiveLoop(_cts.Token));
            LoggerService.Log("[UI МОСТ] Запущен в режиме 'Один порт = Один провод'.", LogLevel.Info);
        }

        // Динамически выдаем уникальный порт для каждой ноды ввода
        public int GetPublisherPort(string pinId)
        {
            if (!_publishers.ContainsKey(pinId))
            {
                var pub = new PublisherSocket();
                int port = pub.BindRandomPort("tcp://*");
                _publishers[pinId] = pub;
                _publisherPorts[pinId] = port;
                LoggerService.Log($"[UI МОСТ] Открыт порт {port} для вывода данных", LogLevel.Debug);
            }
            return _publisherPorts[pinId];
        }

        public void SendData(string pinId, string data)
        {
            if (_publishers.ContainsKey(pinId))
            {
                // ОТПРАВЛЯЕМ РОВНО ОДИН КАДР (только цифру)! Никаких имен топиков.
                _publishers[pinId].SendFrame(data);
            }
        }

        public void ConnectToAgent(string pinId, string address, Action<string> onMessageReceived)
        {
            if (!_subscribers.ContainsKey(pinId))
            {
                var sub = new SubscriberSocket();
                sub.Connect(address);
                sub.Subscribe(""); // Слушаем всё, так как порт выделен лично под нас
                _subscribers[pinId] = sub;
                _uiCallbacks[pinId] = onMessageReceived;
            }
        }

        private void ReceiveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var kvp in _subscribers)
                {
                    // ЧИТАЕМ РОВНО ОДИН КАДР (без зависания потока)
                    if (kvp.Value.TryReceiveFrameString(out string message))
                    {
                        if (_uiCallbacks.ContainsKey(kvp.Key))
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                _uiCallbacks[kvp.Key].Invoke(message);
                            });
                        }
                    }
                }
                Thread.Sleep(10); // Пауза, чтобы не грузить процессор (10 мс)
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            foreach (var p in _publishers.Values) p.Dispose();
            foreach (var s in _subscribers.Values) s.Dispose();
            _publishers.Clear();
            _publisherPorts.Clear();
            _subscribers.Clear();
            _uiCallbacks.Clear();
        }
    }
}