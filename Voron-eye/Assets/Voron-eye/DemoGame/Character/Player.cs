using UnityEngine;
using System.Collections;

public class Player : MonoBehaviour {

	private Animator anim;
	private CharacterController controller;

	public GameObject Root;

	public Camera Camera;

	//Get the keycodes for all the various directions of movement.
	public KeyCode Forward;
	public KeyCode Backward;
	public KeyCode Left;
	public KeyCode Right;

	public float speed = 600.0f;
	public float turnSpeed = 400.0f;
	private Vector3 moveDirection = Vector3.zero;
	public float gravity = 20.0f;

	void Start () {
		controller = GetComponent <CharacterController>();
		anim = gameObject.GetComponentInChildren<Animator>();
	}

	void Update ()
	{
		anim.SetInteger("AnimationPar", 0);
		moveDirection = Vector3.zero;

		if (Input.GetKey (Forward)) 
		{
			anim.SetInteger ("AnimationPar", 1);
			moveDirection = transform.forward * speed;
			//Rootrotation = 
			//	(Root.transform.rotation.x,
			//	Root.transform.rotation.y,
			//	Root.transform.rotation.z
			//	Root.transform.rotation.w);
		}
		else if (Input.GetKey(Backward))
        {
			anim.SetInteger("AnimationPar", 1);
			moveDirection = -transform.forward * speed;
			//Root.transform.Rotate(-transform.forward);
		}

		if (Input.GetKey(Left))
		{
			anim.SetInteger("AnimationPar", 1);
			moveDirection -= transform.right * speed;
			//Root.transform.Rotate(-transform.right);
		}
			
		else if (Input.GetKey(Right))
        {
			anim.SetInteger("AnimationPar", 1);
			moveDirection += transform.right * speed;
			//Root.transform.Rotate(transform.right);
		}

		if (moveDirection != Vector3.zero)
        {
			Root.transform.forward += moveDirection;
        }
		//transform.Rotate(0, turn * turnSpeed * Time.deltaTime, 0);
		//Root.transform.rotation = new Quaternion (0, Root.transform.rotation.y, Root.transform.rotation.z, Root.transform.rotation.w);
		controller.Move(moveDirection * Time.deltaTime);
		moveDirection.y -= gravity * Time.deltaTime;
	}
}
