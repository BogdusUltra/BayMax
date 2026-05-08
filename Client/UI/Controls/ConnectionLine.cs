using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;


namespace BayMax.UI.Controls
{
    public class ConnectionLine
    {
        public Path PathElement { get; private set; }
        public NodePin StartPin { get; set; }
        private NodePin _endPin;
        public NodePin EndPin 
        { 
            get => _endPin;
            set
            {
                _endPin = value;
                if (_endPin != null)
                {
                    PathElement.Stroke = new SolidColorBrush(DataTypeColors.GetBrightColor(StartPin.DataType));
                    StartPin.ValueChanged += TransmitData;

                    if (StartPin.DataValue != null)
                    {
                        TransmitData(StartPin.DataValue);
                    }
                }
            }
        }

        public ConnectionLine(NodePin startPin = null)
        {
            StartPin = startPin;

            PathElement = new Path
            {
                Stroke = new SolidColorBrush(DataTypeColors.GetBaseColor(startPin.DataType)),
                StrokeThickness = 3,
                IsHitTestVisible = false
            };
        }

        private void TransmitData(object data)
        {
            EndPin?.SetValue(data);
        }

        public void Disconnect()
        {
            if (StartPin != null)
            {
                StartPin.ValueChanged -= TransmitData; 
            }
            EndPin?.SetValue(null);
        }

        public void UpdatePosition(Point startPoint, Point endPoint)
        {
            double offset = Math.Max(50, Math.Abs(endPoint.X - startPoint.X) / 2);

            var figure = new PathFigure { StartPoint = startPoint };

            var bezierSegment = new BezierSegment
            {
                Point1 = new Point(startPoint.X + offset, startPoint.Y), 
                Point2 = new Point(endPoint.X - offset, endPoint.Y),     
                Point3 = endPoint
            };

            figure.Segments.Add(bezierSegment);

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            PathElement.Data = geometry;
        }
    }
}
