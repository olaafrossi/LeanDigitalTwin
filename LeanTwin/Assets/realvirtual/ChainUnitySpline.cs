// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using NaughtyAttributes;
using UnityEngine;
#if REALVIRTUAL_SPLINES
using UnityEngine.Splines;
#endif
namespace realvirtual
{
#if REALVIRTUAL_SPLINES
  public class ChainUnitySpline : SplineComponent,IChain
  {

      [Tooltip("Reference to the Unity Spline Container component")]
      public SplineContainer splineContainer;
      private float lastclosestperc;

      private void Awake()
      {
          splineContainer = GetComponent<SplineContainer>();
          if(splineContainer==null)
              Debug.LogError("No SplineContainer found. Please add a SplineContainer to the GameObject");
      }

      public Vector3 GetClosestDirection(Vector3 position)
      {
         
              lastclosestperc= ClosestPoint(position,100);
              return splineContainer.EvaluateTangent(lastclosestperc);
      }

      public Vector3 GetClosestPoint(Vector3 position)
      {
          lastclosestperc= ClosestPoint(position,100);
          return splineContainer.EvaluatePosition(lastclosestperc);
      }

      public Vector3 GetPosition(float normalizedposition, bool normalized = true)
      {
          return splineContainer.EvaluatePosition(normalizedposition);
      }

      public Vector3 GetDirection(float normalizedposition, bool normalized = true)
      {
         // if (normalized) normalizedposition = Reparam(normalizedposition);
          return splineContainer.EvaluateTangent(normalizedposition);
      }
      public Vector3 GetUpDirection(float normalizedposition, bool normalized = true)
      {
          Vector3 dir=Vector3.zero;
          dir=splineContainer.EvaluateUpVector(normalizedposition);
          return dir;
      }

      public float CalculateLength()
      {
          if(splineContainer==null)
              splineContainer = GetComponent<SplineContainer>();

          return splineContainer.CalculateLength();
      }
      

      public bool UseSimulationPath()
      {
          return false;
      }
      public float ClosestPoint(Vector3 point, int divisions = 100)
      {
          //make sure we have at least one division:
          if (divisions <= 0) divisions = 1;

          //variables:
          float shortestDistance = float.MaxValue;
          Vector3 position = Vector3.zero;
          Vector3 offset = Vector3.zero;
          float closestPercentage = 0;
          float percentage = 0;
          float distance = 0;

          //iterate spline and find the closest point on the spline to the provided point:
          for (float i = 0; i < divisions + 1; i++)
          {
              percentage = i / divisions;
              position = GetPosition(percentage);
              offset = position - point;
              distance = offset.sqrMagnitude;

              //if this point is closer than any others so far:
              if (distance < shortestDistance)
              {
                  shortestDistance = distance;
                  closestPercentage = percentage;
              }
          }

          return closestPercentage;
      }
      
  }
  #else
  public class ChainUnitySpline : MonoBehaviour
    {
     [InfoBox("⚠️ Unity Splines Support Not Enabled\n\n" +
              "This Chain component requires Unity Splines package support.\n\n" +
              "The Unity Splines package (com.unity.splines) must be installed and the REALVIRTUAL_SPLINES compiler define must be enabled.\n\n" +
              "If Unity Splines package is already installed, click the button below to enable REALVIRTUAL_SPLINES support.",
              EInfoBoxType.Warning)]
     [HideInInspector]
     public bool _splinesNotEnabled = true;

     [Button("Enable Unity Splines Support (Define REALVIRTUAL_SPLINES)")]
     private void EnableSplinesSupport()
     {
#if UNITY_EDITOR
         // Check if Unity Splines package is installed
         var packageListRequest = UnityEditor.PackageManager.Client.List();
         while (!packageListRequest.IsCompleted)
         {
             System.Threading.Thread.Sleep(10);
         }

         bool splinesInstalled = false;
         foreach (var package in packageListRequest.Result)
         {
             if (package.name == "com.unity.splines")
             {
                 splinesInstalled = true;
                 break;
             }
         }

         if (!splinesInstalled)
         {
             UnityEngine.Debug.LogWarning("Unity Splines package (com.unity.splines) is not installed. Installing now...");
             UnityEditor.PackageManager.Client.Add("com.unity.splines");
             UnityEngine.Debug.Log("Unity Splines package installation started. Please wait for completion, then click this button again.");
             return;
         }

         // Enable REALVIRTUAL_SPLINES define
         var buildTarget = UnityEditor.Build.NamedBuildTarget.Standalone;
         string defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(buildTarget);

         if (!defines.Contains("REALVIRTUAL_SPLINES"))
         {
             if (!string.IsNullOrEmpty(defines))
                 defines += ";";
             defines += "REALVIRTUAL_SPLINES";

             UnityEditor.PlayerSettings.SetScriptingDefineSymbols(buildTarget, defines);
             UnityEngine.Debug.Log("REALVIRTUAL_SPLINES compiler define enabled for Standalone platform. Unity will recompile.");
         }
         else
         {
             UnityEngine.Debug.Log("REALVIRTUAL_SPLINES define is already enabled.");
         }

         // Also enable for Android platform
         buildTarget = UnityEditor.Build.NamedBuildTarget.Android;
         defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(buildTarget);
         if (!defines.Contains("REALVIRTUAL_SPLINES"))
         {
             if (!string.IsNullOrEmpty(defines))
                 defines += ";";
             defines += "REALVIRTUAL_SPLINES";
             UnityEditor.PlayerSettings.SetScriptingDefineSymbols(buildTarget, defines);
         }

         // Also enable for WebGL platform
         buildTarget = UnityEditor.Build.NamedBuildTarget.WebGL;
         defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(buildTarget);
         if (!defines.Contains("REALVIRTUAL_SPLINES"))
         {
             if (!string.IsNullOrEmpty(defines))
                 defines += ";";
             defines += "REALVIRTUAL_SPLINES";
             UnityEditor.PlayerSettings.SetScriptingDefineSymbols(buildTarget, defines);
         }
#endif
     }

     [Tooltip("Reference to the Chain component (requires Unity Spline package)")]
     public Chain Chain;

        void Awake()
        {
            Chain = GetComponent<Chain>();
            if (Chain != null)
            {
                Chain.gameObject.SetActive(false);
                Logger.Warning("Unity Splines support is not enabled. Chain component deactivated. Please enable REALVIRTUAL_SPLINES compiler define.", this);
            }
        }
    }
#endif
}

