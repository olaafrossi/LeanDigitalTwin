// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz
// Integration with OpenCommissioning (https://github.com/OpenCommissioning/OC_Unity_Core)
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using OC.Editor;
using OC.VisualElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace realvirtual.opencommissioning.Editor
{
    [CustomEditor(typeof(realvirtual.opencommissioning.OCLinkCylinder), false), CanEditMultipleObjects]
    // ReSharper disable once InconsistentNaming
    public class OCLinkCylinder : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var component = target as realvirtual.opencommissioning.OCLinkCylinder;
            if (component == null) return null;
            
            var container = new VisualElement();

            var groupControl = new PropertyGroup("Control");
            groupControl.AddOverride(serializedObject);
            var hStack = new StackHorizontal();
            hStack.Add(new PushButton("Minus"){bindingPath = "_minus._value"});
            hStack.Add(new PushButton("Plus"){bindingPath = "_plus._value"});
            groupControl.Add(hStack);

            var groupStatus = new PropertyGroup("Status");
            groupStatus.Add(new OC.Editor.ProgressBar("Progress"){bindingPath = "_progress._value", ShowLimits = true});
            groupStatus.Add(new FloatField("Value"){isReadOnly = true, bindingPath = "_value._value"}.AlignedField());
            
            var groupReferences = new PropertyGroup("References");
            groupReferences.Add(new PropertyField{bindingPath = "_drive"});
            
            var groupSettings = new PropertyGroup("Settings");
            groupSettings.Add(new Vector2Field("Limits"){bindingPath = "_limits._value"}.AlignedField());
            groupSettings.Add(new EnumField("Type"){bindingPath = "_type._value"}.AlignedField());
            groupSettings.Add(new FloatField("Time to Min"){bindingPath = "_timeToMin._value"}.AlignedField());
            groupSettings.Add(new FloatField("Time to Max"){bindingPath = "_timeToMax._value"}.AlignedField());
            groupSettings.Add(new PropertyField{bindingPath = "_profile"});

            var groupEvents = new PropertyGroup("Events");
            groupEvents.Add(new PropertyField{bindingPath = "OnActiveChanged"});
            groupEvents.Add(new PropertyField{bindingPath = "OnLimitMinEvent"});
            groupEvents.Add(new PropertyField{bindingPath = "OnLimitMaxEvent"});
            groupEvents.Add(new PropertyField{bindingPath = "OnValueChanged"});
            groupEvents.Add(new PropertyField{bindingPath = "OnProgressChanged"});
            
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
