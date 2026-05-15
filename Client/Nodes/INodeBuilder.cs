using BayMax.UI.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace BayMax.Nodes
{
    public interface INodeBuilder
    {
        string NodeName { get; } 
        string Category { get; }

        public NodeBlock CreateNode();
    }
}
