// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz
// Integration with OpenCommissioning (https://github.com/OpenCommissioning/OC_Unity_Core)

using OC.Components;
using UnityEngine;

namespace realvirtual.opencommissioning
{
    //! Links an OpenCommissioning DrivePosition component to a realvirtual Drive for position-based motion control.
    //! This component bridges OpenCommissioning's position control with realvirtual's drive system,
    //! automatically synchronizing position values from the OC DrivePosition to the Drive component.
    [AddComponentMenu("realvirtual/OpenCommissioning/OC Link Drive Position")]
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(realvirtual.Drive))]
    // ReSharper disable once InconsistentNaming
    public class OCLinkDrivePosition : DrivePosition
    {
        [SerializeField]
        private realvirtual.Drive _drive;

        protected void Reset()
        {
            TryGetComponent(out _drive);
        }

        protected new void FixedUpdate()
        {
            base.FixedUpdate();
            if (_drive != null) _drive.SetPosition(_value.Value);
        }
    }
}


