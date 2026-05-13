using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BayMax.UI.Controls
{
    public partial class NodePin : UserControl
    {
        public PinType Type { get; private set; }
        public PinDataType DataType { get; private set; }
        public string Title { get; private set; }

        public string Id { get; } = Guid.NewGuid().ToString("N");

        public NodeBlock ParentNode => this.FindParent<NodeBlock>();

        public int ConnectionCount { get; private set; } = 0;
        public Ellipse PinDot => PinCircle;

        public object DataValue { get; private set; }

        public event Action<object> ValueChanged;

        public string NetworkAddress { get; set; }


        public NodePin(string title, PinType type, PinDataType dataType = PinDataType.Any)
        {
            InitializeComponent();

            Type = type;
            DataType = dataType;
            Title = title;

            PinContainer.Children.Clear();
            if (Type == PinType.Output)
            {
                PinContainer.Children.Add(PinTitle);
                PinContainer.Children.Add(PinDot);
                PinTitle.Margin = new Thickness(0, 0, 5, 0);
            }
            else
            {
                PinContainer.Children.Add(PinDot);
                PinContainer.Children.Add(PinTitle);
                PinTitle.Margin = new Thickness(5, 0, 0, 0);
            }

            PinTitle.Text = Title;

            UpdateVisuals();

            UpdateTooltip(false);
        }

        public void UpdateTooltip(bool isDeployed = false)
        {
            string tooltipText = $"Тип: {DataType}";

            if (isDeployed && !string.IsNullOrEmpty(NetworkAddress))
            {
                string cleanAddress = NetworkAddress.Replace("tcp://", "");

                if (Type == PinType.Output)
                    tooltipText += $"\nВещает на: {cleanAddress}";
                else
                    tooltipText += $"\nСлушает: {cleanAddress}";
            }

            this.ToolTip = tooltipText;
        }

        public void AddConnection()
        {
            ConnectionCount++;
            UpdateVisuals();
        }

        public void RemoveConnection()
        {
            ConnectionCount--;
            if (ConnectionCount < 0) ConnectionCount = 0;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            bool isConnected = ConnectionCount > 0;

            Color baseColor = DataTypeColors.GetBaseColor(DataType);
            Color brightColor = DataTypeColors.GetBrightColor(DataType);

            if (Type == PinType.Input)
            {
                if (isConnected)
                {
                    PinDot.Fill = new SolidColorBrush(brightColor);
                    PinDot.Stroke = new SolidColorBrush(brightColor);
                    PinDot.StrokeThickness = 0;
                }
                else
                {
                    PinDot.Fill = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    PinDot.Stroke = new SolidColorBrush(baseColor);
                    PinDot.StrokeThickness = 3;
                }
            }
            else
            {
                if (isConnected)
                {
                    PinDot.Fill = new SolidColorBrush(brightColor);
                    PinDot.Stroke = new SolidColorBrush(brightColor);
                }
                else
                {
                    PinDot.Fill = new SolidColorBrush(baseColor);
                    PinDot.Stroke = new SolidColorBrush(baseColor);
                }
            }
        }

        public void SetValue(object newValue)
        {
            DataValue = newValue;
            ValueChanged?.Invoke(newValue);
        }
        
        private void PinCircle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null && !(parent is NodeCanvas))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            if (parent is NodeCanvas canvas)
            {
                if (Type == PinType.Output)
                {
                    canvas.StartConnection(this);
                }
                else if (Type == PinType.Input)
                {
                    canvas.RemoveConnection(this);
                }

                e.Handled = true;
            }
        }
    }
}

