using HPUI;
using Il2CppEekCharacterEngine;
using Il2CppEekCharacterEngine.Components;
using Il2CppEekEvents;
using Il2CppHouseParty;
using Il2CppInterop.Runtime.Attributes;
using Il2CppRootMotion.FinalIK;
using MelonLoader;
using System.Reflection;
using System.Runtime.Loader;
using Unity.Services.Analytics.Internal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfiniPee
{
    public class InfiniPee : MelonMod
    {
        bool inGameMain = false;
        bool ShowUI = false;
        internal static bool LockCursor;
        ParticleSystem? Pee;
        Transform PeeOrigin = null!;
        private static Canvas? canvas;
        private static GameObject? CanvasGO;
        bool Peeing = false;
        internal static bool HandIK = false;
        float PeeAngle = 0;
        InteractionObject peeInteraction = PlayerCharacter.Player.Intimacy.PeeingHandTarget.InteractionObject;
        GameObject? masturbationTarget;

        //Pee_Particle on the player model, same for male and femal

        //angle can jus tbe set by the rotation of the GO

        static InfiniPee()
        {
            SetOurResolveHandlerAtFront();
        }

        private static Assembly AssemblyResolveEventListener(object sender, ResolveEventArgs args)
        {
            if (args is null)
            {
                return null!;
            }
            string name = "Sticky.Resources." + args.Name[..args.Name.IndexOf(',')] + ".dll";

            using Stream? str = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (str is not null)
            {
                var context = new AssemblyLoadContext(name, false);
                string path = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly()?.Location!)!.Parent!.FullName, "UserLibs", args.Name[..args.Name.IndexOf(',')] + ".dll");
                FileStream fstr = new(path, FileMode.Create);
                str.CopyTo(fstr);
                fstr.Close();
                str.Position = 0;

                var asm = context.LoadFromStream(str);
                MelonLogger.Warning($"Loaded {asm.FullName} from our embedded resources, saving to userlibs for next time");

                return asm;
            }
            return null!;
        }

        private static void SetOurResolveHandlerAtFront()
        {
            //MelonLogger.Msg("setting our resolvehandler");
            BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo? field = null;

            Type domainType = typeof(AssemblyLoadContext);

            while (field is null)
            {
                if (domainType is not null)
                {
                    field = domainType.GetField("AssemblyResolve", flags);
                }
                else
                {
                    //MelonDebug.Error("domainType got set to null for the AssemblyResolve event was null");
                    return;
                }
                if (field is null)
                {
                    domainType = domainType.BaseType!;
                }
            }

            var resolveDelegate = (MulticastDelegate)field.GetValue(null)!;
            Delegate[] subscribers = resolveDelegate.GetInvocationList();

            Delegate currentDelegate = resolveDelegate;
            for (int i = 0; i < subscribers.Length; i++)
            {
                currentDelegate = System.Delegate.RemoveAll(currentDelegate, subscribers[i])!;
            }

            var newSubscriptions = new Delegate[subscribers.Length + 1];
            newSubscriptions[0] = (ResolveEventHandler)AssemblyResolveEventListener!;
            System.Array.Copy(subscribers, 0, newSubscriptions, 1, subscribers.Length);

            currentDelegate = Delegate.Combine(newSubscriptions)!;

            field.SetValue(null, currentDelegate);

            //MelonLogger.Msg("set our resolvehandler");
        }

        public override void OnUpdate()
        {
            if (!inGameMain)
            {
                return;
            }

            if (Keyboard.current.altKey.isPressed && Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                ShowUI = !ShowUI;

                CanvasGO?.SetActive(ShowUI);
                LockCursor = ShowUI;
            }

            ToggleCursorPlayerLock();

            if (Pee is null)
            {
                return;
            }

            if (Peeing)
            {
                if (!Pee.isPlaying)
                {
                    Pee.Play();
                }
            }
            else
            {
                if (Pee.isPlaying)
                {
                    Pee.Stop();
                }
            }
        }

        public override void OnDeinitializeMelon()
        {

        }

        public override void OnInitializeMelon()
        {

        }

        private void BuildUI()
        {
            // Canvas
            CanvasGO = new()
            {
                name = "InfiniPee UI"
            };
            canvas = CanvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = CanvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
            _ = CanvasGO.AddComponent<GraphicRaycaster>();

            _ = UIBuilder.CreatePanel("InfiniPee UI Container", CanvasGO, new(0.18f, 0.11f), new(0, (float)(Screen.height * 0.6)), out GameObject contentHolder);
            UIBuilder.SetLayoutGroup<VerticalLayoutGroup>(contentHolder);

            UIBuilder.CreateToggle(contentHolder, "Pee", out Toggle PeeToggle, out _);
            PeeToggle.onValueChanged.AddListener(new Action<bool>((bool state) =>
            {
                Peeing = state;
                if (Pee is null)
                {
                    return;
                }
                if (Peeing)
                {
                    if (PlayerCharacter.Player.Gender == Genders.Male)
                    {
                        if (!PlayerCharacter.Player.AreGenitalsExposed)
                        {
                            PlayerCharacter.Player.OnPenis();
                        }
                    }
                    Pee.Play();
                    PlayerCharacter.Player.OnPeeingStarted.Invoke();
                }
                else
                {
                    PlayerCharacter.Player.OnPeeingStopped.Invoke();
                }
            }));
            PeeToggle.Set(Peeing);

            UIBuilder.CreateToggle(contentHolder, "Use Hand", out Toggle HandToggle, out _);
            HandToggle.onValueChanged.AddListener(new Action<bool>((bool state) =>
            {
                HandIK = state;
                if (PlayerCharacter.Player is null)
                {
                    return;
                }
                if (PlayerCharacter.Player.FinalIK is null)
                {
                    return;
                }
                if (PlayerCharacter.Player.Intimacy is null)
                {
                    return;
                }
                if (PlayerCharacter.Player.Intimacy.PeeingHandTarget is null)
                {
                    return;
                }

                //ik doesnt work for penis
                //lets check all delegte listeners on onpeeingstarted and onpee
                //onpeeingstarted is only listened to by one method, onpeeingstarteddowork

                //the hand posing is done from within CIntimacy.CheckPeeing......
                //so we nuke it if we force pee and then run the IK ourselves

                if (HandIK)
                {
                    if (peeInteraction is null || masturbationTarget is null || !masturbationTarget.active)
                    {
                        if (PlayerCharacter.Player.Gender == Genders.Male)
                        {
                            peeInteraction = PlayerCharacter.Player.Intimacy.PeeingHandTarget.InteractionObject;
                        }
                        else
                        {
                            //something called "masturbationlHand"
                            masturbationTarget = GameObject.Find("masturbationlHand");
                            masturbationTarget.SetActive(true);
                            peeInteraction = masturbationTarget.GetComponent<InteractionObject>();
                        }
                    }
                    PlayerCharacter.Player.FinalIK.StartObjectInteraction(peeInteraction, FullBodyBipedEffector.LeftHand, false);
                }
                else
                {
                    PlayerCharacter.Player.FinalIK.StopObjectInteraction(FullBodyBipedEffector.LeftHand);
                }
            }));
            HandToggle.Set(HandIK);

            var XSliderContainer = UIBuilder.CreateHorizontalGroup(contentHolder, "Pee angle slider container", true, true);
            UIBuilder.CreateLabel(XSliderContainer, "Pee Angle height", "Pee Angle height");
            UIBuilder.CreateSlider(XSliderContainer, "Angle", out Slider PeeAngleSlider);
            UIBuilder.SetLayoutElement(XSliderContainer, minWidth: 100);
            PeeAngleSlider.onValueChanged.AddListener(new Action<float>((float val) =>
            {
                PeeAngle = 90 - val;
                if (Pee is null)
                {
                    return;
                }
                UpdatePeeAngle();
            }));
            PeeAngleSlider.minValue = 3;
            PeeAngleSlider.maxValue = 150;
            if (PlayerCharacter.Player.Gender == Genders.Male)
            {
                PeeAngleSlider.value = 90f + 16f;
            }
            else
            {
                PeeAngleSlider.value = 90f - 74f;
            }

            CanvasGO?.SetActive(ShowUI);
        }

        private void UpdatePeeAngle()
        {
            if (Pee is null)
            {

                return;
            }

            if (PlayerCharacter.Player is null)
            {
                return;
            }

            float PlayerGenderOffset;
            if (PlayerCharacter.Player.Gender == Genders.Male)
            {
                try
                {
                    if (PlayerCharacter.Player.AreGenitalsExposed)
                    {
                        PlayerGenderOffset = 0f;
                    }
                    else
                    {
                        //y and z are set to 180 in this case
                        PlayerGenderOffset = 180f;
                    }
                }
                catch (Exception)
                {

                    PlayerGenderOffset = 0f;
                }
            }
            else
            {
                PlayerGenderOffset = 0f;
            }

            PeeOrigin.localEulerAngles = new(PlayerGenderOffset + PeeAngle, PeeOrigin.localEulerAngles.y, PeeOrigin.localEulerAngles.z);
        }
        private void ToggleCursorPlayerLock()
        {
            if (PlayerCharacter.Player is null)
            {
                return;
            }

            if (PlayerCharacter.Player._controlManager is null)
            {
                return;
            }

            if (PlayerCharacter.Player._controlManager.PlayerInput is null)
            {
                return;
            }

            if (ShowUI)
            {
                PlayerCharacter.Player._controlManager.PlayerInput.DeactivateInput();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                PlayerCharacter.Player._controlManager.PlayerInput.ActivateInput();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "GameMain")
            {
                inGameMain = true;
                Pee = PlayerCharacter.Player.Pee;

                if (PlayerCharacter.Player.Gender == Genders.Male)
                {
                    //get the "Genital" root
                    PeeOrigin = Pee.transform.parent.parent.parent;
                }
                else
                {
                    PeeOrigin = Pee.transform;
                }

                BuildUI();
            }
            else
            {
                inGameMain = false;
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Cursor), "set_visible")]
    public static class CursorVisiblePatch
    {
        public static bool Prefix(ref bool value)
        {
            if (InfiniPee.LockCursor)
            {
                value = true;
            }
            return true;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Cursor), "set_lockState")]
    public static class CursorLockstatePatch
    {
        public static bool Prefix(ref CursorLockMode value)
        {
            if (InfiniPee.LockCursor)
            {
                value = CursorLockMode.None;
            }
            return true;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(CIntimacy), "CheckPeeing")]
    public static class CheckPeeingPatch
    {
        public static bool Prefix()
        {
            if (InfiniPee.HandIK)
            {
                //dont run the games code if we do it
                return false;
            }
            //run the games own pee IK if we dont force the hand
            return true;
        }
    }
}
