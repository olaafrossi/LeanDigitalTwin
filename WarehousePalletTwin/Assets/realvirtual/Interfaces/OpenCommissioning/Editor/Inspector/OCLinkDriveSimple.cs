// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz
// Integration with OpenCommissioning (https://github.com/OpenCommissioning/OC_Unity_Core)
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using OC.Editor;
using OC.VisualElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace realvirtual.opencommissioning.Editor
{
    [CustomEditor(typeof(realvirtual.opencommissioning.OCLinkDriveSimple), false), CanEditMultipleObjects]
    // ReSharper disable once InconsistentNaming
    public class OCLinkDriveSimple : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var component = target as realvirtual.opencommissioning.OCLinkDriveSimple;
            if (component == null) return null;
            
            var container = new VisualElement();

            var groupControl = new PropertyGroup("Control");
            groupControl.AddOverride(serializedObject);
            var hStack = new StackHorizontal();
            hStack.Add(new OC.Editor.ToggleButton("Backward").BindProperty(component.Backward));
            hStack.Add(new OC.Editor.ToggleButton("Forward").BindProperty(component.Forward));
            groupControl.Add(hStack);

            var groupStatus = new PropertyGroup("Status");
            groupStatus.Add(new LampField("Is Active", Color.green){bindingPath = "_stateObserver._isActive._value"}.AlignedField());
            groupStatus.Add(new FloatField("Value"){isReadOnly = true, bindingPath = "_value._value"}.AlignedField());
            
            var groupReferences = new PropertyGroup("References");
            groupReferences.Add(new PropertyField{bindingPath = "_drive"});
            
            var groupSettings = new PropertyGroup("Settings");
            groupSettings.Add(new FloatField("Speed"){bindingPath = "_speed._value"}.AlignedField());
            groupSettings.Add(new FloatField("Acceleration"){bindingPath = "_acceleration._value"}.AlignedField());

            var groupEvents = new PropertyGroup("Events");
            groupEvents.Add(new PropertyField{bindingPath = "OnActiveChanged"});
            groupEvents.Add(new PropertyField{bindingPath = "OnValueChanged"});
            
            container.Add(groupControl);
            container.Add(groupStatus);
            container.Add(groupReferences);
            container.Add(groupSettings);
            container.Add(new PropertyField{bindingPath = "_link"});
            container.Add(groupEvents);
            
            return container;
        }
    }
}
