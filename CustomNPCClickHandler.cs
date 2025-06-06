using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
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
using CustomTalkManagerMod;

namespace CustomNPCClickHandlerMod
{
    public class CustomNPCClickHandler : MonoBehaviour
    {
        private static Mod mod;
        private bool clickHandled = false; // Flag to track click events

        private const float StaticNPCActivationDistance = 4.0f; // Define the activation distance

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<CustomNPCClickHandler>();
            go.AddComponent<CustomWeaponManager>(); // Add the CustomWeaponManager component

            mod.IsReady = true;
        }

        void Update()
        {
            if (GameManager.IsGamePaused)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && !clickHandled) 
            {
                clickHandled = true;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    var customNPC = hit.transform.GetComponent<CustomStaticNPCMod.CustomStaticNPC>();
                    if (customNPC != null)
                    {

                        // Check the distance between the player and the NPC
                        float distance = Vector3.Distance(GameManager.Instance.PlayerObject.transform.position, customNPC.transform.position);
                        if (distance > StaticNPCActivationDistance)
                        {
                            DaggerfallUI.SetMidScreenText(TextManager.Instance.GetLocalizedText("youAreTooFarAway"));
                            return;
                        }

                        Debug.Log($"Custom NPC clicked: {customNPC.CustomDisplayName} (ID: {customNPC.GetInstanceID()})");

                        // Disable the vanilla TalkManager before starting the custom conversation
                        if (TalkManager.Instance != null)
                        {
                            TalkManager.Instance.enabled = false;
                            Debug.Log($"Vanilla TalkManager disabled");
                        }


                        
                        CustomTalkManagerMod.CustomTalkManager.Instance.enabled = true;
                        CustomTalkManagerMod.CustomTalkManager.Instance.StartConversation(customNPC);
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                clickHandled = false; // Reset the flag when the mouse button is released
            }
        }
    }

    public class CustomWeaponManager : MonoBehaviour
    {
        private GameObject mainCamera;
        private int playerLayerMask;
        private const float SphereCastRadius = 0.25f;
        private WeaponManager weaponManager;

        void Start()
        {
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            weaponManager = GameManager.Instance.WeaponManager;
        }

        void Update()
        {
            // Detect weapon hit only if the player is attacking
            if (weaponManager.ScreenWeapon && weaponManager.ScreenWeapon.IsAttacking())
            {
                Debug.Log($"Calling DetectWeaponHit");
                DetectWeaponHit();
            }
        }

        private void DetectWeaponHit()
        {
            if (!mainCamera)
                return;
            Debug.Log($"WeaponHit registered");
            RaycastHit hit;
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.SphereCast(ray, SphereCastRadius, out hit, WeaponManager.defaultWeaponReach, playerLayerMask))
            {
                var customNPC = hit.transform.GetComponent<CustomStaticNPCMod.CustomStaticNPC>();
                if (customNPC != null)
                {
                    // Call the method to handle being hit by a weapon
                    customNPC.OnHitByWeapon();
                }
            }
        }
    }
}