using System.Collections.Generic;
using System.Linq;

namespace IoCTools.Generator.Analysis;

internal class CircularDependencyDetector
{
    private readonly List<string> _circularDependencyPath = new();
    private readonly Dictionary<string, List<string>> _dependencyGraph = new();
    private readonly HashSet<string> _recursionStack = new();
    private readonly HashSet<string> _visited = new();

    public void AddDependency(string service,
        string dependency)
    {
        if (!_dependencyGraph.ContainsKey(service))
            _dependencyGraph[service] = new List<string>();
        _dependencyGraph[service].Add(dependency);
    }

    public List<string> DetectCircularDependencies()
    {
        var cycles = new List<string>();

        foreach (var service in _dependencyGraph.Keys)
            if (!_visited.Contains(service))
            {
                var cycle = DetectCycleFromNode(service);
                if (cycle != null) cycles.Add(cycle);
            }

        return cycles;
    }

    private string? DetectCycleFromNode(string node)
    {
        if (_recursionStack.Contains(node))
        {
            // Found a cycle, construct the cycle path
            var cycleStart = _circularDependencyPath.IndexOf(node);
            var cyclePath = _circularDependencyPath.Skip(cycleStart).Concat(new[] { node });
            return string.Join(" → ", cyclePath);
        }

        if (_visited.Contains(node))
            return null;

        _visited.Add(node);
        _recursionStack.Add(node);
        _circularDependencyPath.Add(node);

        if (_dependencyGraph.ContainsKey(node))
            foreach (var dependency in _dependencyGraph[node])
            {
                var cycle = DetectCycleFromNode(dependency);
                if (cycle != null) return cycle;
            }

        _recursionStack.Remove(node);
        _circularDependencyPath.Remove(node);
        return null;
    }
}