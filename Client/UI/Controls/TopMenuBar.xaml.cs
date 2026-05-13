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
        }

        public void BindCore(BayMaxCore core)
        {
            _core = core;
            NetworkDevicesList.ItemsSource = _core.AvailableDevices;
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
            DeployButton.IsEnabled = false;

            try
            {
                if (!_canvas.IsDeployed)
                {
                    DeployButton.Content = "ЗАПУСК...";
                    DeployButton.Opacity = 0.7;

                    var compiler = new GraphCompiler(_core);
                    bool isSuccess = await compiler.CompileAndDeployAsync(_canvas);

                    if (isSuccess)
                    {
                        _canvas.ToggleDeployMode(true);
                        DeployButton.Content = "СТОП";
                        DeployButton.Background = new SolidColorBrush(Color.FromRgb(255, 75, 75));
                        _canvas.Focus();

                        MessageBox.Show("Проект успешно скомпилирован и запущен на агентах!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        _canvas.ToggleDeployMode(false);
                        DeployButton.Content = "ДЕПЛОЙ";
                        DeployButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));

                        await _core.StopProjectAsync();
                        LoggerService.Log("Деплой прерван из-за ошибок.", LogLevel.Warning);
                    }
                }
                else
                {
                    DeployButton.Content = "ОСТАНОВКА...";
                    DeployButton.Opacity = 0.7;

                    await _core.StopProjectAsync();

                    _canvas.ToggleDeployMode(false);
                    DeployButton.Content = "ДЕПЛОЙ";
                    DeployButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переключении режима: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _canvas.ToggleDeployMode(false);
                DeployButton.Content = "ДЕПЛОЙ";
            }
            finally
            {
                DeployButton.IsEnabled = true;
                DeployButton.Opacity = 1.0;
            }
        }

        private void DeviceMenuButton_Checked(object sender, RoutedEventArgs e)
        {
            _core?.RefreshDevices();
        }


        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveAsProject_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}