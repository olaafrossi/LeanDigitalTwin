// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz
// Integration with OpenCommissioning (https://github.com/OpenCommissioning/OC_Unity_Core)
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using OC.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using OC.VisualElements;

namespace realvirtual.opencommissioning.Editor
{
    [CustomEditor(typeof(realvirtual.opencommissioning.OCLinkSensor), false), CanEditMultipleObjects]
    // ReSharper disable once InconsistentNaming
    public class OCLinkSensor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var component = target as realvirtual.opencommissioning.OCLinkSensor;
            if (component == null) return null;
            
            var container = new VisualElement();

            var groupControl = new PropertyGroup("Control");
            groupControl.AddOverride(serializedObject);
            groupControl.Add(new IntegerField("Target"){bindingPath = "_target._value"}.AlignedField());
            groupControl.Add(new OC.Editor.ToggleButton("Signal"){bindingPath = "_signal._value"}.AlignedField());
            
            var groupStatus = new PropertyGroup("Status");
            groupStatus.Add(new LampField("Value", Color.green).BindProperty(component.Value).AlignedField());
            
            var groupSettings = new PropertyGroup("Settings");
            groupSettings.Add(new PropertyField{bindingPath = "_sensor"});

            var groupEvents = new PropertyGroup("Events");
            groupEvents.Add(new PropertyField{bindingPath = "OnValueChangedEvent"});

            container.Add(groupControl);
            container.Add(groupStatus);
            container.Add(groupSettings);
            container.Add(new PropertyField{bindingPath = "_link"});
            container.Add(groupEvents);
            
            return container;
        }
        
    }
}
