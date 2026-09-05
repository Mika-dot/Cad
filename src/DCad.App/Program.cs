using DCad.Boolean.Manifold;
using DCad.App;
using DCad.Language;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

var modelPath = args.Length > 0 ? args[0] : Path.Combine("examples", "bracket.dcad");
if (!File.Exists(modelPath))
{
    Console.Error.WriteLine($"Model not found: {modelPath}");
    return 2;
}

var kernel = new ManifoldKernel();
using var document = CadScript.Execute(File.ReadAllText(modelPath), kernel);
var mesh = document.Result.ToMesh();

var native = new NativeWindowSettings
{
    ClientSize = new Vector2i(1280, 800),
    Title = $"DCad — {Path.GetFileName(modelPath)}"
};
using var window = new MeshWindow(GameWindowSettings.Default, native, mesh);
window.Run();
return 0;
