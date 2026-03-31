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
    //! Links an OpenCommissioning device to a realvirtual Lamp component for visual indication control.
    //! This component bridges OpenCommissioning's device control with realvirtual's lamp system,
    //! automatically synchronizing lamp state based on OC control signals.
    [AddComponentMenu("realvirtual/OpenCommissioning/OC Link Lamp")]
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Lamp))]
    // ReSharper disable once InconsistentNaming
    public class OCLinkLamp : MonoComponent, IDevice
    {
        public IProperty<bool> Override => _override;
        public IProperty<bool> Active => _active;
        public Link Link => _link;
        
        [SerializeField]
        protected Property<bool> _override = new (false);
        [SerializeField]
        private Property<bool> _active = new (false);
        
        [SerializeField]
        private Lamp _lamp;
        
        [SerializeField]
        private LinkDataByte _link = new("FB_Lamp");
        
        private void OnEnable()
        {
            _active.Subscribe(ValueChanged);
        }

        private void OnDisable()
        {
            _active.Unsubscribe(ValueChanged);
        }
        
        private void Start()
        {
            Link.Initialize(this);
        }

        private void Reset()
        {
            TryGetComponent(out _lamp);
            _link = new LinkDataByte("FB_Lamp");
        }

        private void ValueChanged(bool value)
        {
            if(_lamp == null) return;
            _lamp.LampOn = value;
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
