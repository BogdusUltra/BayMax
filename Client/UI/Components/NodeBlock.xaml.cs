using BayMax.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BayMax.UI.Components
{

    public enum NodeType
    {
        UI, // C# (Синий)
        Logic   // Python (Зеленый/Желтый)
    }

    public enum PinType
    {
        Input,
        Output
    }

    public partial class NodeBlock : UserControl
    {
        private bool _isDragging;
        private Point _clickPosition;

        public NodeType Type { get; private set; }
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string LogicNodeTypeName { get; set; } = "UnknownNode";

        public List<NodePin> Pins { get; } = new List<NodePin>();

        public event Action Moved;
        public event Action Resized;

        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        public Action OnSaveSettings { get; set; }
        public Action OnLoadSettings { get; set; }

        public event Action<NodeBlock, bool> NodeSelected; // Передает саму ноду и флаг multiSelect
        public event Action<NodeBlock> DeleteRequested;    // Просит родителя удалить эту ноду
        public event Action NodeModified;
        public event Action<NodeBlock, NodePin> PinInteractionRequested;
        public event Action<NodeBlock> RightClicked;

        public bool IsDeployedMode { get; private set; }

        public Device TargetDevice { get; private set; }
        public bool IsSelected { get; private set; }
        private Brush _originalBorderBrush;

        public NodeBlock(NodeType type, string title)
        {
            InitializeComponent();

            Type = type;
            SetupVisuals(title);
        }

        public void BindDevices(IEnumerable<Device> devices, string savedIp = null)
        {
            if (Type != NodeType.Logic) return;

            DeviceSelector.ItemsSource = devices;

            if (!string.IsNullOrEmpty(savedIp))
            {
                var device = devices.FirstOrDefault(d => d.Ip == savedIp);
                if (device != null) DeviceSelector.SelectedItem = device;
            }
            else if (devices.Any())
            {
                DeviceSelector.SelectedIndex = 0;
            }
        }

        private void SetupVisuals(string title)
        {
            if (Type == NodeType.UI)
            {
                NodeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2dd5e1"));
                NodeTitle.Text = title;
                DeviceSelector.Visibility = Visibility.Collapsed;
            }
            else if (Type == NodeType.Logic)
            {
                NodeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7FC00"));
                NodeTitle.Text = $"[Логика] {title}";
                DeviceSelector.Visibility = Visibility.Visible;
                // ФИКС 2: Убрана прямая привязка к MainWindow, теперь все делается в NodeBlock_Loaded
            }
        }

        public void SetContent(UIElement customUI)
        {
            CustomContentContainer.Content = customUI;
        }

        public void SetSelected(bool isSelected)
        {
            IsSelected = isSelected;

            if (IsSelected)
            {
                if (_originalBorderBrush == null) _originalBorderBrush = NodeBorder.BorderBrush;

                if (Type == NodeType.UI)
                {
                    NodeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#78dbe2"));
                }
                else
                {
                    NodeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e1ff85"));
                }

                NodeBorder.BorderThickness = new Thickness(3);
            }
            else
            {
                if (_originalBorderBrush != null) NodeBorder.BorderBrush = _originalBorderBrush;
                NodeBorder.BorderThickness = new Thickness(2);
            }
        }

        private void ResizeNode_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (LockToggle.IsChecked == true) return;

            if (double.IsNaN(this.Width)) this.Width = this.ActualWidth;
            if (double.IsNaN(this.Height)) this.Height = this.ActualHeight;

            double targetWidth = this.Width + e.HorizontalChange;
            double targetHeight = this.Height + e.VerticalChange;

            CustomContentContainer.Measure(new Size(targetWidth, double.PositiveInfinity));

            double contentMinWidth = CustomContentContainer.DesiredSize.Width + 40;
            double extraWidth = this.ActualWidth - CustomContentContainer.ActualWidth;

            // Защита от деления на ноль до полной отрисовки
            if (extraWidth <= 0 || double.IsNaN(extraWidth)) extraWidth = 60;

            double contentMinHeight = CustomContentContainer.DesiredSize.Height + 40;
            int maxPins = Math.Max(InputPinsContainer.Children.Count, OutputPinsContainer.Children.Count);
            double minPinsHeight = (maxPins * 15) + 40;

            double minHeight = Math.Max(minPinsHeight, contentMinHeight);
            double minWidth = Math.Max(100, contentMinWidth + extraWidth);

            this.Width = Math.Max(minWidth, targetWidth);
            this.Height = Math.Max(minHeight, targetHeight);

            Resized?.Invoke();

            e.Handled = true;
        }

        private void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TargetDevice = DeviceSelector.SelectedItem as Device;

            if (TargetDevice == null && Type == NodeType.Logic)
            {
                DeviceSelector.BorderBrush = Brushes.Red;
                DeviceSelector.ToolTip = "ВНИМАНИЕ: Выберите машину для выполнения!";
            }
            else
            {
                DeviceSelector.BorderBrush = Brushes.Gray;
                DeviceSelector.ToolTip = "Устройство для выполнения: " + TargetDevice?.Name;
            }

            NodeModified?.Invoke();
        }

        private void BringToFront()
        {
            if (VisualTreeHelper.GetParent(this) is Canvas parentCanvas)
            {
                int maxZ = 0;

                foreach (UIElement child in parentCanvas.Children)
                {
                    if (child is NodeBlock)
                    {
                        int Z = Panel.GetZIndex(child);
                        if (Z > maxZ)
                        {
                            maxZ = Z;
                        }
                    }
                }

                Panel.SetZIndex(this, maxZ + 1);
            }
        }

        public NodePin AddPin(string title, PinType pinType, PinDataType dataType = PinDataType.Any)
        {
            var newPin = new NodePin(title, pinType, dataType);

            newPin.PinInteractionRequested += (pin) => PinInteractionRequested?.Invoke(this, pin);

            if (pinType == PinType.Input)
            {
                InputPinsContainer.Children.Add(newPin);
            }
            else
            {
                OutputPinsContainer.Children.Add(newPin);
            }

            Pins.Add(newPin);
            return newPin;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);

            if (IsDeployedMode) return;

            RightClicked?.Invoke(this);

            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            try
            {
                base.OnMouseLeftButtonDown(e);

                if (IsDeployedMode)
                {
                    this.Focus();
                    BringToFront();
                    return;
                }

                bool isMultiSelect = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

                NodeSelected?.Invoke(this, isMultiSelect);

                BringToFront();
                this.Focus();

                if (LockToggle.IsChecked != true)
                {
                    _isDragging = true;
                    _clickPosition = e.GetPosition(this);
                    this.CaptureMouse();
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                this.ReleaseMouseCapture();
                MessageBox.Show($"Ошибка при клике на ноду: {ex.Message}");
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging)
            {
                if (VisualTreeHelper.GetParent(this) is Canvas parentCanvas)
                {
                    Point mousePos = e.GetPosition(parentCanvas);

                    double newX = mousePos.X - _clickPosition.X;
                    double newY = mousePos.Y - _clickPosition.Y;

                    double maxX = parentCanvas.ActualWidth - ActualWidth;
                    double maxY = parentCanvas.ActualHeight - ActualHeight;

                    newX = Math.Max(0, Math.Min(newX, maxX));
                    newY = Math.Max(0, Math.Min(newY, maxY));

                    Canvas.SetLeft(this, newX);
                    Canvas.SetTop(this, newY);

                    Moved?.Invoke();
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isDragging = false;
            this.ReleaseMouseCapture();

            e.Handled = true;
        }

        private void DeleteNode_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this);
            e.Handled = true;
        }

        private void CopyNode_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Копирование нод пока в разработке! \nПотребуется сериализация графа.",
                            "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CutNode_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вырезание нод пока в разработке!",
                            "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void SetDeployedMode(bool isDeployed)
        {
            IsDeployedMode = isDeployed;

            LockToggle.IsChecked = isDeployed;
            LockToggle.IsEnabled = !isDeployed;
            DeleteBtn.IsEnabled = !isDeployed;

            if (Type == NodeType.Logic)
            {
                CustomContentContainer.IsEnabled = !isDeployed;
                this.Opacity = isDeployed ? 0.7 : 1.0;
            }
            else
            {
                // Для UI нод контент остается активным
                DeleteBtn.Opacity = isDeployed ? 0.5 : 1.0;
                LockToggle.Opacity = isDeployed ? 0.5 : 1.0;
            }

            InputPinsContainer.Opacity = isDeployed ? 0.4 : 1.0;
            OutputPinsContainer.Opacity = isDeployed ? 0.4 : 1.0;

            if (DeviceSelector != null) DeviceSelector.IsEnabled = !isDeployed;

            foreach (var pin in Pins)
            {
                pin.UpdateTooltip(isDeployed);
            }
        }

        public void RefreshStructure(CustomPythonNode meta)
        {
            InputPinsContainer.Children.Clear();
            OutputPinsContainer.Children.Clear();
            Pins.Clear();

            foreach (var input in meta.Inputs)
            {
                if (!Enum.TryParse(input.DataType, true, out PinDataType parsedType))
                {
                    parsedType = PinDataType.Any;
                }
                AddPin(input.Name, PinType.Input, parsedType);
            }
            foreach (var output in meta.Outputs)
            {
                if (!Enum.TryParse(output.DataType, true, out PinDataType parsedType))
                {
                    parsedType = PinDataType.Any;
                }
                AddPin(output.Name, PinType.Output, parsedType);
            }

            NodeTitle.Text = $"[Логика] {meta.Title}";

            Resized?.Invoke();
        }
    }
}