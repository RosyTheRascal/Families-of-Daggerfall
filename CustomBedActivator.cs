using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
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
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;


namespace CustomBedActivatorMod
{
    public class CustomBedActivator : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<CustomBedActivator>();
            Debug.Log("Mod initialized: " + mod.Title);
            mod.IsReady = true;
        }

        void Awake()
        {
            DaggerfallWorkshop.Game.PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
        }

       private void OnTransitionInterior(DaggerfallWorkshop.Game.PlayerEnterExit.TransitionEventArgs args)
       {
            Debug.Log("Player walked into an interior!");
            var enterMarkers = args.DaggerfallInterior.Markers;
            for (int i = 0; i < enterMarkers.Length; i++)
            {
                if (enterMarkers[i].type == DaggerfallWorkshop.DaggerfallInterior.InteriorMarkerTypes.Enter)
                {
                    Vector3 pos = enterMarkers[i].gameObject.transform.position;
                    pos.y += 0.2f;
                    enterMarkers[i].gameObject.transform.position = pos;
                }
            }
       }

        private static void OnTransitionToInteriorStatic(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("STATIC: Player walked into an interior!");
        }

        private void Start()
        {
            PlayerEnterExit.OnTransitionInterior += OnTransitionToInteriorStatic;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log("Start called, registering custom activations...");
            LoadAudio();
            PlayerActivate.RegisterCustomActivation(mod, 41000, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 41001, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 41002, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42069, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42070, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42071, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42072, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42073, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42074, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42075, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42076, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42077, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42078, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42079, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42080, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42081, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42082, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42083, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42084, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42085, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 42086, SleepActivator);
            PlayerActivate.RegisterCustomActivation(mod, 41120, OrganActivator);
            PlayerActivate.RegisterCustomActivation(mod, 69471, EnterActivator);
            PlayerActivate.RegisterCustomActivation(mod, 69472, ExitActivator);
            Debug.Log("Activations Registered");
        }

        #region Load Audio Clips

        public static AudioClip OrganSound = null;
        public static AudioSource UIAudioSource { get; set; }

        private void LoadAudio()
        {
            ModManager modManager = ModManager.Instance;
            bool success = true;

            DaggerfallAudioSource dfAudio = DaggerfallUI.Instance.GetComponent<DaggerfallAudioSource>();
            if (dfAudio != null) { UIAudioSource = dfAudio.AudioSource; }
            else { Debug.Log("ERROR: Could Not Find Object Reference!"); }

            success &= modManager.TryGetAsset("Organ", false, out OrganSound);

            if (!success)
                throw new Exception("Missing organ effect");
            else
                Debug.Log("Organ sound loaded successfully!");
        }

        #endregion

        private static void SleepActivator(RaycastHit hit)
        {
            Debug.Log("SleepActivator called with hit object: " + hit.transform.name);

            if (GameManager.Instance.AreEnemiesNearby(true))
            {
                // Raise enemy alert status when monsters nearby
                GameManager.Instance.PlayerEntity.SetEnemyAlert(true);

                // Alert player if monsters nearby
                const int enemiesNearby = 354;
                DaggerfallUI.MessageBox(enemiesNearby);
            }
            else if (GameManager.Instance.PlayerEnterExit.IsPlayerSwimming ||
                     !GameManager.Instance.PlayerMotor.StartRestGroundedCheck())
            {
                const int cannotRestNow = 355;
                DaggerfallUI.MessageBox(cannotRestNow);
            }
            else
            {
                var preventedRestMessage = GameManager.Instance.GetPreventedRestMessage();
                if (preventedRestMessage != null)
                {
                    if (preventedRestMessage != "")
                        DaggerfallUI.MessageBox(preventedRestMessage);
                    else
                    {
                        const int cannotRestNow = 355;
                        DaggerfallUI.MessageBox(cannotRestNow);
                    }
                }
                else
                {
                    RacialOverrideEffect racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect(); // Allow custom race to block rest (e.g. vampire not sated)
                    if (racialOverride != null && !racialOverride.CheckStartRest(GameManager.Instance.PlayerEntity))
                        return;

                    IUserInterfaceManager uiManager = DaggerfallUI.UIManager;
                    uiManager.PushWindow(UIWindowFactory.GetInstanceWithArgs(UIWindowType.Rest, new object[] { uiManager, true }));
                }
            }
        }

        private static void OrganActivator(RaycastHit hit)
        {
            Debug.Log("OrganActivator called!");

            if (UIAudioSource != null && OrganSound != null)
            {
                Debug.Log("Playing organ sound...");
                DaggerfallUI.AddHUDText("You play a tune.", 5f);
                UIAudioSource.PlayOneShot(OrganSound, .5f * DaggerfallUnity.Settings.SoundVolume);
            }
            else
            {
                Debug.Log("UIAudioSource or OrganSound is null!");
            }
        }

        private static void EnterActivator(RaycastHit hit)
        {
            Debug.Log("EnterActivator called");

            // Find the nearest prison marker (199.13)
            Vector3 prisonMarker;
            if (FindClosestSpecialMarker(out prisonMarker, GameManager.Instance.PlayerMotor.transform.position, 199, 13))
            {
                GameManager.Instance.PlayerMotor.transform.position = prisonMarker;
                GameManager.Instance.PlayerMotor.FixStanding();
                Debug.Log("Player teleported to nearest prison marker at position: " + prisonMarker);
            }
            else
            {
                Debug.Log("No nearby prison marker found.");
            }
        }

        private static void ExitActivator(RaycastHit hit)
        {
            Debug.Log("ExitActivator called");

            // Find the nearest prison exit marker (199.14)
            Vector3 prisonExitMarker;
            if (FindClosestSpecialMarker(out prisonExitMarker, GameManager.Instance.PlayerMotor.transform.position, 199, 14))
            {
                GameManager.Instance.PlayerMotor.transform.position = prisonExitMarker;
                GameManager.Instance.PlayerMotor.FixStanding();
                Debug.Log("Player teleported to nearest prison exit marker at position: " + prisonExitMarker);
            }
            else
            {
                Debug.Log("No nearby prison exit marker found.");
            }
        }

        private static bool FindClosestSpecialMarker(out Vector3 marker, Vector3 position, int archive, int record)
        {
            marker = Vector3.zero;
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;

            if (!playerEnterExit.IsPlayerInsideBuilding)
            {
                Debug.Log("Player is not inside a building.");
                return false;
            }

            DaggerfallInterior interior = playerEnterExit.Interior;
            List<Vector3> specialMarkers = new List<Vector3>();

            foreach (var m in interior.Markers)
            {
                if (m.gameObject.GetComponent<DaggerfallBillboard>().Summary.Archive == archive &&
                    m.gameObject.GetComponent<DaggerfallBillboard>().Summary.Record == record)
                {
                    specialMarkers.Add(m.gameObject.transform.position);
                }
            }

            if (specialMarkers.Count == 0)
            {
                Debug.Log("No special markers of the specified type found.");
                return false;
            }

            float minDistance = float.MaxValue;
            foreach (Vector3 m in specialMarkers)
            {
                float distance = Vector3.Distance(position, m);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    marker = m;
                }
            }

            return minDistance != float.MaxValue;
        }

    }
}