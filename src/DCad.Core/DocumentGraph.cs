using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace DCad.Core;

public readonly record struct ObjectId(Guid Value)
{
    public static ObjectId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public enum FeatureKind
{
    Primitive,
    Transform,
    Boolean,
    Sketch,
    Extrude,
    Revolve,
    Sweep,
    Loft,
    Pattern,
    Import,
    Analysis,
    Manufacturing,
    Custom
}

public sealed record FeatureNode(
    ObjectId Id,
    string Name,
    FeatureKind Kind,
    IReadOnlyList<ObjectId> Inputs,
    IReadOnlyDictionary<string, double> Parameters,
    string Operation,
    bool Suppressed = false)
{
    public static FeatureNode Create(
        string name,
        FeatureKind kind,
        string operation,
        IEnumerable<ObjectId>? inputs = null,
        IReadOnlyDictionary<string, double>? parameters = null)
        => new(
            ObjectId.New(),
            name,
            kind,
            (inputs ?? []).ToArray(),
            parameters ?? new ReadOnlyDictionary<string, double>(new Dictionary<string, double>()),
            operation,
            false);
}

public sealed class DocumentGraph
{
    private readonly Dictionary<ObjectId, FeatureNode> _nodes = [];
    private readonly List<ObjectId> _order = [];

    public IReadOnlyList<FeatureNode> Nodes => _order.Select(id => _nodes[id]).ToArray();
    public int Count => _nodes.Count;

    public FeatureNode this[ObjectId id] => _nodes[id];

    public bool TryGet(ObjectId id, out FeatureNode node) => _nodes.TryGetValue(id, out node!);

    public void Add(FeatureNode node)
    {
        if (_nodes.ContainsKey(node.Id))
            throw new InvalidOperationException($"Feature id {node.Id} already exists.");

        foreach (var input in node.Inputs)
            if (!_nodes.ContainsKey(input))
                throw new InvalidOperationException($"Feature '{node.Name}' references missing input {input}.");

        _nodes.Add(node.Id, node);
        _order.Add(node.Id);
        EnsureAcyclic();
    }

    public void Replace(FeatureNode node)
    {
        if (!_nodes.ContainsKey(node.Id))
            throw new KeyNotFoundException($"Feature id {node.Id} does not exist.");
        foreach (var input in node.Inputs)
            if (!_nodes.ContainsKey(input))
                throw new InvalidOperationException($"Feature '{node.Name}' references missing input {input}.");

        var old = _nodes[node.Id];
        _nodes[node.Id] = node;
        try
        {
            EnsureAcyclic();
        }
        catch
        {
            _nodes[node.Id] = old;
            throw;
        }
    }

    public void Remove(ObjectId id, bool cascade = false)
    {
        if (!_nodes.ContainsKey(id)) return;
        var dependents = DependentsOf(id).Select(n => n.Id).ToHashSet();
        if (dependents.Count > 0 && !cascade)
            throw new InvalidOperationException($"Cannot remove {id}: {dependents.Count} dependent feature(s) exist.");

        if (cascade)
        {
            var queue = new Queue<ObjectId>();
            queue.Enqueue(id);
            var remove = new HashSet<ObjectId>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!remove.Add(current)) continue;
                foreach (var dep in DependentsOf(current)) queue.Enqueue(dep.Id);
            }
            _order.RemoveAll(remove.Contains);
            foreach (var item in remove) _nodes.Remove(item);
            return;
        }

        _nodes.Remove(id);
        _order.Remove(id);
    }

    public IReadOnlyList<FeatureNode> DependentsOf(ObjectId id)
        => _order.Where(n => _nodes[n].Inputs.Contains(id)).Select(n => _nodes[n]).ToArray();

    public IReadOnlyList<FeatureNode> TopologicalOrder()
    {
        var indegree = _nodes.Keys.ToDictionary(id => id, _ => 0);
        var outgoing = _nodes.Keys.ToDictionary(id => id, _ => new List<ObjectId>());
        foreach (var node in _nodes.Values)
        {
            foreach (var input in node.Inputs)
            {
                indegree[node.Id]++;
                outgoing[input].Add(node.Id);
            }
        }

        var position = _order.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var ready = new SortedSet<(int Position, ObjectId Id)>(Comparer<(int Position, ObjectId Id)>.Create((a, b) =>
        {
            var c = a.Position.CompareTo(b.Position);
            return c != 0 ? c : a.Id.Value.CompareTo(b.Id.Value);
        }));
        foreach (var pair in indegree.Where(p => p.Value == 0)) ready.Add((position[pair.Key], pair.Key));

        var result = new List<FeatureNode>(_nodes.Count);
        while (ready.Count > 0)
        {
            var next = ready.Min;
            ready.Remove(next);
            result.Add(_nodes[next.Id]);
            foreach (var target in outgoing[next.Id])
            {
                indegree[target]--;
                if (indegree[target] == 0) ready.Add((position[target], target));
            }
        }

        if (result.Count != _nodes.Count)
            throw new InvalidOperationException("Feature graph contains a dependency cycle.");
        return result;
    }

    public string DeterministicFingerprint()
    {
        var sb = new StringBuilder();
        foreach (var node in TopologicalOrder())
        {
            sb.Append(node.Id).Append('|').Append(node.Kind).Append('|').Append(node.Operation).Append('|')
              .Append(node.Name).Append('|').Append(node.Suppressed ? '1' : '0').Append(';');
            foreach (var input in node.Inputs) sb.Append(input).Append(',');
            sb.Append(';');
            foreach (var pair in node.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
                sb.Append(pair.Key).Append('=').Append(pair.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append('\n');
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public DocumentGraph Clone()
    {
        var copy = new DocumentGraph();
        foreach (var id in _order)
        {
            var n = _nodes[id];
            copy._nodes.Add(id, n with
            {
                Inputs = n.Inputs.ToArray(),
                Parameters = new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(n.Parameters))
            });
            copy._order.Add(id);
        }
        return copy;
    }

    private void EnsureAcyclic() => _ = TopologicalOrder();
}

public sealed class DocumentHistory
{
    private readonly Stack<DocumentGraph> _undo = [];
    private readonly Stack<DocumentGraph> _redo = [];

    public DocumentGraph Current { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public DocumentHistory(DocumentGraph? initial = null) => Current = initial?.Clone() ?? new DocumentGraph();

    public void Commit(Action<DocumentGraph> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var before = Current.Clone();
        var candidate = Current.Clone();
        edit(candidate);
        _undo.Push(before);
        _redo.Clear();
        Current = candidate;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Push(Current.Clone());
        Current = _undo.Pop();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Push(Current.Clone());
        Current = _redo.Pop();
    }
}
