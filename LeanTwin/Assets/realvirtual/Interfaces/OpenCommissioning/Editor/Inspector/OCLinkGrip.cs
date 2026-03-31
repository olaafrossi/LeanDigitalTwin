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
    [CustomEditor(typeof(realvirtual.opencommissioning.OCLinkGrip), false), CanEditMultipleObjects]
    // ReSharper disable once InconsistentNaming
    public class OCLinkGrip : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var component = target as realvirtual.opencommissioning.OCLinkGrip;
            if (component == null) return null;
            
            var container = new VisualElement();

            var groupControl = new PropertyGroup("Control");
            groupControl.AddOverride(serializedObject);
            groupControl.Add(new OC.Editor.ToggleButton("Active").BindProperty(component.Active).AlignedField());
            
            var groupReferences = new PropertyGroup("References");
            groupReferences.Add(new PropertyField{bindingPath = "_grip"});

            container.Add(groupControl);
            container.Add(groupReferences);
            container.Add(new PropertyField{bindingPath = "_link"});
            
            return container;
        }
        
    }
}
