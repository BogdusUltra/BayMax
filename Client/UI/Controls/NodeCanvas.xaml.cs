using BayMax.Nodes;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

        private bool _isPanning;
        private Point _panMouseStartPoint;
        private Point _panTranslateStartPoint;

        public bool IsDeployed { get; set; } = false;

        public NodeCanvas()
        {
            InitializeComponent();

            DrawArea.MouseRightButtonDown += OnCanvasRightClick;
        }

        private void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
        {
            if (IsDeployed) return;

            _rightClickPoint = e.GetPosition(DrawArea);
            DependencyObject clickedElement = e.OriginalSource as DependencyObject;
            NodeBlock clickedNode = clickedElement.FindParent<NodeBlock>();

            var contextMenu = new ContextMenu();

            if (clickedNode != null)
            {
                if (!SelectedNodes.Contains(clickedNode))
                {
                    SelectNode(clickedNode, false);
                }

                if (SelectedNodes.Count > 1)
                {
                    BuildGroupMenu(contextMenu);
                }
                else
                {
                    BuildSingleNodeMenu(contextMenu);
                }
            }
            else
            {
                ClearSelection();
                BuildCanvasMenu(contextMenu);
            }

            contextMenu.PlacementTarget = DrawArea;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void BuildCanvasMenu(ContextMenu menu)
        {
            var nativeNodes = NodeRegistry.AvailableNodes;

            if (nativeNodes != null && nativeNodes.Count > 0)
            {
                var groupedNativeNodes = nativeNodes.GroupBy(n => n.Category);

                foreach (var group in groupedNativeNodes)
                {
                    var categoryItem = new MenuItem { Header = group.Key };

                    foreach (var builder in group)
                    {
                        var nodeItem = new MenuItem { Header = builder.NodeName };

                        nodeItem.Click += (s, e) =>
                        {
                            var newNode = builder.CreateNode();
                            SpawnNode(newNode);
                        };

                        categoryItem.Items.Add(nodeItem);
                    }
                    menu.Items.Add(categoryItem);
                }
            }

            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new Separator());
            }

            var mainWindow = Application.Current.MainWindow as MainWindow;

            if (mainWindow?.NodeManager?.AvailableNodes != null && mainWindow.NodeManager.AvailableNodes.Count > 0)
            {
                var pythonNodes = mainWindow.NodeManager.AvailableNodes;
                var groupedPythonNodes = pythonNodes.GroupBy(n => n.Category);

                foreach (var group in groupedPythonNodes)
                {
                    var categoryItem = new MenuItem { Header = group.Key };

                    foreach (var nodeMeta in group)
                    {
                        var nodeItem = new MenuItem { Header = nodeMeta.Title };
                        nodeItem.Click += (s, a) => SpawnCustomNode(nodeMeta);
                        categoryItem.Items.Add(nodeItem);
                    }
                    menu.Items.Add(categoryItem);
                }
            }
            else
            {
                menu.Items.Add(new MenuItem { Header = "Python нод пока нет", IsEnabled = false });
            }
        }

        public void SpawnCustomNode(Models.CustomPythonNode meta)
        {
            // Создаем логическую ноду (зеленую)
            var newNode = new NodeBlock(NodeType.Logic, meta.Title);

            // ВАЖНО: Запоминаем системное имя для Архитектора
            newNode.LogicNodeTypeName = meta.Name;

            // Генерируем входные пины
            foreach (var inputName in meta.Inputs)
            {
                newNode.AddPin(inputName, PinType.Input);
            }

            // Генерируем выходные пины
            foreach (var outputName in meta.Outputs)
            {
                newNode.AddPin(outputName, PinType.Output);
            }

            // Спавним на холсте (используя твой существующий метод)
            SpawnNode(newNode);
        }

        private void BuildSingleNodeMenu(ContextMenu menu)
        {
            var copyItem = new MenuItem { Header = "Копировать", InputGestureText = "Ctrl+C" };
            var cutItem = new MenuItem { Header = "Вырезать", InputGestureText = "Ctrl+X" };
            var deleteItem = new MenuItem { Header = "Удалить", InputGestureText = "Del", Foreground = Brushes.Red };

            deleteItem.Click += (s, a) => DeleteSelectedNodes();

            menu.Items.Add(copyItem);
            menu.Items.Add(cutItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(deleteItem);
        }

        private void BuildGroupMenu(ContextMenu menu)
        {
            var count = SelectedNodes.Count;
            var header = new MenuItem { Header = $"Выбрано нод: {count}", IsEnabled = false };

            var groupItem = new MenuItem { Header = "Создать группу (Frame)" };
            var deleteItem = new MenuItem { Header = "Удалить выделенное", Foreground = Brushes.Red };

            deleteItem.Click += (s, a) => DeleteSelectedNodes();

            menu.Items.Add(header);
            menu.Items.Add(new Separator());
            menu.Items.Add(groupItem);
            menu.Items.Add(deleteItem);
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
                lineToRemove.StartPin.RemoveConnection();
                lineToRemove.EndPin.RemoveConnection();

                lineToRemove.Disconnect();
                DrawArea.Children.Remove(lineToRemove.PathElement);
                Connections.Remove(lineToRemove);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);        

            if (e.OriginalSource == DrawArea)
            {
                DrawArea.Focus();

                if (IsDeployed) return;

                bool isMultiSelect = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

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
            if (_isPanning)
            {
                Point currentMousePos = e.GetPosition(this);
                Vector delta = currentMousePos - _panMouseStartPoint;

                CanvasPan.X = _panTranslateStartPoint.X + delta.X;
                CanvasPan.Y = _panTranslateStartPoint.Y + delta.Y;
                return; 
            }

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
                                if (pin.Type == PinType.Input && Connections.Any(c => c.EndPin == pin))
                                {
                                    continue;
                                }

                                Point pinPos = pin.PinDot.TranslatePoint(new Point(8, 8), DrawArea);

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

                Point targetPos = _magneticTarget != null ? _magneticTarget.PinDot.TranslatePoint(new Point(8, 8), DrawArea) : mousePos;

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

                    _draftLine.StartPin.AddConnection();
                    _draftLine.EndPin.AddConnection();

                    Point startPos = _draftLine.StartPin.PinDot.TranslatePoint(new Point(8, 8), DrawArea);
                    Point endPos = _magneticTarget.PinDot.TranslatePoint(new Point(8, 8), DrawArea);

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

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _panMouseStartPoint = e.GetPosition(this);
                _panTranslateStartPoint = new Point(CanvasPan.X, CanvasPan.Y);

                DrawArea.CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.MiddleButton == MouseButtonState.Released && _isPanning)
            {
                _isPanning = false;
                DrawArea.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            double zoomFactor = 1.1; 
            double minScale = 0.1; 
            double maxScale = 3.0;

            double oldScale = CanvasScale.ScaleX;
            double newScale = e.Delta > 0 ? oldScale * zoomFactor : oldScale / zoomFactor;

            newScale = Math.Clamp(newScale, minScale, maxScale);

            Point mousePos = e.GetPosition(this);

            CanvasPan.X = mousePos.X - (mousePos.X - CanvasPan.X) * (newScale / oldScale);
            CanvasPan.Y = mousePos.Y - (mousePos.Y - CanvasPan.Y) * (newScale / oldScale);

            CanvasScale.ScaleX = newScale;
            CanvasScale.ScaleY = newScale;

            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Space)
            {
                CanvasScale.ScaleX = 1.0;
                CanvasScale.ScaleY = 1.0;

                double viewportWidth = ActualWidth;
                double viewportHeight = ActualHeight;

                CanvasPan.X = -50000;
                CanvasPan.Y = -50000;

                e.Handled = true;
            }

            else if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (SelectedNodes.Count > 0)
                {
                    DeleteSelectedNodes();
                }
                e.Handled = true;
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
                    Point startPos = line.StartPin.PinDot.TranslatePoint(new Point(8, 8), DrawArea);
                    Point endPos = line.EndPin.PinDot.TranslatePoint(new Point(8, 8), DrawArea);

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
                line.StartPin.RemoveConnection();
                line.EndPin.RemoveConnection();

                line.Disconnect();
                DrawArea.Children.Remove(line.PathElement);
                Connections.Remove(line);
            }

            if (SelectedNodes.Contains(node))
            {
                node.SetSelected(false);
                SelectedNodes.Remove(node);
            }

            DrawArea.Children.Remove(node);

            SelectionChanged?.Invoke();
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

        public void ToggleDeployMode(bool isDeployed)
        {
            IsDeployed = isDeployed;

            ClearSelection();

            if (isDeployed)
            {
                ClearSelection();
            }

            foreach (UIElement child in DrawArea.Children)
            {
                if (child is NodeBlock node)
                {
                    node.SetDeployedMode(isDeployed);
                }
            }

            foreach (var connection in Connections)
            {
                if (connection.PathElement != null)
                {
                    connection.PathElement.Opacity = isDeployed ? 0.3 : 1.0;
                }
            }
        }
    }
}