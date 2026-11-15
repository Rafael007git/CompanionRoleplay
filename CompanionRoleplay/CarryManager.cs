using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CompanionRoleplay
{
    [DefaultExecutionOrder(20000)]
    public class CarryManager : MonoBehaviour
    {
        public static CarryManager Instance { get; private set; }
        public static bool IsCarrying => Instance != null && Instance._carriedCompanion != null;
        public static CharacterCustomization CarriedCompanion => Instance?._carriedCompanion;

        private CharacterCustomization _carriedCompanion;
        private Transform _originalCompanionParent;

        private Animator _companionAnimator;
        private NavMeshAgent _companionAgent;
        private CharacterController _companionCharCtrl;
        private readonly List<Collider> _disabledColliders = new List<Collider>();
        private readonly List<Behaviour> _disabledAiScripts = new List<Behaviour>();

        private struct SavedRigidbodyState
        {
            public Rigidbody rb;
            public bool isKinematic;
            public RigidbodyInterpolation interpolation;
        }
        private readonly List<SavedRigidbodyState> _savedRigidbodies = new List<SavedRigidbodyState>();

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
            if (IsCarrying && _carriedCompanion != null)
            {
                _carriedCompanion.transform.localPosition = Plugin.CarryLocalPosOffset.Value;
                _carriedCompanion.transform.localRotation = Quaternion.Euler(Plugin.CarryLocalRotOffset.Value);
            }
        }

        public bool StartCarry(CharacterCustomization companion)
        {
            if (IsCarrying || companion == null || Player.code == null)
                return false;

            Transform playerRoot = Player.code.transform;
            var playerBones = RoleplayPoseLoader.BuildBoneMap(playerRoot);

            Transform attachAnchor = playerBones.TryGetValue("chestUpper", out Transform playerChestUpper)
                ? playerChestUpper
                : playerRoot;

            _carriedCompanion = companion;
            _originalCompanionParent = companion.transform.parent;

            // 1. Отключаем движение, навигацию и физику компаньонки
            _companionAgent = companion.GetComponent<NavMeshAgent>();
            if (_companionAgent != null && _companionAgent.enabled)
            {
                _companionAgent.isStopped = true;
                _companionAgent.enabled = false;
            }

            _companionCharCtrl = companion.GetComponent<CharacterController>();
            if (_companionCharCtrl != null) _companionCharCtrl.enabled = false;

            // Переводим все Rigidbody в кинематический режим без интерполяции
            _savedRigidbodies.Clear();
            foreach (var rb in companion.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null) continue;
                _savedRigidbodies.Add(new SavedRigidbodyState
                {
                    rb = rb,
                    isKinematic = rb.isKinematic,
                    interpolation = rb.interpolation
                });
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }

            _disabledColliders.Clear();
            foreach (var col in companion.GetComponentsInChildren<Collider>(true))
            {
                if (col.enabled)
                {
                    col.enabled = false;
                    _disabledColliders.Add(col);
                }
            }

            // 2. Ставим на паузу AI/эмоции из CompanionEmotionsMod
            ToggleCompanionEmotionsScripts(companion, false);

            // 3. Отключаем штатный Animator компаньонки и привязываем к chestUpper игрока
            _companionAnimator = companion.GetComponentInChildren<Animator>();
            if (_companionAnimator != null) _companionAnimator.enabled = false;

            companion.transform.SetParent(attachAnchor, false);
            companion.transform.localPosition = Plugin.CarryLocalPosOffset.Value;
            companion.transform.localRotation = Quaternion.Euler(Plugin.CarryLocalRotOffset.Value);

            // 4. Запускаем позу/анимацию "меня несут" на компаньонке
            var carriedPose = RoleplayPoseLoader.LoadPoseOrAnimation("Carried_Default");
            if (carriedPose != null)
            {
                _companionBoneDriver = companion.gameObject.GetComponent<RoleplayBoneDriver>()
                                       ?? companion.gameObject.AddComponent<RoleplayBoneDriver>();
                _companionBoneDriver.StartPlayback(carriedPose, upperBodyOnly: false);
            }

            // 5. Запускаем позу/анимацию переноски на игроке (ТОЛЬКО верхняя часть тела)
            var carrierPose = RoleplayPoseLoader.LoadPoseOrAnimation("Carrier_Default");
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

            _carriedCompanion.transform.SetParent(_originalCompanionParent, true);

            if (Player.code != null)
            {
                Transform p = Player.code.transform;
                _carriedCompanion.transform.position = p.position + p.forward * 0.85f;
                _carriedCompanion.transform.rotation = Quaternion.LookRotation(-p.forward);
            }

            if (_companionAnimator != null) _companionAnimator.enabled = true;

            foreach (var state in _savedRigidbodies)
            {
                if (state.rb != null)
                {
                    state.rb.isKinematic = state.isKinematic;
                    state.rb.interpolation = state.interpolation;
                }
            }
            _savedRigidbodies.Clear();

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
            _originalCompanionParent = null;
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
