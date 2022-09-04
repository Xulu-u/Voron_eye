using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;

namespace VE
{
    public class LinesControl : MonoBehaviour
    {
        private string LAYERNAME = "Voron-eye_Stencil_Layer_";
        private string LAYERNAME2 = "Screen_Camera";
        [SerializeField] private Camera m_GlobalCamera;
        [SerializeField] private Camera m_ScreenCamera;
        [SerializeField] private GameObject m_FrameOffset;

        public bool m_VoroneyeSetting = true;
        private bool m_VoroneyeActive = false;
        
        //Camera Settings
        Vector3 m_PreviousScreenCameraPosition;
        Vector3 m_PreviousGlobalCameraPosition;
        public float m_MinScreenCameraDistance = 1f;
        public float m_MaxScreenCameraDistance = 100f;
        private float m_ScreenCameraDistance;
        private float m_GlobalCameraDistance;
        private float m_ScreenDistanceFromGlobalCamera = 10f;
        private float m_ScreenDistanceFromScreenCamera;

        //Separator Lines
        public bool m_SeparatorLinesActive = true;
        private List<LineRenderer> m_SeparatorLines;
        public Color m_SeparatorLinesColor = Color.black;
        public float m_SeparatorLinesWidth = 2;
        private bool m_BorderLines = false;//yikes
        private bool m_DuplicatedLines = false;
        private float m_SeparatorLinesCheckingMargin = 0.1f;

        //Camera Multitarget
        public float Pitch;
        public float Yaw;
        public float Roll;
        public float PaddingLeft;
        public float PaddingRight;
        public float PaddingUp;
        public float PaddingDown;
        public float MoveSmoothTime = 0.19f;

        //public int m_LloydsRelaxationPasses = 0;

        private DebugProjection _debugProjection;
        enum DebugProjection { DISABLE, IDENTITY, ROTATED }
        enum ProjectionEdgeHits { TOP_BOTTOM, LEFT_RIGHT }

        //Targets
        [SerializeField] private List<GameObject> m_Targets;
        private List<GameObject> m_ActiveTargets;
        private List<Vector2> m_ScreenTargets;
        private Vector3 m_AverageTargetPosition;
        private int m_TargetCount;
        private int m_ActiveTargetCount;

        //Lines
        public bool m_ConnectingLinesActive = true;
        private List<LineRenderer> m_Lines;
        //private List<LineRenderer> m_PerpendicularLines;
        private GameObject Lines;
        private float m_LinesWidth = 1f;

        //Meshes
        private List<GameObject> m_Meshes;
        Vector2[] screenVertices;
        Vector2[] screenVertices2;

        //Screens(Quads)
        private List<GameObject> m_Quads;
        private float m_ScreenScaleMultiplicator = 1.5f; //Preventing some edge cases where the Voronoi Site would be bigger than the centered screen
        private float m_ScreenSmoothTime = 0.19f;

        //Materials
        private List<Material> m_Materials;
        [SerializeField] private Material m_QuadMaterial;

        //Render Textures
        private List<RenderTexture> m_RenderTextures;

        //Player Cameras
        private List<GameObject> m_PlayerCameras;

        //URP
        [SerializeField] private ForwardRendererData m_forwardRendererData = null;
        public List<RenderObjects> m_RenderObjects;

        private void Awake()
        {
            _debugProjection = DebugProjection.ROTATED;
        }

        // Start is called before the first frame update
        void Start()
        {
            //Initializing Lists
            m_ActiveTargets = new List<GameObject>();
            m_Lines = new List<LineRenderer>();
            //m_PerpendicularLines = new List<LineRenderer>();
            m_ScreenTargets = new List<Vector2>();
            m_Meshes = new List<GameObject>();
            m_Quads = new List<GameObject>();
            m_Materials = new List<Material>();
            m_RenderTextures = new List<RenderTexture>();
            m_PlayerCameras = new List<GameObject>();
            m_SeparatorLines = new List<LineRenderer>();
            //m_ScriptableRendererFeatures = new List<ScriptableRendererFeature>();

            Lines = new GameObject("Lines");

            //Setting Camera Distances
            m_ScreenCameraDistance = m_MinScreenCameraDistance;
            m_GlobalCameraDistance = m_MinScreenCameraDistance;

            //Moving the frame for the Screen Camera(bottom left)
            Vector3 frameOffset = new Vector3
            {
                x = -Screen.width / 2,
                y = -Screen.height / 2,
                z = -m_ScreenDistanceFromGlobalCamera
            };
            m_FrameOffset.transform.localPosition = frameOffset;

            //m_FrameOffset.transform.localPosition

            //Distance required to give a specified frustum height
            m_ScreenDistanceFromScreenCamera = Screen.height * 0.5f / Mathf.Tan(m_ScreenCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            new Layers().AddNewLayer(LAYERNAME2);

            //Setting Global Camera rendering order
            m_GlobalCamera.depth = 0;
            int cameracount = 1;

            //Screen Vertices Array
            screenVertices = new Vector2[4];//this one for the voronoi diagram class
            screenVertices[2] = new Vector2(0, 0); //the order of this almost makes me have a heart attack
            screenVertices[3] = new Vector2(0, Screen.height/*m_GlobalCamera.pixelHeight*/);
            screenVertices[1] = new Vector2(Screen.width/*m_GlobalCamera.pixelWidth*/, 0);
            screenVertices[0] = new Vector2(Screen.width/*m_GlobalCamera.pixelWidth*/, Screen.height/*m_GlobalCamera.pixelHeight*/);

            screenVertices2 = new Vector2[4];//this one for my own calculations (2 players case)
            screenVertices2[0] = new Vector2(0, 0);
            screenVertices2[1] = new Vector2(0, Screen.height);
            screenVertices2[2] = new Vector2(Screen.width, 0);
            screenVertices2[3] = new Vector2(Screen.width, Screen.height);

            m_TargetCount = m_Targets.Count;

            //Check if inactive targets
            CheckActiveTargets();
            //Creation and setting of everything for each active target
            SetScreenTargets();
            
            //Creation and setting of everything needed for each target
            for (int i = 0; i < m_Targets.Count; i++)
            {             
                //Screen position
                //m_ScreenTargets.Add(m_GlobalCamera.WorldToScreenPoint(m_Targets[i].transform.position));

                //Create new Layer for Stencil
                new Layers().AddNewLayer(LAYERNAME + (i + 1));
                //TODO: CHECK IF THERE'S CreateLayer() RETURNS TRUE AND IF NOT, BREAK
                
                //Get the new layer as variable
                LayerMask layer = LayerMask.NameToLayer(LAYERNAME + (i + 1)); //Gets the layer in the layermask (basically the index number)
                
                //Forward Renderer Settings
                m_forwardRendererData.opaqueLayerMask &= ~(1 << layer); //This unchecks the specified layer (Search for: "Remove a Layer from a Layermask") with some bitshift shenanigans 
                m_forwardRendererData.transparentLayerMask &= ~(1 << layer);

                //RenderObject Settings 
                m_RenderObjects[i].settings.filterSettings.LayerMask = (1 << layer);
                //The Render objects had to be manually created and pre-setted beacuse there's no API for them as of the creation of the script.
                //Thankfully I could set the synamically created layer of the filter settings.

                //Create Custom Meshes for the Voronoi Diagram
                GameObject meshgo = new GameObject("Mesh" + (i + 1));
                meshgo.transform.parent = m_FrameOffset.transform;
                meshgo.transform.position = m_FrameOffset.transform.position;
                meshgo.transform.rotation = m_FrameOffset.transform.rotation;
                meshgo.AddComponent<MeshFilter>();
                meshgo.AddComponent<MeshRenderer>();
                meshgo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                meshgo.layer = LayerMask.NameToLayer(LAYERNAME2);

                //Create Quads for the Render Texture (Voronoi Screens)
                GameObject meshgo2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                meshgo2.transform.parent = meshgo.transform;
                meshgo2.transform.rotation = m_FrameOffset.transform.rotation;
                meshgo2.layer = layer;
                meshgo2.GetComponent<Renderer>().material = m_QuadMaterial;
                meshgo2.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                Vector3 scale = meshgo2.transform.localScale;
                scale.x = Screen.width * m_ScreenScaleMultiplicator;
                scale.y = Screen.height * m_ScreenScaleMultiplicator;

                // Create Materials
                Renderer rend = meshgo.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("Custom/StencilShader"));;
                rend.material.SetInt("_StencilID", (i + 1));//Set corresponding Stencil ID

                //Create Render Textures
                RenderTexture rt;
                rt = new RenderTexture(Screen.width * 2, Screen.height * 2, 16, RenderTextureFormat.ARGB32);
                rt.Create();

                //Create Player Cameras
                GameObject camerago = new GameObject("PlayerCamera" + (i + 1));
                camerago.transform.position = m_Targets[i].transform.position;
                camerago.transform.parent = m_Targets[i].transform;
                camerago.AddComponent<Camera>();

                //Player Cameras Settings
                Camera camera = camerago.GetComponent<Camera>();
                camera.depth = i + 1;
                camera.targetDisplay = 8;//doesnt mess with global and screen
                cameracount++;

                //Set Meshes Materials Render Textures and Camera Render Target
                camerago.GetComponent<Camera>().targetTexture = rt;
                rend.material.mainTexture = rt;
                //meshgo.GetComponent<Renderer>().material = m_Targets[i].GetComponent<Renderer>().material;
                meshgo.GetComponent<Renderer>().material = rend.material;

                meshgo2.GetComponent<Renderer>().material.mainTexture = rt;
                meshgo2.transform.localScale = scale;

                //Add to Lists
                m_Meshes.Add(meshgo);
                m_Quads.Add(meshgo2);
                m_PlayerCameras.Add(camerago);
                m_RenderTextures.Add(rt);
                m_Materials.Add(rend.material);

                m_ScreenCamera.cullingMask |= 1 << layer;
            }
            //Checking the screencamera layer in the ScreenCamera cullin mask
            m_ScreenCamera.cullingMask |= 1 << LayerMask.NameToLayer(LAYERNAME2);
            
            //Last in the stack
            m_ScreenCamera.depth = cameracount;

            //Go through all the player cameras to uncheck the voron-eye layers and the screencamera layer from their culling layermask
            for (int i = 0; i < m_PlayerCameras.Count; i++)
            {
                Camera camera = m_PlayerCameras[i].GetComponent<Camera>();
                //Uncheck all the voron-eye layers
                for (int j = 0; j < m_Targets.Count; j++)
                {
                    camera.cullingMask &= ~(1 << LayerMask.NameToLayer(LAYERNAME + (j + 1)));
                }
                //Uncheck the screencamera layer
                camera.cullingMask &= ~(1 << LayerMask.NameToLayer(LAYERNAME2));
            }
        }
        
        private void LateUpdate()
        {
            CheckTargets();
            CheckActiveTargets();
            //Recalculate Targets
            //Recalculate onviewport changed
            CameraMovement();

            UpdateGlobalScreenTargets();

            if (m_ConnectingLinesActive)
            {
                UpdateConnectingLines();
            }

            VoronoiDiagram();

            if (!m_ConnectingLinesActive)
            {
                if (m_Lines.Count > 0)
                {
                    for (int i = 0; i < m_Lines.Count; i++)
                    {
                        Destroy(m_Lines[i].gameObject);
                    }
                }
                m_Lines.Clear();
            }

            if (!m_SeparatorLinesActive)
            {
                if (m_SeparatorLines.Count > 0)
                {
                    for (int i = 0; i < m_SeparatorLines.Count; i++)
                    {
                        Destroy(m_SeparatorLines[i].gameObject);
                    }
                }
                m_SeparatorLines.Clear();
            }
            
        }
        private void CheckTargets()
        {
            if (m_Targets.Count != m_TargetCount)
            {
                //Clearing Lists and stuff
                m_ActiveTargets.Clear();
                m_ScreenTargets.Clear();
                //Has custom amount
                for (int i = 0; i < m_Lines.Count; i++)
                {
                    Destroy(m_Lines[i].gameObject);
                }
                m_Lines.Clear();

                //Have the same amount
                for (int i = 0; i < m_TargetCount; i++)
                {
                    Destroy(m_Meshes[i].gameObject);
                    Destroy(m_Quads[i].gameObject);
                    Destroy(m_Materials[i]);
                    Destroy(m_RenderTextures[i]);
                    Destroy(m_PlayerCameras[i]);
                }
                m_Meshes.Clear();
                m_Quads.Clear();
                m_Materials.Clear();                
                m_RenderTextures.Clear();                
                m_PlayerCameras.Clear();

                //Has custom amount
                for (int i = 0; i < m_SeparatorLines.Count; i++)
                {
                    Destroy(m_SeparatorLines[i].gameObject);
                }
                m_SeparatorLines.Clear();

                //From now very similar to what we do at the Start() function
                
                //Setting Global Camera rendering order
                m_GlobalCamera.depth = 0;
                int cameracount = 1;

                //Check if inactive targets
                CheckActiveTargets();

                //Creation and setting of everything for each active target
                SetScreenTargets();
                SetConnectingLines();

                //Creation and setting of everything needed for each target
                for (int i = 0; i < m_Targets.Count; i++)
                {
                    //Create new Layer for Stencil
                    new Layers().AddNewLayer(LAYERNAME + (i + 1));
                    //TODO: CHECK IF THERE'S CreateLayer() RETURNS TRUE AND IF NOT, BREAK
                    //Maybe not needed bc render target object has less Stencil IDs so I may limit with a max targets?

                    //Get the new layer as variable
                    LayerMask layer = LayerMask.NameToLayer(LAYERNAME + (i + 1)); //Gets the layer in the layermask (basically the index number)

                    //Forward Renderer Settings
                    m_forwardRendererData.opaqueLayerMask &= ~(1 << layer); //This unchecks the specified layer (Search for: "Remove a Layer from a Layermask") with some bitshift shenanigans 
                    m_forwardRendererData.transparentLayerMask &= ~(1 << layer);

                    //RenderObject Settings 
                    m_RenderObjects[i].settings.filterSettings.LayerMask = (1 << layer);
                    //The Render objects had to be manually created and pre-setted beacuse there's no API for them as of the creation of the script.
                    //Thankfully I could set the synamically created layer of the filter settings.

                    //Create Custom Meshes for the Voronoi Diagram
                    GameObject meshgo = new GameObject("Mesh" + (i + 1));
                    meshgo.transform.parent = m_FrameOffset.transform;
                    meshgo.transform.position = m_FrameOffset.transform.position;
                    meshgo.transform.rotation = m_FrameOffset.transform.rotation;
                    meshgo.AddComponent<MeshFilter>();
                    meshgo.AddComponent<MeshRenderer>();
                    meshgo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    meshgo.layer = LayerMask.NameToLayer(LAYERNAME2);

                    //Create Quads for the Render Texture (Voronoi Screens)
                    GameObject meshgo2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    meshgo2.transform.parent = meshgo.transform;
                    meshgo2.transform.rotation = m_FrameOffset.transform.rotation;
                    meshgo2.layer = layer;
                    meshgo2.GetComponent<Renderer>().material = m_QuadMaterial;
                    meshgo2.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                    Vector3 scale = meshgo2.transform.localScale;
                    scale.x = Screen.width * m_ScreenScaleMultiplicator;
                    scale.y = Screen.height * m_ScreenScaleMultiplicator;

                    // Create Materials
                    Renderer rend = meshgo.GetComponent<Renderer>();
                    rend.material = new Material(Shader.Find("Custom/StencilShader")); ;
                    rend.material.SetInt("_StencilID", (i + 1));//Set corresponding Stencil ID

                    //Create Render Textures
                    RenderTexture rt;
                    rt = new RenderTexture(Screen.width * 2, Screen.height * 2, 16, RenderTextureFormat.ARGB32);
                    rt.Create();

                    //Create Player Cameras
                    GameObject camerago = new GameObject("PlayerCamera" + (i + 1));
                    camerago.transform.position = m_Targets[i].transform.position;
                    camerago.transform.parent = m_Targets[i].transform;
                    camerago.AddComponent<Camera>();

                    //Player Cameras Settings
                    Camera camera = camerago.GetComponent<Camera>();
                    camera.depth = i + 1;
                    camera.targetDisplay = 8;//doesnt mess with global and screen
                    cameracount++;

                    //Set Meshes Materials Render Textures and Camera Render Target
                    camerago.GetComponent<Camera>().targetTexture = rt;
                    rend.material.mainTexture = rt;
                    //meshgo.GetComponent<Renderer>().material = m_Targets[i].GetComponent<Renderer>().material;
                    meshgo.GetComponent<Renderer>().material = rend.material;

                    meshgo2.GetComponent<Renderer>().material.mainTexture = rt;
                    meshgo2.transform.localScale = scale;

                    //Add to Lists
                    m_Meshes.Add(meshgo);
                    m_Quads.Add(meshgo2);
                    m_PlayerCameras.Add(camerago);
                    m_RenderTextures.Add(rt);
                    m_Materials.Add(rend.material);

                    m_ScreenCamera.cullingMask |= 1 << layer;
                }
                //Checking the screencamera layer in the ScreenCamera cullin mask
                m_ScreenCamera.cullingMask |= 1 << LayerMask.NameToLayer(LAYERNAME2);

                //Last in the stack
                m_ScreenCamera.depth = cameracount;

                //Go through all the player cameras to uncheck the voron-eye layers and the screencamera layer from their culling layermask
                for (int i = 0; i < m_PlayerCameras.Count; i++)
                {
                    Camera camera = m_PlayerCameras[i].GetComponent<Camera>();
                    //Uncheck all the voron-eye layers
                    for (int j = 0; j < m_Targets.Count; j++)
                    {
                        camera.cullingMask &= ~(1 << LayerMask.NameToLayer(LAYERNAME + (j + 1)));
                    }
                    //Uncheck the screencamera layer
                    camera.cullingMask &= ~(1 << LayerMask.NameToLayer(LAYERNAME2));
                }

                m_TargetCount = m_Targets.Count;
            }
        }
        private void CheckActiveTargets()
        {
            m_ActiveTargets.Clear();
            //Check everywhere that targets inactive
            for (int i = 0; i < m_Targets.Count; i++)
            {
                if (m_Targets[i].gameObject.activeSelf)
                {
                    m_ActiveTargets.Add(m_Targets[i]);
                }
            }
            if (m_ActiveTargetCount != m_ActiveTargets.Count)
            {
                if (m_ConnectingLinesActive)
                {
                    SetConnectingLines();
                }

                m_ActiveTargetCount = m_ActiveTargets.Count;
            }
        }
        private void SetScreenTargets()
        {
            for (int i = 0; i < m_ActiveTargets.Count; i++)
            { 
                //Unity implictly converts vec3 to vec2 and viceversa, it discards z (which we don't need)
                m_ScreenTargets.Add(m_GlobalCamera.WorldToScreenPoint(m_ActiveTargets[i].transform.position));
            }
        }
        private void SetConnectingLines()
        {
            if (m_Lines.Count > 0)
            {
                for (int i = 0; i < m_Lines.Count; i++)
                {
                    Destroy(m_Lines[i].gameObject);
                }
            }
            m_Lines.Clear();

            for (int i = 0; i < m_ActiveTargets.Count; i++)
            {
                //Create the colored Lines between the active targets
                for (int j = i + 1; j < m_ActiveTargets.Count; j++)
                {
                    LineRenderer lr = DrawLine("Line" + (i + 1) + (j + 1), m_ActiveTargets[i].transform.position, m_ActiveTargets[j].transform.position);
                    lr.colorGradient = CreateGradient(m_ActiveTargets[i].GetComponent<Renderer>().material.color, m_ActiveTargets[j].GetComponent<Renderer>().material.color);
                    lr.transform.parent = Lines.transform;
                    lr.widthMultiplier = m_LinesWidth;
                    m_Lines.Add(lr);
                }
            }
        }
        private void UpdateGlobalScreenTargets()
        {
            for (int i = 0; i < m_ActiveTargets.Count; i++)
            {
                m_ScreenTargets[i] = m_GlobalCamera.WorldToScreenPoint(m_ActiveTargets[i].transform.position);
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
        private Vector2 FindAveragePosition2D(List<Vector2> targets)
        {
            Vector2 averagePos = new Vector2();
            int numTargets = 0;

            // If the target is only one just send its position.
            if (targets.Count == 1)
            {
                m_AverageTargetPosition = targets[0];
                return m_AverageTargetPosition;
            }

            // Go through all the targets and add their positions together.
            for (int i = 0; i < targets.Count; i++)
            {
                // Add to the average and increment the number of targets in the average.
                averagePos += targets[i];
                numTargets++;
            }

            // If there are targets divide the sum of the positions by the number of them to find the average.
            if (numTargets > 0)
                averagePos /= numTargets;

            // The desired position is the average position;
            m_AverageTargetPosition = averagePos;

            return m_AverageTargetPosition;
        }
        LineRenderer DrawLine(string name, Vector3 start, Vector3 end/*, Color color*/)
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
        LineRenderer DrawSeparatorLine(string name, Vector3 start, Vector3 end, Color color, float width)
        {
            GameObject myLine = new GameObject(name);
            myLine.transform.parent = m_FrameOffset.transform; 
            myLine.transform.position = m_FrameOffset.transform.position;
            myLine.transform.rotation = m_FrameOffset.transform.rotation;
            myLine.layer = LayerMask.NameToLayer(LAYERNAME2);
            myLine.AddComponent<LineRenderer>();

            LineRenderer lr = myLine.GetComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            lr.colorGradient = CreateGradient(color, color);
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.View;

            //lr.SetWidth(0.1f, 0.1f);
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(start.x, start.y, start.z - 1));
            lr.SetPosition(1, new Vector3(end.x, end.y, end.z - 1));
            lr.widthMultiplier = width;
            
            return lr;
        }
        void UpdateSeparatorLine(List<LineRenderer> lines, int i, Vector3 start, Vector3 end, Color color, float width)
        {
            lines[i].SetPosition(0, new Vector3(start.x, start.y, start.z - 1));
            lines[i].SetPosition(1, new Vector3(end.x, end.y, end.z - 1));
            lines[i].colorGradient = CreateGradient(color, color);
            lines[i].widthMultiplier = width;
        }
        void UpdateConnectingLines()
        {
            if (m_Lines.Count == 0)
            {
                SetConnectingLines();
            }
            else
            { //Player Lines
                int count = 0;
                for (int i = 0; i < m_ActiveTargets.Count; i++)
                {
                    for (int j = i + 1; j < m_ActiveTargets.Count; j++)
                    {
                        //Updating Lines between players
                        m_Lines[count].SetPosition(0, m_ActiveTargets[i].transform.position);
                        m_Lines[count].SetPosition(1, (m_ActiveTargets[i].transform.position + m_ActiveTargets[j].transform.position) / 2); //Midpoint
                        m_Lines[count].SetPosition(2, m_ActiveTargets[j].transform.position);

                        m_Lines[count].colorGradient = CreateGradient(m_ActiveTargets[i].GetComponent<Renderer>().material.color, m_ActiveTargets[j].GetComponent<Renderer>().material.color);
                        //m_Lines[count].widthMultiplier = width;
                        count++;
                    }
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
        public static bool Check(float p0_x, float p0_y, float p1_x, float p1_y, float p2_x, float p2_y, float p3_x, float p3_y, ref float i_x, ref float i_y)
        {
            //Lines intersecting check (a bit modified) from https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect

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
                return true;
            }
            return false; // No collision      
        }
        void CameraMovement()
        {
            if (m_ActiveTargets.Count == 0)
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
            
            //Vector3 velocity = Vector3.zero;
            m_GlobalCamera.transform.position = targetPositionAndRotation.Position;//Vector3.SmoothDamp(m_GlobalCamera.transform.position, targetPositionAndRotation.Position, ref velocity, MoveSmoothTime);
            m_GlobalCamera.transform.rotation = targetPositionAndRotation.Rotation;

            //Checking that the screen camera distance from the targets is not higher than the maximum we set
            if (m_GlobalCameraDistance > m_MaxScreenCameraDistance && m_VoroneyeSetting)
            {
                m_VoroneyeActive = true;

                if (!m_ScreenCamera.enabled)
                {
                    m_ScreenCamera.enabled = true;
                    m_GlobalCamera.enabled = false;
                    //m_ScreenCamera = Camera.main;
                }
                //Substracting the difference to maintain the maximum distance set
                //var distanceDiff = m_MaxScreenCameraDistance - m_GlobalCameraDistance;
                var distanceDiff = m_ScreenDistanceFromGlobalCamera + m_ScreenDistanceFromScreenCamera;
                m_ScreenCamera.transform.localPosition = new Vector3(0, 0, -distanceDiff);
            }
            else
            {
                if (!m_GlobalCamera.enabled | m_ScreenCamera.enabled)
                {
                    m_ScreenCamera.enabled = false;
                    m_GlobalCamera.enabled = true;
                    //m_GlobalCamera = Camera.main;
                }
            }

            //Player cameras
            for (int i = 0; i < m_ActiveTargets.Count; i++)
            {
                m_PlayerCameras[i].transform.rotation = m_GlobalCamera.transform.rotation;  
                m_PlayerCameras[i].transform.position = m_ActiveTargets[i].transform.position; 
                m_PlayerCameras[i].transform.Translate(0, 0, -m_MaxScreenCameraDistance * m_ScreenScaleMultiplicator); //zoom scaled to the screen
            }
        }
        void VoronoiDiagram()
        {
            //Special case for 1 target
            if (m_ScreenTargets.Count == 1)
            {
                //Destroy separator lines and clear list
                if (m_SeparatorLines.Count > 0)
                {
                    for (int i = 0; i < m_SeparatorLines.Count; i++)
                    {
                        Destroy(m_SeparatorLines[i].gameObject);
                    }
                    m_SeparatorLines.Clear();
                }
            }
            //Special case for 2 targets (this mess is mine lol)
            else if (m_ScreenTargets.Count == 2)
            {
                if (m_SeparatorLinesActive)
                {
                    //Check separator lines list and handle it
                    if (m_SeparatorLines.Count != 1)
                    {
                        if (m_SeparatorLines.Count > 1)
                        {
                            for (int i = 1; i < m_SeparatorLines.Count; i++)
                            {
                                GameObject.Destroy(m_SeparatorLines[i].gameObject);
                                m_SeparatorLines.RemoveAt(i);
                            }
                        }
                        if (m_SeparatorLines.Count < 1)
                        {
                            m_SeparatorLines.Clear(); //just in case
                            m_SeparatorLines.Add(DrawSeparatorLine("Separator Line 1", Vector3.zero, Vector3.zero, m_SeparatorLinesColor, m_SeparatorLinesWidth));
                        }
                    }
                }

                //Calculate vector between the two targets
                Vector2 vec = new Vector2
                {
                    x = m_ScreenTargets[1].x - m_ScreenTargets[0].x,
                    y = m_ScreenTargets[1].y - m_ScreenTargets[0].y,
                }.normalized; //and normalize it so it's the direction

                //Calculate perpendicular direction (now we have the direction of the line that will separate them) 
                Vector2 pvec = Vector2.Perpendicular(vec);

                //Calculate midpoint between targets
                Vector2 mp = new Vector2
                {
                    x = (m_ScreenTargets[0].x + m_ScreenTargets[1].x) / 2,
                    y = (m_ScreenTargets[0].y + m_ScreenTargets[1].y) / 2,
                };

                //Calculate point at a distance from the midpoint with the pvec direction
                int linedist = Mathf.Max(Screen.height, Screen.width);
                Vector2 mp_1 = new Vector2
                {
                    x = mp.x + (pvec.x * linedist),//We will be checking where the line intersects with the screen borders so this way it will always be large enough
                    y = mp.y + (pvec.y * linedist),
                };

                //Calculate point at a distance from the midpoint with the -pvec direction opposite to the previous
                Vector2 mp_2 = new Vector2
                {
                    x = mp.x + (-pvec.x * linedist),
                    y = mp.y + (-pvec.y * linedist),
                };

                var clipped = new List<Vector2>();                          
                bool intersectsv;
                bool intersectsh;
                bool idc;
                Vector2 mp_3 = Vector2.zero;                                
                Vector2 mp_4 = Vector2.zero;
                //screenVertices2[4] is arranged like this
                //  1-----3
                //  |     |
                //  0-----2
                //Checking with the borders of the screen (Vertical left)
                intersectsv = Check(mp_1.x, mp_1.y, mp_2.x, mp_2.y, screenVertices2[0].x, screenVertices2[0].y, screenVertices2[1].x, screenVertices2[1].y, ref mp_3.x, ref mp_3.y);
                if (intersectsv)
                {
                    //now we know that the line is intersecting horizontally (kind of)
                    //  1-----3
                    //  |-----|
                    //  0-----2
                    idc = Check(mp_1.x, mp_1.y, mp_2.x, mp_2.y, screenVertices2[2].x, screenVertices2[2].y, screenVertices2[3].x, screenVertices2[3].y, ref mp_4.x, ref mp_4.y);

                    //Checking which player is on top
                    if (Screen.height - m_ScreenTargets[0].y < Screen.height - m_ScreenTargets[1].y)
                    {
                        //target 1 is on top
                        clipped.Clear();

                        clipped.Add(mp_3);
                        clipped.Add(screenVertices2[1]);
                        clipped.Add(screenVertices2[3]);
                        clipped.Add(mp_4);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[0].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[0].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity = Vector3.zero;
                        m_Quads[0].transform.localPosition = Vector3.SmoothDamp(m_Quads[0].transform.localPosition, targetPos, ref velocity, m_ScreenSmoothTime);

                        //target 2 bottom 
                        clipped.Clear();

                        clipped.Add(screenVertices2[0]);
                        clipped.Add(mp_3);
                        clipped.Add(mp_4);
                        clipped.Add(screenVertices2[2]);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[1].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[1].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos2 = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity2 = Vector3.zero;
                        m_Quads[1].transform.localPosition = Vector3.SmoothDamp(m_Quads[1].transform.localPosition, targetPos2, ref velocity2, m_ScreenSmoothTime);
                    }
                    else
                    {
                        //target 2 is on top
                        clipped.Clear();

                        clipped.Add(mp_3);
                        clipped.Add(screenVertices2[1]);
                        clipped.Add(screenVertices2[3]);
                        clipped.Add(mp_4);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[1].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[1].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity = Vector3.zero;
                        m_Quads[1].transform.localPosition = Vector3.SmoothDamp(m_Quads[1].transform.localPosition, targetPos, ref velocity, m_ScreenSmoothTime);

                        //target 1 bottom 
                        clipped.Clear();

                        clipped.Add(screenVertices2[0]);
                        clipped.Add(mp_3);
                        clipped.Add(mp_4);
                        clipped.Add(screenVertices2[2]);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[0].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[0].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos2 = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity2 = Vector3.zero;
                        m_Quads[0].transform.localPosition = Vector3.SmoothDamp(m_Quads[0].transform.localPosition, targetPos2, ref velocity2, m_ScreenSmoothTime);
                    }
                    //Update separating line
                    if (m_SeparatorLinesActive)
                    {
                        UpdateSeparatorLine(m_SeparatorLines, 0, mp_3, mp_4, m_SeparatorLinesColor, m_SeparatorLinesWidth);
                    }
                }

                //Checking with the borders of the screen (Horizontal left)
                intersectsh = Check(mp_1.x, mp_1.y, mp_2.x, mp_2.y, screenVertices2[0].x, screenVertices2[0].y, screenVertices2[2].x, screenVertices2[2].y, ref mp_3.x, ref mp_3.y);
                if (intersectsh)
                {
                    //now we know that the line is intersecting vertically (kind of)
                    //  1-----3
                    //  |  |  |
                    //  0-----2
                    idc = Check(mp_1.x, mp_1.y, mp_2.x, mp_2.y, screenVertices2[1].x, screenVertices2[1].y, screenVertices2[3].x, screenVertices2[3].y, ref mp_4.x, ref mp_4.y);

                    //Checking which player is on the right
                    if (Screen.width - m_ScreenTargets[0].y > Screen.width - m_ScreenTargets[1].y)
                    {
                        //target 1 is on the right
                        clipped.Clear();

                        clipped.Add(mp_3);
                        clipped.Add(mp_4);
                        clipped.Add(screenVertices2[3]);
                        clipped.Add(screenVertices2[2]);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[0].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[0].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity = Vector3.zero;
                        m_Quads[0].transform.localPosition = Vector3.SmoothDamp(m_Quads[0].transform.localPosition, targetPos, ref velocity, m_ScreenSmoothTime);

                        //target 2 left
                        clipped.Clear();

                        clipped.Add(screenVertices2[0]);
                        clipped.Add(screenVertices2[1]);
                        clipped.Add(mp_4);
                        clipped.Add(mp_3);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[1].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[1].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos2 = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity2 = Vector3.zero;
                        m_Quads[1].transform.localPosition = Vector3.SmoothDamp(m_Quads[1].transform.localPosition, targetPos2, ref velocity2, m_ScreenSmoothTime);
                    }
                    else
                    {
                        //target 2 is on top
                        clipped.Clear();

                        clipped.Add(mp_3);
                        clipped.Add(mp_4);
                        clipped.Add(screenVertices2[3]);
                        clipped.Add(screenVertices2[2]);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[1].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[1].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity = Vector3.zero;
                        m_Quads[1].transform.localPosition = Vector3.SmoothDamp(m_Quads[1].transform.localPosition, targetPos, ref velocity, m_ScreenSmoothTime);

                        //target 1 bottom 
                        clipped.Clear();

                        clipped.Add(screenVertices2[0]);
                        clipped.Add(screenVertices2[1]);
                        clipped.Add(mp_4);
                        clipped.Add(mp_3);

                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[0].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[0].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos2 = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity2 = Vector3.zero;
                        m_Quads[0].transform.localPosition = Vector3.SmoothDamp(m_Quads[0].transform.localPosition, targetPos2, ref velocity2, m_ScreenSmoothTime);
                    }
                    //Update separating line
                    UpdateSeparatorLine(m_SeparatorLines, 0, mp_3, mp_4, m_SeparatorLinesColor, m_SeparatorLinesWidth);
                }
            }
            
            //3 targets is handled by the voronoi calculator
            else if (m_ScreenTargets.Count > 2)
            {
                //Variables for the Voronoi Classes
                var calc = new VoronoiCalculator();
                var clip = new VoronoiClipper();

                //Set the screen targets as sites for the voronoi diagram (kind of redundant but more clean? idk)
                var sites = new Vector2[m_ScreenTargets.Count];

                for (int i = 0; i < sites.Length; i++)
                {
                    sites[i] = m_ScreenTargets[i];
                }

                //Calculate the voronoi diagram with the given sites (2D points)
                var diagram = calc.CalculateDiagram(sites);

                var clipped = new List<Vector2>();

                //LLoyd's relaxation?

                int slcounter = 0;
                //Iterate the number of sites
                for (int i = 0; i < sites.Length; i++)
                {
                    //Clip the Voronoi diagram with a given rectangle (the screen in this case)
                    clip.ClipSite(diagram, screenVertices, i, ref clipped);

                    //Create a mesh for each clipped site
                    if (clipped.Count > 0)
                    {
                        //Clears current mesh and replaces it with new clipped voronoi diagram site
                        m_Meshes[i].GetComponent<MeshFilter>().mesh.Clear();
                        m_Meshes[i].GetComponent<MeshFilter>().mesh = MeshPolygonFromPolygon(clipped);

                        //Move screen quads to the centroid of the resulting convex polygon
                        Vector3 targetPos = new Vector3(FindAveragePosition2D(clipped).x, FindAveragePosition2D(clipped).y, 0f);
                        Vector3 velocity = Vector3.zero;
                        m_Quads[i].transform.localPosition = Vector3.SmoothDamp(m_Quads[i].transform.localPosition, targetPos, ref velocity, m_ScreenSmoothTime);

                        //Separator Lines handling
                        if (m_SeparatorLinesActive)
                        {
                            bool skippedfirst = false;
                            for (int j = 0; j < clipped.Count; j++)
                            {
                                //Checking that the vertex is not a screen corner (with some margin)
                                if (!m_BorderLines
                                    && (m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[0].x)
                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[0].y)
                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[1].x)
                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[1].y)
                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[2].x)
                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[2].y)
                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[3].x)
                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[3].y)
                                    //|| clipped[j] == screenVertices[0]
                                    //|| clipped[j] == screenVertices[1]
                                    //|| clipped[j] == screenVertices[2]
                                    //|| clipped[j] == screenVertices[3]
                                    ))
                                {
                                    if (j == 0) skippedfirst = true;
                                    continue;
                                }

                                else
                                {
                                    int j1 = j + 1;
                                    //Checking that the next vertex is not out of the clipped list
                                    if (j1 < clipped.Count)
                                    {
                                        if (!m_BorderLines)
                                        {
                                            //Checking that the next vertex is not a screen corner either
                                            if (m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[0].x)
                                                && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[0].y)
                                                || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[1].x)
                                                && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[1].y)
                                                || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[2].x)
                                                && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[2].y)
                                                || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[3].x)
                                                && m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[3].y)
                                                //|| clipped[j1] == screenVertices[0]
                                                //|| clipped[j1] == screenVertices[1]
                                                //|| clipped[j1] == screenVertices[2]
                                                //|| clipped[j1] == screenVertices[3]
                                                )
                                            {
                                                continue;
                                            }
                                            //Skip lines that are inside the border (but don't have corner vertex)
                                            //Get the direction
                                            Vector2 dirvec = new Vector2(clipped[j1].x - clipped[j].x, clipped[j1].y - clipped[j].y).normalized;
                                            //Check border directions both ways
                                            if (dirvec == Vector2.up
                                                || dirvec == Vector2.down
                                                || dirvec == Vector2.left
                                                || dirvec == Vector2.right)
                                            {
                                                //Check if any point is inside any border line
                                                if (m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[0].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[1].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[2].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[3].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[0].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[1].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[2].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[3].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[0].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[1].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[2].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].x - screenVertices[3].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[0].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[1].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[2].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j1].y - screenVertices[3].y)
                                                    //|| clipped[j].x == screenVertices[0].x 
                                                    //|| clipped[j].x == screenVertices[1].x 
                                                    //|| clipped[j].x == screenVertices[2].x 
                                                    //|| clipped[j].x == screenVertices[3].x
                                                    //|| clipped[j].y == screenVertices[0].y 
                                                    //|| clipped[j].y == screenVertices[1].y 
                                                    //|| clipped[j].y == screenVertices[2].y 
                                                    //|| clipped[j].y == screenVertices[3].y
                                                    //|| clipped[j1].x == screenVertices[0].x 
                                                    //|| clipped[j1].x == screenVertices[1].x 
                                                    //|| clipped[j1].x == screenVertices[2].x 
                                                    //|| clipped[j1].x == screenVertices[3].x
                                                    //|| clipped[j1].y == screenVertices[0].y 
                                                    //|| clipped[j1].y == screenVertices[1].y 
                                                    //|| clipped[j1].y == screenVertices[2].y 
                                                    //|| clipped[j1].y == screenVertices[3].y
                                                    )
                                                {
                                                    continue;
                                                }
                                            }
                                        }

                                        if (!m_DuplicatedLines)
                                        {
                                            //Skip duplicate lines that are equal (same vertices) or reversed (swapped vertices)
                                            bool isduplicate = false;
                                            for (int k = 0; k < m_SeparatorLines.Count; k++)
                                            {
                                                //Check equal
                                                if (m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).x - clipped[j].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).y - clipped[j].y)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).x - clipped[j1].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).y - clipped[j1].y))
                                                {
                                                    isduplicate = true;
                                                }
                                                //Check reversed
                                                else if (m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).x - clipped[j1].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).y - clipped[j1].y)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).x - clipped[j].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).y - clipped[j].y))
                                                {
                                                    isduplicate = true;
                                                }
                                            }

                                            if (isduplicate)
                                            {
                                                continue;
                                            }
                                        }

                                        //Adding counter to check if there are enough lines in the list
                                        slcounter++;

                                        if (slcounter > m_SeparatorLines.Count)
                                        {
                                            m_SeparatorLines.Add(DrawSeparatorLine("Separator Line " + (slcounter - 1), clipped[j], clipped[j1], m_SeparatorLinesColor, m_SeparatorLinesWidth));
                                        }
                                        else
                                        {
                                            UpdateSeparatorLine(m_SeparatorLines, slcounter - 1, clipped[j], clipped[j1], m_SeparatorLinesColor, m_SeparatorLinesWidth);
                                        }
                                    }
                                    else
                                    {
                                        if (!m_BorderLines)
                                        {
                                            //Checking if we skipped the first vertex bc it was a corner vertex
                                            if (skippedfirst)
                                            {
                                                continue;
                                            }

                                            //Skip lines that are inside the border (but don't have corner vertex)
                                            //Get the direction
                                            Vector2 dirvec = new Vector2(clipped[0].x - clipped[j].x, clipped[0].y - clipped[j].y).normalized;

                                            //Check border directions both ways
                                            if (dirvec == Vector2.up || dirvec == Vector2.down || dirvec == Vector2.left || dirvec == Vector2.right)
                                            {
                                                //Check if any point is inside any border line
                                                if (m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[0].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[1].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[2].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].x - screenVertices[3].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[0].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[1].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[2].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[j].y - screenVertices[3].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].x - screenVertices[0].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].x - screenVertices[1].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].x - screenVertices[2].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].x - screenVertices[3].x)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].y - screenVertices[0].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].y - screenVertices[1].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].y - screenVertices[2].y)
                                                    || m_SeparatorLinesCheckingMargin > Mathf.Abs(clipped[0].y - screenVertices[3].y)
                                                    //|| clipped[j].x == screenVertices[0].x 
                                                    //|| clipped[j].x == screenVertices[1].x 
                                                    //|| clipped[j].x == screenVertices[2].x 
                                                    //|| clipped[j].x == screenVertices[3].x
                                                    //|| clipped[j].y == screenVertices[0].y 
                                                    //|| clipped[j].y == screenVertices[1].y 
                                                    //|| clipped[j].y == screenVertices[2].y 
                                                    //|| clipped[j].y == screenVertices[3].y
                                                    //|| clipped[0].x == screenVertices[0].x 
                                                    //|| clipped[0].x == screenVertices[1].x 
                                                    //|| clipped[0].x == screenVertices[2].x 
                                                    //|| clipped[0].x == screenVertices[3].x
                                                    //|| clipped[0].y == screenVertices[0].y 
                                                    //|| clipped[0].y == screenVertices[1].y 
                                                    //|| clipped[0].y == screenVertices[2].y 
                                                    //|| clipped[0].y == screenVertices[3].y
                                                    )
                                                {
                                                    continue;
                                                }
                                            }
                                        }

                                        if (!m_DuplicatedLines)
                                        {
                                            //Skip lines that are equal (same vertices) or reversed (swapped vertices)
                                            bool isduplicate = false;
                                            for (int k = 0; k < m_SeparatorLines.Count; k++)
                                            {
                                                //Check equal
                                                if (m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).x - clipped[j].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).y - clipped[j].y)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).x - clipped[0].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).y - clipped[0].y))
                                                {
                                                    isduplicate = true;
                                                }
                                                //Check reversed
                                                else if (m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).x - clipped[0].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(0).y - clipped[0].y)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).x - clipped[j].x)
                                                    && m_SeparatorLinesCheckingMargin > Mathf.Abs(m_SeparatorLines[k].GetPosition(1).y - clipped[j].y))
                                                {
                                                    isduplicate = true;
                                                }
                                            }

                                            if (isduplicate)
                                            {
                                                continue;
                                            }
                                        }

                                        //Adding counter to check if there are enough lines in the list
                                        slcounter++;

                                        if (slcounter > m_SeparatorLines.Count)
                                        {
                                            m_SeparatorLines.Add(DrawSeparatorLine("Separator Line " + (slcounter - 1), clipped[j], clipped[0], m_SeparatorLinesColor, m_SeparatorLinesWidth));
                                        }
                                        else
                                        {
                                            UpdateSeparatorLine(m_SeparatorLines, slcounter - 1, clipped[j], clipped[0], m_SeparatorLinesColor, m_SeparatorLinesWidth);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    //Destroy and remove excess lines if there are any
                    if (m_SeparatorLinesActive)
                    {
                        if (slcounter < m_SeparatorLines.Count)
                        {
                            for (int l = slcounter; l < m_SeparatorLines.Count; l++)
                            {
                                Destroy(m_SeparatorLines[l].gameObject);
                                m_SeparatorLines.RemoveAt(l);
                            }
                        }
                    }
                }
            }
        }
        static Mesh MeshPolygonFromPolygon(List<Vector2> polygon)
        {
            //Simplified version from Oskar's MeshFromPolygon() where he adds thickness (thus making it 3d)

            //TODO: check if convex
            //TODO: check if 3 or more points
            var count = polygon.Count;
            // TODO: cache these things to avoid garbage
            var verts = new Vector3[count];
            var norms = new Vector3[count];
            var tris = new int[3*(count - 2)];//The polygon has to be convex (so traingles relation to verts is n-2)
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
            mesh.RecalculateTangents();
            mesh.RecalculateUVDistributionMetrics();

            return mesh;
        }
        PositionAndRotation TargetPositionAndRotation()
        {
            float halfVerticalFovRad = (m_GlobalCamera.fieldOfView * Mathf.Deg2Rad) / 2f;
            float halfHorizontalFovRad = Mathf.Atan(Mathf.Tan(halfVerticalFovRad) * m_GlobalCamera.aspect);

            var rotation = Quaternion.Euler(Pitch, Yaw, Roll);
            var inverseRotation = Quaternion.Inverse(rotation);

            var targetsRotatedToCameraIdentity = m_ActiveTargets.Select(target => inverseRotation * target.transform.position).ToArray();

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

//----------------------------------------------------------------
//Random notes

//LLoyd's relaxation?
//for (int i = 0; i < m_LloydsRelaxationPasses; i++)
//{
//    for (int j = 0; j < sites.Length; j++)
//    {
//        clip.ClipSite(diagram, screenVertices, j, ref clipped);

//        if (clipped.Count > 0)
//        {
//            sites[j] = FindAveragePosition2D(clipped);
//        }
//    }
//    //Recalculates diagram with new sites values
//    diagram = calc.CalculateDiagram(sites);
//}

//Asymptotic average
//x+=(target-x)*.1*timeScale