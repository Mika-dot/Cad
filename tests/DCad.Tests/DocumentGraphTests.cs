using DCad.Core;
using Xunit;

namespace DCad.Tests;

public sealed class DocumentGraphTests
{
    [Fact]
    public void FeatureGraph_IsDeterministicAndTopologicallyOrdered()
    {
        var graph = new DocumentGraph();
        var box = FeatureNode.Create("base", FeatureKind.Primitive, "box", parameters: new Dictionary<string, double>
        {
            ["x"] = 60,
            ["y"] = 40,
            ["z"] = 8,
        });
        graph.Add(box);
        var hole = FeatureNode.Create("hole", FeatureKind.Primitive, "cylinder", parameters: new Dictionary<string, double>
        {
            ["radius"] = 5,
            ["height"] = 20,
        });
        graph.Add(hole);
        var cut = FeatureNode.Create("result", FeatureKind.Boolean, "difference", [box.Id, hole.Id]);
        graph.Add(cut);

        var order = graph.TopologicalOrder().ToList();
        Assert.Equal(3, order.Count);
        Assert.True(order.IndexOf(box) < order.IndexOf(cut));
        Assert.True(order.IndexOf(hole) < order.IndexOf(cut));
        Assert.Equal(graph.DeterministicFingerprint(), graph.Clone().DeterministicFingerprint());
    }

    [Fact]
    public void History_UndoRedoRestoresGraphState()
    {
        var history = new DocumentHistory();
        FeatureNode? box = null;
        history.Commit(g =>
        {
            box = FeatureNode.Create("base", FeatureKind.Primitive, "box");
            g.Add(box);
        });
        Assert.Equal(1, history.Current.Count);
        history.Commit(g => g.Add(FeatureNode.Create("move", FeatureKind.Transform, "translate", [box!.Id])));
        Assert.Equal(2, history.Current.Count);

        history.Undo();
        Assert.Equal(1, history.Current.Count);
        history.Redo();
        Assert.Equal(2, history.Current.Count);
    }

    [Fact]
    public void RemovingInputWithoutCascadeIsRejected()
    {
        var graph = new DocumentGraph();
        var a = FeatureNode.Create("a", FeatureKind.Primitive, "box");
        graph.Add(a);
        graph.Add(FeatureNode.Create("b", FeatureKind.Transform, "translate", [a.Id]));
        Assert.Throws<InvalidOperationException>(() => graph.Remove(a.Id));
        graph.Remove(a.Id, cascade: true);
        Assert.Equal(0, graph.Count);
    }
}
