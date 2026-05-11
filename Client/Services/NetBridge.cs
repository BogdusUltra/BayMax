using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BayMax.Services
{
    public class NetBridge : IDisposable
    {
        private readonly Dictionary<string, PublisherSocket> _publishers = new Dictionary<string, PublisherSocket>();
        private readonly Dictionary<string, int> _publisherPorts = new Dictionary<string, int>();

        private readonly Dictionary<string, SubscriberSocket> _subscribers = new Dictionary<string, SubscriberSocket>();

        private NetMQPoller _poller;

        public void Start()
        {
            Stop();
            _poller = new NetMQPoller();
            _poller.RunAsync();
            LoggerService.Log("[UI МОСТ] Запущен с использованием NetMQPoller.", LogLevel.Info);
        }

        public int GetPublisherPort(string pinId)
        {
            if (!_publishers.ContainsKey(pinId))
            {
                var pub = new PublisherSocket();
                int port = pub.BindRandomPort("tcp://*");
                _publishers[pinId] = pub;
                _publisherPorts[pinId] = port;
                LoggerService.Log($"[UI МОСТ] Открыт порт {port} для вывода данных (Пин: {pinId})", LogLevel.Debug);
            }
            return _publisherPorts[pinId];
        }

        public void SendData(string pinId, string data)
        {
            if (_publishers.TryGetValue(pinId, out var pub))
            {
                pub.SendFrame(data); // Отправляем чистые данные в один кадр
            }
        }

        public void ConnectToAgent(string pinId, string address, Action<string> onMessageReceived)
        {
            if (!_subscribers.ContainsKey(pinId))
            {
                var sub = new SubscriberSocket();
                sub.Connect(address);
                sub.Subscribe(""); // Слушаем весь поток на этом порту

                sub.ReceiveReady += (s, e) =>
                {
                    if (e.Socket.TryReceiveFrameString(out string msg))
                    {
                        // Передаем данные в UI поток
                        Application.Current.Dispatcher.Invoke(() => onMessageReceived(msg));
                    }
                };

                _subscribers[pinId] = sub;
                _poller.Add(sub); // Отдаем сокет под управление поллеру

                LoggerService.Log($"[UI МОСТ] Подписан на {address}", LogLevel.Debug);
            }
        }

        public void Stop()
        {
            if (_poller != null)
            {
                _poller.StopAsync();
                _poller.Dispose();
                _poller = null;
            }

            foreach (var p in _publishers.Values) p.Dispose();
            foreach (var s in _subscribers.Values) s.Dispose();

            _publishers.Clear();
            _publisherPorts.Clear();
            _subscribers.Clear();
        }

        public void Dispose() => Stop();
    }
}