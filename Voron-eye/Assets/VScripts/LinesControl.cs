using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VE
{
    public class LinesControl : MonoBehaviour
    {
        [SerializeField] private Camera m_GlobalCamera;
        [SerializeField] private Camera m_ScreenCamera;

        Vector3 m_PreviousScreenCameraPosition;
        Vector3 m_PreviousGlobalCameraPosition;
        public float m_MinScreenCameraDistance = 1f;
        public float m_MaxScreenCameraDistance = 100f;
        float m_ScreenCameraDistance;
        float m_GlobalCameraDistance;

        //Camera Multitarget
        public float Pitch;
        public float Yaw;
        public float Roll;
        public float PaddingLeft;
        public float PaddingRight;
        public float PaddingUp;
        public float PaddingDown;
        public float MoveSmoothTime = 0.19f;

        private DebugProjection _debugProjection;
        enum DebugProjection { DISABLE, IDENTITY, ROTATED }
        enum ProjectionEdgeHits { TOP_BOTTOM, LEFT_RIGHT }

        //Targets
        [SerializeField] private List<GameObject> m_Targets;
        private List<Vector2> m_ScreenTargets;
        private Vector3 m_AverageTargetPosition;

        //Lines
        private List<LineRenderer> m_Lines;
        private List<LineRenderer> m_PerpendicularLines;

        //Meshes
        private List<GameObject> m_Meshes;
        Vector2[] screenVertices;
        GameObject meshgo;
        //Mesh mesh;
        Vector3[] vertices;
        int[] triangles;


        private void Awake()
        {
            _debugProjection = DebugProjection.ROTATED;
        }

        // Start is called before the first frame update
        void Start()
        {
            //Initializing Lists
            m_Lines = new List<LineRenderer>();
            m_PerpendicularLines = new List<LineRenderer>();
            m_ScreenTargets = new List<Vector2>();
            m_Meshes = new List<GameObject>();

            //Setting Camera Distances
            m_ScreenCameraDistance = m_MinScreenCameraDistance;
            m_GlobalCameraDistance = m_MinScreenCameraDistance;

            //Screen Vertices Array
            screenVertices = new Vector2[4];
            screenVertices[2] = new Vector2(0, 0); //the order of this almost makes me have a heart attack
            screenVertices[3] = new Vector2(0, m_GlobalCamera.pixelHeight);
            screenVertices[1] = new Vector2(m_GlobalCamera.pixelWidth, 0);
            screenVertices[0] = new Vector2(m_GlobalCamera.pixelWidth, m_GlobalCamera.pixelHeight);

            GameObject Lines = new GameObject("Lines");
            GameObject Plines = new GameObject("PLines");
            GameObject Meshes = new GameObject("ScreenMeshes");

            for (int i = 0; i < m_Targets.Count; i++)
            {
                //Get the screen position
                m_ScreenTargets.Add(m_GlobalCamera.WorldToScreenPoint(m_Targets[i].transform.position));

                //Create the colored Lines between them and the perpendicular lines and add them to their respective lists
                for (int j = i + 1; j < m_Targets.Count; j++)
                {
                    LineRenderer lr = DrawLine("Line" + (i + 1) + (j + 1), m_Targets[i].transform.position, m_Targets[j].transform.position);
                    lr.colorGradient = CreateGradient(m_Targets[i].GetComponent<Renderer>().material.color, m_Targets[j].GetComponent<Renderer>().material.color);
                    lr.transform.parent = Lines.transform;
                    m_Lines.Add(lr);

                    LineRenderer plr = DrawLinePerpendicular("PLine" + (i + 1) + (j + 1), lr, -m_GlobalCamera.transform.forward);
                    plr.colorGradient = CreateGradient(Color.black, Color.black);
                    plr.transform.parent = Plines.transform;
                    m_PerpendicularLines.Add(plr);
                }

                //Create Meshes and add them to the list
                GameObject meshgo = new GameObject("Mesh" + i + 1);
                meshgo.transform.position = transform.position;
                meshgo.transform.parent = Meshes.transform;
                meshgo.AddComponent<MeshFilter>();
                meshgo.AddComponent<MeshRenderer>();
                meshgo.GetComponent<Renderer>().material = m_Targets[i].GetComponent<Renderer>().material;
                m_Meshes.Add(meshgo);

                //Mesh mesh;
                //mesh = meshgo.GetComponent<MeshFilter>().mesh;

            }

            //Test mesh
            meshgo = new GameObject("meshgo");
            meshgo.AddComponent<MeshFilter>();
            meshgo.AddComponent<MeshRenderer>();

            //mesh = meshgo.GetComponent<MeshFilter>().mesh;
        }

        // Update is called once per frame
        void Update()
        {
            // Make mesh data
            vertices = new Vector3[4];
            vertices[0] = new Vector3(-1, -1);
            vertices[1] = new Vector3(-1, MeshValue.ins.yValue);
            vertices[2] = new Vector3(1, 1);
            vertices[3] = new Vector3(1, -1);

            triangles = new int[] { 0, 1, 2, 0, 2, 3 };

            //create mesh
            //mesh.Clear();
            //mesh.vertices = vertices;
            //mesh.triangles = triangles;

        }

        private void LateUpdate()
        {
            //FindAverageTargetPosition();
            CameraMovement();

            UpdateGlobalScreenTargets();

            UpdateLines(m_Targets, m_Lines, m_PerpendicularLines);

            VoronoiDiagram();
        }
        private void SetGlobalScreenTargets()
        {
            for (int i = 0; i < m_Targets.Count; i++)
            {
                // If the target isn't active, go on to the next one.
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;

                //Unity implictly converts vec3 to vec2 and viceversa, it discards z (which we don't need)
                m_ScreenTargets.Add(m_GlobalCamera.WorldToScreenPoint(m_Targets[i].transform.position));
            }
        }
        private void UpdateGlobalScreenTargets()
        {
            for (int i = 0; i < m_Targets.Count; i++)
            {
                // If the target isn't active, go on to the next one.
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;

                m_ScreenTargets[i] = m_GlobalCamera.WorldToScreenPoint(m_Targets[i].transform.position);
            }
        }
        private void FindAverageTargetPosition()
        {
            Vector3 averagePos = new Vector3();
            int numTargets = 0;

            // If the target is only one just send its position.
            if (m_Targets.Count == 1)
            {
                m_AverageTargetPosition = m_Targets[0].transform.position;
                return;
            }

            // Go through all the targets and add their positions together.
            for (int i = 0; i < m_Targets.Count; i++)
            {
                // If the target isn't active, go on to the next one.
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;

                // Add to the average and increment the number of targets in the average.
                averagePos += m_Targets[i].transform.position;
                numTargets++;
            }

            // If there are targets divide the sum of the positions by the number of them to find the average.
            if (numTargets > 0)
                averagePos /= numTargets;

            // Keep the same y value.
            averagePos.y = transform.position.y;

            // The desired position is the average position;
            m_AverageTargetPosition = averagePos;
        }
        //Creates a LineRenderer Line with 3 points from 2 positions
        LineRenderer DrawLine(string name, Vector3 start, Vector3 end/*, Color color*//*, float duration = 0.2f*/)
        {
            GameObject myLine = new GameObject(name);
            myLine.transform.position = start;
            myLine.AddComponent<LineRenderer>();
            LineRenderer lr = myLine.GetComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));

            //lr.SetWidth(0.1f, 0.1f);
            lr.SetVertexCount(3); //The Players and the midpoint
            lr.SetPosition(0, start);
            lr.SetPosition(1, (start + end) / 2); //Midpoint
            lr.SetPosition(2, end);
            //GameObject.Destroy(myLine, duration);

            return lr;
        }

        //Creates a LineRenderer Line with 3 points perpendicular from an existing line inside the camera plane
        LineRenderer DrawLinePerpendicular(string name, LineRenderer line, Vector3 normal)
        {
            GameObject myLine = new GameObject(name);
            myLine.transform.position = line.GetPosition(1);//my lines have 3 points so 1 is the middle
            myLine.AddComponent<LineRenderer>();
            LineRenderer plr = myLine.GetComponent<LineRenderer>();
            plr.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));

            Vector3 linevec = line.GetPosition(2) - line.GetPosition(0);
            Vector3 crossvec = Vector3.Cross(linevec, normal).normalized;

            plr.SetVertexCount(3);
            plr.SetPosition(0, line.GetPosition(1) + crossvec * 100);
            plr.SetPosition(1, line.GetPosition(1));
            plr.SetPosition(2, line.GetPosition(1) - crossvec * 100);

            return plr;
        }

        Gradient CreateGradient(Color startColor, Color endColor)
        {
            float alpha = 1.0f;
            Gradient gradient = new Gradient();

            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(startColor, 0.0f), new GradientColorKey(endColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(alpha, 1.0f), new GradientAlphaKey(alpha, 1.0f) }
            );

            return gradient;
        }

        void UpdateLines(List<GameObject> targets, List<LineRenderer> lines, List<LineRenderer> plines)
        {
            //Player Lines
            int count = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                for (int j = i + 1; j < targets.Count; j++)
                {
                    //Updating Lines between players
                    lines[count].SetPosition(0, targets[i].transform.position);
                    lines[count].SetPosition(1, (targets[i].transform.position + targets[j].transform.position) / 2); //Midpoint
                    lines[count].SetPosition(2, targets[j].transform.position);

                    //Updating perpendicular lines
                    Vector3 linevec = lines[count].GetPosition(2) - lines[count].GetPosition(0);
                    Vector3 crossvec = Vector3.Cross(linevec, -m_GlobalCamera.transform.forward).normalized;

                    plines[count].SetPosition(0, lines[count].GetPosition(1) + crossvec * 100);
                    plines[count].SetPosition(1, lines[count].GetPosition(1));
                    plines[count].SetPosition(2, lines[count].GetPosition(1) - crossvec * 100);

                    count++;
                }
            }
        }

        //Lines intersecting check https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect
        public static int Check(float p0_x, float p0_y, float p1_x, float p1_y, float p2_x,
                                      float p2_y, float p3_x, float p3_y, float i_x, float i_y)
        {
            float s1_x, s1_y, s2_x, s2_y;
            s1_x = p1_x - p0_x; s1_y = p1_y - p0_y;
            s2_x = p3_x - p2_x; s2_y = p3_y - p2_y;
            float s, t;
            s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / (-s2_x * s1_y + s1_x * s2_y);
            t = (s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / (-s2_x * s1_y + s1_x * s2_y);

            if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
            {
                // Collision detected
                if (i_x != null)
                    i_x = p0_x + (t * s1_x); //Intersection point x
                if (i_y != null)
                    i_y = p0_y + (t * s1_y); //Intersection point y
                return 1;
            }
            return 0; // No collision      
        }

        void CameraMovement()
        {
            if (m_Targets.Count == 0)
                return;

            //Mouse Camera Rotation
            if (Input.GetMouseButtonDown(0))
            {
                m_PreviousGlobalCameraPosition = m_GlobalCamera.ScreenToViewportPoint(Input.mousePosition);
            }

            if (Input.GetMouseButton(0))
            {
                Vector3 direction = m_PreviousGlobalCameraPosition - m_GlobalCamera.ScreenToViewportPoint(Input.mousePosition);

                //Global Camera
                m_GlobalCamera.transform.Rotate(new Vector3(1, 0, 0), direction.y * 180);
                m_GlobalCamera.transform.Rotate(new Vector3(0, 1, 0), -direction.x * 180, Space.World);

                m_PreviousGlobalCameraPosition = m_GlobalCamera.ScreenToViewportPoint(Input.mousePosition);

                Pitch = m_GlobalCamera.transform.rotation.eulerAngles.x;
                Yaw = m_GlobalCamera.transform.rotation.eulerAngles.y;
                Roll = m_GlobalCamera.transform.rotation.eulerAngles.z;
            }

            //Multitarget repositioning
            var targetPositionAndRotation = TargetPositionAndRotation();

            m_GlobalCamera.transform.position = targetPositionAndRotation.Position;//Vector3.SmoothDamp(m_GlobalCamera.transform.position, targetPositionAndRotation.Position, ref velocity, MoveSmoothTime);
            m_GlobalCamera.transform.rotation = targetPositionAndRotation.Rotation;

            //Checking that the screen camera distance from the targets is not higher than the maximum we set
            if (m_GlobalCameraDistance > m_MaxScreenCameraDistance)
            {
                //Substracting the difference to maintain the maximum distance set
                var distanceDiff = m_MaxScreenCameraDistance - m_GlobalCameraDistance;
                m_ScreenCamera.transform.localPosition = new Vector3(0, 0, -distanceDiff);
            }
        }

        void VoronoiDiagram()
        {
            var calc = new VoronoiCalculator();
            var clip = new VoronoiClipper();

            var sites = new Vector2[m_ScreenTargets.Count];

            for (int i = 0; i < sites.Length; i++)
            {
                sites[i] = m_ScreenTargets[i];
            }

            var diagram = calc.CalculateDiagram(sites);

            var clipped = new List<Vector2>();

            //DO THE SAME AS THE BREAK FUNCTION IN BREAKABLE SURFACE BUT NOT INSTANTIATE

            for (int i = 0; i < sites.Length; i++)
            {
                clip.ClipSite(diagram, screenVertices, i, ref clipped);

                if (clipped.Count > 0)
                {
                    m_Meshes[i].GetComponent<MeshFilter>().mesh.Clear();

                    m_Meshes[i].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);
                }
            }
            

        }
        static Mesh MeshFromPolygon(List<Vector2> polygon, float thickness)
        {
            var count = polygon.Count;
            // TODO: cache these things to avoid garbage
            var verts = new Vector3[6 * count];
            var norms = new Vector3[6 * count];
            var tris = new int[3 * (4 * count - 4)];
            // TODO: add UVs

            var vi = 0;
            var ni = 0;
            var ti = 0;

            var ext = 0.5f * thickness;

            // Top
            for (int i = 0; i < count; i++)
            {
                verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, ext);
                norms[ni++] = Vector3.forward;
            }

            // Bottom
            for (int i = 0; i < count; i++)
            {
                verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, -ext);
                norms[ni++] = Vector3.back;
            }

            // Sides
            for (int i = 0; i < count; i++)
            {
                var iNext = i == count - 1 ? 0 : i + 1;

                verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, ext);
                verts[vi++] = new Vector3(polygon[i].x, polygon[i].y, -ext);
                verts[vi++] = new Vector3(polygon[iNext].x, polygon[iNext].y, -ext);
                verts[vi++] = new Vector3(polygon[iNext].x, polygon[iNext].y, ext);

                var norm = Vector3.Cross(polygon[iNext] - polygon[i], Vector3.forward).normalized;

                norms[ni++] = norm;
                norms[ni++] = norm;
                norms[ni++] = norm;
                norms[ni++] = norm;
            }


            for (int vert = 2; vert < count; vert++)
            {
                tris[ti++] = 0;
                tris[ti++] = vert - 1;
                tris[ti++] = vert;
            }

            for (int vert = 2; vert < count; vert++)
            {
                tris[ti++] = count;
                tris[ti++] = count + vert;
                tris[ti++] = count + vert - 1;
            }

            for (int vert = 0; vert < count; vert++)
            {
                var si = 2 * count + 4 * vert;

                tris[ti++] = si;
                tris[ti++] = si + 1;
                tris[ti++] = si + 2;

                tris[ti++] = si;
                tris[ti++] = si + 2;
                tris[ti++] = si + 3;
            }

            Debug.Assert(ti == tris.Length);
            Debug.Assert(vi == verts.Length);

            var mesh = new Mesh();


            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.normals = norms;

            return mesh;
        }
        static Mesh MeshPolygonFromPolygon(List<Vector2> polygon)
        {
            //Simplified version from Oskar's MeshFromPolygon() where he adds thickness
            //The polygon has to be convex (so traingles relation to verts is n-2)
            //TODO: check if convex
            //TODO: check if 3 or more points
            var count = polygon.Count;
            // TODO: cache these things to avoid garbage
            var verts = new Vector3[count];
            var norms = new Vector3[count];
            var tris = new int[3*(count - 2)];
            // TODO: add UVs

            var vi = 0;
            var ni = 0;
            var ti = 0;

            for (int i = 0; i < count; i++)
            {
                verts[vi++] = new Vector3(polygon[i].x, polygon[i].y);
                norms[ni++] = Vector3.forward;
            }

            for (int vert = 2; vert < count; vert++)
            {
                tris[ti++] = 0;
                tris[ti++] = vert - 1;
                tris[ti++] = vert;
            }

            Debug.Assert(ti == tris.Length);
            Debug.Assert(vi == verts.Length);

            var mesh = new Mesh();


            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.normals = norms;

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }
        PositionAndRotation TargetPositionAndRotation()
        {
            float halfVerticalFovRad = (m_GlobalCamera.fieldOfView * Mathf.Deg2Rad) / 2f;
            float halfHorizontalFovRad = Mathf.Atan(Mathf.Tan(halfVerticalFovRad) * m_GlobalCamera.aspect);

            var rotation = Quaternion.Euler(Pitch, Yaw, Roll);
            var inverseRotation = Quaternion.Inverse(rotation);

            var targetsRotatedToCameraIdentity = m_Targets.Select(target => inverseRotation * target.transform.position).ToArray();

            float furthestPointDistanceFromCamera = targetsRotatedToCameraIdentity.Max(target => target.z);
            float projectionPlaneZ = furthestPointDistanceFromCamera + 3f;

            ProjectionHits viewProjectionLeftAndRightEdgeHits =
                ViewProjectionEdgeHits(targetsRotatedToCameraIdentity, ProjectionEdgeHits.LEFT_RIGHT, projectionPlaneZ, halfHorizontalFovRad).AddPadding(PaddingRight, PaddingLeft);
            ProjectionHits viewProjectionTopAndBottomEdgeHits =
                ViewProjectionEdgeHits(targetsRotatedToCameraIdentity, ProjectionEdgeHits.TOP_BOTTOM, projectionPlaneZ, halfVerticalFovRad).AddPadding(PaddingUp, PaddingDown);

            var requiredCameraPerpedicularDistanceFromProjectionPlane =
                Mathf.Max(
                    RequiredCameraPerpedicularDistanceFromProjectionPlane(viewProjectionTopAndBottomEdgeHits, halfVerticalFovRad),
                    RequiredCameraPerpedicularDistanceFromProjectionPlane(viewProjectionLeftAndRightEdgeHits, halfHorizontalFovRad)
            );

            Vector3 cameraPositionIdentity = new Vector3(
                (viewProjectionLeftAndRightEdgeHits.Max + viewProjectionLeftAndRightEdgeHits.Min) / 2f,
                (viewProjectionTopAndBottomEdgeHits.Max + viewProjectionTopAndBottomEdgeHits.Min) / 2f,
                projectionPlaneZ - requiredCameraPerpedicularDistanceFromProjectionPlane);

            DebugDrawProjectionRays(cameraPositionIdentity,
                viewProjectionLeftAndRightEdgeHits,
                viewProjectionTopAndBottomEdgeHits,
                requiredCameraPerpedicularDistanceFromProjectionPlane,
                targetsRotatedToCameraIdentity,
                projectionPlaneZ,
                halfHorizontalFovRad,
                halfVerticalFovRad);

            //We store the distance
            m_GlobalCameraDistance = requiredCameraPerpedicularDistanceFromProjectionPlane;
            m_ScreenCameraDistance = requiredCameraPerpedicularDistanceFromProjectionPlane;

            return new PositionAndRotation(rotation * cameraPositionIdentity, rotation);
        }


        private static float RequiredCameraPerpedicularDistanceFromProjectionPlane(ProjectionHits viewProjectionEdgeHits, float halfFovRad)
        {
            float distanceBetweenEdgeProjectionHits = viewProjectionEdgeHits.Max - viewProjectionEdgeHits.Min;
            return (distanceBetweenEdgeProjectionHits / 2f) / Mathf.Tan(halfFovRad);
        }

        private ProjectionHits ViewProjectionEdgeHits(IEnumerable<Vector3> targetsRotatedToCameraIdentity, ProjectionEdgeHits alongAxis, float projectionPlaneZ, float halfFovRad)
        {
            float[] projectionHits = targetsRotatedToCameraIdentity
                .SelectMany(target => TargetProjectionHits(target, alongAxis, projectionPlaneZ, halfFovRad))
                .ToArray();
            return new ProjectionHits(projectionHits.Max(), projectionHits.Min());
        }

        private float[] TargetProjectionHits(Vector3 target, ProjectionEdgeHits alongAxis, float projectionPlaneDistance, float halfFovRad)
        {
            float distanceFromProjectionPlane = projectionPlaneDistance - target.z;
            float projectionHalfSpan = Mathf.Tan(halfFovRad) * distanceFromProjectionPlane;

            if (alongAxis == ProjectionEdgeHits.LEFT_RIGHT)
            {
                return new[] { target.x + projectionHalfSpan, target.x - projectionHalfSpan };
            }
            else
            {
                return new[] { target.y + projectionHalfSpan, target.y - projectionHalfSpan };
            }

        }

        private void DebugDrawProjectionRays(Vector3 cameraPositionIdentity, ProjectionHits viewProjectionLeftAndRightEdgeHits,
            ProjectionHits viewProjectionTopAndBottomEdgeHits, float requiredCameraPerpedicularDistanceFromProjectionPlane,
            IEnumerable<Vector3> targetsRotatedToCameraIdentity, float projectionPlaneZ, float halfHorizontalFovRad,
            float halfVerticalFovRad)
        {

            if (_debugProjection == DebugProjection.DISABLE)
                return;

            DebugDrawProjectionRay(
                cameraPositionIdentity,
                new Vector3((viewProjectionLeftAndRightEdgeHits.Max - viewProjectionLeftAndRightEdgeHits.Min) / 2f,
                    (viewProjectionTopAndBottomEdgeHits.Max - viewProjectionTopAndBottomEdgeHits.Min) / 2f,
                    requiredCameraPerpedicularDistanceFromProjectionPlane), new Color32(31, 119, 180, 255));
            DebugDrawProjectionRay(
                cameraPositionIdentity,
                new Vector3((viewProjectionLeftAndRightEdgeHits.Max - viewProjectionLeftAndRightEdgeHits.Min) / 2f,
                    -(viewProjectionTopAndBottomEdgeHits.Max - viewProjectionTopAndBottomEdgeHits.Min) / 2f,
                    requiredCameraPerpedicularDistanceFromProjectionPlane), new Color32(31, 119, 180, 255));
            DebugDrawProjectionRay(
                cameraPositionIdentity,
                new Vector3(-(viewProjectionLeftAndRightEdgeHits.Max - viewProjectionLeftAndRightEdgeHits.Min) / 2f,
                    (viewProjectionTopAndBottomEdgeHits.Max - viewProjectionTopAndBottomEdgeHits.Min) / 2f,
                    requiredCameraPerpedicularDistanceFromProjectionPlane), new Color32(31, 119, 180, 255));
            DebugDrawProjectionRay(
                cameraPositionIdentity,
                new Vector3(-(viewProjectionLeftAndRightEdgeHits.Max - viewProjectionLeftAndRightEdgeHits.Min) / 2f,
                    -(viewProjectionTopAndBottomEdgeHits.Max - viewProjectionTopAndBottomEdgeHits.Min) / 2f,
                    requiredCameraPerpedicularDistanceFromProjectionPlane), new Color32(31, 119, 180, 255));

            foreach (var target in targetsRotatedToCameraIdentity)
            {
                float distanceFromProjectionPlane = projectionPlaneZ - target.z;
                float halfHorizontalProjectionVolumeCircumcircleDiameter = Mathf.Sin(Mathf.PI - ((Mathf.PI / 2f) + halfHorizontalFovRad)) / (distanceFromProjectionPlane);
                float projectionHalfHorizontalSpan = Mathf.Sin(halfHorizontalFovRad) / halfHorizontalProjectionVolumeCircumcircleDiameter;
                float halfVerticalProjectionVolumeCircumcircleDiameter = Mathf.Sin(Mathf.PI - ((Mathf.PI / 2f) + halfVerticalFovRad)) / (distanceFromProjectionPlane);
                float projectionHalfVerticalSpan = Mathf.Sin(halfVerticalFovRad) / halfVerticalProjectionVolumeCircumcircleDiameter;

                DebugDrawProjectionRay(target,
                    new Vector3(projectionHalfHorizontalSpan, 0f, distanceFromProjectionPlane),
                    new Color32(214, 39, 40, 255));
                DebugDrawProjectionRay(target,
                    new Vector3(-projectionHalfHorizontalSpan, 0f, distanceFromProjectionPlane),
                    new Color32(214, 39, 40, 255));
                DebugDrawProjectionRay(target,
                    new Vector3(0f, projectionHalfVerticalSpan, distanceFromProjectionPlane),
                    new Color32(214, 39, 40, 255));
                DebugDrawProjectionRay(target,
                    new Vector3(0f, -projectionHalfVerticalSpan, distanceFromProjectionPlane),
                    new Color32(214, 39, 40, 255));
            }
        }

        private void DebugDrawProjectionRay(Vector3 start, Vector3 direction, Color color)
        {
            Quaternion rotation = _debugProjection == DebugProjection.IDENTITY ? Quaternion.identity : m_GlobalCamera.transform.rotation;
            Debug.DrawRay(rotation * start, rotation * direction, color);
        }

    }
}