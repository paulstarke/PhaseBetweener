using UnityEngine;
using UnityEngine.UI;


public class BetweeningDemo_UI : MonoBehaviour {

    public BinaryButton ShowUIElements;
    public BinaryButton ShowDebugLines;
    public Button ChangeClip;
    public Text AssetText;
    public Slider DurationTime;
    public Text SliderValue;
    public InBetweeningController Controller;
    
    private Color InactiveColor = new Color(150f/255f, 150f/255f, 150f/255f);
    private Color ActiveColor = new Color(250f/255f, 180f/255f, 0f);
    private Color InactiveTextColor = new Color(0.8f, 0.8f, 0.8f);
    private Color ActiveTextColor = Color.white;

    #if UNITY_EDITOR
    public bool EnablePausing = true;
    #endif

    [System.Serializable]
    public class BinaryButton {
        public bool State;
        public Button Button;
    }

    void Start() {
        SetState(ShowUIElements, Controller.DrawGUI);
        SetState(ShowDebugLines, Controller.DrawDebug);
        SliderValue.text = Controller.SamplingOffset.ToString("0.0") + "s";
    }

    #if UNITY_EDITOR
    void Update() {
        if(EnablePausing && Input.GetKeyDown(KeyCode.Escape)) {
            UnityEditor.EditorApplication.isPaused = true;
        }
    }
    #endif

    public void Callback(Button button) {
        if(button == ShowUIElements.Button) {
            ToggleState(ShowUIElements);
        }
        if(button == ShowDebugLines.Button) {
            ToggleState(ShowDebugLines);
        }
        if(button == ChangeClip) {
            Controller.ChangeClip();
            AssetText.text = Controller.Asset.GetName();
        }
    }

/*     public void SetTransitionStyle(Dropdown dropdown) {
        Controller.ActiveStyle = (InBetweeningController.STYLE)dropdown.value;
    } */

    public void SetBetweeningTime(Slider slider) {
        SliderValue.text = slider.value.ToString("0.0") + "s";
        Controller.SamplingOffset = slider.value;
    }

    private void ToggleState(BinaryButton button) {
        SetState(button, !button.State);
    }

    private void SetState(BinaryButton button, bool state) {
        Image image = button.Button.GetComponent<Image>();
        image.color = state ? ActiveColor : InactiveColor;
        Text text = button.Button.GetComponentInChildren<Text>();
        text.color = state ? ActiveTextColor : InactiveTextColor;
        button.State = state;
        if(button == ShowUIElements) {
            Controller.DrawGUI = button.State;
            Controller.Camera.GetComponent<CameraController>().ShowGUI = button.State;
        }
        if(button == ShowDebugLines) {
            Controller.DrawDebug = button.State;
            Controller.ShowBiDirectional = button.State;
        }
    }
}
