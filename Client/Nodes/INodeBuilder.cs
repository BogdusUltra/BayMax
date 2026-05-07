using BayMax.UI.Controls;
using System;
using System.Collections.Generic;
using System.Text;

namespace BayMax.Nodes
{
    public interface INodeBuilder
    {
        string NodeName { get; } 
        string Category { get; }

        NodeBlock CreateNode();
    }
}
