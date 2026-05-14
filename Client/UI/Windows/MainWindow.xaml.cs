using BayMax.Services;
using NetMQ;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BayMax.Models;
using System.Windows.Media.Animation;
using System;

namespace BayMax
{
    public partial class MainWindow : Window
    {
        public BayMaxCore Core;
        private bool _isRefreshing = false;

        public MainWindow()
        {
            InitializeComponent();

            Nodes.NodeRegistry.Initialize();
            Core = new BayMaxCore();

            TopMenu.BindCore(Core);

            AddNewTab("Новый проект");

            this.Activated += MainWindow_Activated;
            LoggerService.OnLog += HandleNewLog;
        }

        // ==========================================
        // ГОРЯЧИЕ КЛАВИШИ
        // ==========================================
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.N) { TopMenu.NewProject_Click(null, null); e.Handled = true; }
                else if (e.Key == Key.O) { TopMenu.OpenProject_Click(null, null); e.Handled = true; }
                else if (e.Key == Key.S) { TopMenu.SaveProject_Click(null, null); e.Handled = true; }
            }
        }

        // ==========================================
        // УПРАВЛЕНИЕ ВКЛАДКАМИ
        // ==========================================
        public void AddNewTab(string title = "Новый проект")
        {
            var newCanvas = new UI.Controls.NodeCanvas();
            newCanvas.BindCore(Core);

            newCanvas.IsDirtyChanged += () =>
            {
                string currentTitle = string.IsNullOrEmpty(newCanvas.FilePath) ? "Новый проект" : System.IO.Path.GetFileName(newCanvas.FilePath);
                UpdateActiveTabTitle(currentTitle);
            };

            var tab = new TabItem
            {
                Header = title,
                Content = newCanvas
            };

            ProjectTabs.Items.Add(tab);
            ProjectTabs.SelectedItem = tab;
        }

        public bool FocusTabByFilePath(string filePath)
        {
            foreach (TabItem tab in ProjectTabs.Items)
            {
                if (tab.Content is UI.Controls.NodeCanvas canvas &&
                    string.Equals(canvas.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
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

            var btn = sender as Button;
            var tab = btn?.Tag as TabItem;

            if (tab != null && tab.Content is UI.Controls.NodeCanvas canvas)
            {
                var previouslySelected = ProjectTabs.SelectedItem as TabItem;

                if (canvas.IsDirty)
                {
                    ProjectTabs.SelectedItem = tab;

                    string projectName = tab.Header.ToString().TrimEnd('*');
                    var result = MessageBox.Show($"В проекте \"{projectName}\" есть несохраненные изменения. Сохранить перед закрытием?",
                                                 "Сохранение", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Cancel) return;
                    if (result == MessageBoxResult.Yes)
                    {
                        TopMenu.SaveProject_Click(null, null);
                        if (canvas.IsDirty) return;
                    }
                }

                ProjectTabs.Items.Remove(tab);

                if (previouslySelected != null && previouslySelected != tab && ProjectTabs.Items.Contains(previouslySelected))
                {
                    ProjectTabs.SelectedItem = previouslySelected;
                }

                if (ProjectTabs.Items.Count == 0)
                {
                    TopMenu.BindCanvas(null);
                }
            }
        }

        private void ProjectTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectTabs.SelectedItem is TabItem selectedTab && selectedTab.Content is UI.Controls.NodeCanvas activeCanvas)
            {
                TopMenu.BindCanvas(activeCanvas);
            }
        }

        public void UpdateActiveTabTitle(string title)
        {
            if (ProjectTabs.SelectedItem is TabItem selectedTab && selectedTab.Content is UI.Controls.NodeCanvas activeCanvas)
            {
                selectedTab.Header = title + (activeCanvas.IsDirty ? "*" : "");
            }
        }

        // ==========================================
        // ОБНОВЛЕНИЕ НОД ПРИ ВОЗВРАТЕ В ОКНО
        // ==========================================
        private async void MainWindow_Activated(object sender, EventArgs e)
        {
            // Получаем текущий активный холст
            UI.Controls.NodeCanvas activeCanvas = null;
            if (ProjectTabs?.SelectedItem is TabItem selectedTab)
            {
                activeCanvas = selectedTab.Content as UI.Controls.NodeCanvas;
            }

            if (_isRefreshing || (activeCanvas != null && activeCanvas.IsDeployed)) return;

            try
            {
                _isRefreshing = true;

                bool wasUpdated = await Core.AutoRefreshPythonNodesAsync();

                if (wasUpdated && ProjectTabs != null)
                {
                    foreach (TabItem tab in ProjectTabs.Items)
                    {
                        if (tab.Content is UI.Controls.NodeCanvas canvas)
                        {
                            canvas.SyncLogicNodes();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Игнорируем или логируем
            }
            finally
            {
                _isRefreshing = false;
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

        private void HandleNewLog(string message, LogLevel level)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");

                ConsoleOutput.AppendText($"[{time}] {message}\n");

                ConsoleOutput.ScrollToEnd();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            Core.Shutdown();
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}