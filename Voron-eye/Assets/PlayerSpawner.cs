using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VE
{
    public class PlayerSpawner : MonoBehaviour
    {
        public bool isInBox;
        public KeyCode Spawn;
        public KeyCode Despawn;

        [SerializeField] private List<GameObject> m_TargetsGO;

        private void Start()
        {
            //for (int i = 1; i < m_TargetsGO.Count; i++)
            //{
            //    if (m_TargetsGO[i].activeSelf)
            //    {
            //        m_TargetsGO[i].SetActive(false);
            //    }
            //}
        }

        void Update()
        {
            if (isInBox)
            {
                //Debug.Log("Found in box!");
                if (Input.GetKeyDown(Spawn))
                {
                    SpawnTarget();
                }

                if (Input.GetKeyDown(Despawn))
                {
                    DespawnTarget();
                }
            }
            else
            { 
                //Debug.Log("Not in box!");
            }
        }

        void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isInBox = true;
            }
        }
        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isInBox = false;
            }
        }

        public void SpawnTarget()
        {
            for (int i = 0; i < m_TargetsGO.Count; i++)
            {
                if (!m_TargetsGO[i].activeSelf)
                {
                    m_TargetsGO[i].SetActive(true);
                    break;
                }
            }

            return;
        }

        public void DespawnTarget()
        {
            for (int i = m_TargetsGO.Count - 1; i > 0; i--)
            {
                if (m_TargetsGO[i].activeSelf)
                {
                    m_TargetsGO[i].SetActive(false);
                    break;
                }
            }
            return;
        }
    }
}
