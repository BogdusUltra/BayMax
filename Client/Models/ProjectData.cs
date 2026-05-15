using System;
using System.Collections.Generic;
using System.Text;

namespace BayMax.Models
{
    public class ProjectData
    {
        public string ProjectName { get; set; }
        public DateTime SaveTime { get; set; }
        public List<DeviceProjectRef> ProjectDevices { get; set; } = new List<DeviceProjectRef>();
        public CanvasData CanvasData { get; set; }
    }

   
}
