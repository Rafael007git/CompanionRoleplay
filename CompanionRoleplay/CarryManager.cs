using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace CompanionRoleplay
{
    public class CarryManager : MonoBehaviour
    {
        public static CarryManager Instance { get; private set; }
        public static bool IsCarrying => Instance != null && Instance._carriedCompanion != null;
        public static CharacterCustomization CarriedCompanion => Instance?._carriedCompanion;

        private CharacterCustomization _carriedCompanion;
        private Transform _companionHip;
        private Transform _originalHipParent;
        private Vector3 _originalHipLocalPos;
        private Quaternion _originalHipLocalRot;

        private Animator _companionAnimator;
        private NavMeshAgent _companionAgent;
        private CharacterController _companionCharCtrl;
        private readonly List<Collider> _disabledColliders = new List<Collider>();
        private readonly List<Behaviour> _disabledAiScripts = new List<Behaviour>();

        private RoleplayBoneDriver _playerBoneDriver;
        private RoleplayBoneDriver _companionBoneDriver;

        private void Awake()
        {
            Instance = this;
            RoleplayPoseLoader.EnsureFolder();
        }

        private void LateUpdate()
        {
            // Позволяет подстраивать смещение прямо в рантайме через конфиг
            if (IsCarrying && _companionHip != null)
            {
                _companionHip.localPosition = Plugin.CarryLocalPosOffset.Value;
                _companionHip.localRotation = Quaternion.Euler(Plugin.CarryLocalRotOffset.Value);
            }
        }

        public bool StartCarry(CharacterCustomization companion)
        {
            if (IsCarrying || companion == null || Player.code == null)
                return false;

            Transform playerRoot = Player.code.transform;
            var playerBones = RoleplayPoseLoader.BuildBoneMap(playerRoot);
            var compBones = RoleplayPoseLoader.BuildBoneMap(companion.transform);

            if (!playerBones.TryGetValue("chestUpper", out Transform playerChestUpper))
            {
                Plugin.Log.LogError("[CarryManager] Не найдена кость chestUpper у игрока!");
                return false;
            }

            if (!compBones.TryGetValue("hip", out Transform compHip))
            {
                Plugin.Log.LogError($"[CarryManager] Не найдена кость hip у компаньонки {companion.name}!");
                return false;
            }

            _carriedCompanion = companion;
            _companionHip = compHip;
            _originalHipParent = compHip.parent;
            _originalHipLocalPos = compHip.localPosition;
            _originalHipLocalRot = compHip.localRotation;

            // 1. Отключаем движение, навигацию и физику компаньонки
            _companionAgent = companion.GetComponent<NavMeshAgent>();
            if (_companionAgent != null && _companionAgent.enabled)
            {
                _companionAgent.isStopped = true;
                _companionAgent.enabled = false;
            }

            _companionCharCtrl = companion.GetComponent<CharacterController>();
            if (_companionCharCtrl != null) _companionCharCtrl.enabled = false;

            _disabledColliders.Clear();
            foreach (var col in companion.GetComponentsInChildren<Collider>(true))
            {
                if (col.enabled)
                {
                    col.enabled = false;
                    _disabledColliders.Add(col);
                }
            }

            // 2. Ставим на паузу AI/эмоции из CompanionEmotionsMod (если установлены)
            ToggleCompanionEmotionsScripts(companion, false);

            // 3. Отключаем штатный Animator компаньонки и сажаем hip на chestUpper игрока
            _companionAnimator = companion.GetComponentInChildren<Animator>();
            if (_companionAnimator != null) _companionAnimator.enabled = false;

            _companionHip.SetParent(playerChestUpper, false);
            _companionHip.localPosition = Plugin.CarryLocalPosOffset.Value;
            _companionHip.localRotation = Quaternion.Euler(Plugin.CarryLocalRotOffset.Value);

            // 4. Запускаем позу/анимацию "меня несут" на компаньонке (все кости)
            var carriedPose = RoleplayPoseLoader.LoadPoseOrAnimation("Carried_Default.json");
            if (carriedPose != null)
            {
                _companionBoneDriver = companion.gameObject.GetComponent<RoleplayBoneDriver>()
                                       ?? companion.gameObject.AddComponent<RoleplayBoneDriver>();
                _companionBoneDriver.StartPlayback(carriedPose, upperBodyOnly: false);
            }

            // 5. Запускаем позу/анимацию переноски на игроке (ТОЛЬКО верхняя часть тела)
            var carrierPose = RoleplayPoseLoader.LoadPoseOrAnimation("Carrier_Default.json");
            if (carrierPose != null)
            {
                _playerBoneDriver = playerRoot.gameObject.GetComponent<RoleplayBoneDriver>()
                                    ?? playerRoot.gameObject.AddComponent<RoleplayBoneDriver>();
                _playerBoneDriver.StartPlayback(carrierPose, upperBodyOnly: true);
            }

            Plugin.Log.LogInfo($"[CarryManager] Игрок взял на руки компаньонку: {companion.name}");
            return true;
        }

        public void StopCarry()
        {
            if (!IsCarrying) return;

            if (_playerBoneDriver != null) _playerBoneDriver.StopPlayback();
            if (_companionBoneDriver != null) _companionBoneDriver.StopPlayback();

            // Возвращаем hip компаньонки в её родную иерархию скелета
            if (_companionHip != null && _originalHipParent != null)
            {
                _companionHip.SetParent(_originalHipParent, false);
                _companionHip.localPosition = _originalHipLocalPos;
                _companionHip.localRotation = _originalHipLocalRot;
            }

            // Ставим компаньонку аккуратно перед игроком
            if (Player.code != null)
            {
                Transform p = Player.code.transform;
                _carriedCompanion.transform.position = p.position + p.forward * 0.85f;
                _carriedCompanion.transform.rotation = Quaternion.LookRotation(-p.forward);
            }

            if (_companionAnimator != null) _companionAnimator.enabled = true;

            foreach (var col in _disabledColliders)
            {
                if (col != null) col.enabled = true;
            }
            _disabledColliders.Clear();

            if (_companionCharCtrl != null) _companionCharCtrl.enabled = true;
            if (_companionAgent != null)
            {
                _companionAgent.enabled = true;
                _companionAgent.isStopped = false;
            }

            ToggleCompanionEmotionsScripts(_carriedCompanion, true);

            Plugin.Log.LogInfo($"[CarryManager] Игрок отпустил компаньонку: {_carriedCompanion.name}");

            _carriedCompanion = null;
            _companionHip = null;
            _originalHipParent = null;
        }

        private void ToggleCompanionEmotionsScripts(CharacterCustomization companion, bool state)
        {
            if (!state)
            {
                _disabledAiScripts.Clear();
                foreach (var mb in companion.GetComponents<MonoBehaviour>())
                {
                    if (mb == null || !mb.enabled) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName == "CastleWanderTracker" || typeName == "CompanionEmotionController")
                    {
                        mb.enabled = false;
                        _disabledAiScripts.Add(mb);
                    }
                }
            }
            else
            {
                foreach (var mb in _disabledAiScripts)
                {
                    if (mb != null) mb.enabled = true;
                }
                _disabledAiScripts.Clear();
            }
        }
    }
}
