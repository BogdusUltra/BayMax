using BayMax.Models;
using BayMax.Services;
using BayMax.Utils;
using BayMax.Workspaces;
using Microsoft.Win32;
using NetMQ;
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;

namespace BayMax.Windows
{
    public partial class MainWindow : Window
    {

        public BayMaxCore Core { get; } = new BayMaxCore();
        private ProjectWorkspace _activeWorkspace;
        private bool _isRefreshing = false;

        public MainWindow()
        {
            InitializeComponent();
            Nodes.NodeRegistry.Initialize();

            TopMenu.NewProjectRequested += () => AddNewTab("Новый проект");
            TopMenu.OpenProjectRequested += OpenProject;
            TopMenu.SaveProjectRequested += SaveProject;
            TopMenu.SaveAsProjectRequested += SaveAsProject;
            TopMenu.DeployRequested += ToggleDeploy;
            TopMenu.RefreshDevicesRequested += () => Core.RefreshDevices();
            TopMenu.ToggleDeviceRequested += ToggleDevice;

            Core.DevicesUpdated += UpdateTopMenu;
            Core.OnDeviceDisconnected += HandleDeviceDisconnected;

            LoggerService.Global.OnLog += HandleNewLog;

            this.Activated += MainWindow_Activated;

            AddNewTab("Новый проект");
        }

        // ==========================================
        // ЛОГИКА ДИРИЖЕРА ПРОЕКТОВ (События меню)
        // ==========================================
        private void SaveProject()
        {
            if (_activeWorkspace == null) return;
            if (string.IsNullOrEmpty(_activeWorkspace.FilePath)) SaveAsProject();
            else _activeWorkspace.SaveToFile(_activeWorkspace.FilePath);
        }

        private void SaveAsProject()
        {
            if (_activeWorkspace == null) return;
            var saveDialog = new SaveFileDialog { Filter = "BayMax Project (*.bmx)|*.bmx", FileName = "MyProject.bmx" };
            if (saveDialog.ShowDialog() == true) _activeWorkspace.SaveToFile(saveDialog.FileName);
        }

        private void OpenProject()
        {
            var openDialog = new OpenFileDialog { Filter = "BayMax Project (*.bmx)|*.bmx" };
            if (openDialog.ShowDialog() == true)
            {
                if (FocusTabByFilePath(openDialog.FileName)) return;
                var workspace = AddNewTab(Path.GetFileName(openDialog.FileName));
                workspace.LoadFromFile(openDialog.FileName);
            }
        }

        private async void ToggleDeploy()
        {
            if (_activeWorkspace == null) return;
            TopMenu.DeployButton.IsEnabled = false;

            try
            {
                if (!_activeWorkspace.IsDeployed)
                    await _activeWorkspace.DeployAsync();
                else
                    await _activeWorkspace.StopDeployAsync(Core);
            }
            finally
            {
                TopMenu.DeployButton.IsEnabled = true;
                UpdateTopMenu();
            }
        }

        private async void ToggleDevice(Device device)
        {
            if (_activeWorkspace == null) return;

            if (!device.IsInProject)
            {
                var status = await Core.CheckAutorizationAsync(device);
                if (status != ConnectionStatus.Offline)
                {
                    device.IsInProject = true;
                    if (!_activeWorkspace.ProjectDevices.Contains(device)) _activeWorkspace.ProjectDevices.Add(device);
                }
                else
                {
                    MessageBox.Show("Устройство оффлайн.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Core.DeviceOffline(device);
                }
            }
            else
            {
                device.IsInProject = false;
                _activeWorkspace.ProjectDevices.Remove(device);
            }
            UpdateTopMenu();
        }

        private void HandleDeviceDisconnected(Device device)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                bool wasActiveStopped = false;
                foreach (TabItem tab in ProjectTabs.Items)
                {
                    if (tab.Content is ProjectWorkspace workspace && workspace.IsDeployed && workspace.ProjectDevices.Contains(device))
                    {
                        await workspace.StopDeployAsync(Core);
                        if (workspace == _activeWorkspace) wasActiveStopped = true;
                    }
                }
                if (wasActiveStopped)
                {
                    MessageBox.Show($"Связь с агентом \"{device.Name}\" была потеряна!\nДеплой остановлен.", "Обрыв связи", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UpdateTopMenu();
                }
            }));
        }

        // ==========================================
        // УПРАВЛЕНИЕ ВКЛАДКАМИ
        // ==========================================
        public ProjectWorkspace AddNewTab(string title = "Новый проект")
        {
            var workspace = new ProjectWorkspace();
            workspace.BindCore(Core);

            workspace.StateChanged += () =>
            {
                string currentTitle = string.IsNullOrEmpty(workspace.FilePath) ? "Новый проект" : System.IO.Path.GetFileName(workspace.FilePath);
                UpdateActiveTabTitle(workspace, currentTitle);
                UpdateTopMenu();
            };

            var tab = new TabItem
            {
                Header = title,
                Content = workspace
            };

            ProjectTabs.Items.Add(tab);
            ProjectTabs.SelectedItem = tab;

            return workspace;
        }

        private void ProjectTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != ProjectTabs) return;

            if (_activeWorkspace != null) _activeWorkspace.Logger.OnLog -= HandleNewLog;

            if (ProjectTabs.SelectedItem is TabItem selectedTab && selectedTab.Content is ProjectWorkspace workspace)
            {
                _activeWorkspace = workspace;
                UpdateTopMenu();

                _activeWorkspace.Logger.OnLog += HandleNewLog;
            }
            else
            {
                _activeWorkspace = null;
            }

            RefreshConsoleFromHistory();
        }
        private void UpdateTopMenu()
        {
            if (_activeWorkspace != null)
            {
                TopMenu.UpdateState(_activeWorkspace.IsDeployed, Core.AvailableDevices, _activeWorkspace.ProjectDevices);
            }
        }

        public void UpdateActiveTabTitle(ProjectWorkspace workspace, string title)
        {
            foreach (TabItem tab in ProjectTabs.Items)
            {
                if (tab.Content == workspace)
                {
                    tab.Header = title + (workspace.IsDirty ? "*" : "");
                    break;
                }
            }
        }

        public bool FocusTabByFilePath(string filePath)
        {
            foreach (TabItem tab in ProjectTabs.Items)
            {
                if (tab.Content is ProjectWorkspace workspace && string.Equals(workspace.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    ProjectTabs.SelectedItem = tab;
                    return true;
                }
            }
            return false; 
        }

        private void CloseTab_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            var tab = (sender as Button)?.Tag as TabItem;

            if (tab?.Content is ProjectWorkspace workspace)
            {
                var previouslySelected = ProjectTabs.SelectedItem as TabItem;

                if (workspace.IsDirty)
                {
                    ProjectTabs.SelectedItem = tab;
                    string projectName = tab.Header.ToString().TrimEnd('*');
                    var result = MessageBox.Show($"Сохранить изменения в \"{projectName}\"?", "Сохранение", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Cancel) return;
                    if (result == MessageBoxResult.Yes)
                    {
                        SaveProject();
                        if (workspace.IsDirty) return;
                    }
                }

                if (_activeWorkspace == workspace)
                {
                    _activeWorkspace.Logger.OnLog -= HandleNewLog;
                    _activeWorkspace = null;
                    RefreshConsoleFromHistory();
                }

                ProjectTabs.Items.Remove(tab);

                if (previouslySelected != null && previouslySelected != tab && ProjectTabs.Items.Contains(previouslySelected))
                    ProjectTabs.SelectedItem = previouslySelected;
            }
        }

        // ==========================================
        // ЛОГИРОВАНИЕ
        // ==========================================
        private void HandleNewLog(LogMessage log)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => AppendLogToConsole(log)));
        }

        private void AppendLogToConsole(LogMessage log)
        {
            string time = log.Time.ToString("HH:mm:ss");
            ConsoleOutput.AppendText($"[{time}] [{log.Level.ToString().ToUpper()}] {log.Message}\n");
            ConsoleOutput.ScrollToEnd();
        }

        // ==========================================
        // ПРОЧЕЕ (Горячие клавиши, Консоль, Обновление нод)
        // ==========================================
        private async void MainWindow_Activated(object sender, EventArgs e)
        {
            if (_isRefreshing || (_activeWorkspace != null && _activeWorkspace.IsDeployed)) return;

            try
            {
                _isRefreshing = true;
                bool wasUpdated = await Core.AutoRefreshPythonNodesAsync();
                if (wasUpdated)
                {
                    foreach (TabItem tab in ProjectTabs.Items)
                        if (tab.Content is ProjectWorkspace workspace) workspace.Editor.SyncLogicNodes();
                }
            }
            catch { }
            finally { _isRefreshing = false; }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.N) { AddNewTab("Новый проект"); e.Handled = true; }
                else if (e.Key == Key.O) { OpenProject(); e.Handled = true; }
                else if (e.Key == Key.S) { SaveProject(); e.Handled = true; }
            }
        }

        // ==========================================
        // ЛОГИКА КОНСОЛИ
        // ==========================================
        public static readonly DependencyProperty ConsoleHeightProperty =
            DependencyProperty.Register("ConsoleHeight", typeof(double), typeof(MainWindow),
            new PropertyMetadata(0.0, OnConsoleHeightChanged));

        public double ConsoleHeight
        {
            get { return (double)GetValue(ConsoleHeightProperty); }
            set { SetValue(ConsoleHeightProperty, value); }
        }

        private static void OnConsoleHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var win = (MainWindow)d;
            win.ConsoleRow.Height = new GridLength((double)e.NewValue);
        }

        private double _lastConsoleHeight = 250.0;

        private void ToggleConsole_Click(object sender, RoutedEventArgs e)
        {
            if (_lastConsoleHeight < 100) { _lastConsoleHeight = 100; }

            if (ConsolePanel.Visibility == Visibility.Collapsed)
            {
                OpenConsole();
            }
            else
            {
                CloseConsole();
            }
        }

        private void ConsoleHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                CloseConsole();
                e.Handled = true;
            }
        }

        private void CloseConsole_Click(object sender, RoutedEventArgs e)
        {
            CloseConsole();
        }

        private void OpenConsole()
        {
            ConsolePanel.Visibility = Visibility.Visible;
            ConsoleSplitter.Visibility = Visibility.Visible;

            var anim = new DoubleAnimation
            {
                From = ConsoleRow.ActualHeight,
                To = _lastConsoleHeight,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };

            anim.Completed += (s, e) =>
            {
                this.BeginAnimation(ConsoleHeightProperty, null);
                ConsoleRow.Height = new GridLength(_lastConsoleHeight);
            };

            this.BeginAnimation(ConsoleHeightProperty, anim);
        }

        private void CloseConsole()
        {
            if (ConsoleRow.ActualHeight > 10)
            {
                _lastConsoleHeight = ConsoleRow.ActualHeight;
            }

            var anim = new DoubleAnimation
            {
                From = ConsoleRow.ActualHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            anim.Completed += (s, e) =>
            {
                ConsolePanel.Visibility = Visibility.Collapsed;
                ConsoleSplitter.Visibility = Visibility.Collapsed;

                this.BeginAnimation(ConsoleHeightProperty, null);
                ConsoleRow.Height = new GridLength(0);
            };

            this.BeginAnimation(ConsoleHeightProperty, anim);
        }

        private void RefreshConsoleFromHistory()
        {
            ConsoleOutput.Clear();

            var combinedLogs = new List<LogMessage>();

            combinedLogs.AddRange(LoggerService.Global.History);

            if (_activeWorkspace != null)
            {
                combinedLogs.AddRange(_activeWorkspace.Logger.History);
            }

            foreach (var log in combinedLogs.OrderBy(l => l.Time))
            {
                AppendLogToConsole(log);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Core.Shutdown();
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}