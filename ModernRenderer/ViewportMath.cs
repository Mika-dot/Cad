using OpenTK.Mathematics;

namespace DCad.Renderer;

public readonly record struct Ray3(Vector3 Origin, Vector3 Direction)
{
    public Ray3 Normalized() => new(Origin, Direction.LengthSquared > 1e-20f ? Direction.Normalized() : Vector3.UnitZ);
}

public readonly record struct PickHit(int TriangleIndex, float Distance, Vector3 Position, Vector3 Barycentric);

public enum ScalarScale
{
    Linear,
    Log10,
    Symmetric
}

public readonly record struct ScalarMapping(float Min, float Max, ScalarScale Scale = ScalarScale.Linear)
{
    public float Normalize(float value)
    {
        if (!float.IsFinite(value)) return 0f;
        if (!(Max > Min)) return 0.5f;
        return Scale switch
        {
            ScalarScale.Log10 => NormalizeLog(value),
            ScalarScale.Symmetric => NormalizeSymmetric(value),
            _ => Math.Clamp((value - Min) / (Max - Min), 0f, 1f),
        };
    }

    private float NormalizeLog(float value)
    {
        float floor = Math.Max(Math.Abs(Min), 1e-20f);
        float lo = MathF.Log10(floor);
        float hi = MathF.Log10(Math.Max(Math.Abs(Max), floor * 1.000001f));
        float x = MathF.Log10(Math.Max(Math.Abs(value), floor));
        return hi > lo ? Math.Clamp((x - lo) / (hi - lo), 0f, 1f) : 0.5f;
    }

    private float NormalizeSymmetric(float value)
    {
        float bound = Math.Max(Math.Abs(Min), Math.Abs(Max));
        return bound > 1e-20f ? Math.Clamp(0.5f + 0.5f * value / bound, 0f, 1f) : 0.5f;
    }

    public static ScalarMapping Robust(IEnumerable<float> values, float lowerQuantile = 0.02f, float upperQuantile = 0.98f, ScalarScale scale = ScalarScale.Linear)
    {
        var finite = values.Where(float.IsFinite).OrderBy(v => v).ToArray();
        if (finite.Length == 0) return new ScalarMapping(0f, 1f, scale);
        lowerQuantile = Math.Clamp(lowerQuantile, 0f, 1f);
        upperQuantile = Math.Clamp(upperQuantile, lowerQuantile, 1f);
        int lo = (int)MathF.Round((finite.Length - 1) * lowerQuantile);
        int hi = (int)MathF.Round((finite.Length - 1) * upperQuantile);
        return new ScalarMapping(finite[lo], finite[hi], scale);
    }
}

public static class ViewportPicking
{
    public static Ray3 ScreenRay(
        Vector2 screen,
        Vector2 viewportSize,
        Matrix4 view,
        Matrix4 projection)
    {
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) throw new ArgumentOutOfRangeException(nameof(viewportSize));
        float x = 2f * screen.X / viewportSize.X - 1f;
        float y = 1f - 2f * screen.Y / viewportSize.Y;

        Matrix4 invViewProjection = Matrix4.Invert(view * projection);
        var near4 = Vector4.TransformRow(new Vector4(x, y, -1f, 1f), invViewProjection);
        var far4 = Vector4.TransformRow(new Vector4(x, y, 1f, 1f), invViewProjection);
        if (Math.Abs(near4.W) < 1e-20f || Math.Abs(far4.W) < 1e-20f)
            throw new InvalidOperationException("Cannot unproject screen point.");
        Vector3 near = near4.Xyz / near4.W;
        Vector3 far = far4.Xyz / far4.W;
        return new Ray3(near, (far - near).Normalized());
    }

    public static PickHit? PickMesh(MeshData mesh, Ray3 ray, Matrix4 model)
    {
        ray = ray.Normalized();
        PickHit? best = null;
        int stride = 7;
        for (int triangle = 0; triangle < mesh.Indices.Length / 3; triangle++)
        {
            int ia = checked((int)mesh.Indices[triangle * 3]) * stride;
            int ib = checked((int)mesh.Indices[triangle * 3 + 1]) * stride;
            int ic = checked((int)mesh.Indices[triangle * 3 + 2]) * stride;
            Vector3 a = Vector3.TransformPosition(new Vector3(mesh.Vertices[ia], mesh.Vertices[ia + 1], mesh.Vertices[ia + 2]), model);
            Vector3 b = Vector3.TransformPosition(new Vector3(mesh.Vertices[ib], mesh.Vertices[ib + 1], mesh.Vertices[ib + 2]), model);
            Vector3 c = Vector3.TransformPosition(new Vector3(mesh.Vertices[ic], mesh.Vertices[ic + 1], mesh.Vertices[ic + 2]), model);
            if (!IntersectTriangle(ray, a, b, c, out float distance, out Vector3 barycentric)) continue;
            if (best is null || distance < best.Value.Distance)
                best = new PickHit(triangle, distance, ray.Origin + ray.Direction * distance, barycentric);
        }
        return best;
    }

    public static bool IntersectTriangle(Ray3 ray, Vector3 a, Vector3 b, Vector3 c, out float distance, out Vector3 barycentric)
    {
        // Moller-Trumbore with a scale-relative degeneracy threshold.
        Vector3 e1 = b - a;
        Vector3 e2 = c - a;
        Vector3 p = Vector3.Cross(ray.Direction, e2);
        float det = Vector3.Dot(e1, p);
        float scale = Math.Max(e1.Length * e2.Length, 1f);
        float eps = 1e-7f * scale;
        if (Math.Abs(det) <= eps)
        {
            distance = 0; barycentric = default; return false;
        }
        float inv = 1f / det;
        Vector3 t = ray.Origin - a;
        float u = Vector3.Dot(t, p) * inv;
        if (u < 0f || u > 1f) { distance = 0; barycentric = default; return false; }
        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(ray.Direction, q) * inv;
        if (v < 0f || u + v > 1f) { distance = 0; barycentric = default; return false; }
        float d = Vector3.Dot(e2, q) * inv;
        if (d < 0f) { distance = 0; barycentric = default; return false; }
        distance = d;
        barycentric = new Vector3(1f - u - v, u, v);
        return true;
    }
}
