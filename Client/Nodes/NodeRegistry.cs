using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BayMax.Nodes
{
    public static class NodeRegistry
    {
        public static List<INodeBuilder> AvailableNodes { get; } = new List<INodeBuilder>();

        public static void Initialize()
        {
            AvailableNodes.Clear();

            var typeToFind = typeof(INodeBuilder);

            var foundTypes = Assembly.GetExecutingAssembly().GetTypes().Where(p => typeToFind.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

            foreach (var type in foundTypes)
            {
                var nodeInstance = (INodeBuilder)Activator.CreateInstance(type);
                AvailableNodes.Add(nodeInstance);
            }
        }
    }
}