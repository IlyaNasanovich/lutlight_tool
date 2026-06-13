using UnityEngine;
using UnityEngine.UIElements;

namespace LutLight2D
{
    [RequireComponent(typeof(UIDocument))]
    public class LutLightToolSetup : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private VisualTreeAsset _uxml;
        [SerializeField] private StyleSheet _uss;

        private void Awake()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            if (_uxml != null)
            {
                _uiDocument.visualTreeAsset = _uxml;
            }

            if (_uss != null)
            {
                _uiDocument.rootVisualElement.styleSheets.Add(_uss);
            }

            Debug.Log($"UIDocument visual tree: {_uiDocument.visualTreeAsset != null}");
            Debug.Log($"Root element child count: {_uiDocument.rootVisualElement.childCount}");
        }
    }
}
