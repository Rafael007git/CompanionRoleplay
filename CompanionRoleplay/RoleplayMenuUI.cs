using System;
using UnityEngine;

namespace CompanionRoleplay
{
    public class RoleplayMenuUI : MonoBehaviour
    {
        private bool _isMenuOpen;
        private CharacterCustomization _targetCompanion;
        private Rect _windowRect = new Rect(0, 0, 240, 140);

        private void Update()
        {
            if (Input.GetKeyDown(Plugin.InteractionKey.Value))
            {
                if (_isMenuOpen)
                {
                    CloseMenu();
                    return;
                }

                if (CarryManager.IsCarrying)
                {
                    CarryManager.Instance.StopCarry();
                    return;
                }

                CharacterCustomization nearest = FindNearestVisibleCompanion(Plugin.GrabDistance.Value);
                if (nearest != null)
                {
                    OpenMenu(nearest);
                }
            }
        }

        private void OpenMenu(CharacterCustomization companion)
        {
            _targetCompanion = companion;
            _isMenuOpen = true;
            _windowRect.x = (Screen.width - _windowRect.width) * 0.5f;
            _windowRect.y = (Screen.height - _windowRect.height) * 0.5f;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void CloseMenu()
        {
            _isMenuOpen = false;
            _targetCompanion = null;

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnGUI()
        {
            if (!_isMenuOpen || _targetCompanion == null) return;

            _windowRect = GUI.Window(94821, _windowRect, DrawMenuWindow, _targetCompanion.name);
        }

        private void DrawMenuWindow(int id)
        {
            GUILayout.Space(8);

            if (GUILayout.Button("Carry", GUILayout.Height(34)))
            {
                CharacterCustomization target = _targetCompanion;
                CloseMenu();
                CarryManager.Instance.StartCarry(target);
            }

            GUILayout.Space(6);

            if (GUILayout.Button("Cancel", GUILayout.Height(26)))
            {
                CloseMenu();
            }
        }

        // Логика поиска ближайшей видимой цели в пределах дистанции вытянутой руки
        private CharacterCustomization FindNearestVisibleCompanion(float maxDistance)
        {
            Camera cam = Camera.main;
            if (cam == null || Player.code == null)
                return null;

            Vector3 playerPos = Player.code.transform.position;
            CharacterCustomization bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (var comp in FindObjectsOfType<Companion>())
            {
                if (comp == null || !comp.gameObject.activeInHierarchy)
                    continue;

                CharacterCustomization cc = comp.GetComponent<CharacterCustomization>();
                if (cc == null)
                    continue;

                Vector3 targetPos = cc.transform.position + Vector3.up * 1.2f;
                float dist = Vector3.Distance(playerPos, cc.transform.position);
                if (dist > maxDistance)
                    continue;

                Vector3 vp = cam.WorldToViewportPoint(targetPos);
                if (vp.z <= 0f || vp.x < 0.15f || vp.x > 0.85f || vp.y < 0.1f || vp.y > 0.9f)
                    continue;

                float centerOffset = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f));
                float score = dist + centerOffset * 2.0f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = cc;
                }
            }

            return bestTarget;
        }

    }
}
