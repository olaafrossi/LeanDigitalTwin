// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_6000_0_OR_NEWER || !UNITY_RENDER_PIPELINE_UNIVERSAL
using NaughtyAttributes;
#endif

namespace realvirtual.RendererFeatures
{
    [ExecuteInEditMode]
    public class OutlineSelectionManager :
#if UNITY_6000_0_OR_NEWER && UNITY_RENDER_PIPELINE_UNIVERSAL
        AbstractSelectionManager
#else
        MonoBehaviour
#endif
    {
#if UNITY_6000_0_OR_NEWER && UNITY_RENDER_PIPELINE_UNIVERSAL
        [SerializeField] private BlurredBufferMultiObjectOutlineRendererFeature outlineRendererFeature;
        [SerializeField] private List<Renderer> selectedRenderers = new List<Renderer>();
        
        [SerializeField] public Color color = Color.white;
        [SerializeField] public int spread = 15;
        
        private bool needsDelayedInit = false;

        [ContextMenu("Reassign Renderers Now")]
        private void OnValidate()
        {
            #if UNITY_EDITOR
            // CRITICAL: Never perform renderer feature operations in OnValidate during serialization
            // This can cause Unity to crash during domain reload or scene loading
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && 
                !UnityEditor.EditorApplication.isCompiling &&
                !UnityEditor.EditorApplication.isUpdating)
            {
                // Only set the flag, never apply settings directly in OnValidate
                needsDelayedInit = true;
            }
            #endif
        }

        private void Awake()
        {
            needsDelayedInit = true;
        }
        
        void OnEnable()
        {
            needsDelayedInit = true;
        }
        
        void Start()
        {
            if (needsDelayedInit)
            {
                ApplySettingsToRendererFeature();
                needsDelayedInit = false;
            }
        }
        
        void Update()
        {
            if (needsDelayedInit)
            {
                ApplySettingsToRendererFeature();
                needsDelayedInit = false;
            }
            
            #if UNITY_EDITOR
            // In editor, periodically check if we need to update (safer than delayCall)
            if (!Application.isPlaying && needsDelayedInit)
            {
                ApplySettingsToRendererFeature();
                needsDelayedInit = false;
            }
            #endif
        }
        
        void ApplySettingsToRendererFeature()
        {
            // Extra safety checks to prevent crashes
            if (outlineRendererFeature == null)
                return;
            
            #if UNITY_EDITOR
            // Additional safety check for editor - don't apply during unsafe times
            if (!Application.isPlaying && 
                (UnityEditor.EditorApplication.isCompiling || 
                 UnityEditor.EditorApplication.isUpdating))
            {
                return;
            }
            #endif
                
            try
            {
                // Only apply settings if we have renderers selected or in edit mode
                // This prevents empty managers from overriding active ones
                if (selectedRenderers != null && (selectedRenderers.Count > 0 || !Application.isPlaying))
                {
                    // Store values in the renderer feature's fields directly
                    outlineRendererFeature.color = color;
                    outlineRendererFeature.spread = spread;
                    
                    // Then apply them through the setters
                    outlineRendererFeature.SetColor(color);
                    outlineRendererFeature.SetSpread(spread);
                }
                
                // Always update the renderer list
                if (selectedRenderers != null)
                {
                    outlineRendererFeature.SetRenderers(selectedRenderers.ToArray());
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error applying settings to renderer feature: {e.Message}", this);
            }
        }

        public override void Select(Renderer renderer)
        {
            if (selectedRenderers.Contains(renderer))
            {
                return;
            }

            selectedRenderers.Add(renderer);
            
            // Apply our settings when we become active
            if (outlineRendererFeature != null)
            {
                outlineRendererFeature.color = color;
                outlineRendererFeature.spread = spread;
                outlineRendererFeature.SetColor(color);
                outlineRendererFeature.SetSpread(spread);
                outlineRendererFeature.SetRenderers(selectedRenderers.ToArray());
            }
        }

        public override void Deselect(Renderer renderer)
        {
            if (!selectedRenderers.Contains(renderer))
            {
                return;
            }

            selectedRenderers.Remove(renderer);
            outlineRendererFeature.SetRenderers(selectedRenderers.ToArray());
        }

        public override void DeselectAll()
        {
            selectedRenderers.Clear();
            outlineRendererFeature.SetRenderers(selectedRenderers.ToArray());
        }
#else
        [InfoBox("⚠️ UNITY 6 ONLY FEATURE ⚠️\n\nThis Outline Selection Manager component requires Unity 6.0 or newer to function.\n\nUpgrade to Unity 6 to enable advanced render features.", EInfoBoxType.Warning)]
        [SerializeField] private bool unity6OnlyWarning;

        // Dummy methods for Unity 2022 compatibility
        public virtual void Select(Renderer renderer)
        {
            Debug.LogWarning("OutlineSelectionManager: This feature requires Unity 6.0 or newer.", this);
        }

        public virtual void Deselect(Renderer renderer)
        {
            Debug.LogWarning("OutlineSelectionManager: This feature requires Unity 6.0 or newer.", this);
        }

        public virtual void DeselectAll()
        {
            Debug.LogWarning("OutlineSelectionManager: This feature requires Unity 6.0 or newer.", this);
        }
#endif
    }
}