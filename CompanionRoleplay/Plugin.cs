using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace CompanionRoleplay
{
    [BepInPlugin("com.user.companionroleplay", "Companion Roleplay", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static Plugin Instance;

        public static ConfigEntry<KeyCode> InteractionKey;
        public static ConfigEntry<float> GrabDistance;
        public static ConfigEntry<Vector3> CarryLocalPosOffset;
        public static ConfigEntry<Vector3> CarryLocalRotOffset;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            InteractionKey = Config.Bind("Controls", "InteractionKey", KeyCode.G,
                "Горячая клавиша для открытия меню взаимодействия с компаньонкой или её отпускания.");

            GrabDistance = Config.Bind("Controls", "GrabDistance", 2.0f,
                "Максимальная дистанция (на расстоянии вытянутой руки) для взаимодействия с компаньонкой.");

            CarryLocalPosOffset = Config.Bind("CarryOffsets", "LocalPositionOffset", new Vector3(0f, -0.15f, 0.28f),
                "Локальное смещение кости hip компаньонки относительно chestUpper игрока.");

            CarryLocalRotOffset = Config.Bind("CarryOffsets", "LocalRotationOffset", new Vector3(0f, 90f, 0f),
                "Локальный поворот кости hip компаньонки относительно chestUpper игрока (в градусах).");

            Harmony harmony = new Harmony("com.user.companionroleplay");
            harmony.PatchAll();

            gameObject.AddComponent<RoleplayMenuUI>();
            gameObject.AddComponent<CarryManager>();

            Log.LogInfo("CompanionRoleplay 0.1.0 успешно загружен!");
        }
    }

    [HarmonyPatch(typeof(Player), "Update")]
    public static class PlayerSprintPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (CarryManager.IsCarrying && __instance.sprinting)
            {
                __instance.sprinting = false;
            }
        }
    }

}
