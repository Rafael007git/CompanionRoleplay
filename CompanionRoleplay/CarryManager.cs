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
        private Transform _attachAnchor;

        private Animator _companionAnimator;
        private NavMeshAgent _companionAgent;
        private CharacterController _companionCharCtrl;
        private Companion _nativeCompanionScript;
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
            // Синхронизируем мировую позицию и поворот напрямую от якоря (chestUpper), без SetParent!
            if (IsCarrying && _carriedCompanion != null && _attachAnchor != null)
            {
                _carriedCompanion.transform.position = _attachAnchor.TransformPoint(Plugin.CarryLocalPosOffset.Value);
                _carriedCompanion.transform.rotation = _attachAnchor.rotation * Quaternion.Euler(Plugin.CarryLocalRotOffset.Value);
            }
        }

        public bool StartCarry(CharacterCustomization companion)
        {
            if (IsCarrying || companion == null || Player.code == null)
                return false;

            Transform playerRoot = Player.code.transform;
            var playerBones = RoleplayPoseLoader.BuildBoneMap(playerRoot);

            _attachAnchor = playerBones.TryGetValue("chestUpper", out Transform playerChestUpper)
                ? playerChestUpper
                : playerRoot;

            _carriedCompanion = companion;

            // 1. Отключаем навигацию и контроллер столкновений компаньонки
            _companionAgent = companion.GetComponent<NavMeshAgent>();
            if (_companionAgent != null && _companionAgent.enabled)
            {
                _companionAgent.isStopped = true;
                _companionAgent.enabled = false;
            }

            _companionCharCtrl = companion.GetComponent<CharacterController>();
            if (_companionCharCtrl != null) _companionCharCtrl.enabled = false;

            // Отключаем родной скрипт Companion на время переноски, чтобы он не пытался возвращать её назад
            _nativeCompanionScript = companion.GetComponent<Companion>();
            if (_nativeCompanionScript != null && _nativeCompanionScript.enabled)
            {
                _nativeCompanionScript.enabled = false;
            }

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

            // 3. Отключаем штатный Animator компаньонки и сразу ставим её на якорь
            _companionAnimator = companion.GetComponentInChildren<Animator>();
            if (_companionAnimator != null) _companionAnimator.enabled = false;

            companion.transform.position = _attachAnchor.TransformPoint(Plugin.CarryLocalPosOffset.Value);
            companion.transform.rotation = _attachAnchor.rotation * Quaternion.Euler(Plugin.CarryLocalRotOffset.Value);

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

            Plugin.Log.LogInfo($"[CarryManager] Игрок взял на руки компаньонку: {companion.name} в точке {playerRoot.position}");
            return true;
        }

        public void StopCarry()
        {
            if (!IsCarrying) return;

            Transform p = Player.code != null ? Player.code.transform : null;
            Plugin.Log.LogInfo($"[TeleCheck] 0. Start StopCarry: Player={p?.position}");

            Vector3 dropPos = _carriedCompanion.transform.position;
            Quaternion dropRot = _carriedCompanion.transform.rotation;

            if (p != null)
            {
                dropPos = p.position + p.forward * 1.1f + Vector3.up * 0.1f;
                dropRot = Quaternion.LookRotation(-p.forward);
            }

            if (_playerBoneDriver != null) _playerBoneDriver.StopPlayback();
            if (_companionBoneDriver != null) _companionBoneDriver.StopPlayback();
            Plugin.Log.LogInfo($"[TeleCheck] 1. After BoneDriver: Player={p?.position}");

            _carriedCompanion.transform.position = dropPos;
            _carriedCompanion.transform.rotation = dropRot;

            if (_companionAnimator != null) _companionAnimator.enabled = true;

            foreach (var state in _savedRigidbodies)
            {
                if (state.rb != null)
                {
                    state.rb.velocity = Vector3.zero;
                    state.rb.angularVelocity = Vector3.zero;
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
                _companionAgent.Warp(dropPos);
                _companionAgent.ResetPath();
                _companionAgent.isStopped = false;
            }
            Plugin.Log.LogInfo($"[TeleCheck] 2. After Physics/Agent: Player={p?.position}");

            if (_nativeCompanionScript != null)
            {
                _nativeCompanionScript.enabled = true;
            }
            Plugin.Log.LogInfo($"[TeleCheck] 3. After NativeCompanion.enabled: Player={p?.position}");

            ToggleCompanionEmotionsScripts(_carriedCompanion, true);
            Plugin.Log.LogInfo($"[TeleCheck] 4. After EmotionsScripts: Player={p?.position}");

            _carriedCompanion = null;
            _attachAnchor = null;
            _nativeCompanionScript = null;

            StartCoroutine(CheckPlayerNextFrames());
        }

        /// <summary>
        /// Освобождает компаньонку с рук и сразу передаёт её мебельному взаимодействию.
        /// </summary>
        public CharacterCustomization ReleaseToFurniture(Furniture furniture)
        {
            if (!IsCarrying) return null;

            CharacterCustomization companion = _carriedCompanion;

            if (_playerBoneDriver != null) _playerBoneDriver.StopPlayback();
            if (_companionBoneDriver != null) _companionBoneDriver.StopPlayback();

            if (furniture != null)
            {
                companion.transform.position = furniture.transform.position;
            }

            if (_companionAnimator != null) _companionAnimator.enabled = true;

            foreach (var state in _savedRigidbodies)
            {
                if (state.rb != null)
                {
                    state.rb.velocity = Vector3.zero;
                    state.rb.angularVelocity = Vector3.zero;
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
                _companionAgent.Warp(companion.transform.position);
                _companionAgent.ResetPath();
                _companionAgent.isStopped = false;
            }

            if (_nativeCompanionScript != null)
            {
                _nativeCompanionScript.enabled = true;
            }

            ToggleCompanionEmotionsScripts(companion, true);

            Plugin.Log.LogInfo($"[CarryManager] Компаньонка {companion.name} передана на мебель: {(furniture != null ? furniture.name : "null")}");

            _carriedCompanion = null;
            _attachAnchor = null;
            _nativeCompanionScript = null;

            return companion;
        }

        private System.Collections.IEnumerator CheckPlayerNextFrames()
        {
            yield return null;
            Plugin.Log.LogInfo($"[TeleCheck] 5. Next Update: Player={Player.code?.transform.position}");
            yield return new WaitForFixedUpdate();
            Plugin.Log.LogInfo($"[TeleCheck] 6. Next FixedUpdate: Player={Player.code?.transform.position}");
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
