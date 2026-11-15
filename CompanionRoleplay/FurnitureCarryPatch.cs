using HarmonyLib;
using UnityStandardAssets.Cameras;
using UnityStandardAssets.Utility;

namespace CompanionRoleplay
{
    public static class CarriedFurnitureState
    {
        public static bool IsSeatedViaCarry;
        public static Furniture ActiveFurniture;
    }

    // 1. Перехватываем нажатие кнопки взаимодействия у мебели (до открытия меню выбора действий)
    [HarmonyPatch(typeof(Interaction), "Interact", new[] { typeof(CharacterCustomization) })]
    public static class InteractionInteractPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Interaction __instance, CharacterCustomization customization)
        {
            if (!CarryManager.IsCarrying || Player.code == null)
                return true;

            Furniture furniture = __instance.GetComponent<Furniture>();
            if (furniture == null)
                return true;

            if (customization == null || customization == Player.code.customization)
            {
                CharacterCustomization companion = CarryManager.Instance.ReleaseToFurniture(furniture);
                if (companion != null)
                {
                    CarriedFurnitureState.IsSeatedViaCarry = true;
                    CarriedFurnitureState.ActiveFurniture = furniture;

                    if (Global.code != null && Global.code.uiCombat != null && Global.code.uiCombat.interactionOptionsPanel != null)
                    {
                        Global.code.uiCombat.interactionOptionsPanel.SetActive(false);
                    }

                    furniture.InitiateInteract(companion);
                    return false; // Пропускаем посадку самого игрока и лишние меню
                }
            }

            return true;
        }
    }

    // 2. Страховка на случай, если Furniture.InitiateInteract вызвали напрямую (например, через кнопку INTERACT в меню)
    [HarmonyPatch(typeof(Furniture), "InitiateInteract", new[] { typeof(CharacterCustomization) })]
    public static class FurnitureInitiateInteractPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Furniture __instance, ref CharacterCustomization customization)
        {
            if (!CarryManager.IsCarrying || Player.code == null)
                return true;

            if (customization == null || customization == Player.code.customization)
            {
                CharacterCustomization companion = CarryManager.Instance.ReleaseToFurniture(__instance);
                if (companion != null)
                {
                    CarriedFurnitureState.IsSeatedViaCarry = true;
                    CarriedFurnitureState.ActiveFurniture = __instance;
                    customization = companion;
                    return true;
                }
            }

            return true;
        }
    }

    // 3. Корректно закрываем UIPose и возвращаем камеру игроку при выходе по Esc, если посадка была через Carry
    [HarmonyPatch(typeof(Global), "ButtonEscape")]
    public static class GlobalButtonEscapeCarryPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!CarriedFurnitureState.IsSeatedViaCarry)
                return;

            CarriedFurnitureState.IsSeatedViaCarry = false;
            Furniture furniture = CarriedFurnitureState.ActiveFurniture;
            CarriedFurnitureState.ActiveFurniture = null;

            if (Global.code != null)
            {
                if (Global.code.freeCamera != null) Global.code.freeCamera.SetActive(false);
                if (Global.code.freeCameraCollider != null) Global.code.freeCameraCollider.SetActive(false);
                if (Global.code.uiPose != null)
                {
                    if (Global.code.uiPose.freecameraTips != null) Global.code.uiPose.freecameraTips.SetActive(false);
                    Global.code.uiPose.Close();
                }
                if (Global.code.uiCombat != null)
                {
                    if (Global.code.uiCombat.hud != null) Global.code.uiCombat.hud.SetActive(true);
                    if (Global.code.uiCombat.buffGroup != null) Global.code.uiCombat.buffGroup.gameObject.SetActive(true);
                }
            }

            if (Player.code != null)
            {
                Player.code.ResetEmotion();
                var rotator = Player.code.GetComponent<SimpleMouseRotator>();
                if (rotator != null)
                {
                    rotator.enabled = false;
                    rotator.resetRotation();
                }
            }

            if (FreeLookCam.code != null && (furniture == null || !furniture.donthideMainCamera))
            {
                FreeLookCam.code.gameObject.SetActive(true);
            }

            if (furniture != null && furniture.camerasGroup != null)
            {
                furniture.camerasGroup.gameObject.SetActive(false);
            }
        }
    }
}
