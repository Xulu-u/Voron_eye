using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Complete
{
    public class CameraControl : MonoBehaviour
    {
        // MULTIPLAYER CAMERA
        public float m_DampTime = 0.2f;                 // Approximate time for the camera to refocus.
        public float m_ScreenEdgeBuffer = 4f;           // Space between the top/bottom most target and the screen edge.
        public float m_MaxZoom_Orth = 6.5f;             // The minimum orthographic size the camera can be.
        public float m_MinZoom_Orth = 20f;              // The maximum orthographic size the camera can be.
        public float m_MaxZoom_Pers = 10f;              // The minimum perspective distnace the camera can be.
        public float m_MinZoom_Pers = 40f;              // The maximum perspective distnace the camera can be.
        public float m_ZoomLimiter_Pers = 50f;
        public Vector3 m_Offset;                        // Camera target offset
        
        public List<Transform> m_Targets;               // All the targets the camera needs to encompass.


        private Camera m_MainCamera;                        // Used for referencing the camera.
        private float m_ZoomSpeed;                      // Reference speed for the smooth damping of the orthographic size.
        private Vector3 m_MoveVelocity;                 // Reference velocity for the smooth damping of the position.
        private Vector3 m_AveragePosition;
        private Vector3 m_DesiredPosition;              // The position the camera is moving towards.
        private float m_DesiredZoom;
        private Vector3 m_StartLocalPosition;           // The starting local position for the ortographic camera.

        //SPLITSCREEN CAMERA
        private float m_SplitDistance;

        //Temporary
        bool splitbool = false;

        public Color splitterColor;
        public float splitterWidth;

        private GameObject camera2;

        private GameObject split;
        private GameObject splitter;

        //public Material unlitMaterial;

        //public List<Camera> m_Cameras;

        private void Awake ()
        {
            m_MainCamera = GetComponentInChildren<Camera> ();
            m_StartLocalPosition = m_MainCamera.gameObject.transform.position;
        }
        
        void Start()
        {
            //Referencing camera1 and initalizing camera2.
            Camera c1 = m_MainCamera;
            camera2 = new GameObject("Camera 2");
            camera2.transform.parent = gameObject.transform;
            Camera c2 = camera2.AddComponent<Camera>();

            // Ensure the second camera renders before the first one
            c2.depth = c1.depth - 1;
            //Setting up the culling mask of camera2 to ignore the layer "TransparentFX" as to avoid rendering the split and splitter on both cameras.
            c2.cullingMask = ~(1 << LayerMask.NameToLayer("TransparentFX"));

            //Setting up the splitter and initalizing the gameobject.
            splitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            splitter.transform.parent = m_MainCamera.transform;
            splitter.transform.localPosition = Vector3.forward;
            splitter.transform.localScale = new Vector3(2, splitterWidth / 10, 1);
            splitter.transform.localEulerAngles = Vector3.zero;
            splitter.SetActive(false);

            //Setting up the split and initalizing the gameobject.
            split = GameObject.CreatePrimitive(PrimitiveType.Quad);
            split.transform.parent = splitter.transform;
            split.transform.localPosition = new Vector3(0, -(1 / (splitterWidth / 10)), 0.0001f); // Add a little bit of Z-distance to avoid clipping with splitter
            split.transform.localScale = new Vector3(1, 2 / (splitterWidth / 10), 1);
            split.transform.localEulerAngles = Vector3.zero;

            //Creates both temporary materials required to create the splitscreen.
            Material tempMat = new Material(Shader.Find("Unlit/Color")); // new Material(shader); // new Material (Shader.Find ("Unlit/Color"));
            tempMat.color = splitterColor;
            splitter.GetComponent<Renderer>().material = tempMat;
            splitter.GetComponent<Renderer>().sortingOrder = 2;
            splitter.layer = LayerMask.NameToLayer("TransparentFX");

            Material tempMat2 = new Material(Shader.Find("Mask/SplitScreen"));

            split.GetComponent<Renderer>().material = tempMat2;
            split.layer = LayerMask.NameToLayer("TransparentFX");



            //For now i'm forcing to pre calculate starting position and zoom
            m_MainCamera.fieldOfView = Mathf.Clamp(FindRequiredFOV(), m_MaxZoom_Pers, m_MinZoom_Pers);
            // Find the average position of the targets.
            FindAveragePosition();

            // Add offset to the average position.
            transform.position = m_AveragePosition + m_Offset;
        }

        private void LateUpdate ()
        {
            ////If there's no cameras do nothing
            //if (m_Cameras.Count == 0)
            //{
            //    Debug.Log("No Cameras in Camera List");
            //    return;
            //}

            ////If the first camera in the list is not the main one do nothing
            //if (m_Cameras[0] != m_MainCamera)
            //{
            //    Debug.Log("1st Camera in List is not Main Camera");
            //    return;
            //}

            //// Check if there's a camera for every target
            //if (m_Cameras.Count != m_Targets.Count)
            //{
            //    //Check if there's more targets than cameras
            //    if (m_Cameras.Count < m_Targets.Count)
            //    {
            //        //Add Camera GO with the correct name, ex: Player 2 == Camera 2
            //        for (int i = m_Cameras.Count; i < m_Targets.Count; i++)
            //        {
            //            GameObject camObj = new GameObject("Camera" + i + 1);
            //            Camera addCam = camObj.AddComponent<Camera>();
            //            m_Cameras.Add(addCam);
            //        }
            //    }
            //    //Check if there's more cameras than targets
            //    //Destroy and Remove them
            //}

            //If there's no players do nothing
            if (m_Targets.Count == 0)
            {
                Debug.Log("No Targets on targets list");
                return;
            }
            if (!splitbool)
            { // Move the camera towards a desired position.
                Move();

                // Change the size of the camera based.
                Zoom();
            }
            else
                Split();
        }


        private void Move ()
        {
            // Find the average position of the targets.
            FindAveragePosition ();

            // Add offset to the average position.
            m_DesiredPosition = m_AveragePosition + m_Offset;

            // Smoothly transition to that position.
            transform.position = Vector3.SmoothDamp(transform.position, m_DesiredPosition, ref m_MoveVelocity, m_DampTime);
        }


        private void FindAveragePosition ()
        {
            Vector3 averagePos = new Vector3 ();
            int numTargets = 0;

            // If the target is only one just send its position.
            if (m_Targets.Count == 1)
            {
                m_AveragePosition = m_Targets[0].position;
                return; 
            }

            // Go through all the targets and add their positions together.
            for (int i = 0; i < m_Targets.Count; i++)
            {
                // If the target isn't active, go on to the next one.
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;

                // Add to the average and increment the number of targets in the average.
                averagePos += m_Targets[i].position;
                numTargets++;
            }

            // If there are targets divide the sum of the positions by the number of them to find the average.
            if (numTargets > 0)
                averagePos /= numTargets;

            // Keep the same y value.
            averagePos.y = transform.position.y;

            // The desired position is the average position;
            m_AveragePosition = averagePos;

            // And yes we could use bounds to get the position.
        }


        private void Zoom ()
        {
            // Check the camera projection
            if (m_MainCamera.orthographic)
            {
                //Check local position
                m_MainCamera.gameObject.transform.position = m_StartLocalPosition;

                // Find the required size based on the desired position and smoothly transition to that size.
                float requiredSize = FindRequiredSize();
                m_MainCamera.orthographicSize = Mathf.SmoothDamp(m_MainCamera.orthographicSize, requiredSize, ref m_ZoomSpeed, m_DampTime);
            }

            else
            {
                m_DesiredZoom = Mathf.Lerp(m_MaxZoom_Pers, m_MinZoom_Pers, FindRequiredFOV() / m_ZoomLimiter_Pers);
                m_MainCamera.fieldOfView = Mathf.Lerp(m_MainCamera.fieldOfView, m_DesiredZoom, Time.deltaTime);

                //Check if the minimum zoon has reached to start the split
                if (m_MainCamera.fieldOfView >= m_MinZoom_Pers - 1)//cuz with lerp it never gets to the actual number
                {
                    //Grab the last maximum distance between the 2 players and save it to check later
                    m_SplitDistance = GetMaxTargetDistance();
                    splitbool = true;
                }
            }
        }

        private void Split ()
        {
            //Check the maximum distance with the one saved 
            if (m_SplitDistance > GetMaxTargetDistance())
            {
                splitbool = false;
                return;
            }

            //TEMPORARY
            Transform player1 = m_Targets[0];
            Transform player2 = m_Targets[1];
            float splitDistance = m_SplitDistance;

            //Gets the z axis distance between the two players and just the standard distance.
            float zDistance = player1.position.z - player2.transform.position.z;
            float distance = Vector3.Distance(player1.position, player2.transform.position);

            //Sets the angle of the player up, depending on who's leading on the x axis.
            float angle;
            if (player1.transform.position.x <= player2.transform.position.x)
            {
                angle = Mathf.Rad2Deg * Mathf.Acos(zDistance / distance);
            }
            else
            {
                angle = Mathf.Rad2Deg * Mathf.Asin(zDistance / distance) - 90;
            }

            //Rotates the splitter according to the new angle.
            splitter.transform.localEulerAngles = new Vector3(0, 0, angle);

            //Gets the exact midpoint between the two players.
            Vector3 midPoint = new Vector3((player1.position.x + player2.position.x) / 2, (player1.position.y + player2.position.y) / 2, (player1.position.z + player2.position.z) / 2);

            //Waits for the two cameras to split and then calcuates a midpoint relevant to the difference in position between the two cameras.
            if (distance > splitDistance)
            {
                Vector3 offset = midPoint - player1.position;
                offset.x = Mathf.Clamp(offset.x, -splitDistance / 2, splitDistance / 2);
                offset.y = Mathf.Clamp(offset.y, -splitDistance / 2, splitDistance / 2);
                offset.z = Mathf.Clamp(offset.z, -splitDistance / 2, splitDistance / 2);
                midPoint = player1.position + offset;

                Vector3 offset2 = midPoint - player2.position;
                offset2.x = Mathf.Clamp(offset.x, -splitDistance / 2, splitDistance / 2);
                offset2.y = Mathf.Clamp(offset.y, -splitDistance / 2, splitDistance / 2);
                offset2.z = Mathf.Clamp(offset.z, -splitDistance / 2, splitDistance / 2);
                Vector3 midPoint2 = player2.position - offset;

                //Sets the splitter and camera to active and sets the second camera position as to avoid lerping continuity errors.
                if (splitter.activeSelf == false)
                {
                    splitter.SetActive(true);
                    camera2.SetActive(true);

                    camera2.transform.position = m_MainCamera.transform.position;
                    camera2.transform.rotation = m_MainCamera.transform.rotation;

                }
                else
                {
                    //Lerps the second cameras position and rotation to that of the second midpoint, so relative to the second player.
                    camera2.transform.position = Vector3.Lerp(camera2.transform.position, midPoint2 + new Vector3(0, 6, -5), Time.deltaTime * 5);
                    Quaternion newRot2 = Quaternion.LookRotation(midPoint2 - camera2.transform.position);
                    camera2.transform.rotation = Quaternion.Lerp(camera2.transform.rotation, newRot2, Time.deltaTime * 5);
                }

            }
            else
            {
                //Deactivates the splitter and camera once the distance is less than the splitting distance (assuming it was at one point).
                if (splitter.activeSelf)
                    splitter.SetActive(false);
                camera2.SetActive(false);
            }

            /*Lerps the first cameras position and rotation to that of the second midpoint, so relative to the first player
            or when both players are in view it lerps the camera to their midpoint.*/
            m_MainCamera.fieldOfView = 60;
            m_MainCamera.transform.position = Vector3.Lerp(m_MainCamera.transform.position, midPoint + new Vector3(0, 6, -5), Time.deltaTime * 5);
            Quaternion newRot = Quaternion.LookRotation(midPoint - m_MainCamera.transform.position);
            m_MainCamera.transform.rotation = Quaternion.Lerp(m_MainCamera.transform.rotation, newRot, Time.deltaTime * 5);
        }
        private float FindRequiredSize ()
        {
            // Find the position the camera rig is moving towards in its local space.
            Vector3 desiredLocalPos = transform.InverseTransformPoint(m_DesiredPosition);

            // Start the camera's size calculation at zero.
            float size = 0f;

            // Go through all the targets...
            for (int i = 0; i < m_Targets.Count; i++)
            {
                // ... and if they aren't active continue on to the next target.
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;

                // Otherwise, find the position of the target in the camera's local space.
                Vector3 targetLocalPos = transform.InverseTransformPoint(m_Targets[i].position);

                // Find the position of the target from the desired position of the camera's local space.
                Vector3 desiredPosToTarget = targetLocalPos - desiredLocalPos;

                // Choose the largest out of the current size and the distance of the tank 'up' or 'down' from the camera.
                size = Mathf.Max(size, Mathf.Abs(desiredPosToTarget.y));

                // Choose the largest out of the current size and the calculated size based on the tank being to the left or right of the camera.
                size = Mathf.Max(size, Mathf.Abs(desiredPosToTarget.x) / m_MainCamera.aspect);
            }

            // Add the edge buffer to the size.
            size += m_ScreenEdgeBuffer;

            // Make sure the camera's size isn't below the minimum.
            size = Mathf.Max (size, m_MaxZoom_Orth);

            // Make sure the camera's size isn't above the maximum.
            size = Mathf.Min(size, m_MinZoom_Orth);

            return size;
        }

        private float FindRequiredFOV()
        {
            var bounds = new Bounds(m_Targets[0].position, Vector3.zero);

            for (int i = 0; i < m_Targets.Count; i++)
            {
                bounds.Encapsulate(m_Targets[i].position);
            }

            return Mathf.Max(bounds.size.x, bounds.size.z);
        }


        public void SetStartPositionAndSize ()
        {
            // Find the desired position.
            FindAveragePosition ();

            // Set the camera's position to the desired position without damping.
            transform.position = m_DesiredPosition;

            // Find and set the required size of the camera.
            m_MainCamera.orthographicSize = FindRequiredSize ();
        }

        private float GetMaxTargetDistance ()
        {
            float maxDistance;
            float lastDistance;
            maxDistance = 0f;

            // Go through all the targets and add their positions together.
            for (int i = 0; i < m_Targets.Count; i++)
            {
                // If the target isn't active, go on to the next one.
                if (!m_Targets[i].gameObject.activeSelf)
                    continue;

                // Add to the average and increment the number of targets in the average.
                lastDistance = Vector3.Distance(m_Targets[0].transform.position, m_Targets[i].transform.position);

                if (lastDistance > maxDistance)
                {
                    maxDistance = lastDistance;
                }
            }

            return maxDistance;
        }
    }
}