using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top-Left In-Game Button Handler that opens the Level Selection Modal.
/// </summary>
public class LevelSwitcher : MonoBehaviour
{
    private void Start()
    {
        UnityEngine.UI.Button btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OpenMapMenu);
        }
    }

    public void OpenMapMenu()
    {
        if (MapSelectMenu.Instance != null)
        {
            MapSelectMenu.Instance.ToggleMenu();
        }
    }
}
