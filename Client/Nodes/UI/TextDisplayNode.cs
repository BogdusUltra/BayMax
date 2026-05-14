using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BayMax.UI.Controls;

namespace BayMax.Nodes.UI
{
    public class TextDisplayNode : INodeBuilder
    {
        public string NodeName => "Текст (Вывод)";
        public string Category => "UI (Вывод)";

        public NodeBlock CreateNode()
        {
            var node = new NodeBlock(NodeType.UI, NodeName);

            node.LogicNodeTypeName = NodeName;

            node.Width = 200;
            node.Height = 150;

            NodePin inPin = node.AddPin("Данные", PinType.Input, PinDataType.String);

            var displayBox = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.Transparent,
                Foreground = Brushes.LightSkyBlue,
                BorderThickness = new Thickness(0),
                Text = "Ожидание...",
                MinWidth = 120,
                MinHeight = 60,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto // Включаем скролл
            };

            inPin.ValueChanged += (newValue) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string text = newValue?.ToString();
                    displayBox.Text = string.IsNullOrWhiteSpace(text) ? "Ожидание..." : text;

                    // Авто-скролл вниз при получении новых данных (как в чате)
                    displayBox.ScrollToEnd();
                });
            };

            node.SetContent(displayBox);
            return node;
        }
    }
}