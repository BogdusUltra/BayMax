using BayMax.Nodes;
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

        public Ellipse PinDot => PinCircle;

        public object DataValue { get; private set; }

        public event Action<object> ValueChanged;
        

        public NodePin(string title, PinType type, PinDataType dataType = PinDataType.Any)
        {
            InitializeComponent();

            Type = type;
            DataType = dataType;
            Title = title;

            PinTitle.Text = Title;

            PinDot.Fill = DataTypeColors.GetColor(dataType);
            PinDot.Stroke = DataTypeColors.GetColor(dataType);

            if (type == PinType.Output)
            {
                PinContainer.Children.Remove(PinCircle);
                PinContainer.Children.Add(PinCircle);

                PinContainer.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                PinContainer.HorizontalAlignment = HorizontalAlignment.Left;
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

