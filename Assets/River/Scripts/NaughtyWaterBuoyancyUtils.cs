using UnityEngine;

/// <summary>
/// Utility math helpers required by the custom buoyancy scripts.
/// Provides a replacement for the missing asset dependency.
/// </summary>
public static class MathfUtils {
    /// <summary>
    /// Approximate the volume of a mesh by summing tetrahedrons defined by each triangle and the origin.
    /// The mesh vertices are first transformed into world space so scaling on the transform is respected.
    /// </summary>
    public static float CalculateVolume_Mesh (Mesh mesh, Transform transform) {
        if (mesh == null) {
            return 0f;
        }

        var vertices = mesh.vertices;
        var triangles = mesh.triangles;

        if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length == 0) {
            return 0f;
        }

        double volume = 0d;

        for (int i = 0; i < triangles.Length; i += 3) {
            Vector3 p0 = transform.TransformPoint (vertices[triangles[i]]);
            Vector3 p1 = transform.TransformPoint (vertices[triangles[i + 1]]);
            Vector3 p2 = transform.TransformPoint (vertices[triangles[i + 2]]);

            volume += SignedVolumeOfTriangle (p0, p1, p2);
        }

        return Mathf.Abs ((float) volume);
    }

    private static double SignedVolumeOfTriangle (Vector3 p1, Vector3 p2, Vector3 p3) {
        return Vector3.Dot (p1, Vector3.Cross (p2, p3)) / 6.0f;
    }
}

/// <summary>
/// Basic collider helper methods used by the buoyancy scripts.
/// </summary>
public static class ColliderUtils {
    /// <summary>
    /// Determine whether the point lies inside the collider. Uses the collider bounds as a quick reject,
    /// then relies on ClosestPoint which snaps the point onto the surface when it lies outside.
    /// </summary>
    public static bool IsPointInsideCollider (Vector3 point, Collider collider, ref Bounds cachedBounds) {
        if (collider == null) {
            return false;
        }

        if (!cachedBounds.Contains (point)) {
            return false;
        }

        Vector3 closestPoint = collider.ClosestPoint (point);
        // If the point was outside the collider, ClosestPoint returns a different position.
        return (closestPoint - point).sqrMagnitude < 1e-6f;
    }
}

