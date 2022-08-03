using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    private List<Vector3> m_ScreenTargets;
    private Vector3 m_AverageTargetPosition;
    
    //Lines
    private List<LineRenderer> m_Lines;
    private List<LineRenderer> m_PerpendicularLines;

    private void Awake()
    {
        _debugProjection = DebugProjection.ROTATED;
    }

    // Start is called before the first frame update
    void Start()
    {
        //m_Targets = new List<Transform>();
        m_Lines = new List<LineRenderer>();
        m_PerpendicularLines = new List<LineRenderer>();
        m_ScreenTargets = new List<Vector3>();

        //DrawLine(m_Targets[0].position, m_Targets[1].position);
        DrawLines(m_Targets);
        m_ScreenCameraDistance = m_MinScreenCameraDistance;
        m_GlobalCameraDistance = m_MinScreenCameraDistance;

        SetGlobalScreenTargets();
    }

    // Update is called once per frame
    void Update()
    {
        

      
    }

    private void LateUpdate()
    {
        FindAverageTargetPosition();
        CameraMovement();
        GetGlobalScreenTargets();

        UpdateLines(m_Targets, m_Lines, m_PerpendicularLines);
    }
    private void SetGlobalScreenTargets()
    {
        for (int i = 0; i < m_Targets.Count; i++)
        {
            // If the target isn't active, go on to the next one.
            if (!m_Targets[i].gameObject.activeSelf)
                continue;

            m_ScreenTargets.Add(m_GlobalCamera.WorldToScreenPoint(m_Targets[i].transform.position));
        }
    }
    private void GetGlobalScreenTargets()
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

    void DrawLines(List<GameObject> targets)
    {
        GameObject Lines = new GameObject("Lines");
        GameObject Plines = new GameObject("PLines");

        for (int i = 0; i < targets.Count; i++)
        {
            for (int j = i + 1; j < targets.Count; j++)
            {
                LineRenderer lr = DrawLine("Line" + (i + 1) + (j + 1), targets[i].transform.position, targets[j].transform.position);              
                lr.colorGradient = CreateGradient(targets[i].GetComponent<Renderer>().material.color, targets[j].GetComponent<Renderer>().material.color);
                lr.transform.parent = Lines.transform;
                m_Lines.Add(lr);

                LineRenderer plr = DrawLinePerpendicular("PLine" + (i + 1) + (j + 1), lr, -m_GlobalCamera.transform.forward);
                plr.colorGradient = CreateGradient(Color.black, Color.black);
                plr.transform.parent = Plines.transform;
                m_PerpendicularLines.Add(plr);
            }
        }
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
