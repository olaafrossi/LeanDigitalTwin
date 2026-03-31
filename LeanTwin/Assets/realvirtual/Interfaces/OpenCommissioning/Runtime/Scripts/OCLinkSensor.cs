// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz
// Integration with OpenCommissioning (https://github.com/OpenCommissioning/OC_Unity_Core)

using OC;
using OC.Communication;
using OC.Components;
using UnityEngine;

namespace realvirtual.opencommissioning
{
    //! Links an OpenCommissioning device to a realvirtual Sensor component for sensor signal communication.
    //! This component bridges realvirtual's sensor system with OpenCommissioning's device control,
    //! automatically synchronizing sensor occupation state to OC status signals.
    [AddComponentMenu("realvirtual/OpenCommissioning/OC Link Sensor")]
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(realvirtual.Sensor))]
    // ReSharper disable once InconsistentNaming
    public class OCLinkSensor : MonoComponent, IDevice
    {
        public Link Link => _link;
        public IProperty<bool> Override => _override;
        public IPropertyReadOnly<bool> Value => _value;
        public IProperty<bool> Signal => _signal;
        
        [SerializeField]
        protected Property<bool> _override = new (false);
        [SerializeField]
        protected Property<bool> _value = new (false);
        [SerializeField]
        protected Property<bool> _signal = new (false);
        [SerializeField]
        private realvirtual.Sensor _sensor;
        [SerializeField]
        private Link _link;
        
        private void OnEnable()
        {
            _signal.OnValueChanged += OnSignalChanged;
            _value.OnValueChanged += OnValueChanged;
        }

        private void OnDisable()
        {
            _signal.OnValueChanged -= OnSignalChanged;
            _value.OnValueChanged -= OnValueChanged;
        }
        
        private void Start()
        {
            _link.Initialize(this);
            OnValueChanged(_value.Value);
        }
        
        private void Reset()
        {
            TryGetComponent(out _sensor);
            _link = new Link
            {
                Type = "FB_SensorBinary"
            };
        }

        private void FixedUpdate()
        {
            if (_sensor == null) return;
            if (_override) return;
            _signal.Value = _sensor.Occupied;
        }

        private void OnSignalChanged(bool value)
        {
            _value.Value = value;
        }
        
        private void OnValueChanged(bool value)
        {
            _link.Status.SetBit(0, value);
        }
    }
}
