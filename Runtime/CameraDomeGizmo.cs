using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class CameraDomeGizmo : MonoBehaviour
{
    public Transform target;
    public int numRings = 4;
    public int viewsPerRing = 20;
    public float radius = 5f;
    public float height = 1.5f;
    public float gizmoSize = 0.1f;
    public int mode = 0;
    public Vector3 volumeCenter = Vector3.zero;
    public Vector3 volumeSize = new Vector3(5, 5, 5);
    public int subdivX = 2, subdivY = 2, subdivZ = 2;
    public bool showGrid = true;
    public float currentTime = 0f;

    // Spherical capture parameters
    public Transform sphericalTarget;
    public int numSpherePoints = 100;
    public float sphereRadius = 5f;

    // Ellipsoid capture parameters
    public Transform ellipsoidTarget;
    public int numEllipsoidPoints = 100;
    public float ellipsoidRadiusX = 5f;
    public float ellipsoidRadiusY = 3f;
    public float ellipsoidRadiusZ = 4f;

    // Cylinder capture parameters
    public Transform cylinderTarget;
    public int numCylinderSidePoints = 50;
    public int numCylinderCapPoints = 20;
    public float cylinderRadius = 3f;
    public float cylinderHeight = 5f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        if (mode == 0)
        {
            if (target == null) return;

            for (int ring = 0; ring < numRings; ring++)
            {
                float elevation = Mathf.Lerp(-Mathf.PI / 4, Mathf.PI / 4, (float)ring / (numRings - 1));

                for (int i = 0; i < viewsPerRing; i++)
                {
                    float azimuth = i * Mathf.PI * 2 / viewsPerRing;

                    float x = radius * Mathf.Cos(elevation) * Mathf.Cos(azimuth);
                    float y = radius * Mathf.Sin(elevation);
                    float z = radius * Mathf.Cos(elevation) * Mathf.Sin(azimuth);

                    Vector3 position = target.position + new Vector3(x, y + height, z);
                    Vector3 direction = (target.position - position).normalized;

                    Gizmos.DrawSphere(position, gizmoSize);
                    Gizmos.DrawLine(position, position + direction * 0.5f);
                }
            }
        }
        else if (mode == 1)
        {
            Vector3 step = new Vector3(volumeSize.x / subdivX, volumeSize.y / subdivY, volumeSize.z / subdivZ);

            List<Vector3> directions = GenerateCustomSphericalDirections(); 
          

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(volumeCenter, volumeSize);
            if (showGrid)
                DrawSubdivisionGrid();

            Vector3 center = volumeCenter;


            Gizmos.color = Color.cyan;
            foreach (Vector3 dir in directions)
            {
                Gizmos.DrawSphere(center, gizmoSize);
                Gizmos.DrawLine(center, center + dir.normalized * 0.2f);
            }
        }
        else if (mode == 2)
        {
            // Spherical capture mode
            if (sphericalTarget == null) return;

            List<Vector3> spherePoints = GenerateFibonacciSpherePoints(numSpherePoints, sphereRadius, sphericalTarget.position);

            Gizmos.color = Color.yellow;
            // Draw sphere wireframe
            Gizmos.DrawWireSphere(sphericalTarget.position, sphereRadius);

            Gizmos.color = Color.cyan;
            foreach (Vector3 point in spherePoints)
            {
                Gizmos.DrawSphere(point, gizmoSize);
                Vector3 direction = (sphericalTarget.position - point).normalized;
                Gizmos.DrawLine(point, point + direction * 0.5f);
            }
        }
        else if (mode == 3)
        {
            // Ellipsoid capture mode
            if (ellipsoidTarget == null) return;

            List<Vector3> ellipsoidPoints = GenerateFibonacciEllipsoidPoints(numEllipsoidPoints, ellipsoidRadiusX, ellipsoidRadiusY, ellipsoidRadiusZ, ellipsoidTarget.position);

            Gizmos.color = Color.magenta;
            // Draw ellipsoid wireframe approximation with lines
            DrawEllipsoidWireframe(ellipsoidTarget.position, ellipsoidRadiusX, ellipsoidRadiusY, ellipsoidRadiusZ);

            Gizmos.color = Color.cyan;
            foreach (Vector3 point in ellipsoidPoints)
            {
                Gizmos.DrawSphere(point, gizmoSize);
                Vector3 direction = (ellipsoidTarget.position - point).normalized;
                Gizmos.DrawLine(point, point + direction * 0.5f);
            }
        }
        else if (mode == 4)
        {
            // Cylinder capture mode
            if (cylinderTarget == null) return;

            // Draw cylinder wireframe
            Gizmos.color = Color.green;
            DrawCylinderWireframe(cylinderTarget.position, cylinderRadius, cylinderHeight);

            // Generate and draw camera positions
            List<(Vector3, Vector3, bool)> cylinderPoints = GenerateCylinderPointsGizmo(numCylinderSidePoints, numCylinderCapPoints, cylinderRadius, cylinderHeight, cylinderTarget.position);

            Gizmos.color = Color.cyan;
            foreach (var point in cylinderPoints)
            {
                Vector3 position = point.Item1;
                Vector3 lookAt = point.Item2;

                Gizmos.DrawSphere(position, gizmoSize);
                Vector3 direction = (lookAt - position).normalized;
                Gizmos.DrawLine(position, position + direction * 0.5f);
            }
        }
    }

    private List<Vector3> GenerateCustomSphericalDirections()
    {
        List<Vector3> directions = new List<Vector3>();

        for (int i = 0; i < 8; i++)
        {
            float azimuth = i * 45f;
            Quaternion rot = Quaternion.Euler(0f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }

        for (int i = 0; i < 4; i++)
        {
            float azimuth = i * 90f;
            Quaternion rot = Quaternion.Euler(45f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }

        for (int i = 0; i < 4; i++)
        {
            float azimuth = i * 90f;
            Quaternion rot = Quaternion.Euler(-45f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }



        return directions;
    }

    // Generate points uniformly distributed on a sphere using Fibonacci lattice
    private List<Vector3> GenerateFibonacciSpherePoints(int numPoints, float radius, Vector3 center)
    {
        List<Vector3> points = new List<Vector3>();
        float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden angle in radians

        for (int i = 0; i < numPoints; i++)
        {
            float y = 1.0f - (i / (float)(numPoints - 1)) * 2.0f; // y goes from 1 to -1
            float radiusAtY = Mathf.Sqrt(1.0f - y * y); // radius at y

            float theta = phi * i; // golden angle increment

            float x = Mathf.Cos(theta) * radiusAtY;
            float z = Mathf.Sin(theta) * radiusAtY;

            Vector3 point = center + new Vector3(x, y, z) * radius;
            points.Add(point);
        }

        return points;
    }

    // Generate points uniformly distributed on an ellipsoid using Fibonacci lattice
    private List<Vector3> GenerateFibonacciEllipsoidPoints(int numPoints, float radiusX, float radiusY, float radiusZ, Vector3 center)
    {
        List<Vector3> points = new List<Vector3>();
        float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden angle in radians

        for (int i = 0; i < numPoints; i++)
        {
            // Generate uniform sphere point
            float y = 1.0f - (i / (float)(numPoints - 1)) * 2.0f; // y goes from 1 to -1
            float radiusAtY = Mathf.Sqrt(1.0f - y * y); // radius at y

            float theta = phi * i; // golden angle increment

            float x = Mathf.Cos(theta) * radiusAtY;
            float z = Mathf.Sin(theta) * radiusAtY;

            // Scale by ellipsoid radii to create ellipsoid
            Vector3 point = center + new Vector3(x * radiusX, y * radiusY, z * radiusZ);
            points.Add(point);
        }

        return points;
    }

    // Draw ellipsoid wireframe approximation
    private void DrawEllipsoidWireframe(Vector3 center, float radiusX, float radiusY, float radiusZ)
    {
        int segments = 32;

        // Draw XY ellipse (Z=0 plane)
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i / (float)segments) * Mathf.PI * 2;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radiusX, Mathf.Sin(angle1) * radiusY, 0);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radiusX, Mathf.Sin(angle2) * radiusY, 0);
            Gizmos.DrawLine(p1, p2);
        }

        // Draw XZ ellipse (Y=0 plane)
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i / (float)segments) * Mathf.PI * 2;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radiusX, 0, Mathf.Sin(angle1) * radiusZ);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radiusX, 0, Mathf.Sin(angle2) * radiusZ);
            Gizmos.DrawLine(p1, p2);
        }

        // Draw YZ ellipse (X=0 plane)
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i / (float)segments) * Mathf.PI * 2;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2;

            Vector3 p1 = center + new Vector3(0, Mathf.Cos(angle1) * radiusY, Mathf.Sin(angle1) * radiusZ);
            Vector3 p2 = center + new Vector3(0, Mathf.Cos(angle2) * radiusY, Mathf.Sin(angle2) * radiusZ);
            Gizmos.DrawLine(p1, p2);
        }
    }

    private void DrawSubdivisionGrid()
    {
        Gizmos.color = Color.gray;

        Vector3 start = volumeCenter - volumeSize / 2f;
        Vector3 step = new Vector3(volumeSize.x / subdivX, volumeSize.y / subdivY, volumeSize.z / subdivZ);

        for (int y = 0; y <= subdivY; y++)
        {
            for (int z = 0; z <= subdivZ; z++)
            {
                Vector3 p1 = start + new Vector3(0, y * step.y, z * step.z);
                Vector3 p2 = p1 + new Vector3(volumeSize.x, 0, 0);
                Gizmos.DrawLine(p1, p2);
            }
        }

        for (int x = 0; x <= subdivX; x++)
        {
            for (int z = 0; z <= subdivZ; z++)
            {
                Vector3 p1 = start + new Vector3(x * step.x, 0, z * step.z);
                Vector3 p2 = p1 + new Vector3(0, volumeSize.y, 0);
                Gizmos.DrawLine(p1, p2);
            }
        }

        for (int x = 0; x <= subdivX; x++)
        {
            for (int y = 0; y <= subdivY; y++)
            {
                Vector3 p1 = start + new Vector3(x * step.x, y * step.y, 0);
                Vector3 p2 = p1 + new Vector3(0, 0, volumeSize.z);
                Gizmos.DrawLine(p1, p2);
            }
        }
    }

    // Draw cylinder wireframe
    private void DrawCylinderWireframe(Vector3 center, float radius, float height)
    {
        int segments = 32;

        // Draw top circle
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i / (float)segments) * Mathf.PI * 2;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, height / 2f, Mathf.Sin(angle1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, height / 2f, Mathf.Sin(angle2) * radius);
            Gizmos.DrawLine(p1, p2);
        }

        // Draw bottom circle
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i / (float)segments) * Mathf.PI * 2;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, -height / 2f, Mathf.Sin(angle1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, -height / 2f, Mathf.Sin(angle2) * radius);
            Gizmos.DrawLine(p1, p2);
        }

        // Draw vertical lines connecting top and bottom
        for (int i = 0; i < 8; i++)
        {
            float angle = (i / 8f) * Mathf.PI * 2;
            Vector3 top = center + new Vector3(Mathf.Cos(angle) * radius, height / 2f, Mathf.Sin(angle) * radius);
            Vector3 bottom = center + new Vector3(Mathf.Cos(angle) * radius, -height / 2f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(top, bottom);
        }
    }

    // Generate cylinder capture points for gizmo (position, lookAt, isSide)
    private List<(Vector3, Vector3, bool)> GenerateCylinderPointsGizmo(int sidePoints, int capPoints, float radius, float height, Vector3 center)
    {
        List<(Vector3, Vector3, bool)> result = new List<(Vector3, Vector3, bool)>();

        // Generate side points (horizontal cameras looking at center)
        for (int i = 0; i < sidePoints; i++)
        {
            float angle = (i / (float)sidePoints) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 position = center + new Vector3(x, 0, z);
            Vector3 lookAt = center + new Vector3(0, position.y, 0);

            result.Add((position, lookAt, true));
        }

        // Generate top cap points (cameras looking down)
        List<Vector2> topCapDisc = GenerateFibonacciDiscGizmo(capPoints, radius);
        foreach (Vector2 disc in topCapDisc)
        {
            Vector3 position = center + new Vector3(disc.x, height / 2f, disc.y);
            Vector3 lookAt = center + new Vector3(disc.x, center.y - 1f, disc.y);

            result.Add((position, lookAt, false));
        }

        // Generate bottom cap points (cameras looking up)
        List<Vector2> bottomCapDisc = GenerateFibonacciDiscGizmo(capPoints, radius);
        foreach (Vector2 disc in bottomCapDisc)
        {
            Vector3 position = center + new Vector3(disc.x, -height / 2f, disc.y);
            Vector3 lookAt = center + new Vector3(disc.x, center.y + 1f, disc.y);

            result.Add((position, lookAt, false));
        }

        return result;
    }

    // Generate points uniformly distributed on a disc using Fibonacci lattice
    private List<Vector2> GenerateFibonacciDiscGizmo(int numPoints, float radius)
    {
        List<Vector2> points = new List<Vector2>();
        float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden angle

        for (int i = 0; i < numPoints; i++)
        {
            float r = radius * Mathf.Sqrt(i / (float)numPoints);
            float theta = phi * i;

            float x = r * Mathf.Cos(theta);
            float y = r * Mathf.Sin(theta);

            points.Add(new Vector2(x, y));
        }

        return points;
    }

    private void Update()
    {
        if (EditorApplication.isPlaying && !EditorApplication.isPaused)
            currentTime += Time.deltaTime;
        else
            currentTime = 0;



    }
}
