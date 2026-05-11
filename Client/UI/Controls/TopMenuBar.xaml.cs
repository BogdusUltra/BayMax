using BayMax.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BayMax.UI.Controls
{
    public partial class TopMenuBar : UserControl
    {
        private NodeCanvas _canvas;
        private BayMaxCore _core;

        public TopMenuBar()
        {
            InitializeComponent();
        }

        public void BindCanvas(NodeCanvas canvas)
        {
            _canvas = canvas;
            _canvas.SelectionChanged += UpdateMenuState;
            UpdateMenuState();
        }

        public void BindCore(BayMaxCore core)
        {
            _core = core;
            NetworkDevicesList.ItemsSource = _core.AvailableDevices;
        }

        private void UpdateMenuState()
        {
            State0_Menu.Visibility = Visibility.Collapsed;
            State1_Menu.Visibility = Visibility.Collapsed;
            StateMulti_Menu.Visibility = Visibility.Collapsed;

            if (_canvas != null && _canvas.IsDeployed)
            {
                State0_Menu.Visibility = Visibility.Visible;
                return;
            }

            int count = _canvas.SelectedNodes.Count;

            if (count == 0)
            {
                State0_Menu.Visibility = Visibility.Visible;
            }
            else if (count == 1)
            {
                State1_Menu.Visibility = Visibility.Visible;
            }
            else
            {
                MultiSelectText.Text = $"Выбрано нод: {count}";
                StateMulti_Menu.Visibility = Visibility.Visible;
            }
        }

        private async void ToggleDeviceInProject_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var device = btn?.Tag as Models.Device;
            if (device == null) return;

            btn.IsEnabled = false;

            if (!device.IsInProject)
            {
                var status = await _core.CheckAutorizationAsync(device);

                if (status == ConnectionStatus.Offline)
                {
                    MessageBox.Show("Устройство не отвечает и будет удалено из списка.", "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _core.AvailableDevices.Remove(device);
                    _core.ProjectDevices.Remove(device);
                    _core.DisconnectDevice(device.Ip);
                    btn.IsEnabled = true;
                    return;
                }
                else if (status == ConnectionStatus.Unauthorized)
                {
                    device.IsAuthorized = false;

                    var pinWin = new PinWindow { Owner = Window.GetWindow(this) };
                    if (pinWin.ShowDialog() == true)
                    {
                        bool isPaired = await _core.PairAsync(device, pinWin.ResultPin);
                        if (!isPaired)
                        {
                            MessageBox.Show("Ошибка авторизации. Неверный PIN-код.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            btn.IsEnabled = true;
                            return;
                        }
                        device.IsAuthorized = true;
                    }
                    else
                    {
                        btn.IsEnabled = true;
                        return;
                    }
                }
                else
                {
                    device.IsAuthorized = true;
                }

                device.IsInProject = true;
                if (!_core.ProjectDevices.Contains(device))
                    _core.ProjectDevices.Add(device);
            }
            else
            {
                device.IsInProject = false;
                _core.ProjectDevices.Remove(device);
            }

            btn.IsEnabled = true;
        }

        private void AddUI_Click(object sender, RoutedEventArgs e) => _canvas.AddUINode();
        private void AddLogic_Click(object sender, RoutedEventArgs e) => _canvas.AddLogicNode();
        private void Delete_Click(object sender, RoutedEventArgs e) => _canvas.DeleteSelectedNodes();

        private void OnMenuBackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Focus();
        }

        private async void Deploy_Click(object sender, RoutedEventArgs e)
        {
            bool newState = !_canvas.IsDeployed;
            _canvas.ToggleDeployMode(newState);

            if (newState)
            {
                DeployButton.Content = "СТОП";
                DeployButton.Background = new SolidColorBrush(Color.FromRgb(255, 75, 75));
                _canvas.Focus();

                var compiler = new GraphCompiler(_core);
                bool isSuccess = await compiler.CompileAndDeployAsync(_canvas);

                if (isSuccess)
                    MessageBox.Show("Проект успешно скомпилирован и запущен на агентах!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                {
                    LoggerService.Log("Деплой прерван из-за ошибок.", LogLevel.Warning);

                    _canvas.ToggleDeployMode(false);
                    DeployButton.Content = "ДЕПЛОЙ";
                    DeployButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    _core.StopNetBridge();
                }   
            }
            else
            {
                DeployButton.Content = "ДЕПЛОЙ";
                DeployButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));

                _core.StopNetBridge();
            }
        }
    }
}