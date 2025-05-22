using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallConnect.Utility;

namespace LightsOutScriptMod
{
    public class LightsOutScript : MonoBehaviour
    {
        private static Mod mod;
        private float lastCheckedHour = -1;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<LightsOutScript>();
            mod.IsReady = true;
            Debug.Log("[LightsOut] Mod initialized, nya~!");
        }

        void Update()
        {
            var now = DaggerfallUnity.Instance.WorldTime.Now;
            if (Mathf.Floor(now.Hour) != lastCheckedHour)
            {
                lastCheckedHour = Mathf.Floor(now.Hour);

                Debug.Log($"[LightsOut] Hour changed to {now.Hour}, checking window state, nya!");

                if (now.Hour == 22)
                {
                    Debug.Log("[LightsOut] 22:00 - Turning OFF residential windows, nya!");
                    SetResidentialWindows(false); // Turn OFF
                }
                else if (now.Hour == 6)
                {
                    Debug.Log("[LightsOut] 06:00 - Turning ON residential windows, nya!");
                    SetResidentialWindows(true); // Turn ON
                }
                else if (now.Hour == 8)
                {
                    Debug.Log("[LightsOut] 08:00 - Turning OFF residential windows (let vanilla logic take over), nya!");
                    SetResidentialWindows(false); // Let vanilla logic resume, or forcibly OFF
                }
            }
        }

        void SetResidentialWindows(bool on)
        {
            int foundBuildings = 0;
            int changedWindows = 0;
            foreach (var bd in FindObjectsOfType<DaggerfallWorkshop.Game.BuildingDirectory>())
            {
                foreach (var building in bd.GetBuildingsOfFaction(0))
                {
                    foundBuildings++;
                    var go = FindBuildingGameObject(building);
                    if (go == null)
                    {
                        Debug.LogWarning($"[LightsOut] No GameObject found for buildingKey={building.buildingKey}, nya!");
                        continue;
                    }

                    var dayNight = go.GetComponentInChildren<DayNight>();
                    if (dayNight == null)
                    {
                        Debug.LogWarning($"[LightsOut] No DayNight component found on buildingKey={building.buildingKey}, nya!");
                        continue;
                    }

                    var mat = GetEmissiveMaterial(dayNight);
                    if (mat != null)
                    {
                        var color = on ? dayNight.nightColor : dayNight.dayColor;
                        mat.SetColor("_EmissionColor", color);
                        Debug.Log($"[LightsOut] Set emission for buildingKey={building.buildingKey} to {(on ? "ON" : "OFF")}, nya!");
                        changedWindows++;
                    }
                    else
                    {
                        Debug.LogWarning($"[LightsOut] No emission material found for buildingKey={building.buildingKey}, nya!");
                    }
                }
            }
            Debug.Log($"[LightsOut] Processed {foundBuildings} residential buildings, changed {changedWindows} window emissions, nya!");
        }

        GameObject FindBuildingGameObject(BuildingSummary summary)
        {
            foreach (var staticBuildings in FindObjectsOfType<DaggerfallWorkshop.DaggerfallStaticBuildings>())
            {
                if (staticBuildings.Buildings == null) continue;
                foreach (var building in staticBuildings.Buildings)
                {
                    if (building.buildingKey == summary.buildingKey)
                    {
                        return staticBuildings.gameObject;
                    }
                }
            }
            return null;
        }

        Material GetEmissiveMaterial(DayNight dayNight)
        {
            MeshRenderer renderer = dayNight.GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[LightsOut] No MeshRenderer found on DayNight, nya!");
                return null;
            }
            try
            {
                var mat = renderer.materials[dayNight.materialIndex];
                if (!mat.IsKeywordEnabled("_EMISSION"))
                    mat.EnableKeyword("_EMISSION");
                return mat;
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("[LightsOut] Failed to get emissive material: {0}", e.Message);
                return null;
            }
        }
    }
}
