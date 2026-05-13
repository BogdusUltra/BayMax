using BayMax.UI.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BayMax.Nodes.UINodes
{
    public class NumberDisplayNode : INodeBuilder
    {
        public string NodeName => "Число (Вывод)";
        public string Category => "UI (Вывод)";

        public NodeBlock CreateNode()
        {
            var node = new NodeBlock(NodeType.UI, "Индикатор числа");

            // Создаем входной пин типа Number
            var inputPin = node.AddPin("Значение", PinType.Input, PinDataType.Number);

            // Создаем UI
            var textBlock = new TextBlock
            {
                Text = "0.00",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 191, 255)), // Голубой
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };

            node.SetContent(textBlock);

            // Подписываемся на новые данные
            inputPin.ValueChanged += (val) =>
            {
                // Обязательно через Dispatcher, так как данные прилетают из сети (ZMQ)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (val != null) textBlock.Text = val.ToString();
                    else textBlock.Text = "---";
                });
            };

            return node;
        }
    }
}