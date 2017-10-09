// Base Physics Controllable|ArtificialControllables|120010
namespace VRTK.Controllables.ArtificialBased
{
    using UnityEngine;
    using System.Collections;

    /// <summary>
    /// An artificially simulated pushable button.
    /// </summary>
    /// <remarks>
    /// **Required Components:**
    ///  * `Collider` - A Unity Collider to determine when an interaction has occured. Can be a compound collider set in child GameObjects. Will be automatically added at runtime.
    ///
    /// **Script Usage:**
    ///  * Place the `VRTK_PhysicsButton` script onto the GameObject that is to become the button.
    /// </remarks>
    [AddComponentMenu("VRTK/Scripts/Interactables/Controllables/Artificial/VRTK_ArtificialButton")]
    public class VRTK_ArtificialButton : VRTK_BaseControllable
    {
        [Header("Button Settings")]

        [Tooltip("The speed in which the button moves towards to the `Pressed Distance` position.")]
        public float pressSpeed = 10f;
        [Tooltip("The distance along the `Operate Axis` until the button reaches the pressed position.")]
        public float pressedDistance = 0.1f;
        [Range(0f, 1f)]
        [Tooltip("The threshold in which the button's current normalized position along the `Operate Axis` has to be within the pressed normalized position for the button to be considered pressed.")]
        public float pressedThreshold = 0f;
        [Tooltip("If this is checked then the button will stay in the pressed position when it reaches the pressed position.")]
        [SerializeField]
        protected bool stayPressed = false;
        [Tooltip("The position of the button between the original position and the pressed position. `0f` will set the button position to the original position, `1f` will set the button position to the pressed position.")]
        [Range(0f, 1f)]
        [SerializeField]
        protected float positionTarget = 0f;
        [Tooltip("The speed in which the button will return to the `Target Position` of the button.")]
        public float returnSpeed = 10f;

        protected Coroutine positionLerpRoutine;
        protected float vectorEqualityThreshold = 0.001f;
        protected bool isPressed = false;
        protected bool isMoving = false;
        protected bool isTouched = false;

        /// <summary>
        /// The GetValue method returns the current position value of the button.
        /// </summary>
        /// <returns>The actual position of the button.</returns>
        public override float GetValue()
        {
            return transform.localPosition[(int)operateAxis];
        }

        /// <summary>
        /// The GetNormalizedValue method returns the current position value of the button normalized between `0f` and `1f`.
        /// </summary>
        /// <returns>The normalized position of the button.</returns>
        public override float GetNormalizedValue()
        {
            return VRTK_SharedMethods.NormalizeValue(GetValue(), originalLocalPosition[(int)operateAxis], PressedPosition()[(int)operateAxis]);
        }

        /// <summary>
        /// The SetStayPressed method sets the `Stay Pressed` parameter to the given state and if the state is false and the button is currently pressed then it is reset to the original position.
        /// </summary>
        /// <param name="state">The state to set the `Stay Pressed` parameter to.</param>
        public virtual void SetStayPressed(bool state)
        {
            stayPressed = state;
            if (!stayPressed && AtPressedPosition())
            {
                ReturnToOrigin();
            }
        }

        /// <summary>
        /// The SetPositionTarget method sets the `Position Target` parameter to the given normalized value.
        /// </summary>
        /// <param name="normalizedTarget">The `Position Target` to set the button to between `0f` and `1f`.</param>
        public virtual void SetPositionTarget(float normalizedTarget)
        {
            positionTarget = Mathf.Clamp01(normalizedTarget);
            SetTargetPosition();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            isPressed = false;
            isMoving = false;
            isTouched = false;
            SetTargetPosition();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CancelPositionLerp();
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Vector3 objectHalf = AxisDirection(true) * (transform.lossyScale[(int)operateAxis] * 0.5f);
            Vector3 initialPoint = transform.position + (objectHalf * Mathf.Sign(pressedDistance));
            Vector3 destinationPoint = initialPoint + (AxisDirection(true) * pressedDistance);
            Gizmos.DrawLine(initialPoint, destinationPoint);
            Gizmos.DrawSphere(destinationPoint, 0.01f);
        }

        protected override void OnTouched(Collider collider)
        {
            if (!VRTK_PlayerObject.IsPlayerObject(collider.gameObject) || VRTK_PlayerObject.IsPlayerObject(collider.gameObject, VRTK_PlayerObject.ObjectTypes.Controller))
            {
                base.OnTouched(collider);

                if (!isMoving)
                {
                    Vector3 targetPosition = (!stayPressed && AtPressedPosition() ? originalLocalPosition : PressedPosition());
                    float targetSpeed = (!stayPressed && AtPressedPosition() ? returnSpeed : pressSpeed);
                    if (!AtTargetPosition(targetPosition))
                    {
                        positionLerpRoutine = StartCoroutine(PositionLerp(targetPosition, targetSpeed));
                    }
                }
                isTouched = true;
            }
        }

        protected override void OnUntouched(Collider collider)
        {
            isTouched = false;
        }

        protected virtual void SetTargetPosition()
        {
            transform.localPosition = Vector3.Lerp(originalLocalPosition, PressedPosition(), positionTarget);
        }

        protected virtual Vector3 PressedPosition()
        {
            return originalLocalPosition + (AxisDirection(true) * pressedDistance);
        }

        protected virtual void CancelPositionLerp()
        {
            if (positionLerpRoutine != null)
            {
                StopCoroutine(positionLerpRoutine);
            }
            positionLerpRoutine = null;
        }

        protected virtual IEnumerator PositionLerp(Vector3 targetPosition, float moveSpeed)
        {
            while (!VRTK_SharedMethods.Vector3ShallowCompare(transform.localPosition, targetPosition, vectorEqualityThreshold))
            {
                yield return null;
                isMoving = true;
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, moveSpeed * Time.deltaTime);
                CheckEvents();
            }
            transform.localPosition = targetPosition;
            isMoving = false;
            CheckEvents();

            ManageAtPressedPosition();
            ManageAtOriginPosition();
        }

        protected virtual void ManageAtPressedPosition()
        {
            if (AtPressedPosition())
            {
                if (stayPressed)
                {
                    ResetInteractor();
                }
                else
                {
                    ReturnToOrigin();
                }
            }
        }

        protected virtual void ManageAtOriginPosition()
        {
            if (AtOriginPosition() && isTouched == false)
            {
                ResetInteractor();
            }
        }

        protected virtual bool AtOriginPosition()
        {
            return VRTK_SharedMethods.Vector3ShallowCompare(transform.localPosition, originalLocalPosition, vectorEqualityThreshold);
        }

        protected virtual bool AtPressedPosition()
        {
            return VRTK_SharedMethods.Vector3ShallowCompare(transform.localPosition, PressedPosition(), vectorEqualityThreshold);
        }

        public virtual bool AtTargetPosition(Vector3 targetPosition)
        {
            return VRTK_SharedMethods.Vector3ShallowCompare(transform.localPosition, targetPosition, vectorEqualityThreshold);
        }

        protected virtual void ResetInteractor()
        {
            interactingCollider = null;
            interactingTouchScript = null;
        }

        protected virtual void ReturnToOrigin()
        {
            positionLerpRoutine = StartCoroutine(PositionLerp(originalLocalPosition, returnSpeed));
        }

        protected virtual void CheckEvents()
        {
            float currentPosition = GetNormalizedValue();
            ControllableEventArgs payload = EventPayload();
            OnValueChanged(EventPayload());

            if (currentPosition >= (1f - pressedThreshold) && !AtMaxLimit())
            {
                atMaxLimit = true;
                OnMaxLimitReached(payload);
            }
            else if (currentPosition <= (0f + pressedThreshold) && !AtMinLimit())
            {
                atMinLimit = true;
                OnMinLimitReached(payload);
            }
            else if (currentPosition > pressedThreshold && currentPosition < (1f - pressedThreshold))
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