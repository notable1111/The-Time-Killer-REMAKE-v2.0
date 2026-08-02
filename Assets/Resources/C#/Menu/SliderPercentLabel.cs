// Shows a slider's value as a whole percentage next to it.
//
// A bar alone tells you "somewhere near the middle". A number tells you 60, which
// is the difference between a player being able to come back and set it the same
// way tomorrow and not. Cheap, and the only reason it is a component at all is so
// the pause menu's setup script can wire it without a bespoke listener.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Menu
{
    public class SliderPercentLabel : MonoBehaviour
    {
        [SerializeField] Slider slider;
        [SerializeField] TMP_Text label;

        void Reset() => label = GetComponent<TMP_Text>();

        void OnEnable()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (slider == null) return;
            slider.onValueChanged.AddListener(Show);
            Show(slider.value);
        }

        void OnDisable()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(Show);
        }

        void Show(float value)
        {
            if (label != null) label.text = Mathf.RoundToInt(value * 100f).ToString();
        }
    }
}
