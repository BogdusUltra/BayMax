using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BayMax.Models
{
    public class Device : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }
        public string PublicKey { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;

        private bool _isAuthorized;
        public bool IsAuthorized
        {
            get => _isAuthorized;
            set
            {
                _isAuthorized = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        private bool _isInProject;
        public bool IsInProject
        {
            get => _isInProject;
            set
            {
                _isInProject = value;
                OnPropertyChanged();
            }
        }

        // Тот самый значок, про который ты просил
        public string StatusIcon => IsAuthorized ? "✅" : "🔒";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}