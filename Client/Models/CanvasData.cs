using System;
using System.Collections.Generic;
using System.Text;

namespace BayMax.Models
{
    public class CanvasData
    {
        public List<NodeData> Nodes { get; set; } = new List<NodeData>();
        public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();
    }
    public class DeviceProjectRef
    {
        public string Name { get; set; }
        public string Ip { get; set; }
    }

    public class NodeData
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string LogicTypeName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<string> SavedPinIds { get; set; } = new List<string>();
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }

    public class ConnectionData
    {
        public string StartNodeId { get; set; }
        public string StartPinId { get; set; }
        public string EndNodeId { get; set; }
        public string EndPinId { get; set; }
    }
}
