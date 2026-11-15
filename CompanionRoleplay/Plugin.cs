using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace CompanionRoleplay
{
    [BepInPlugin("com.user.companionroleplay", "Companion Roleplay", "0.1.3")]
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

            CarryLocalPosOffset = Config.Bind("CarryOffsets", "LocalPositionOffset", new Vector3(0f, -1f, 0f),
                "Локальное смещение кости hip компаньонки относительно chestUpper игрока.");

            CarryLocalRotOffset = Config.Bind("CarryOffsets", "LocalRotationOffset", new Vector3(0f, 0f, 0f),
                "Локальный поворот кости hip компаньонки относительно chestUpper игрока (в градусах).");

            // Сначала регистрируем компоненты UI и CarryManager
            gameObject.AddComponent<RoleplayMenuUI>();
            gameObject.AddComponent<CarryManager>();

            // Затем применяем патчи
            Harmony harmony = new Harmony("com.user.companionroleplay");
            foreach (var type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                {
                    try
                    {
                        harmony.CreateClassProcessor(type).Patch();
                    }
                    catch (System.Exception ex)
                    {
                        Log.LogError($"[Harmony] Не удалось применить патч {type.Name}: {ex.Message}");
                    }
                }
            }

            Log.LogInfo("CompanionRoleplay 0.1.0 успешно загружен!");
        }
    }

    // Принудительно включаем режим шага и блокируем спринт у игрока во время переноски
    [HarmonyPatch(typeof(ThirdPersonCharacter), "Move")]
    public static class PlayerWalkMovePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ThirdPersonCharacter __instance, ref Vector3 move, ref bool crouch, ref bool jump)
        {
            if (!CarryManager.IsCarrying || Player.code == null)
                return;

            // Применяем только к игроку, чтобы не затронуть NPC
            if (__instance.gameObject != Player.code.gameObject)
                return;

            // Отключаем спринт на персонаже игрока
            Player.code.sprinting = false;
            if (Player.code.customization != null)
            {
                Player.code.customization.Unsprint();
            }

            // Включаем режим шага (параметр walk/crouch в ThirdPersonCharacter) и убираем прыжок при переноске
            crouch = true;
            jump = false;

            // На случай если скорость в ThirdPersonCharacter зависит от магнитуды вектора move
            if (move.sqrMagnitude > 0.25f)
            {
                move = move.normalized * 0.5f;
            }
        }
    }

}
