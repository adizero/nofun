using Nofun.Driver.Unity.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nofun
{
    public class TouchKeypadButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private InputDriver inputDriver;
        [SerializeField] private string key;

        private bool isPressed;

        public void Init(InputDriver driver, char keypadKey)
        {
            inputDriver = driver;
            key = keypadKey.ToString();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            isPressed = true;
            inputDriver?.SetKeypadKey(key[0], true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            if (!isPressed || string.IsNullOrEmpty(key))
            {
                return;
            }

            isPressed = false;
            inputDriver?.SetKeypadKey(key[0], false);
        }
    }
}
