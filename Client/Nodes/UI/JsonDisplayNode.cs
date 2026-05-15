using BayMax.UI.Components;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BayMax.Nodes.UI
{
    public class JsonDisplayNode : INodeBuilder
    {
        public string NodeName => "JSON (Вывод)";
        public string Category => "UI (Вывод)";

        public NodeBlock CreateNode()
        {
            var node = new NodeBlock(NodeType.UI, "Инспектор JSON");

            node.LogicNodeTypeName = NodeName;

            var inPin = node.AddPin("Данные", PinType.Input, PinDataType.Json);

            var textBox = new TextBox
            {
                Text = "{ ... }",
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100)), // Красноватый
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Width = 200,
                Height = 120,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.DarkGray,
                Margin = new Thickness(5)
            };

            node.SetContent(textBox);

            inPin.ValueChanged += (val) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (val == null) return;

                    try
                    {
                        // Если DataConverter вернул нам JsonDocument, форматируем его красиво
                        if (val is JsonDocument doc)
                        {
                            var options = new JsonSerializerOptions { WriteIndented = true };
                            textBox.Text = JsonSerializer.Serialize(doc, options);
                        }
                        else
                        {
                            // На всякий случай, если пришла просто строка
                            textBox.Text = val.ToString();
                        }

                        // Прокручиваем вниз при новых данных
                        textBox.ScrollToEnd();
                    }
                    catch
                    {
                        textBox.Text = "Ошибка парсинга JSON";
                    }
                });
            };

            return node;
        }
    }
}