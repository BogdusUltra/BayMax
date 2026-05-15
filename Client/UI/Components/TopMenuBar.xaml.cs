using BayMax.Models;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;


namespace BayMax.UI.Components
{
    public partial class TopMenuBar : UserControl
    {
        public event Action NewProjectRequested;
        public event Action OpenProjectRequested;
        public event Action SaveProjectRequested;
        public event Action SaveAsProjectRequested;
        public event Action DeployRequested;
        public event Action<Device> ToggleDeviceRequested;
        public event Action RefreshDevicesRequested;

        public TopMenuBar()
        {
            InitializeComponent();
        }

        public void UpdateState(bool isDeployed, IEnumerable<Device> availableDevices, IEnumerable<Device> projectDevices)
        {
            if (isDeployed)
            {
                DeployButton.Content = "ОСТАНОВИТЬ";
                DeployButton.Background = new SolidColorBrush(Color.FromRgb(255, 75, 75));
            }
            else
            {
                DeployButton.Content = "ДЕПЛОЙ";
                DeployButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
            }

            if (availableDevices == null || projectDevices == null) return;

            OnlineDevicesList.ItemsSource = availableDevices
                .Where(d => d.IsOnline)
                .OrderByDescending(d => projectDevices.Contains(d))
                .ThenByDescending(d => d.IsAuthorized)
                .ThenBy(d => d.Name)
                .ToList();

            OfflineDevicesList.ItemsSource = availableDevices
                .Where(d => !d.IsOnline && projectDevices.Contains(d))
                .OrderBy(d => d.Name)
                .ToList();
        }

        private void NewProject_Click(object sender, RoutedEventArgs e) => NewProjectRequested?.Invoke();
        private void OpenProject_Click(object sender, RoutedEventArgs e) => OpenProjectRequested?.Invoke();
        private void SaveProject_Click(object sender, RoutedEventArgs e) => SaveProjectRequested?.Invoke();
        private void SaveAsProject_Click(object sender, RoutedEventArgs e) => SaveAsProjectRequested?.Invoke();
        private void Deploy_Click(object sender, RoutedEventArgs e) => DeployRequested?.Invoke();
        private void DeviceMenuButton_Checked(object sender, RoutedEventArgs e) => RefreshDevicesRequested?.Invoke();

        private async void ToggleDeviceInProject_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Device device)
            {
                ToggleDeviceRequested?.Invoke(device);
            }
        }

        private void OnMenuBackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Focus();
        }
    }
}