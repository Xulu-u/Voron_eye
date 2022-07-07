using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinesControl : MonoBehaviour
{

    public List<Transform> m_Targets;
    
    private List<LineRenderer> m_Lines;

    // Start is called before the first frame update
    void Start()
    {
        //m_Targets = new List<Transform>();
        m_Lines = new List<LineRenderer>();

        //DrawLine(m_Targets[0].position, m_Targets[1].position);
        DrawLines(m_Targets);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateLines(m_Targets, m_Lines);
    }

    LineRenderer DrawLine(string name, Vector3 start, Vector3 end/*, Color color*//*, float duration = 0.2f*/)
    {
        GameObject myLine = new GameObject(name);
        myLine.transform.position = start;
        myLine.AddComponent<LineRenderer>();
        LineRenderer lr = myLine.GetComponent<LineRenderer>();
        //lr.material = new Material(Shader.Find("Particles/Alpha Blended Premultiply"));
        //lr.startColor(color);
        //lr.endColor(color);
        //lr.SetWidth(0.1f, 0.1f);
        lr.SetVertexCount(3); //The Players and the midpoint
        lr.SetPosition(0, start);
        lr.SetPosition(1, (start + end) / 2); //Midpoint
        lr.SetPosition(2, end);
        //GameObject.Destroy(myLine, duration);
        return lr;
    }

    void DrawLines(List<Transform> targets)
    {
        GameObject Lines = new GameObject("Lines");

        for (int i = 0; i < targets.Count; i++)
        {
            for (int j = i + 1; j < targets.Count; j++)
            {
                LineRenderer lr = DrawLine("Line" + (i+1) + (j+1), targets[i].position, targets[j].position);
                lr.transform.parent = Lines.transform;
                m_Lines.Add(lr);
            }
        }
    }

    void UpdateLines(List<Transform> targets, List<LineRenderer> lines)
    {
        //Player Lines
        int count = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            for (int j = i + 1; j < targets.Count; j++)
            {
                lines[count].SetPosition(0, targets[i].position);
                lines[count].SetPosition(1, (targets[i].position + targets[j].position) / 2); //Midpoint
                lines[count].SetPosition(2, targets[j].position);

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
