using BayMax.UI.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BayMax.Nodes.UINodes
{
    public class BoolInputNode : INodeBuilder
    {
        public string NodeName => "Переключатель (Ввод)";
        public string Category => "UI (Ввод)";

        public NodeBlock CreateNode()
        {
            var node = new NodeBlock(NodeType.UI, "Тумблер");

            node.LogicNodeTypeName = NodeName;

            // Создаем выходной пин типа Boolean
            var outPin = node.AddPin("Состояние", PinType.Output, PinDataType.Boolean);

            var checkBox = new CheckBox
            {
                Content = "False",
                Foreground = Brushes.LightGray,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };

            node.SetContent(checkBox);

            // Обработка кликов
            checkBox.Checked += (s, e) =>
            {
                checkBox.Content = "True";
                checkBox.Foreground = new SolidColorBrush(Color.FromRgb(218, 112, 214)); // Ярко-фиолетовый
                outPin.SetValue(true);
            };

            checkBox.Unchecked += (s, e) =>
            {
                checkBox.Content = "False";
                checkBox.Foreground = Brushes.LightGray;
                outPin.SetValue(false);
            };

            // Инициализация стартового значения
            outPin.SetValue(false);

            node.OnSaveSettings = () =>
            {
                node.Settings["BoolValue"] = checkBox.IsChecked.ToString();
            };

            node.OnLoadSettings = () =>
            {
                if (node.Settings.TryGetValue("BoolValue", out string savedBool) && bool.TryParse(savedBool, out bool isChecked))
                {
                    checkBox.IsChecked = isChecked;
                }
            };

            return node;
        }
    }
}