using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using BayMax.Nodes;

namespace BayMax.UI.Controls
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

        public List<NodePin> Pins { get; } = new List<NodePin>();

        public event Action Moved;
        public event Action Resized;

        public bool IsSelected { get; private set; }
        private Brush _originalBorderBrush;

        public NodeBlock(NodeType type, string title)
        {
            InitializeComponent();

            Type = type;
            SetupVisuals(title);
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

                NodeBorder.BorderBrush = new SolidColorBrush(Colors.White);
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

            double newWidth = this.Width + e.HorizontalChange;
            double newHeight = this.Height + e.VerticalChange;

            int maxPins = Math.Max(InputPinsContainer.Children.Count, OutputPinsContainer.Children.Count);
            double minRequiredHeight = (maxPins * 15);

            double finalMinHeight = Math.Max(70, minRequiredHeight);

            this.Width = Math.Max(120, newWidth);
            this.Height = Math.Max(finalMinHeight, newHeight);

            Resized?.Invoke();

            e.Handled = true;
        }

        private void SetupVisuals(string title)
        {
            if (Type == NodeType.UI)
            {
                NodeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC")); 
                NodeTitle.Text = $"[Интерфейс] {title}";
            }
            else if (Type == NodeType.Logic)
            {
                NodeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7FC00"));
                NodeTitle.Text = $"[Логика] {title}";
            }
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

                // Берем самый высокий слой, прибавляем 1 и назначаем себе
                Panel.SetZIndex(this, maxZ + 1);
            }
        }

        public NodePin AddPin(string title, PinType pinType, PinDataType dataType = PinDataType.Any)
        {
            var newPin = new NodePin(title, pinType, dataType);

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

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            try
            {
                base.OnMouseLeftButtonDown(e);

                this.Focus();

                BringToFront();

                var canvas = this.FindParent<NodeCanvas>();
                if (canvas != null)
                {
                    bool isMultiSelect = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

                    canvas.SelectNode(this, isMultiSelect);
                }

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
            MessageBoxResult result = MessageBox.Show(
                "Вы уверены, что хотите удалить эту ноду и все её связи?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var canvas = this.FindParent<NodeCanvas>();
                if (canvas != null)
                {
                    canvas.DeleteNode(this);
                }
            }

            e.Handled = true;
        }
    }
}