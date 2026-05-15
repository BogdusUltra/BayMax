using BayMax.Models;
using BayMax.Services;
using BayMax.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;

namespace BayMax.Workspaces
{
    public partial class ProjectWorkspace : UserControl
    {
        private BayMaxCore _core;
        public LoggerService Logger { get; } = new LoggerService();
        public ObservableCollection<Device> ProjectDevices { get; } = new ObservableCollection<Device>();
        public string FilePath { get; set; }
        public bool IsDirty => Canvas.IsDirty;
        public bool IsDeployed => Canvas.IsDeployed;
        public NodeCanvas Editor => Canvas;

        public event Action StateChanged;
        public ProjectWorkspace()
        {
            InitializeComponent();

            Canvas.ProjectDevices = ProjectDevices;
            Canvas.IsDirtyChanged += () => StateChanged?.Invoke();
        }

        public void BindCore(BayMaxCore Core)
        {
            _core = Core;
            Canvas.SetAvailableNodes(_core.AvailablePythonNodes);
        }

        public void SaveToFile(string filePath)
        {
            try
            {
                var projectData = new ProjectData();
                projectData.CanvasData = Canvas.GetCanvasData();
                projectData.ProjectName = Path.GetFileNameWithoutExtension(filePath);

                projectData.ProjectDevices.Clear();
                foreach (Device dev in ProjectDevices)
                {
                    projectData.ProjectDevices.Add(new DeviceProjectRef { Name = dev.Name, Ip = dev.Ip });
                }

                JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(projectData, options);
                File.WriteAllText(filePath, json);

                FilePath = filePath;
                Canvas.ClearDirty();

                Logger.Log($"Проект успешно сохранен: {Path.GetFileName(filePath)}", LogLevel.Success);
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при сохранении: {ex.Message}", LogLevel.Error);
            }
        }

        public void LoadFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                ProjectData projectData = JsonSerializer.Deserialize<ProjectData>(json);

                if (projectData != null)
                {
                    FilePath = filePath;
                    ProjectDevices.Clear();

                    foreach (DeviceProjectRef devRef in projectData.ProjectDevices)
                    {
                        Device existing = _core.AvailableDevices.FirstOrDefault(d => d.Ip == devRef.Ip);
                        if (existing != null)
                        {
                            existing.IsInProject = true;
                            ProjectDevices.Add(existing);
                        }
                        else
                        {
                            var offlineDevice = new Device
                            {
                                Ip = devRef.Ip,
                                Name = devRef.Name,
                                IsOnline = false,
                                IsInProject = true
                            };
                            ProjectDevices.Add(offlineDevice);

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (!_core.AvailableDevices.Any(d => d.Ip == offlineDevice.Ip))
                                    _core.AvailableDevices.Add(offlineDevice);
                            });
                        }
                    }

                    Canvas.LoadCanvasData(projectData.CanvasData);

                    Logger.Log($"Проект загружен: {Path.GetFileName(filePath)}", LogLevel.Info);
                    StateChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка загрузки файла: {ex.Message}", LogLevel.Error);
            }
        }

        public async Task<bool> DeployAsync()
        {
            if (_core == null) return false;

            Logger.Log("Запуск деплоя проекта...", LogLevel.Info);

            bool isSuccess = await _core.DeployProjectAsync(Canvas);

            if (isSuccess)
            {
                Canvas.ToggleDeployMode(true);
                Logger.Log("Проект успешно скомпилирован и запущен!", LogLevel.Success);
            }
            else
            {
                Canvas.ToggleDeployMode(false);
                await _core.StopProjectAsync(ProjectDevices);
                Logger.Log("Деплой прерван из-за ошибок.", LogLevel.Warning);
            }

            StateChanged?.Invoke();
            return isSuccess;
        }

        public async Task StopDeployAsync(BayMaxCore core)
        {
            if (core == null) return;

            Logger.Log("Остановка деплоя...", LogLevel.Warning);
            await core.StopProjectAsync(ProjectDevices);
            Canvas.ToggleDeployMode(false);
            StateChanged?.Invoke();
        }
    }
}

