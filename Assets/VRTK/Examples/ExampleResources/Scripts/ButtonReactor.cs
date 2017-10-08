namespace VRTK.Examples
{
    using UnityEngine;
    using VRTK.Controllables.PhysicsBased;

    public class ButtonReactor : MonoBehaviour
    {
        protected VRTK_PhysicsButton buttonEvents;

        protected virtual void OnEnable()
        {
            buttonEvents = GetComponent<VRTK_PhysicsButton>();
            if (buttonEvents != null)
            {
                buttonEvents.MaxLimitReached += MaxLimitReached;
            }
        }

        protected virtual void OnDisable()
        {
            if (buttonEvents != null)
            {
                buttonEvents.MaxLimitReached -= MaxLimitReached;
            }
        }

        private void MaxLimitReached(object sender, Controllables.ControllableEventArgs e)
        {
            VRTK_PhysicsButton senderButton = sender as VRTK_PhysicsButton;
            VRTK_Logger.Info(senderButton.name + " was pushed");
        }
    }
}