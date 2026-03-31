// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz
// Integration with OpenCommissioning (https://github.com/OpenCommissioning/OC_Unity_Core)

using OC;
using OC.Communication;
using OC.Components;
using realvirtual;
using UnityEngine;

namespace realvirtual.opencommissioning
{
    //! Links an OpenCommissioning device to a realvirtual Grip component for pick and place operations.
    //! This component bridges OpenCommissioning's device control with realvirtual's grip system,
    //! automatically synchronizing grip state and controlling pick/place actions based on OC signals.
    [AddComponentMenu("realvirtual/OpenCommissioning/OC Link Grip")]
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Grip))]
    // ReSharper disable once InconsistentNaming
    public class OCLinkGrip : MonoComponent, IDevice
    {
        public IProperty<bool> Override => _override;
        public IProperty<bool> Active => _active;
        public Link Link => _link;
        
        [SerializeField]
        protected Property<bool> _override = new (false);
        [SerializeField]
        private Property<bool> _active = new (false);
        [SerializeField]
        private Grip _grip;
        [SerializeField]
        private LinkDataByte _link = new("FB_DeviceByte");

        private void OnEnable()
        {
            _active.Subscribe(GripChanged);
        }

        private void OnDisable()
        {
            _active.Unsubscribe(GripChanged);
        }
        
        private void Start()
        {
            Link.Initialize(this);
        }

        private void Reset()
        {
            TryGetComponent(out _grip);
            _link = new LinkDataByte("FB_DeviceByte");
        }

        private void GripChanged(bool value)
        {
            if(_grip == null) return;
            
            if (value)
            {
                _grip.Pick();
            }
            else
            {
                _grip.Place();
            }
        }

        private void FixedUpdate()
        {
            GetLinkData();
        }
        
        private void GetLinkData()
        {
            if (_override || !_link.Connected) return;
            _active.Value = _link.Control.GetBit(0);
        }
    }
}
