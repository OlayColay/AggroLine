using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using PathCreation;

[RequireComponent(typeof(Rigidbody))]
public class AggroLineController : MonoBehaviour
{
    [HideInInspector] 
    public Controls controls;
    [Header("Drag-ins")]
    [Tooltip("Animator of player character")]
    public Animator animator;
    [Tooltip("Camera used for contolling where the player turns to")]
    public Transform playerCamera;
    [Tooltip("Cinemachine Free Look object that is used for resetting camera view")]
    public CinemachineFreeLook CMFreeLook;
    [Tooltip("Object where ground and grinds are looked for")]
    public Transform groundCheck;
    [Header("Layer Masks")]
    [Tooltip("Which layers count as ground")]
    public LayerMask whatIsGround;
    [Tooltip("Which layers can be grinded on")]
    public LayerMask whatIsGrindable;

    [Header("Skating Options")]
    [Tooltip("How far to check for ground from groundCheck's position")]
    public float groundCheckRadius = 0.25f;
    [Tooltip("Acceleration of player to maxGroundSpeed")]
    public float accelerationMultiplier = 1f;
    [Tooltip("Maximum speed on foot without boosting")]
    public float maxGroundSpeed = 8f;
    [Tooltip("Turn speed (cornering) while on foot")]
    public float groundTurnSpeed = 3f;
    [Header("Falling Options")]
    [Tooltip("Turn speed (cornering) while airborne")]
    public float airTurnSpeed = 1f;
    [Tooltip("Maximum vertical falling speed. Should be posative")]
    public float terminalVelocity = 7f;
    [Header("Jumping Options")]
    [Tooltip("Starting upwards velocity of a jump")]
    public float jumpForce = 10f;
    [Tooltip("Subtract from jumpForce every FixedUpdate")]
    public float jumpDamper = 0.1f;
    [Tooltip("How long, in seconds, player travels upwards when holding down the jump button")]
    public float maxJumpTime = 1f;
    [Tooltip("Minimum length of an upwards jump")]
    public float minJumpTime = 0.1f;
    [Header("Trick Jumping Options")]
    [Tooltip("Starting upwards velocity of a trick jump")]
    public float trickJumpForce = 11f;
    [Tooltip("How long, in seconds, player travels upwards when holding down the jump button for trick jumps")]
    public float maxTrickJumpTime = 1f;
    [Tooltip("Speed at which player trick jumps from")]
    public float trickJumpSpeed = 9f;
    [Tooltip("Speed reduction at the start of a trick jump")]
    public float trickJumpLoss = 1f;
    [Tooltip("Gravity multiplier while airborne from a trick jump")]
    public float trickJumpGravityMult = 0.75f;
    [Header("Grinding Options")]
    [Tooltip("How far to check for grinds from groundCheck's position")]
    public float grindCheckRadius = 0.5f;
    [Tooltip("Bonus to speed when starting a grind")]
    public float grindStartBonus = 5f;
    [Tooltip("Subtract from speed every FixedUpdate while grinding")]
    public float grindDrag = 0.1f;
    [Tooltip("Gravity multiplier while grinding")]
    public float grindGravityMult = 0.1f;
    [Tooltip("Maximum speed while grinding without boosting")]
    public float maxGrindSpeed = 11f;
    [Tooltip("Minimum speed while grinding")]
    public float minGrindSpeed = 2f;
    [Tooltip("Whether the player can grind")]
    public bool canGrind = true;
    [Header("BoostingOptions")]
    [Tooltip("Speed while boosting. Also used for the absolute maximum horizontal player speed.")]
    public float boostSpeed = 13f;

    private Rigidbody rb;
    private Vector2 move;
    private Vector2 lastMove = Vector2.zero;
    private Vector3 moveFlattened;
    private Vector3 localVelocity;
    private PathCreator curRail;
    private EndOfPathInstruction endOfPathInstruction;

    [HideInInspector] public float currentSpeed;
    private float jumpTimeCounter;
    private float curJumpForce;
    private float curAccel;
    private float drag;
    private float grindDist;
    private float smoothMovementTime;
    private bool canMove = true;
    private bool isGrounded = false;
    private bool isJumping = false;
    private bool isTrickJumping = false;
    private bool isGrinding = false;
    private bool isMovingBackward = false;
    private bool isMovingForward = false;
    private bool isChangingDirection = false;
    private bool canJumpMore = false;
    private bool grindingBackwards = false;

    void Awake()
    {
        smoothMovementTime = 1 / accelerationMultiplier;

        rb = GetComponent<Rigidbody>();
        drag = rb.drag;

        controls = new Controls();

        controls.Gameplay.Move.performed += ctx => move = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => move = Vector2.zero;

        controls.Gameplay.Jump.started += ctx => Jump();
        controls.Gameplay.Jump.canceled += ctx => isJumping = false;

        controls.Gameplay.CamReset.started += ctx => CamReset();
    }

    void OnEnable()
    {
        controls.Gameplay.Enable();
    }

    void FixedUpdate()
    {
        // The player is grinding if a circlecast to the groundcheck position hits anything designated as grindable
        // This can be done using layers instead but Sample Assets will not overwrite your project settings.
        Collider[] colliders = Physics.OverlapSphere(groundCheck.position, grindCheckRadius, whatIsGrindable);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].name != name && canGrind)
            {
                // Debug.Log("Grind found: " + colliders[i].name);
                if (!isGrinding)
                {
                    curRail = (colliders[i].GetComponent<PathCreator>() != null) ? colliders[i].GetComponent<PathCreator>() : colliders[i].transform.parent.GetComponent<PathCreator>();
                    grindDist = curRail.path.GetClosestDistanceAlongPath(transform.position);
                    endOfPathInstruction = curRail.GetComponent<GrindRail>().endOfPathInstruction;
                    currentSpeed += grindStartBonus;
                    
                    float angle = Vector3.Angle(transform.forward, curRail.path.GetDirectionAtDistance(grindDist, endOfPathInstruction));
                    grindingBackwards = (90f < angle);
                }
                isGrinding = true;
                break;
            }
            if (i == colliders.Length - 1)
                isGrinding = false;
        }
        transform.SetParent(isGrinding ? curRail.transform : null);
        animator.SetBool("Grind", isGrinding);
        rb.useGravity = !(isGrinding || isTrickJumping);

        colliders = Physics.OverlapSphere(groundCheck.position, groundCheckRadius, whatIsGround);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].name != name)
            {
                // Debug.Log("Ground found: " + colliders[i].name);
                isGrounded = true;
                isTrickJumping = false;
                break;
            }
            if (i == colliders.Length - 1)
            {
                isGrounded = false;
                if (isTrickJumping)
                {
                    rb.AddForce(Physics.gravity * Mathf.Pow(rb.mass, 2) * trickJumpGravityMult);
                }
            }
        }
        animator.SetBool("Ground", isGrounded);

        rb.drag = isGrounded ? drag : 0f;

        Move();
        Grind();

        // If the player continues jumping...
        if (canJumpMore && (isJumping || jumpTimeCounter < minJumpTime)) 
        {
            if (jumpTimeCounter < (isTrickJumping ? maxTrickJumpTime : maxJumpTime))
            {
                rb.AddForce(0f, curJumpForce, 0f, ForceMode.Impulse);
                jumpTimeCounter += Time.deltaTime;
                curJumpForce -= jumpDamper;
            }
            else 
                isJumping = false;
        }
        else 
            canJumpMore = isJumping = false;

        localVelocity = rb.transform.InverseTransformDirection(rb.velocity);
        localVelocity.x = 0f;                                               // Remove sideways speed
        localVelocity.y = Mathf.Max(localVelocity.y, -terminalVelocity);    // Limit falling speed
        localVelocity.z = Mathf.Min(currentSpeed, boostSpeed);              // Limit forward speed
        rb.velocity = rb.transform.TransformDirection(localVelocity);
        animator.SetFloat("Velocity", currentSpeed);
    }

    void Move()
    { 
        animator.SetFloat("LSMag", move.magnitude);

        isChangingDirection = move != lastMove;
        lastMove = move;

        if (!canMove || isGrinding)
        {
            return;
        }
        // StopCoroutine(Turn180());

        // Set movement speed, and its maximum and minimum values.
        moveFlattened = new Vector3(move.x, 0f, move.y);
   
        // Flatten camera rotation.
        Vector3 camForward = playerCamera.forward;
        camForward.y = 0f;
        Quaternion camRotationFlattened = Quaternion.LookRotation(camForward);
 
        // Make movement relative to camera.
        moveFlattened = camRotationFlattened * moveFlattened;

        if (move.magnitude < float.Epsilon)
        {
            isMovingBackward = isMovingForward = false;
            currentSpeed = (currentSpeed < 0f) ? 0f : currentSpeed - rb.drag*Time.fixedDeltaTime;
            return;
        }
 
        isMovingBackward = 150f < Vector3.Angle(moveFlattened, new Vector3(transform.forward.x, 0f, transform.forward.z));
        // Debug.Log(Vector3.Angle(VelocityAmount, new Vector3(transform.forward.x, 0f, transform.forward.z)));
        if (isMovingBackward)
        {
            if (isGrounded && !animator.GetBool("Turn"))
            {
                animator.SetBool("Turn", true);
            }
            else
            {
                currentSpeed = Mathf.SmoothDamp(currentSpeed, 0f, ref curAccel, smoothMovementTime * 10);
            }
            return;
        }

        
        isMovingForward = Vector3.Angle(moveFlattened, new Vector3(transform.forward.x, 0f, transform.forward.z)) < 20f;

        if (isMovingForward && animator.GetBool("Turn"))
        {
            rb.velocity = transform.forward * currentSpeed;
            animator.SetBool("Turn", false);
            return;
        }
        
        // Set rotation to movement direction.
        // Debug.Log("Turn " + transform.rotation + " to " + Quaternion.LookRotation(VelocityAmount));
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveFlattened), isGrounded ? groundTurnSpeed : airTurnSpeed);

        if(!isGrounded)
        {
            return;
        }
 
        // Update current speed.
        currentSpeed = maxGroundSpeed * move.magnitude;
        // if (Mathf.Abs(rb.velocity.x) + Mathf.Abs(rb.velocity.z) < currentSpeed + 1)
        //     rb.velocity = new Vector3(transform.forward.x * currentSpeed, 0f, transform.forward.z * currentSpeed);
        // else
        //     Debug.Log("Too fast: " + (Mathf.Abs(rb.velocity.x) + Mathf.Abs(rb.velocity.z)));
    }

    void Grind()
    {
        if (isGrinding && curRail != null && canGrind)
        {
            grindDist += currentSpeed * Time.fixedDeltaTime * (grindingBackwards ? -1 : 1);
            float t = grindDist / curRail.path.length;
            transform.position = curRail.path.GetPointAtTime(t, endOfPathInstruction);
            transform.rotation = curRail.path.GetRotation(t, endOfPathInstruction);
            if (grindingBackwards)
            {
                transform.RotateAround(transform.position, curRail.path.GetNormalAtDistance(grindDist, endOfPathInstruction), 180);
            }

            if (minGrindSpeed < currentSpeed)
            {
                currentSpeed = Mathf.Min(currentSpeed - grindDrag - transform.forward.y * grindGravityMult, maxGrindSpeed);
            }

            if (endOfPathInstruction == EndOfPathInstruction.Stop && ((!grindingBackwards && t >= 1f) || (grindingBackwards && t <= 0f)))
            {
                transform.SetParent(null);
                isGrinding = false;
                canGrind = false;
                StartCoroutine(Dismount());
            }
        }
    }

#if UNITY_EDITOR

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, grindCheckRadius);
    }

#endif
    
    public void Jump()
    {
        // If the player should jump...
        if (isGrounded || isGrinding)
        {
            animator.SetTrigger("Jump");
            JumpHelper();
        }
    }
    private void JumpHelper()
    {
        isTrickJumping = (trickJumpSpeed <= currentSpeed);
        isJumping = canJumpMore = true;
        isGrounded = false;
        // Add a vertical force to the player.
        curJumpForce = (isTrickJumping ? trickJumpForce : jumpForce);
        animator.SetBool("Ground", false);
        jumpTimeCounter = 0f;

        // If jumping from riding, preserve momentum and reset parent
        if (isGrinding)
        {
            // curJumpForce += currentSpeed * Mathf.Max(transform.parent.right.y, 0f);
            transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);
            transform.SetParent(null);
            isGrinding = false;
            canGrind = false;
            StartCoroutine(Dismount());
        }

        if (isTrickJumping)
        {
            currentSpeed -= trickJumpLoss;
            
        }
    }
    public IEnumerator<WaitForSeconds> Dismount()
    {
        yield return new WaitForSeconds(0.25f);

        canGrind = true;
    }

    public void CamReset()
    {
        StartCoroutine(CMFreeLook.GetComponent<SimpleFollowRecenter>().RevertCam());
        CMFreeLook.m_YAxis.Value = 0.5f;
    }

    public IEnumerator Turn180()
    {
        float deltaTime = Time.deltaTime;
        for (float i = 0f; i < 0.5f; i += deltaTime)
        {
            transform.Rotate(transform.up, 360f * deltaTime);
            if (isMovingForward)
            {
                Debug.Log("Cancel Turn");
                break;
            }
            yield return null;
        }
        if (move.magnitude < float.Epsilon)
        {
            currentSpeed = 0f;
            rb.velocity = Vector3.zero;
        }
        // Reset animator's rotation in case a turn animation would modify it
        animator.transform.localRotation = Quaternion.identity;
        animator.SetBool("Turn", false);
    }

    void OnDisable()
    {
        controls.Gameplay.Disable();
    }
}
