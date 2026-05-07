using BayMax.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BayMax.UI.Controls
{
    public partial class NodeCanvas : UserControl
    {
        private Point _rightClickPoint;

        public List<ConnectionLine> Connections { get; private set; } = new List<ConnectionLine>();
        private ConnectionLine _draftLine;
        private NodePin _magneticTarget;

        public List<NodeBlock> SelectedNodes { get; } = new List<NodeBlock>();
        private bool _isMarqueeSelecting;
        private Point _marqueeStartPoint;

        public event Action SelectionChanged;

        public NodeCanvas()
        {
            InitializeComponent();

            DrawArea.MouseRightButtonDown += OnCanvasRightClick;
        }

        private void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
        {
            _rightClickPoint = e.GetPosition(DrawArea);

            var contextMenu = new ContextMenu();

            var groupedNodes = NodeRegistry.AvailableNodes.GroupBy(n => n.Category);

            foreach (var group in groupedNodes)
            {
                var categoryItem = new MenuItem { Header = group.Key };

                foreach (var nodeBuilder in group)
                {
                    var nodeItem = new MenuItem { Header = nodeBuilder.NodeName };

                    nodeItem.Click += (s, args) =>
                    {
                        NodeBlock newNode = nodeBuilder.CreateNode();
                        SpawnNode(newNode);
                    };

                    categoryItem.Items.Add(nodeItem);
                }

                contextMenu.Items.Add(categoryItem);
            }

            contextMenu.PlacementTarget = DrawArea;
            contextMenu.IsOpen = true;
        }

        public void ClearSelection()
        {
            foreach (var node in SelectedNodes)
            {
                node.SetSelected(false);
            }
            SelectedNodes.Clear();
            SelectionChanged?.Invoke();
        }

        public void SelectNode(NodeBlock node, bool multiSelect = false)
        {
            if (!multiSelect)
            {
                ClearSelection();
                node.SetSelected(true);
                SelectedNodes.Add(node);
            }
            else
            {
                if (SelectedNodes.Contains(node))
                {
                    node.SetSelected(false);
                    SelectedNodes.Remove(node);
                }
                else
                {
                    node.SetSelected(true);
                    SelectedNodes.Add(node);
                }
            }
            SelectionChanged?.Invoke();
        }

        public void StartConnection(NodePin startPin)
        {
            _draftLine = new ConnectionLine(startPin);
            DrawArea.Children.Add(_draftLine.PathElement);

            DrawArea.CaptureMouse();
        }

        public void RemoveConnection(NodePin inputPin)
        {
            ConnectionLine lineToRemove = null;
            foreach (var conn in Connections)
            {
                if (conn.EndPin == inputPin)
                {
                    lineToRemove = conn;
                    break;
                }
            }

            if (lineToRemove != null)
            {
                DrawArea.Children.Remove(lineToRemove.PathElement);
                Connections.Remove(lineToRemove);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (e.OriginalSource == DrawArea)
            {
                bool isMultiSelect = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

                DrawArea.Focus();

                if (!isMultiSelect)
                {
                    ClearSelection();
                }

                _isMarqueeSelecting = true;
                _marqueeStartPoint = e.GetPosition(DrawArea);

                Canvas.SetLeft(SelectionMarquee, _marqueeStartPoint.X);
                Canvas.SetTop(SelectionMarquee, _marqueeStartPoint.Y);
                SelectionMarquee.Width = 0;
                SelectionMarquee.Height = 0;
                SelectionMarquee.Visibility = Visibility.Visible;

                DrawArea.CaptureMouse();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isMarqueeSelecting)
            {
                Point currentPos = e.GetPosition(DrawArea);

                double x = Math.Min(_marqueeStartPoint.X, currentPos.X);
                double y = Math.Min(_marqueeStartPoint.Y, currentPos.Y);
                double width = Math.Abs(_marqueeStartPoint.X - currentPos.X);
                double height = Math.Abs(_marqueeStartPoint.Y - currentPos.Y);

                Canvas.SetLeft(SelectionMarquee, x);
                Canvas.SetTop(SelectionMarquee, y);
                SelectionMarquee.Width = width;
                SelectionMarquee.Height = height;
                return;
            }

            if (_draftLine != null)
            {
                Point startPos = _draftLine.StartPin.PinDot.TranslatePoint(new Point(6, 6), DrawArea);
                Point mousePos = e.GetPosition(DrawArea);

                _magneticTarget = null;
                double snapRadius = 25.0; // Радиус магнита (25 пикселей)
                double minDistance = double.MaxValue;

                foreach (UIElement element in DrawArea.Children)
                {
                    if (element is NodeBlock node)
                    {
                        foreach (NodePin pin in node.Pins)
                        {
                            NodeBlock startNode = _draftLine.StartPin.FindParent<NodeBlock>();

                            if (startNode != node && pin.Type == PinType.Input)
                            {
                                if (_draftLine.StartPin.DataType != pin.DataType && _draftLine.StartPin.DataType != PinDataType.Any && pin.DataType != PinDataType.Any)
                                {
                                    continue; 
                                }

                                Point pinPos = pin.PinDot.TranslatePoint(new Point(6, 6), DrawArea);

                                double dx = mousePos.X - pinPos.X;
                                double dy = mousePos.Y - pinPos.Y;
                                double distance = Math.Sqrt(dx * dx + dy * dy);

                                if (distance <= snapRadius && distance < minDistance)
                                {
                                    minDistance = distance;
                                    _magneticTarget = pin;
                                }
                            }
                        }
                    }
                }

                Point targetPos = _magneticTarget != null ? _magneticTarget.PinDot.TranslatePoint(new Point(6, 6), DrawArea) : mousePos;

                _draftLine.UpdatePosition(startPos, targetPos);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_isMarqueeSelecting)
            {


                _isMarqueeSelecting = false;
                SelectionMarquee.Visibility = Visibility.Collapsed;
                DrawArea.ReleaseMouseCapture();

                Rect marqueeRect = new Rect(Canvas.GetLeft(SelectionMarquee), Canvas.GetTop(SelectionMarquee), SelectionMarquee.Width, SelectionMarquee.Height);

                foreach (UIElement child in DrawArea.Children)
                {
                    if (child is NodeBlock node)
                    {
                        double nodeX = Canvas.GetLeft(node);
                        double nodeY = Canvas.GetTop(node);
                        double nodeW = double.IsNaN(node.Width) ? node.ActualWidth : node.Width;
                        double nodeH = double.IsNaN(node.Height) ? node.ActualHeight : node.Height;

                        Rect nodeRect = new Rect(nodeX, nodeY, nodeW, nodeH);

                        if (marqueeRect.IntersectsWith(nodeRect))
                        {
                            if (!SelectedNodes.Contains(node))
                            {
                                node.SetSelected(true);
                                SelectedNodes.Add(node);
                            }
                        }
                    }
                }
                SelectionChanged?.Invoke();
                return;
            }

            if (_draftLine != null)
            {
                DrawArea.ReleaseMouseCapture();

                if (_magneticTarget != null)
                {
                    _draftLine.EndPin = _magneticTarget;

                    Point startPos = _draftLine.StartPin.PinDot.TranslatePoint(new Point(6, 6), DrawArea);
                    Point endPos = _magneticTarget.PinDot.TranslatePoint(new Point(6, 6), DrawArea);

                    _draftLine.UpdatePosition(startPos, endPos);
                    Connections.Add(_draftLine);
                }
                else
                {
                    DrawArea.Children.Remove(_draftLine.PathElement);
                }
                _draftLine = null;
                _magneticTarget = null;
            }
        }

        public void AddUINode()
        {
            var newNode = new NodeBlock(NodeType.UI, "Панель вывода");

            newNode.AddPin("Вход 1", PinType.Input);
            newNode.AddPin("Вход 2", PinType.Input);
            newNode.AddPin("Результат", PinType.Output);

            SpawnNode(newNode);
        }

        public void AddLogicNode()
        {
            var newNode = new NodeBlock(NodeType.Logic, "Чтение датчика");

            newNode.AddPin("Вход 1", PinType.Input);
            newNode.AddPin("Вход 2", PinType.Input);
            newNode.AddPin("Вход 3", PinType.Input);
            newNode.AddPin("Вход 1", PinType.Input);
            newNode.AddPin("Вход 2", PinType.Input);
            newNode.AddPin("Вход 3", PinType.Input);
            newNode.AddPin("Вход 1", PinType.Input);
            newNode.AddPin("Вход 2", PinType.Input);
            newNode.AddPin("Вход 3", PinType.Input);
            newNode.AddPin("Вход 1", PinType.Input);
            newNode.AddPin("Вход 2", PinType.Input);
            newNode.AddPin("Вход 3", PinType.Input);
            newNode.AddPin("Вход 1", PinType.Input);
            newNode.AddPin("Вход 2", PinType.Input);
            newNode.AddPin("Вход 3", PinType.Input);
            newNode.AddPin("Результат 1", PinType.Output);
            newNode.AddPin("Результат 2", PinType.Output);

            SpawnNode(newNode);
        }

        private void SpawnNode(NodeBlock node)
        {
            node.Moved += () => UpdateLinesForNode(node);
            node.Resized += () => UpdateLinesForNode(node);
            Canvas.SetLeft(node, _rightClickPoint.X);
            Canvas.SetTop(node, _rightClickPoint.Y);
            DrawArea.Children.Add(node);
        }

        private void UpdateLinesForNode(NodeBlock node)
        {
            foreach (var line in Connections)
            {
                if (line.StartPin.FindParent<NodeBlock>() == node || line.EndPin.FindParent<NodeBlock>() == node)
                {
                    Point startPos = line.StartPin.PinDot.TranslatePoint(new Point(6, 6), DrawArea);
                    Point endPos = line.EndPin.PinDot.TranslatePoint(new Point(6, 6), DrawArea);

                    line.UpdatePosition(startPos, endPos);
                }
            }
        }

        public void DeleteNode(NodeBlock node)
        {
            List<ConnectionLine> linesToRemove = new List<ConnectionLine>();
            foreach (var conn in Connections)
            {
                if (conn.StartPin.FindParent<NodeBlock>() == node || conn.EndPin.FindParent<NodeBlock>() == node)
                {
                    linesToRemove.Add(conn);
                }
            }

            foreach (var line in linesToRemove)
            {
                DrawArea.Children.Remove(line.PathElement);
                Connections.Remove(line);
            }

            DrawArea.Children.Remove(node);
        }

        public void DeleteSelectedNodes()
        {
            var nodesToDelete = SelectedNodes.ToList();
            foreach (var node in nodesToDelete)
            {
                DeleteNode(node);
            }
            ClearSelection();
        }

        private void AddUINode_Click(object sender, RoutedEventArgs e)
        {
            AddUINode();
        }

        private void AddLogicNode_Click(object sender, RoutedEventArgs e)
        {
            AddLogicNode();
        }
    }
}