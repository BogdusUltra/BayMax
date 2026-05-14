using BayMax.Models;
using BayMax.Services;
using Microsoft.Win32;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.IO;

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
            RefreshDeviceLists();
        }

        public void BindCore(BayMaxCore core)
        {
            _core = core;
            _core.DevicesUpdated += RefreshDeviceLists;
            RefreshDeviceLists();
        }


        public void RefreshDeviceLists()
        {
            if (_core == null || _canvas == null) return;

            var allDevices = _core.AvailableDevices.ToList();
            var projectDevices = _canvas.ProjectDevices;

            // ФОРМИРУЕМ ОНЛАЙН СПИСОК (В сети)
            // Сортировка: Сначала те кто в проекте -> Авторизованные -> По имени
            OnlineDevicesList.ItemsSource = allDevices
                .Where(d => d.IsOnline)
                .OrderByDescending(d => projectDevices.Contains(d))
                .ThenByDescending(d => d.IsAuthorized)
                .ThenBy(d => d.Name)
                .ToList();

            // ФОРМИРУЕМ ОФФЛАЙН СПИСОК (Не в сети, но в проекте)
            // Сортировка: Просто по имени
            OfflineDevicesList.ItemsSource = allDevices
                .Where(d => !d.IsOnline && projectDevices.Contains(d))
                .OrderBy(d => d.Name)
                .ToList();
        }

        private async void ToggleDeviceInProject_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var device = btn?.Tag as Models.Device;
            if (device == null) return;

            btn.IsEnabled = false;

            if (!device.IsInProject)
            {
                ConnectionStatus status = await _core.CheckAutorizationAsync(device);
                if (status != ConnectionStatus.Offline)
                {
                    device.IsInProject = true;
                    if (!_canvas.ProjectDevices.Contains(device))
                        _canvas.ProjectDevices.Add(device);
                }
                else
                {
                    MessageBox.Show("Устройство, которое вы хотели добавить в проект - оффлайн.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    _core.DeviceOffline(device);
                }
            }
            else
            {
                device.IsInProject = false;
                _canvas.ProjectDevices.Remove(device);  
            }

            RefreshDeviceLists();
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

                        await _core.StopProjectAsync(_canvas.ProjectDevices);
                        LoggerService.Log("Деплой прерван из-за ошибок.", LogLevel.Warning);
                    }
                }
                else
                {
                    DeployButton.Content = "ОСТАНОВКА...";
                    DeployButton.Opacity = 0.7;

                    await _core.StopProjectAsync(_canvas.ProjectDevices);

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


        public void NewProject_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            // Просто создаем новую вкладку. MainWindow сам привяжет меню к новому холсту.
            mainWindow?.AddNewTab("Новый проект");
        }

        public void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "BayMax Project (*.bmx)|*.bmx",
                Title = "Открыть проект BayMax"
            };

            if (openDialog.ShowDialog() == true)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;

                if (mainWindow != null && mainWindow.FocusTabByFilePath(openDialog.FileName))
                {
                    return;
                }

                try
                {
                    string json = File.ReadAllText(openDialog.FileName);
                    var project = JsonSerializer.Deserialize<ProjectData>(json);

                    if (project != null)
                    {
                        
                        mainWindow?.AddNewTab(Path.GetFileName(openDialog.FileName));

                        if (_canvas != null)
                        {
                            _canvas.FilePath = openDialog.FileName;

                            _canvas.ProjectDevices.Clear();
                            foreach (var devRef in project.ProjectDevices)
                            {
                                var existing = _core.AvailableDevices.FirstOrDefault(d => d.Ip == devRef.Ip);
                                if (existing != null)
                                {
                                    existing.IsInProject = true;
                                    _canvas.ProjectDevices.Add(existing);
                                }
                            }

                            _canvas.LoadProjectData(project);
                            RefreshDeviceLists();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии файла:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsProject_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "BayMax Project (*.bmx)|*.bmx",
                Title = "Сохранить проект как...",
                FileName = "MyProject.bmx"
            };

            if (saveDialog.ShowDialog() == true)
            {
                _canvas.FilePath = saveDialog.FileName;
                SaveToFile(_canvas.FilePath);
            }
        }

        public void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_canvas.FilePath))
            {
                SaveToFile(_canvas.FilePath);
            }
            else
            {
                SaveAsProject_Click(sender, e);
            }
        }

        private void SaveToFile(string filePath)
        {
            try
            {
                var project = _canvas.GetProjectData();
                project.ProjectName = Path.GetFileNameWithoutExtension(filePath);

                project.ProjectDevices.Clear();
                foreach (var dev in _canvas.ProjectDevices)
                {
                    project.ProjectDevices.Add(new DeviceProjectRef { Name = dev.Name, Ip = dev.Ip });
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(project, options);

                File.WriteAllText(filePath, json);

                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.UpdateActiveTabTitle(Path.GetFileName(filePath));

                _canvas.ClearDirty();

                MessageBox.Show("Проект успешно сохранен!", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



    }
}