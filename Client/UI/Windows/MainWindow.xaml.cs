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

namespace BayMax
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly BayMaxCore _core;


        public MainWindow()
        {
            BayMax.Nodes.NodeRegistry.Initialize();
            InitializeComponent();
            TopMenu.BindCanvas(MainEditor);

            _core = new BayMaxCore();

            this.DataContext = _core;

            

            //LoggerService.OnLog += OnLogReceived;
        }

        //private void OnLogReceived(string message, LogLevel level)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        //        string logEntry = $"[{timestamp}] [{level.ToString().ToUpper()}] {message}\n";

        //        LogConsole.AppendText(logEntry);
        //        LogConsole.ScrollToEnd();

        //        if (level >= LogLevel.Info)
        //        {
        //            StatusLabel.Text = message;
        //        }
        //    });
        //}

        private async void StartLocal_Click(object sender, RoutedEventArgs e)
        {
            // 1. Запустили
            _core.StartLocalAgent();

            // 2. Подождали 2 секунды, пока Питон поднимется и отправит маяки
            await _core.ScanNetworkAsync(timeoutSeconds: 0.6);

            // 3. Нашли его в списке
            var localDevice = _core.AvailableDevices.FirstOrDefault(d => d.Ip == "127.0.0.1");

            // 4. Подключились умным коннектом
            if (localDevice != null)
            {
                bool authorized = await _core.IsAlreadyAuthorizedAsync(localDevice);

                if (authorized)
                {
                    localDevice.IsConnected = true;
                    LoggerService.Log("Бесшовное подключение к локальному агенту прошло успешно!", LogLevel.Success);
                }
                else
                {
                    LoggerService.Log("Странно, локальный агент не узнал наш ключ и требует PIN.", LogLevel.Error);
                }
            }
            else
            {
                LoggerService.Log("Локальный агент не ответил на радар за отведенное время.", LogLevel.Warning);
            }
        }

        //private void Refresh_Click(object sender, RoutedEventArgs e) => _core.ScanNetworkAsync();

        //private async void Connect_Click(object sender, RoutedEventArgs e)
        //{
        //    var selected = (Device)DeviceCombo.SelectedItem;
        //    if (selected == null) return;

        //    ConnectButton.IsEnabled = false;

        //    try
        //    {
        //        LoggerService.Log($"Проверка авторизации для {selected.Name}...");
        //        bool authorized = await _core.IsAlreadyAuthorizedAsync(selected);

        //        if (authorized)
        //        {
        //            // ШАГ 2: "Если да, то пока ничего, так как это просто тест"
        //            selected.IsConnected = true;
        //            LoggerService.Log("Устройство узнало нас! Доступ разрешен без PIN.", LogLevel.Success);
        //        }
        //        else
        //        {
        //            // ШАГ 3: "Если нет, то пароль просим"
        //            LoggerService.Log("Устройство требует авторизации. Запрос PIN...");

        //            var pinWindow = new PinWindow { Owner = this };
        //            if (pinWindow.ShowDialog() == true)
        //            {
        //                string response = await _core.PairAsync(selected, pinWindow.ResultPin);
        //                LoggerService.Log($"Результат сопряжения: {response}");
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        LoggerService.Log($"Ошибка алгоритма подключения: {ex.Message}", LogLevel.Error);
        //    }
        //    finally
        //    {
        //        ConnectButton.IsEnabled = true;
        //    }
        //}

        protected override void OnClosed(EventArgs e)
        {
            _core.Shutdown();
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}