using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BayMax.UI.Controls;

namespace BayMax.Nodes.UI
{
    public class NumberInputNode : INodeBuilder
    {
        public string NodeName => "Ввод числа";
        public string Category => "UI";

        public NodeBlock CreateNode()
        {
            var node = new NodeBlock(NodeType.UI, NodeName);

            node.Width = 150;
            node.Height = 100;

            NodePin outPin = node.AddPin("Число", PinType.Output, PinDataType.Number);

            var textBox = new TextBox
            {
                Text = "0",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                Padding = new Thickness(5),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray
            };

            textBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(textBox.Text, out double result))
                {
                    textBox.BorderBrush = Brushes.Gray; 
                    outPin.SetValue(result);            
                }
                else
                {
                    textBox.BorderBrush = Brushes.Red;
                }
            };

            node.SetContent(textBox);
            return node;
        }
    }
}