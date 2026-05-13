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
            TopMenu.BindCanvas(MainEditor);

            MainEditor.BindCore(Core);

            this.Activated += MainWindow_Activated;

            LoggerService.OnLog += HandleNewLog;
        }

        private async void MainWindow_Activated(object sender, EventArgs e)
        {
            if (_isRefreshing || (MainEditor != null && MainEditor.IsDeployed)) return;

            try
            {
                _isRefreshing = true;

                bool wasUpdated = await Core.AutoRefreshPythonNodesAsync();

                if (wasUpdated)
                {
                    MainEditor.SyncLogicNodes();
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _isRefreshing = false;
            }
        }

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