using BayMax.Models;
using BayMax.Nodes;
using BayMax.Services;
using System.Collections.ObjectModel;
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
        private BayMaxCore _core;

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

        public ObservableCollection<Device> ProjectDevices { get; } = new ObservableCollection<Device>();

        public bool IsDeployed { get; set; } = false;
        public string FilePath { get; set; } = null;

        public bool IsDirty { get; private set; } = false;
        public event Action IsDirtyChanged;

        public NodeCanvas()
        {
            InitializeComponent();

            DrawArea.MouseRightButtonDown += OnCanvasRightClick;
        }

        public void BindCore(BayMaxCore core)
        {
            _core = core;
        }

        public void SyncLogicNodes()
        {
            if (_core?.AvailablePythonNodes == null) return;

            var nodesOnCanvas = DrawArea.Children.OfType<NodeBlock>()
                                                 .Where(n => n.Type == NodeType.Logic)
                                                 .ToList();

            foreach (var node in nodesOnCanvas)
            {
                var freshMeta = _core.AvailablePythonNodes.FirstOrDefault(m => m.Name == node.LogicNodeTypeName);
                if (freshMeta == null) continue;

                var nodeConnections = Connections.Where(c => c.StartPin.ParentNode == node || c.EndPin.ParentNode == node).ToList();
                var connectionMap = new List<(ConnectionLine line, string pinTitle, PinType pinType, PinDataType otherPinType)> ();

                foreach (var conn in nodeConnections)
                {
                    if (conn.StartPin.ParentNode == node)
                        connectionMap.Add((conn, conn.StartPin.Title, PinType.Output, conn.EndPin.DataType));
                    else
                        connectionMap.Add((conn, conn.EndPin.Title, PinType.Input, conn.StartPin.DataType));
                }

                node.RefreshStructure(freshMeta);

                node.UpdateLayout();

                foreach (var entry in connectionMap)
                {
                    var newPin = node.Pins.FirstOrDefault(p => p.Title == entry.pinTitle && p.Type == entry.pinType);

                    bool keepConnection = false;

                    if (newPin != null)
                    {
                        if (newPin.DataType == entry.otherPinType || newPin.DataType == PinDataType.Any || entry.otherPinType == PinDataType.Any)
                        {
                            keepConnection = true;
                        }
                    }

                    if (keepConnection)
                    {
                        if (entry.pinType == PinType.Output)
                        {
                            entry.line.StartPin = newPin;
                            entry.line.PathElement.Stroke = new SolidColorBrush(DataTypeColors.GetBrightColor(newPin.DataType));
                        }
                        else
                        {
                            entry.line.EndPin = newPin;
                        }

                        newPin.AddConnection();
                    }
                    else
                    {
                        if (entry.line.StartPin != null) entry.line.StartPin.RemoveConnection();
                        if (entry.line.EndPin != null) entry.line.EndPin.RemoveConnection();

                        entry.line.Disconnect();
                        DrawArea.Children.Remove(entry.line.PathElement);
                        Connections.Remove(entry.line);
                    }
                }

                UpdateLinesForNode(node); 
            }
        }

        public ProjectData GetProjectData()
        {
            var data = new ProjectData
            {
                ProjectName = "Новый Проект",
                SaveTime = DateTime.Now
            };

            foreach (NodeBlock node in DrawArea.Children.OfType<NodeBlock>())
            {
                node.OnSaveSettings?.Invoke();
                var nData = new NodeData
                {
                    Id = node.Id,
                    Type = node.Type.ToString(),
                    LogicTypeName = node.LogicNodeTypeName,
                    X = Canvas.GetLeft(node),
                    Y = Canvas.GetTop(node),
                    Width = node.ActualWidth,
                    Height = node.ActualHeight,
                    Settings = new Dictionary<string, string>(node.Settings)
                };

                foreach (var pin in node.Pins)
                {
                    nData.SavedPinIds.Add(pin.Id);
                }

                data.Nodes.Add(nData);
            }

            foreach (var conn in Connections)
            {
                data.Connections.Add(new ConnectionData
                {
                    StartNodeId = conn.StartPin.ParentNode.Id,
                    StartPinId = conn.StartPin.Id,
                    EndNodeId = conn.EndPin.ParentNode.Id,
                    EndPinId = conn.EndPin.Id
                });
            }

            return data;
        }

        public void ClearCanvas()
        {
            foreach (var conn in Connections.ToList())
            {
                DrawArea.Children.Remove(conn.PathElement);
            }
            Connections.Clear();

            var nodes = DrawArea.Children.OfType<NodeBlock>().ToList();
            foreach (var node in nodes)
            {
                DrawArea.Children.Remove(node);
            }

            ClearSelection();
        }

        public void LoadProjectData(ProjectData data)
        {
            ClearCanvas();

            // Словарь для быстрого поиска нод по ID при создании связей
            var nodeMap = new Dictionary<string, NodeBlock>();

            foreach (var nData in data.Nodes)
            {
                NodeBlock newNode = null;

                if (nData.Type == "UI")
                {
                    var builder = NodeRegistry.AvailableNodes.FirstOrDefault(b => b.NodeName == nData.LogicTypeName);
                    if (builder != null) newNode = builder.CreateNode();
                }
                else
                {
                    var meta = _core.AvailablePythonNodes.FirstOrDefault(m => m.Name == nData.LogicTypeName);
                    if (meta != null)
                    {
                        newNode = new NodeBlock(NodeType.Logic, meta.Title) { LogicNodeTypeName = meta.Name };

                        foreach (var i in meta.Inputs) newNode.AddPin(i.Name, PinType.Input, Enum.Parse<PinDataType>(i.DataType, true));
                        foreach (var o in meta.Outputs) newNode.AddPin(o.Name, PinType.Output, Enum.Parse<PinDataType>(o.DataType, true));

                        SetupPythonNodeUI(newNode, meta);
                    }
                }

                if (newNode != null)
                {
                    newNode.Id = nData.Id;

                    for (int i = 0; i < nData.SavedPinIds.Count; i++)
                    {
                        if (i < newNode.Pins.Count)
                        {
                            newNode.Pins[i].Id = nData.SavedPinIds[i];
                        }
                    }

                    newNode.Settings = nData.Settings;
                    newNode.OnLoadSettings?.Invoke();

                    if (newNode.Type == NodeType.Logic && newNode.Settings.TryGetValue("_TargetDeviceIp", out string targetIp))
                    {
                        var device = ProjectDevices.FirstOrDefault(d => d.Ip == targetIp);
                        if (device != null)
                        {
                            newNode.DeviceSelector.SelectedItem = device;
                        }
                    }

                    if (nData.Width > 0) newNode.Width = nData.Width;
                    if (nData.Height > 0) newNode.Height = nData.Height;

                    SpawnNode(newNode, new Point(nData.X, nData.Y));
                    nodeMap[newNode.Id] = newNode;
                }
            }
            DrawArea.UpdateLayout();

            foreach (var cData in data.Connections)
            {
                if (nodeMap.TryGetValue(cData.StartNodeId, out var startNode) &&
                    nodeMap.TryGetValue(cData.EndNodeId, out var endNode))
                {
                    var startPin = startNode.Pins.FirstOrDefault(p => p.Id == cData.StartPinId);
                    var endPin = endNode.Pins.FirstOrDefault(p => p.Id == cData.EndPinId);

                    if (startPin != null && endPin != null)
                    {
                        var line = new ConnectionLine(startPin) { EndPin = endPin };
                        startPin.AddConnection();
                        endPin.AddConnection();

                        DrawArea.Children.Add(line.PathElement);
                        Connections.Add(line);

                        UpdateLinesForNode(startNode);
                    }
                }
            }

            ClearDirty();
        }

        public void MarkAsDirty()
        {
            if (!IsDirty)
            {
                IsDirty = true;
                IsDirtyChanged?.Invoke();
            }
        }

        public void ClearDirty()
        {
            if (IsDirty)
            {
                IsDirty = false;
                IsDirtyChanged?.Invoke();
            }
        }

        private async void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
        {
            if (IsDeployed) return;

            //bool wasUpdated = await _core.AutoRefreshPythonNodesAsync();
            //if (wasUpdated)
            //{
            //    SyncLogicNodes();
            //}

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

            if (_core?.AvailablePythonNodes != null && _core.AvailablePythonNodes.Count > 0)
            {
                var pythonNodes = _core.AvailablePythonNodes;
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
                menu.Items.Add(new MenuItem { Header = "Python нод не найдено", IsEnabled = false });
            }
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

                    MarkAsDirty();
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

        private void SpawnNode(NodeBlock node, Point? position = null)
        {
            node.Moved += () => UpdateLinesForNode(node);
            node.Resized += () => UpdateLinesForNode(node);
            Point finalPos = position ?? _rightClickPoint;
            Canvas.SetLeft(node, finalPos.X);
            Canvas.SetTop(node, finalPos.Y);
            DrawArea.Children.Add(node);
            MarkAsDirty();
        }

        public void SpawnCustomNode(CustomPythonNode meta)
        {
            var newNode = new NodeBlock(NodeType.Logic, meta.Title);
            newNode.LogicNodeTypeName = meta.Name;

            foreach (var input in meta.Inputs)
            {
                if (!Enum.TryParse(input.DataType, true, out PinDataType pType)) pType = PinDataType.Any;
                newNode.AddPin(input.Name, PinType.Input, pType);
            }
            foreach (var output in meta.Outputs)
            {
                if (!Enum.TryParse(output.DataType, true, out PinDataType pType)) pType = PinDataType.Any;
                newNode.AddPin(output.Name, PinType.Output, pType);
            }

            SetupPythonNodeUI(newNode, meta);

            SpawnNode(newNode);
        }

        private void SetupPythonNodeUI(NodeBlock node, CustomPythonNode meta)
        {
            var settingsPanel = new StackPanel { Margin = new Thickness(5) };
            var uiControls = new Dictionary<string, Control>();

            foreach (var param in meta.Parameters)
            {
                settingsPanel.Children.Add(new TextBlock
                {
                    Text = param.Name,
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    Margin = new Thickness(0, 5, 0, 2),
                    HorizontalAlignment = HorizontalAlignment.Left
                });

                if (param.Type == "bool")
                {
                    var cb = new CheckBox
                    {
                        IsChecked = bool.Parse(param.Default),
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    settingsPanel.Children.Add(cb);
                    uiControls[param.Name] = cb;
                }
                else // text / number
                {
                    var tb = new TextBox
                    {
                        Text = param.Default,
                        Margin = new Thickness(0, 0, 0, 5),
                        Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(1),
                        BorderBrush = Brushes.DimGray,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Padding = new Thickness(3)
                    };
                    settingsPanel.Children.Add(tb);
                    uiControls[param.Name] = tb;
                }
            }

            node.SetContent(settingsPanel);

            // Привязываем логику сохранения
            node.OnSaveSettings = () =>
            {
                foreach (var ctrl in uiControls)
                {
                    if (ctrl.Value is CheckBox cb) node.Settings[ctrl.Key] = cb.IsChecked.ToString();
                    else if (ctrl.Value is TextBox tb) node.Settings[ctrl.Key] = tb.Text;
                }
                if (node.TargetDevice != null) node.Settings["_TargetDeviceIp"] = node.TargetDevice.Ip;
            };

            // Привязываем логику загрузки
            node.OnLoadSettings = () =>
            {
                foreach (var ctrl in uiControls)
                {
                    if (node.Settings.TryGetValue(ctrl.Key, out string val))
                    {
                        if (ctrl.Value is CheckBox cb) cb.IsChecked = bool.Parse(val);
                        else if (ctrl.Value is TextBox tb) tb.Text = val;
                    }
                }
            };
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
                //MessageBox.Show($"{node}", "Сбой компилятора", MessageBoxButton.OK, MessageBoxImage.Error);
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