using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinesControl : MonoBehaviour
{
    public Camera m_MainCamera;
    public List<Transform> m_Targets;
    
    private List<LineRenderer> m_Lines;
    private List<LineRenderer> m_PerpendicularLines;

    // Start is called before the first frame update
    void Start()
    {
        //m_Targets = new List<Transform>();
        m_Lines = new List<LineRenderer>();
        m_PerpendicularLines = new List<LineRenderer>();

        //DrawLine(m_Targets[0].position, m_Targets[1].position);
        DrawLines(m_Targets);


    }

    // Update is called once per frame
    void Update()
    {
        UpdateLines(m_Targets, m_Lines, m_PerpendicularLines);
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

    void DrawLines(List<Transform> targets)
    {
        GameObject Lines = new GameObject("Lines");
        GameObject Plines = new GameObject("PLines");

        for (int i = 0; i < targets.Count; i++)
        {
            for (int j = i + 1; j < targets.Count; j++)
            {
                LineRenderer lr = DrawLine("Line" + (i + 1) + (j + 1), targets[i].position, targets[j].position);              
                lr.colorGradient = CreateGradient(targets[i].GetComponent<Renderer>().material.color, targets[j].GetComponent<Renderer>().material.color);
                lr.transform.parent = Lines.transform;
                m_Lines.Add(lr);

                LineRenderer plr = DrawLinePerpendicular("PLine" + (i + 1) + (j + 1), lr, -m_MainCamera.transform.forward);
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

    void UpdateLines(List<Transform> targets, List<LineRenderer> lines, List<LineRenderer> plines)
    {
        //Player Lines
        int count = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            for (int j = i + 1; j < targets.Count; j++)
            {
                //Updating Lines between players
                lines[count].SetPosition(0, targets[i].position);
                lines[count].SetPosition(1, (targets[i].position + targets[j].position) / 2); //Midpoint
                lines[count].SetPosition(2, targets[j].position);

                //Updating perpendicular lines
                Vector3 linevec = lines[count].GetPosition(2) - lines[count].GetPosition(0);
                Vector3 crossvec = Vector3.Cross(linevec, -m_MainCamera.transform.forward).normalized;

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
}
