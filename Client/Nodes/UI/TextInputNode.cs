using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BayMax.UI.Components;

namespace BayMax.Nodes.UI
{
    public class TextInputNode : INodeBuilder
    {
        public string NodeName => "Текст (Ввод)";
        public string Category => "UI (Ввод)";

        public NodeBlock CreateNode()
        {
            var node = new NodeBlock(NodeType.UI, NodeName);

            node.LogicNodeTypeName = NodeName;

            node.Width = 200;
            node.Height = 150;

            NodePin outPin = node.AddPin("Текст", PinType.Output, PinDataType.String);

            var panel = new Grid();

            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var streamToggle = new CheckBox
            {
                Content = "Стримить данные",
                IsChecked = true,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 5),
                Cursor = Cursors.Hand
            };

            Grid.SetRow(streamToggle, 0);

            var textBox = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinWidth = 120,
                MinHeight = 60,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                Padding = new Thickness(5)
            };
            Grid.SetRow(textBox, 1);

            textBox.TextChanged += (s, e) =>
            {
                if (streamToggle.IsChecked == true)
                {
                    outPin.SetValue(textBox.Text);
                }
            };

            textBox.PreviewKeyDown += (s, e) =>
            {
                if (streamToggle.IsChecked == false && e.Key == Key.Enter)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;

                    outPin.SetValue(textBox.Text);
                    textBox.Text = "";

                    e.Handled = true;
                }
            };

            panel.Children.Add(streamToggle);
            panel.Children.Add(textBox);
            node.SetContent(panel);

            node.OnSaveSettings = () =>
            {
                node.Settings["TextValue"] = textBox.Text;
                node.Settings["IsStreaming"] = streamToggle.IsChecked.ToString();
            };

            node.OnLoadSettings = () =>
            {
                if (node.Settings.TryGetValue("TextValue", out string savedText))
                {
                    textBox.Text = savedText;
                }

                if (node.Settings.TryGetValue("IsStreaming", out string isStreamStr) && bool.TryParse(isStreamStr, out bool isStreaming))
                {
                    streamToggle.IsChecked = isStreaming;
                }
            };

            return node;
        }
    }
}