using System;
using realvirtual;
using UnityEngine;
using UnityEngine.UI;

public class rvUIModeButton : rvUIContainer
{
    [NaughtyAttributes.ReadOnly] public rvUIMenuButton activeModeButton;

    

    public override void RefreshLayout()
    {
        
    }

    private void OnValidate()
    {
        MakeFirstButtonActive();
    }
    
    
    void MakeFirstButtonActive()
    {
        rvUIMenuButton button = GetComponent<rvUIMenuButton>();
        
        rvUIToggleGroup toggleGroup = button.GetComponentInChildren<rvUIToggleGroup>(true);
        if (toggleGroup.buttons.Count > 0)
        {
            rvUIMenuButton firstButton = toggleGroup.buttons[0];
            firstButton.ToggleOn();
            
            // Set this button's icon to the active mode icon
            button.icon.CopyFromOther(firstButton.icon);
        }
    }
    
    

    public void SwitchIconToActiveMode()
    {
        rvUIMenuButton button = GetComponent<rvUIMenuButton>();
        
        rvUIToggleGroup toggleGroup = button.GetComponentInChildren<rvUIToggleGroup>(true);
        foreach (var btn in toggleGroup.buttons)
        {
            
            if (btn.IsOn())
            {
                // Set this button's icon to the active mode icon
                button.icon.CopyFromOther(btn.icon);
                break;
            }
        }
        
    }


    public override RectTransform GetContentRoot()
    {
        // Try to find the toggle group container
        rvUIMenuButton button = GetComponent<rvUIMenuButton>();
        if (button != null)
        {
            rvUIToggleGroup toggleGroup = button.GetComponentInChildren<rvUIToggleGroup>(true);
            if (toggleGroup != null)
            {
                return toggleGroup.GetComponent<RectTransform>();
            }
        }

        // Fallback to this component's RectTransform
        return GetComponent<RectTransform>();
    }
}
