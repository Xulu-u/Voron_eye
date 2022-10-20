using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VE
{
    public class Target
    {
        public int index;
        public GameObject GO;

        public bool active;
        public bool activeLastFrame;

        public Vector2 screenPos;
        public Vector2 screenPosCluster;
        public Vector3 worldPosCluster;
        
        public GameObject mesh;
        public GameObject quad;
        public RenderTexture renderTexture;
        public GameObject camera;

        public enum dbscan
            {
            NOISE = -1,
            CLUSTERPARENT,
            CLUSTERCHILD,
        }

        public dbscan dbtype;
        public int clusterParentIndex;
        public Target(int i, GameObject Target, Camera GlobalCamera)
        {
            index = i;
            GO = Target;
            active = Target.activeSelf;
            activeLastFrame = active;

            screenPos = GlobalCamera.WorldToScreenPoint(Target.transform.position);

            dbtype = dbscan.NOISE;
            clusterParentIndex = index;
        }

        public void CheckActive()
        {
            if (GO != null)
            {
                active = GO.activeSelf;
                mesh.SetActive(active);
                quad.SetActive(active);
                camera.SetActive(active);
            }
        }

        public void CheckDBSCAN()
        {
            if (GO != null)
            {
                if (dbtype == dbscan.CLUSTERCHILD)
                {
                    mesh.SetActive(false);
                    quad.SetActive(false);
                    camera.SetActive(false);
                }
            }
        }

        ~Target()
        {
            GO = null;
            mesh = null;
            quad = null;
            renderTexture = null;
            camera = null;
        }
    }
}
