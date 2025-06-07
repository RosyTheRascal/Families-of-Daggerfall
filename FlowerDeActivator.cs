using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using UnityEngine.SceneManagement; // Add this line
using System.Reflection;
using CustomStaticNPCMod;
using CustomNPCBridgeMod;
using FactionNPCInitializerMod;
using FactionParserMod;
using FamilyNameModifierMod;
using CustomNPCClickHandlerMod;
using CustomTalkManagerMod;
using CustomDaggerfallTalkWindowMod;

namespace FlowerDeActivatorMod
{
    public class FlowerDeActivator : MonoBehaviour
    {
        private static Mod mod;
        private static readonly HashSet<int> TargetModelIds = new HashSet<int> { 45084, 45116, 45117, 45086, 45120, 45122, 45076, 45077 };
        private bool componentsDeactivated = false;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<FlowerDeActivator>();

            mod.IsReady = true;
        }

        private void Start()
        {
            PlayerEnterExit.OnTransitionInterior += OnTransitionToInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionToExterior;
        }

        private void OnDestroy()
        {
            PlayerEnterExit.OnTransitionInterior -= OnTransitionToInterior;
            PlayerEnterExit.OnTransitionExterior -= OnTransitionToExterior;
        }

        private void OnLoad(SaveData_v1 saveData)
        {
            // Check if the player loaded a save inside an interior
            GameObject interiorParent = GameManager.Instance.InteriorParent;
            if (interiorParent != null && interiorParent.activeSelf)
            {
                Debug.LogWarning("Player loaded into an interior");
               DisableTargetComponents();
               NudgeInteriorChildrenUpwards();
            }
        }

        private void NudgeInteriorChildrenUpwards()
        {
            GameObject interiorParent = GameManager.Instance.InteriorParent;
            if (interiorParent == null)
            {
                Debug.LogWarning("FlowerDeActivator: Interior parent not found!");
                return;
            }

            foreach (Transform child in interiorParent.transform)
            {
                Vector3 pos = child.position;
                pos.y += 0.1f;
                child.position = pos;
                Debug.Log("Moved objects up!");
            }
        }

        private void OnTransitionToInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            // Nudge all enter markers upwards by 0.2f
            var enterMarkers = args.DaggerfallInterior.Markers
                .Where(m => m.type == DaggerfallInterior.InteriorMarkerTypes.Enter)
                .ToArray();

            foreach (var marker in enterMarkers)
            {
                Vector3 pos = marker.gameObject.transform.position;
                pos.y += 0.2f;
                marker.gameObject.transform.position = pos;
            }
            Debug.Log($"Player walked into an interior!");
            StartCoroutine(TeleportPlayerToEnterDoorOrMarkerAfterDelay(args.DaggerfallInterior, args.StaticDoor, 0.3f));
            DisableTargetComponents();
            NudgeInteriorChildrenUpwards();
        }

        private IEnumerator TeleportPlayerToEnterDoorOrMarkerAfterDelay(
                 DaggerfallWorkshop.DaggerfallInterior interior,
                 DaggerfallWorkshop.StaticDoor entryDoor,
                 float delay = 0.3f)
        {
            // Wait extra frames for timing/stability
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return new WaitForSeconds(delay);

            Vector3 entryWorldPos = DaggerfallStaticDoors.GetDoorPosition(entryDoor);
            Debug.Log($"[DBG] Entry door world pos: {entryWorldPos}");

            // 2. Fallback: use the closest enter marker (nudged up)
            DaggerfallWorkshop.DaggerfallInterior.InteriorEditorMarker? closestMarker = null;
            float minMarkerDist = float.MaxValue;
            foreach (var marker in interior.Markers)
            {
                if (marker.type == DaggerfallWorkshop.DaggerfallInterior.InteriorMarkerTypes.Enter)
                {
                    Vector3 markerPos = marker.gameObject.transform.position;
                    float dist = Vector3.Distance(markerPos, entryWorldPos);
                    Debug.Log($"[DBG] Enter marker at {markerPos}, distance to entry door: {dist}");
                    if (dist < minMarkerDist)
                    {
                        minMarkerDist = dist;
                        closestMarker = marker;
                    }
                }
            }
            if (closestMarker != null && closestMarker.Value.gameObject != null)
            {
                Vector3 markerPos = closestMarker.Value.gameObject.transform.position;
                Vector3 toDoor = (entryWorldPos - markerPos).normalized;
                Vector3 fakeNormal = -toDoor; // "inside" direction

                float upNudge = 0.3f; // vertical
                float height = GameManager.Instance.PlayerController ? GameManager.Instance.PlayerController.height : 1.8f;

                Vector3 spawnPos = markerPos
                    + Vector3.up * (height * upNudge);

                GameManager.Instance.PlayerObject.transform.position = spawnPos;
                Debug.Log($"[DBG] Fallback: teleported to closest enter marker at {spawnPos} (distance: {minMarkerDist})");
            }
            else
            {
                Debug.LogWarning("[DBG] No enter markers found, player position unchanged!");
            }
        }

        private void OnTransitionToExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            
            EnableTargetComponents();
        }

        private void DisableTargetComponents()
        {

            Debug.Log($"Disabling Target Components");
            Transform exteriorParent = GameManager.Instance.ExteriorParent?.transform; // Get the exterior parent object
            if (exteriorParent == null)
            {
                Debug.LogWarning("FluteDeActivator: Exterior parent not found!");
                return;
            }

            foreach (Transform child in exteriorParent.GetComponentsInChildren<Transform>())
            {
                if (IsTargetModel(child.name)) // Check if the model name matches any target ID
                {
                    Debug.LogWarning("Target models detected, disabling");
                    var renderer = child.GetComponent<Renderer>();
                    var collider = child.GetComponent<Collider>();
                    if (renderer != null) renderer.enabled = false;
                    if (collider != null) collider.enabled = false;
                   
                }
            }

            componentsDeactivated = true;
        }

        private void EnableTargetComponents()
        {
            Transform exteriorParent = GameManager.Instance.ExteriorParent?.transform; // Get the exterior parent object
            if (exteriorParent == null)
            {
                
                return;
            }

            foreach (Transform child in exteriorParent.GetComponentsInChildren<Transform>())
            {
                if (IsTargetModel(child.name)) // Check if the model name matches any target ID
                {
                    var renderer = child.GetComponent<Renderer>();
                    var collider = child.GetComponent<Collider>();
                    if (renderer != null) renderer.enabled = true;
                    if (collider != null) collider.enabled = true;
                    
                }
            }

            componentsDeactivated = false;
        }

        private bool IsTargetModel(string modelName)
        {
            // Check if the model name contains any target model ID
            foreach (int modelId in TargetModelIds)
            {
                if (modelName.Contains(modelId.ToString()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}