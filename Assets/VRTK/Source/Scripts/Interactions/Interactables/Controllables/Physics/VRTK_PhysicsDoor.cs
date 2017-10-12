// Physics Door|PhysicsControllables|110030
namespace VRTK.Controllables.PhysicsBased
{
    using UnityEngine;
    using VRTK.GrabAttachMechanics;
    using VRTK.SecondaryControllerGrabActions;

    /// <summary>
    /// A physics based openable door.
    /// </summary>
    /// <remarks>
    /// **Required Components:**
    ///  * `Collider` - A Unity Collider to determine when an interaction has occured. Can be a compound collider set in child GameObjects. Will be automatically added at runtime.
    ///  * `Rigidbody` - A Unity Rigidbody to allow the GameObject to be affected by the Unity Physics System. Will be automatically added at runtime.
    ///
    /// **Optional Components:**
    ///  * `VRTK_ControllerRigidbodyActivator` - A Controller Rigidbody Activator to automatically enable the controller rigidbody when near the door. Will be automatically created if the `Auto Interaction` paramter is checked.
    /// 
    /// **Script Usage:**
    ///  * Place the `VRTK_PhysicsDoor` script onto the GameObject that is to become the door.
    ///  * Create a nested GameObject under the door GameObject and position it where the hinge should operate.
    ///  * Apply the nested hinge GameObject to the `Hinge Point` parameter on the Physics Door script.
    /// </remarks>
    [AddComponentMenu("VRTK/Scripts/Interactables/Controllables/Physics/VRTK_PhysicsDoor")]
    public class VRTK_PhysicsDoor : VRTK_BasePhysicsControllable
    {
        /// <summary>
        /// Type of Grab Mechanic
        /// </summary>
        public enum GrabMechanic
        {
            /// <summary>
            /// The Track Object Grab Mechanic
            /// </summary>
            TrackObject,
            /// <summary>
            /// The Rotator Track Grab Mechanic
            /// </summary>
            RotatorTrack
        }

        [Header("Handle Settings")]

        [Tooltip("The GameObject that represents the handle of the door.")]
        public GameObject handle;
        [Tooltip("If this is checked then only grabbing the specified handle will operate the door.")]
        public bool grabHandleOnly = false;

        [Header("Hinge Settings")]

        [Tooltip("A Transform that denotes the position where the door hinge will be created.")]
        public Transform hingePoint;
        [Tooltip("The minimum angle the door can swing to, will be translated into a negative angle.")]
        [Range(0f, 180f)]
        public float minimumAngle = 180f;
        [Tooltip("The maximum angle the door can swing to, will be considered a positive angle.")]
        [Range(0f, 180f)]
        public float maximumAngle = 180f;
        [Tooltip("The angle at which the door rotation can be within the minimum or maximum angle before the minimum or maximum angles are considered reached.")]
        public float minMaxThresholdAngle = 1f;
        [Tooltip("The angle at which will be considered as the resting position of the door.")]
        public float restingAngle = 0f;
        [Tooltip("The threshold angle from the `Resting Angle` that the current angle of the door needs to be within to snap the door back to the `Resting Angle`")]
        public float forceShutThresholdAngle = 1f;
        [Tooltip("If this is checked then the door will not be able to be moved.")]
        public bool isLocked = false;

        [Header("Grab Settings")]

        [Tooltip("The type of Interactable Object grab mechanic to use when operating the door.")]
        public GrabMechanic grabMechanic = GrabMechanic.RotatorTrack;
        [Tooltip("If this is checked then the `Grabbed Friction` value will be used as the Rigidbody drag value when the door is grabbed and the `Released Friction` value will be used as the Rigidbody drag value when the door is released.")]
        public bool useFrictionOverrides = false;
        [Tooltip("The Rigidbody drag value when the door is grabbed.")]
        public float grabbedFriction = 0f;
        [Tooltip("The Rigidbody drag value when the door is released.")]
        public float releasedFriction = 0f;

        protected VRTK_InteractableObject controlInteractableObject;
        protected VRTK_TrackObjectGrabAttach controlGrabAttach;
        protected VRTK_SwapControllerGrabAction controlSecondaryGrabAction;
        protected bool createControlInteractableObject;
        protected HingeJoint controlJoint;
        protected bool createControlJoint;
        protected RigidbodyConstraints savedConstraints;
        protected bool stillLocked;
        protected float previousValue;
        protected float previousRestingAngle;

        /// <summary>
        /// The GetValue method returns the current rotation value of the door.
        /// </summary>
        /// <returns>The actual rotation of the door.</returns>
        public override float GetValue()
        {
            float currentValue = transform.localEulerAngles[(int)operateAxis];
            return (currentValue > 180f ? currentValue - 360f : currentValue);
        }

        /// <summary>
        /// The GetNormalizedValue method returns the current rotation value of the door normalized between `0f` and `1f`.
        /// </summary>
        /// <returns>The normalized rotation of the door.</returns>
        public override float GetNormalizedValue()
        {
            return VRTK_SharedMethods.NormalizeValue(GetValue(), -minimumAngle, maximumAngle);
        }

        protected override void OnEnable()
        {
            excludeColliders = (handle != null ? handle.GetComponentsInChildren<Collider>() : new Collider[0]);
            base.OnEnable();
            savedConstraints = controlRigidbody.constraints;
            SetupInteractableObject();
            SetupJoint();
            stillLocked = false;
            CheckLock();
            previousRestingAngle = restingAngle;
            ManageRestingAngle();
            previousValue = GetValue();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (createControlInteractableObject)
            {
                ManageInteractableObjectListeners(false);
                Destroy(controlSecondaryGrabAction);
                Destroy(controlGrabAttach);
                Destroy(controlInteractableObject);
            }
            else
            {
                ManageInteractableObjectListeners(false);
            }
            if (createControlJoint)
            {
                Destroy(controlJoint);
            }
        }

        protected virtual void Update()
        {
            ManageSpringState();
            if (previousRestingAngle != restingAngle)
            {
                ManageRestingAngle();
            }
            previousRestingAngle = restingAngle;
            SetJointLimits();
            if (previousValue != GetValue())
            {
                EmitEvents();
            }
            previousValue = GetValue();
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (hingePoint != null)
            {
                Bounds doorBounds = VRTK_SharedMethods.GetBounds(transform, transform, (handle != null ? handle.transform : null));
                Vector3 limits = transform.rotation * ((AxisDirection() * doorBounds.size[(int)operateAxis]) * 0.53f);
                Vector3 hingeStart = hingePoint.transform.position - limits;
                Vector3 hingeEnd = hingePoint.transform.position + limits;
                Gizmos.DrawLine(hingeStart, hingeEnd);
                Gizmos.DrawSphere(hingeStart, 0.01f);
                Gizmos.DrawSphere(hingeEnd, 0.01f);
            }
        }

        protected virtual void SetupInteractableObject()
        {
            createControlInteractableObject = false;
            controlInteractableObject = GetComponent<VRTK_InteractableObject>();
            if (controlInteractableObject == null)
            {
                controlInteractableObject = gameObject.AddComponent<VRTK_InteractableObject>();
                createControlInteractableObject = true;
                controlInteractableObject.isGrabbable = true;
                controlInteractableObject.ignoredColliders = (grabHandleOnly && handle != null ? controlColliders : new Collider[0]);

                AddGrabMechanic();
                AddSecondaryAction();
                ManageInteractableObjectListeners(true);
            }
        }

        protected virtual void AddGrabMechanic()
        {
            switch (grabMechanic)
            {
                case GrabMechanic.TrackObject:
                    controlGrabAttach = controlInteractableObject.gameObject.AddComponent<VRTK_TrackObjectGrabAttach>();
                    controlGrabAttach.precisionGrab = false;
                    break;
                case GrabMechanic.RotatorTrack:
                    controlGrabAttach = controlInteractableObject.gameObject.AddComponent<VRTK_RotatorTrackGrabAttach>();
                    controlGrabAttach.precisionGrab = true;
                    break;
            }

            controlInteractableObject.grabAttachMechanicScript = controlGrabAttach;
        }

        protected virtual void AddSecondaryAction()
        {
            controlSecondaryGrabAction = controlInteractableObject.gameObject.AddComponent<VRTK_SwapControllerGrabAction>();
            controlInteractableObject.secondaryGrabActionScript = controlSecondaryGrabAction;
        }

        protected virtual void ManageInteractableObjectListeners(bool state)
        {
            if (controlInteractableObject != null)
            {
                if (state)
                {
                    controlInteractableObject.InteractableObjectTouched += InteractableObjectTouched;
                    controlInteractableObject.InteractableObjectUntouched += InteractableObjectUntouched;
                    controlInteractableObject.InteractableObjectGrabbed += InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed += InteractableObjectUngrabbed;
                }
                else
                {
                    controlInteractableObject.InteractableObjectTouched -= InteractableObjectTouched;
                    controlInteractableObject.InteractableObjectUntouched -= InteractableObjectUntouched;
                    controlInteractableObject.InteractableObjectGrabbed -= InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed -= InteractableObjectUngrabbed;
                }
            }
        }

        protected virtual void InteractableObjectTouched(object sender, InteractableObjectEventArgs e)
        {
            CheckLock();
            if (autoInteraction)
            {
                ManageSpring(false, restingAngle);
            }
        }

        protected virtual void InteractableObjectUntouched(object sender, InteractableObjectEventArgs e)
        {
            CheckLock();
        }

        protected virtual void InteractableObjectGrabbed(object sender, InteractableObjectEventArgs e)
        {
            ManageSpring(false, restingAngle);
            if (useFrictionOverrides && controlRigidbody != null)
            {
                controlRigidbody.drag = grabbedFriction;
            }
        }

        protected virtual void InteractableObjectUngrabbed(object sender, InteractableObjectEventArgs e)
        {
            if (useFrictionOverrides && controlRigidbody != null)
            {
                controlRigidbody.drag = releasedFriction;
            }
        }

        protected virtual void CheckLock()
        {
            if (controlRigidbody != null)
            {
                if (isLocked && !stillLocked)
                {
                    savedConstraints = controlRigidbody.constraints;
                    controlRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                    stillLocked = true;
                }
                else if (!isLocked && stillLocked)
                {
                    controlRigidbody.constraints = savedConstraints;
                    stillLocked = false;
                }
            }
        }

        protected virtual void ManageRestingAngle()
        {
            //If the current rotation isn't the same as the resting angle then reset to the resting angle
            ManageSpring(VRTK_SharedMethods.RoundFloat(GetValue(), 3) != VRTK_SharedMethods.RoundFloat(restingAngle, 3), restingAngle);
        }

        protected virtual void ManageSpringState()
        {
            if (controlInteractableObject != null && !controlInteractableObject.IsGrabbed() && (!autoInteraction || !controlInteractableObject.IsTouched()))
            {
                //If the current rotation angle is below the force shut angle then reset to the resting angle
                float absoluteCurrentValue = Mathf.Abs(GetValue());
                float absoluteRestingAngle = Mathf.Abs(restingAngle);
                if (absoluteCurrentValue > (absoluteRestingAngle - forceShutThresholdAngle) && absoluteCurrentValue < (absoluteRestingAngle + forceShutThresholdAngle))
                {
                    ManageSpring(true, restingAngle);
                }
            }
        }

        protected virtual void SetupJoint()
        {
            createControlJoint = false;
            controlJoint = GetComponent<HingeJoint>();
            if (controlJoint == null && hingePoint != null)
            {
                controlJoint = gameObject.AddComponent<HingeJoint>();
                createControlJoint = true;
                controlJoint.axis = AxisDirection();
                controlJoint.connectedBody = connectedTo;
                hingePoint.SetParent(transform);
                controlJoint.anchor = (hingePoint != null ? hingePoint.localPosition : Vector3.zero);
                controlJoint.useLimits = true;
                SetJointLimits();
            }
        }

        protected virtual void SetJointLimits()
        {
            if (controlJoint != null)
            {
                JointLimits controlJointLimits = new JointLimits();
                controlJointLimits.min = -minimumAngle;
                controlJointLimits.max = maximumAngle;
                controlJoint.limits = controlJointLimits;
            }
        }

        protected virtual void ManageSpring(bool activate, float springTarget)
        {
            if (controlJoint != null)
            {
                controlJoint.useSpring = activate;
                JointSpring controlJointSpring = new JointSpring();
                controlJointSpring.spring = 100f;
                controlJointSpring.damper = 10f;
                controlJointSpring.targetPosition = springTarget;
                controlJoint.spring = controlJointSpring;
            }
        }

        protected virtual void EmitEvents()
        {
            ControllableEventArgs payload = EventPayload();
            float currentAngle = GetValue();
            OnValueChanged(payload);

            bool atMaxAngle = ((maximumAngle > 0f && (currentAngle >= (maximumAngle - minMaxThresholdAngle))) || (minimumAngle > 0f && (currentAngle <= (-minimumAngle + minMaxThresholdAngle))));
            bool atMinAngle = ((currentAngle <= (restingAngle + minMaxThresholdAngle)) && (currentAngle >= (restingAngle - minMaxThresholdAngle)));

            if (atMaxAngle && !AtMaxLimit())
            {
                atMaxLimit = true;
                OnMaxLimitReached(payload);
            }
            else if (atMinAngle && !AtMinLimit())
            {
                atMinLimit = true;
                OnMinLimitReached(payload);
            }
            else if (!atMaxAngle && !atMinAngle)
            {
                if (AtMinLimit())
                {
                    OnMinLimitExited(payload);
                }
                if (AtMaxLimit())
                {
                    OnMaxLimitExited(payload);
                }
                atMinLimit = false;
                atMaxLimit = false;
            }
        }
    }
}