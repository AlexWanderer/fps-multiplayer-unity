﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FPSController : NetworkBehaviour {

    private Transform firstPerson_View;
    private Transform firstPerson_Camera;

    private Vector3 firstPerson_View_Rotation = Vector3.zero;

    public float walkSpeed = 6.75f;
    public float runSpeed = 10f;
    public float crouchSpeed = 4f;
    public float jumpSpeed = 8f;
    public float gravity = 20f;

    private float speed;

    private bool isMoving, isGrounded, isCrouching;

    private float inputX, inputY;
    private float inputX_Set, inputY_Set;
    private float inputModifyFactor;

    private bool limitDiagonalSpeed = true;

    private float antiBumpFactor = 0.75f;

    private CharacterController charController;
    private Vector3 moveDirection = Vector3.zero;

    public LayerMask groundLayer;
    private float rayDistance;
    private float default_ControllerHeight;
    private Vector3 default_CameraPosition;
    private float cameraHeight;

    private FPSPlayerAnimations playerAnimations;

    [SerializeField]
    private WeaponManager weaponManager;
    private FPSWeapon currentWeapon;

    private float fireRate = 15f;
    private float nextTimeToFire = 0f;

    [SerializeField]
    private WeaponManager handsWeaponManager;
    private FPSHandsWeapon currentHandsWeapon;

    public GameObject playerHolder, weaponsHolder;
    public GameObject[] weaponsFPS;
    private Camera mainCam;
    public FPSMouseLook[] mouseLook;

    //private Color32[] playerColors = new Color32[] {new Color32(0,44,255,1),
    //new Color32(252,208,193,1), new Color32(0,0,0,1)};

    public Material delta, swat;
    public Renderer playerRenderer1, playerRenderer2, playerRenderer3;

    void Start () {
        // Find is not optimal
        firstPerson_View = transform.Find ("FPS View").transform;
        charController = GetComponent<CharacterController> ();
        speed = walkSpeed;
        isMoving = false;

        rayDistance = charController.height * 0.5f + charController.radius;
        default_ControllerHeight = charController.height;
        default_CameraPosition = firstPerson_View.localPosition;

        playerAnimations = GetComponent<FPSPlayerAnimations> ();

        weaponManager.weapons[0].SetActive (true);
        currentWeapon = weaponManager.weapons[0].GetComponent<FPSWeapon> ();

        handsWeaponManager.weapons[0].SetActive (true);
        currentHandsWeapon = handsWeaponManager.weapons[0].GetComponent<FPSHandsWeapon> ();

        if (isLocalPlayer) {
            playerHolder.layer = LayerMask.NameToLayer ("Player");

            foreach (Transform child in playerHolder.transform) {
                child.gameObject.layer = LayerMask.NameToLayer ("Player");
            }

            for (int i = 0; i < weaponsFPS.Length; i++) {
                weaponsFPS[i].layer = LayerMask.NameToLayer ("Player");
            }

            weaponsHolder.layer = LayerMask.NameToLayer ("Enemy");

            foreach (Transform child in weaponsHolder.transform) {
                child.gameObject.layer = LayerMask.NameToLayer ("Enemy");
            }
        }

        if (!isLocalPlayer) {
            playerHolder.layer = LayerMask.NameToLayer ("Enemy");

            foreach (Transform child in playerHolder.transform) {
                child.gameObject.layer = LayerMask.NameToLayer ("Enemy");
            }

            for (int i = 0; i < weaponsFPS.Length; i++) {
                weaponsFPS[i].layer = LayerMask.NameToLayer ("Enemy");
            }

            weaponsHolder.layer = LayerMask.NameToLayer ("Player");

            foreach (Transform child in weaponsHolder.transform) {
                child.gameObject.layer = LayerMask.NameToLayer ("Player");
            }
        }

        if (!isLocalPlayer) {
            for (int i = 0; i < mouseLook.Length; i++) {
                mouseLook[i].enabled = false;
            }
        }

        // Deactivates all FPS Cameras
        mainCam = transform.Find ("FPS View").Find ("FPS Camera").GetComponent<Camera> ();
        mainCam.gameObject.SetActive (false);

        if (!isLocalPlayer) {
            for (int i = 0; i < playerRenderer1.materials.Length; i++) {
                //playerRenderer1.materials[i].color = playerColors[i];
                //playerRenderer2.materials[i].color = playerColors[i];
                //playerRenderer3.materials[i].color = playerColors[i];

                // Players on server are set to delta skin
                playerRenderer1.material = delta;
                playerRenderer2.material = delta;
                playerRenderer3.material = delta;
            }
        } else {
            // Local player is set to swat skin
            playerRenderer1.material = swat;
            playerRenderer2.material = swat;
            playerRenderer3.material = swat;
        }

    }

    public override void OnStartLocalPlayer () {
        tag = "Player";
    }

    void Update () {

        // Activates local FPS Camera
        if (isLocalPlayer) {
            if (!mainCam.gameObject.activeInHierarchy) {
                mainCam.gameObject.SetActive (true);
            }
        }

        // If you are not the local player
        // Not running on own computer
        if (!isLocalPlayer) {
            // Don't execute code below return
            return;
        }

        PlayerMovement ();
        SelectWeapon ();
    }

    void PlayerMovement () {
        if (Input.GetKey (KeyCode.W) || Input.GetKey (KeyCode.S)) {
            if (Input.GetKey (KeyCode.W)) {
                inputY_Set = 1f;
            } else {
                inputY_Set = -1f;
            }

        } else {
            inputY_Set = 0f;
        }

        if (Input.GetKey (KeyCode.A) || Input.GetKey (KeyCode.D)) {
            if (Input.GetKey (KeyCode.A)) {
                inputX_Set = -1f;
            } else {
                inputX_Set = 1f;
            }
        } else {
            inputX_Set = 0f;
        }

        inputY = Mathf.Lerp (inputY, inputY_Set, Time.deltaTime * 19f);
        inputX = Mathf.Lerp (inputX, inputX_Set, Time.deltaTime * 19f);

        inputModifyFactor = Mathf.Lerp (inputModifyFactor, (inputY_Set != 0 && inputX_Set != 0 && limitDiagonalSpeed) ? 0.75f : 1.0f, Time.deltaTime * 19f);

        firstPerson_View_Rotation = Vector3.Lerp (firstPerson_View_Rotation, Vector3.zero, Time.deltaTime * 5f);
        firstPerson_View.localEulerAngles = firstPerson_View_Rotation;

        if (isGrounded) {
            PlayerCrouchingAndSprinting ();

            moveDirection = new Vector3 (inputX * inputModifyFactor, -antiBumpFactor, inputY * inputModifyFactor);
            moveDirection = transform.TransformDirection (moveDirection) * speed;

            PlayerJump ();
        }

        moveDirection.y -= gravity * Time.deltaTime;

        isGrounded = (charController.Move (moveDirection * Time.deltaTime) & CollisionFlags.Below) != 0;
        isMoving = charController.velocity.magnitude > 0.15f;

        HandleAnimations ();
    }

    void PlayerCrouchingAndSprinting () {
        if (Input.GetKeyDown (KeyCode.LeftControl)) {
            if (!isCrouching) {
                isCrouching = true;

            } else {
                if (CanGetUp ()) {
                    isCrouching = false;
                }
            }

            StopCoroutine (MoveCameraCrouch ());
            StartCoroutine (MoveCameraCrouch ());

        }

        if (isCrouching) {
            speed = crouchSpeed;
        } else {
            if (Input.GetKey (KeyCode.LeftShift)) {
                speed = runSpeed;
            } else {
                speed = walkSpeed;
            }
        }

        playerAnimations.PlayerCrouch (isCrouching);
    }

    bool CanGetUp () {
        Ray groundRay = new Ray (transform.position, transform.up);
        RaycastHit groundHit;

        if (Physics.SphereCast (groundRay, charController.radius + 0.05f, out groundHit, rayDistance, groundLayer)) {
            if (Vector3.Distance (transform.position, groundHit.point) < 2.3f) {
                return false;
            }
        }

        return true;
    }

    IEnumerator MoveCameraCrouch () {
        charController.height = isCrouching ? default_ControllerHeight / 1.5f : default_ControllerHeight;
        charController.center = new Vector3 (0f, charController.height / 2, 0f);

        cameraHeight = isCrouching ? default_CameraPosition.y / 1.5f : default_CameraPosition.y;

        while (Mathf.Abs (cameraHeight - firstPerson_View.localPosition.y) > 0.01f) {
            firstPerson_View.localPosition = Vector3.Lerp (firstPerson_View.localPosition, new Vector3 (default_CameraPosition.x, cameraHeight, default_CameraPosition.z), Time.deltaTime * 11f);
            yield return null;
        }
    }

    void PlayerJump () {
        if (Input.GetKeyDown (KeyCode.Space)) {
            if (isCrouching) {
                if (CanGetUp ()) {
                    isCrouching = false;

                    playerAnimations.PlayerCrouch (isCrouching);

                    StopCoroutine (MoveCameraCrouch ());
                    StartCoroutine (MoveCameraCrouch ());
                }

            } else {
                moveDirection.y = jumpSpeed;
            }
        }
    }

    void HandleAnimations () {
        playerAnimations.Movement (charController.velocity.magnitude);
        playerAnimations.PlayerJump (charController.velocity.y);

        if (isCrouching && charController.velocity.magnitude > 0f) {
            playerAnimations.PlayerCrouchWalk (charController.velocity.magnitude);
        }

        // Shooting Animations
        if (Input.GetMouseButtonDown (0) && Time.time > nextTimeToFire) {
            nextTimeToFire = Time.time + 1f / fireRate;

            if (isCrouching) {
                playerAnimations.Shoot (false);
            } else {
                playerAnimations.Shoot (true);
            }

            currentWeapon.Shoot ();
            currentHandsWeapon.Shoot ();
        }

        if (Input.GetKeyDown (KeyCode.R)) {
            playerAnimations.Reload ();
            currentHandsWeapon.Reload ();
        }
    }

    void SelectWeapon () {
        if (Input.GetKeyDown (KeyCode.Alpha1)) {

            if (!handsWeaponManager.weapons[0].activeInHierarchy) {
                for (int i = 0; i < handsWeaponManager.weapons.Length; i++) {
                    handsWeaponManager.weapons[i].SetActive (false);
                }

                currentHandsWeapon = null;
                handsWeaponManager.weapons[0].SetActive (true);
                currentHandsWeapon = handsWeaponManager.weapons[0].GetComponent<FPSHandsWeapon> ();
            }

            if (!weaponManager.weapons[0].activeInHierarchy) {
                for (int i = 0; i < weaponManager.weapons.Length; i++) {
                    weaponManager.weapons[i].SetActive (false);
                }

                currentWeapon = null;
                weaponManager.weapons[0].SetActive (true);
                currentWeapon = weaponManager.weapons[0].GetComponent<FPSWeapon> ();

                playerAnimations.ChangeController (true);
            }
        }

        if (Input.GetKeyDown (KeyCode.Alpha2)) {

            if (!handsWeaponManager.weapons[1].activeInHierarchy) {
                for (int i = 0; i < handsWeaponManager.weapons.Length; i++) {
                    handsWeaponManager.weapons[i].SetActive (false);
                }

                currentHandsWeapon = null;
                handsWeaponManager.weapons[1].SetActive (true);
                currentHandsWeapon = handsWeaponManager.weapons[1].GetComponent<FPSHandsWeapon> ();
            }

            if (!weaponManager.weapons[1].activeInHierarchy) {
                for (int i = 0; i < weaponManager.weapons.Length; i++) {
                    weaponManager.weapons[i].SetActive (false);
                }

                currentWeapon = null;
                weaponManager.weapons[1].SetActive (true);
                currentWeapon = weaponManager.weapons[1].GetComponent<FPSWeapon> ();

                playerAnimations.ChangeController (true);
            }
        }

        if (Input.GetKeyDown (KeyCode.Alpha3)) {

            if (!handsWeaponManager.weapons[2].activeInHierarchy) {
                for (int i = 0; i < handsWeaponManager.weapons.Length; i++) {
                    handsWeaponManager.weapons[i].SetActive (false);
                }

                currentHandsWeapon = null;
                handsWeaponManager.weapons[2].SetActive (true);
                currentHandsWeapon = handsWeaponManager.weapons[2].GetComponent<FPSHandsWeapon> ();
            }

            if (!weaponManager.weapons[2].activeInHierarchy) {
                for (int i = 0; i < weaponManager.weapons.Length; i++) {
                    weaponManager.weapons[i].SetActive (false);
                }

                currentWeapon = null;
                weaponManager.weapons[2].SetActive (true);
                currentWeapon = weaponManager.weapons[2].GetComponent<FPSWeapon> ();

                playerAnimations.ChangeController (false);
            }
        }
    }

} // FPSController