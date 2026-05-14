using BayMax.UI.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BayMax.Nodes.UINodes
{
    public class BoolDisplayNode : INodeBuilder
    {
        public string NodeName => "Индикатор (Вывод)";
        public string Category => "UI (Вывод)";

        public NodeBlock CreateNode()
        {
            var node = new NodeBlock(NodeType.UI, "Статус");

            node.LogicNodeTypeName = NodeName;

            var inPin = node.AddPin("Вход", PinType.Input, PinDataType.Boolean);

            var textBlock = new TextBlock
            {
                Text = "❌ Ожидание",
                Foreground = Brushes.Gray,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };

            node.SetContent(textBlock);

            inPin.ValueChanged += (val) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (val is bool state)
                    {
                        if (state)
                        {
                            textBlock.Text = "✅ TRUE";
                            textBlock.Foreground = Brushes.LimeGreen;
                        }
                        else
                        {
                            textBlock.Text = "❌ FALSE";
                            textBlock.Foreground = Brushes.Tomato;
                        }
                    }
                });
            };

            return node;
        }
    }
}