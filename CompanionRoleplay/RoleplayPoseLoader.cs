using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

namespace CompanionRoleplay
{
    [Serializable]
    public class BoneTransformData
    {
        public string boneName;
        public float[] localPos;
        public float[] localRot;
        public bool isAbsolutePos;
    }

    [Serializable]
    public class KeyframeData
    {
        public float time;
        public List<BoneTransformData> bones = new List<BoneTransformData>();
    }

    [Serializable]
    public class RoleplayAnimationData
    {
        public bool loop = true;
        public List<KeyframeData> keyframes = new List<KeyframeData>();
    }

    public static class RoleplayPoseLoader
    {
        public static readonly HashSet<string> UpperBodyBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abdomenLower", "abdomenUpper", "chestLower", "chestUpper", "neckLower", "neckUpper", "head",
            "lCollar", "lShldrBend", "lShldrTwist", "lForearmBend", "lForearmTwist", "lHand",
            "rCollar", "rShldrBend", "rShldrTwist", "rForearmBend", "rForearmTwist", "rHand",
            "lThumb1", "lThumb2", "lThumb3", "lIndex1", "lIndex2", "lIndex3", "lMid1", "lMid2", "lMid3", "lRing1", "lRing2", "lRing3", "lPinky1", "lPinky2", "lPinky3",
            "rThumb1", "rThumb2", "rThumb3", "rIndex1", "rIndex2", "rIndex3", "rMid1", "rMid2", "rMid3", "rRing1", "rRing2", "rRing3", "rPinky1", "rPinky2", "rPinky3"
        };

        public static readonly string[] AllBodyBones =
        {
            "hip", "pelvis", "lThighBend", "lThighTwist", "lShin", "lFoot", "lMetatarsals", "lToe",
            "rThighBend", "rThighTwist", "rShin", "rFoot", "rMetatarsals", "rToe",
            "abdomenLower", "abdomenUpper", "chestLower", "chestUpper", "neckLower", "neckUpper", "head",
            "lCollar", "lShldrBend", "lShldrTwist", "lForearmBend", "lForearmTwist", "lHand",
            "rCollar", "rShldrBend", "rShldrTwist", "rForearmBend", "rForearmTwist", "rHand",
            "lThumb1", "lThumb2", "lThumb3", "lIndex1", "lIndex2", "lIndex3", "lMid1", "lMid2", "lMid3", "lRing1", "lRing2", "lRing3", "lPinky1", "lPinky2", "lPinky3",
            "rThumb1", "rThumb2", "rThumb3", "rIndex1", "rIndex2", "rIndex3", "rMid1", "rMid2", "rMid3", "rRing1", "rRing2", "rRing3", "rPinky1", "rPinky2", "rPinky3"
        };

        public static string PosesFolder => Path.Combine(Paths.PluginPath, "CompanionRoleplay", "Poses");

        public static void EnsureFolder()
        {
            if (!Directory.Exists(PosesFolder))
                Directory.CreateDirectory(PosesFolder);
        }

        public static RoleplayAnimationData LoadPoseOrAnimation(string baseOrFileName)
        {
            EnsureFolder();
            string baseName = Path.GetFileNameWithoutExtension(baseOrFileName);

            // 1. Проверяем JSON-анимацию
            string jsonPath = Path.Combine(PosesFolder, baseName + ".json");
            if (File.Exists(jsonPath))
            {
                return LoadJsonAnimation(jsonPath);
            }

            // 2. Проверяем бинарные ES2-файлы поз игры (.bytes, .txt, .pose или без расширения)
            string[] candidates =
            {
                Path.Combine(PosesFolder, baseOrFileName),
                Path.Combine(PosesFolder, baseName + ".bytes"),
                Path.Combine(PosesFolder, baseName + ".txt"),
                Path.Combine(PosesFolder, baseName + ".pose"),
                Path.Combine(PosesFolder, baseName)
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    return LoadES2Pose(path);
                }
            }

            Plugin.Log.LogWarning($"[RoleplayPoseLoader] Файл позы не найден для '{baseOrFileName}' в {PosesFolder}");
            return null;
        }

        private static RoleplayAnimationData LoadJsonAnimation(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<RoleplayAnimationData>(json);
                if (data != null && data.keyframes != null)
                {
                    foreach (var kf in data.keyframes)
                    {
                        foreach (var b in kf.bones) b.isAbsolutePos = true;
                    }
                    if (data.keyframes.Count > 1)
                    {
                        data.keyframes.Sort((a, b) => a.time.CompareTo(b.time));
                    }
                }
                Plugin.Log.LogInfo($"[RoleplayPoseLoader] Загружена JSON-анимация: {Path.GetFileName(path)}");
                return data;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[RoleplayPoseLoader] Ошибка чтения JSON {path}: {ex.Message}");
                return null;
            }
        }

        private static RoleplayAnimationData LoadES2Pose(string filePath)
        {
            try
            {
                var animData = new RoleplayAnimationData { loop = true };
                var kf = new KeyframeData { time = 0f };

                foreach (string boneName in AllBodyBones)
                {
                    bool hasRot = global::ES2.Exists(filePath + "?tag=rotation" + boneName);
                    bool hasPos = global::ES2.Exists(filePath + "?tag=pos" + boneName);

                    if (!hasRot && !hasPos) continue;

                    var bData = new BoneTransformData
                    {
                        boneName = boneName,
                        isAbsolutePos = true
                    };

                    if (hasRot)
                    {
                        Quaternion q = global::ES2.Load<Quaternion>(filePath + "?tag=rotation" + boneName);
                        bData.localRot = new[] { q.x, q.y, q.z, q.w };
                    }

                    if (hasPos)
                    {
                        Vector3 p = global::ES2.Load<Vector3>(filePath + "?tag=pos" + boneName);
                        bData.localPos = new[] { p.x, p.y, p.z };
                    }

                    kf.bones.Add(bData);
                }

                if (kf.bones.Count == 0)
                {
                    Plugin.Log.LogWarning($"[RoleplayPoseLoader] ES2 не нашёл костей в файле: {Path.GetFileName(filePath)}");
                    return null;
                }

                animData.keyframes.Add(kf);
                Plugin.Log.LogInfo($"[RoleplayPoseLoader] Загружена ES2-поза: {Path.GetFileName(filePath)} ({kf.bones.Count} костей)");
                return animData;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[RoleplayPoseLoader] Ошибка чтения ES2-позы {filePath}: {ex.Message}");
                return null;
            }
        }

        public static Dictionary<string, Transform> BuildBoneMap(Transform root)
        {
            var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            if (root == null) return map;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!map.ContainsKey(t.name))
                    map.Add(t.name, t);
            }
            return map;
        }
    }

    [DefaultExecutionOrder(10000)]
    public class RoleplayBoneDriver : MonoBehaviour
    {
        private RoleplayAnimationData _animData;
        private Dictionary<string, Transform> _boneMap;
        private Dictionary<string, Vector3> _initialPositions;
        private bool _upperBodyOnly;
        private float _timer;

        public void StartPlayback(RoleplayAnimationData data, bool upperBodyOnly)
        {
            _animData = data;
            _upperBodyOnly = upperBodyOnly;
            _boneMap = RoleplayPoseLoader.BuildBoneMap(transform);

            _initialPositions = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _boneMap)
            {
                if (kvp.Value != null)
                    _initialPositions[kvp.Key] = kvp.Value.localPosition;
            }

            _timer = 0f;
            enabled = true;
        }

        public void StopPlayback()
        {
            _animData = null;
            _initialPositions?.Clear();
            enabled = false;
        }

        private void LateUpdate()
        {
            if (_animData == null || _animData.keyframes == null || _animData.keyframes.Count == 0 || _boneMap == null)
                return;

            KeyframeData firstFrame = _animData.keyframes[0];
            KeyframeData lastFrame = _animData.keyframes[_animData.keyframes.Count - 1];

            if (_animData.keyframes.Count == 1 || lastFrame.time <= 0f)
            {
                ApplyKeyframe(firstFrame);
                return;
            }

            _timer += Time.deltaTime;
            if (_timer > lastFrame.time)
            {
                _timer = _animData.loop ? (_timer % lastFrame.time) : lastFrame.time;
            }

            KeyframeData prev = firstFrame;
            KeyframeData next = lastFrame;

            for (int i = 0; i < _animData.keyframes.Count - 1; i++)
            {
                if (_timer >= _animData.keyframes[i].time && _timer <= _animData.keyframes[i + 1].time)
                {
                    prev = _animData.keyframes[i];
                    next = _animData.keyframes[i + 1];
                    break;
                }
            }

            float span = next.time - prev.time;
            float t = span > 0.0001f ? Mathf.Clamp01((_timer - prev.time) / span) : 0f;
            ApplyInterpolated(prev, next, t);
        }

        private void ApplyKeyframe(KeyframeData kf)
        {
            foreach (var b in kf.bones)
            {
                if (_upperBodyOnly && !RoleplayPoseLoader.UpperBodyBones.Contains(b.boneName))
                    continue;

                if (_boneMap.TryGetValue(b.boneName, out Transform bone) && bone != null)
                {
                    if (b.localRot != null && b.localRot.Length == 4)
                    {
                        bone.localRotation = new Quaternion(b.localRot[0], b.localRot[1], b.localRot[2], b.localRot[3]);
                    }

                    if (!_upperBodyOnly && b.localPos != null && b.localPos.Length == 3)
                    {
                        Vector3 posVal = new Vector3(b.localPos[0], b.localPos[1], b.localPos[2]);
                        if (b.isAbsolutePos)
                        {
                            bone.localPosition = posVal;
                        }
                        else if (_initialPositions != null && _initialPositions.TryGetValue(b.boneName, out Vector3 initPos))
                        {
                            bone.localPosition = initPos + posVal;
                        }
                    }
                }
            }
        }

        private void ApplyInterpolated(KeyframeData prev, KeyframeData next, float t)
        {
            var nextDict = new Dictionary<string, BoneTransformData>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in next.bones) nextDict[b.boneName] = b;

            foreach (var bPrev in prev.bones)
            {
                if (_upperBodyOnly && !RoleplayPoseLoader.UpperBodyBones.Contains(bPrev.boneName))
                    continue;

                if (!_boneMap.TryGetValue(bPrev.boneName, out Transform bone) || bone == null)
                    continue;

                if (bPrev.localRot != null && bPrev.localRot.Length == 4)
                {
                    Quaternion q1 = new Quaternion(bPrev.localRot[0], bPrev.localRot[1], bPrev.localRot[2], bPrev.localRot[3]);
                    if (nextDict.TryGetValue(bPrev.boneName, out var bNext) && bNext.localRot != null && bNext.localRot.Length == 4)
                    {
                        Quaternion q2 = new Quaternion(bNext.localRot[0], bNext.localRot[1], bNext.localRot[2], bNext.localRot[3]);
                        bone.localRotation = Quaternion.Slerp(q1, q2, t);
                    }
                    else
                    {
                        bone.localRotation = q1;
                    }
                }
            }
        }
    }
}
