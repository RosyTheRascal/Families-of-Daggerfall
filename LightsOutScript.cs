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

namespace LightsOutMod
{
    public class LightsOut : MonoBehaviour
    {
        private static Mod mod;
        private float lastCheckedHour = -1;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<LightsOut>();
            mod.IsReady = true;
        }

        void Update()
        {
            var now = DaggerfallUnity.Instance.WorldTime.Now;
            if (Mathf.Floor(now.Hour) != lastCheckedHour)
            {
                lastCheckedHour = Mathf.Floor(now.Hour);

                if (now.Hour == 22)
                {
                    SetResidentialWindows(false); // Turn OFF
                }
                else if (now.Hour == 6)
                {
                    SetResidentialWindows(true); // Turn ON
                }
                else if (now.Hour == 8)
                {
                    SetResidentialWindows(false); // Let vanilla logic resume, or forcibly OFF
                }
            }
        }

        void SetResidentialWindows(bool on)
        {
            foreach (var bd in FindObjectsOfType<DaggerfallWorkshop.Game.BuildingDirectory>())
            {
                foreach (var building in bd.GetBuildingsOfFaction(0))
                {
                    // Find the DayNight component for this building's GameObject
                    var obj = building; // You may need to map from BuildingSummary to the GameObject in the scene
                    var go = FindBuildingGameObject(obj); // implement this!
                    if (go == null) continue;

                    var dayNight = go.GetComponentInChildren<DayNight>();
                    if (dayNight != null)
                    {
                        // Set emission manually, override DayNight logic here
                        var mat = /* get emission material as in DayNight.InitEmissiveMaterial() */;
                        if (mat != null)
                        {
                            var color = on ? dayNight.nightColor : dayNight.dayColor;
                            mat.SetColor("_EmissionColor", color);
                        }
                    }
                }
            }
        }
    }
}
