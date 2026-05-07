using System;
using System.Collections.Generic;
using System.Text;

namespace BayMax.Models
{
    public class Device
    {
        public string Name { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }
        public string PublicKey { get; set; }
        public bool IsConnected { get; set; }
        public string DisplayName => $"{Name} ({Ip}) {(IsConnected ? "●" : "")}";
    }
}
