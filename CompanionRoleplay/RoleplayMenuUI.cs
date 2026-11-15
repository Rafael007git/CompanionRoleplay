using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionRoleplay
{
    public class RoleplayMenuUI : MonoBehaviour
    {
        private GameObject _customPanel;
        private CharacterCustomization _targetCompanion;

        private bool IsMenuOpen => _customPanel != null && _customPanel.activeSelf;

        private void Update()
        {
            // Пока меню открыто, гарантируем свободный курсор
            if (IsMenuOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (Input.GetKeyDown(Plugin.InteractionKey.Value))
            {
                if (IsMenuOpen)
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
            if (Global.code == null || Global.code.uiCombat == null || Global.code.uiCombat.interactionOptionsPanel == null)
            {
                Plugin.Log.LogError("[RoleplayMenuUI] Global.code.uiCombat.interactionOptionsPanel недоступен!");
                return;
            }

            _targetCompanion = companion;

            if (_customPanel == null)
            {
                BuildNativePanel();
            }

            if (_customPanel != null)
            {
                _customPanel.SetActive(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void CloseMenu()
        {
            if (_customPanel != null)
            {
                _customPanel.SetActive(false);
            }
            _targetCompanion = null;

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void BuildNativePanel()
        {
            GameObject origPanel = Global.code.uiCombat.interactionOptionsPanel;
            _customPanel = Instantiate(origPanel, origPanel.transform.parent);
            _customPanel.name = "CompanionRoleplayOptionsPanel";

            // Собираем все дочерние кнопки шаблона
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < _customPanel.transform.childCount; i++)
            {
                children.Add(_customPanel.transform.GetChild(i));
            }

            if (children.Count == 0) return;

            // Первую кнопку настраиваем как "CARRY"
            Transform carryBtnTrans = children[0];
            SetupOptionButton(carryBtnTrans, "CARRY", () =>
            {
                CharacterCustomization target = _targetCompanion;
                CloseMenu();
                if (target != null)
                {
                    CarryManager.Instance.StartCarry(target);
                }
            });

            // Вторую кнопку (или клон первой) настраиваем как "CLOSE"
            Transform closeBtnTrans = children.Count > 1
                ? children[1]
                : Instantiate(carryBtnTrans, _customPanel.transform);

            SetupOptionButton(closeBtnTrans, "CLOSE", CloseMenu);

            // Все остальные кнопки из шаблона (RELOCATE, DESTROY и т.д.) скрываем/удаляем
            for (int i = 2; i < children.Count; i++)
            {
                Destroy(children[i].gameObject);
            }
        }

        private void SetupOptionButton(Transform btnTransform, string label, UnityEngine.Events.UnityAction onClickAction)
        {
            btnTransform.gameObject.SetActive(true);

            // Отключаем локализатор, чтобы игра не перезаписывала наш текст
            LocalizationText locText = btnTransform.GetComponentInChildren<LocalizationText>();
            if (locText != null)
            {
                locText.enabled = false;
            }

            Text txt = btnTransform.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = label;
            }

            Button btn = btnTransform.GetComponent<Button>();
            if (btn != null)
            {
                // Отключаем старые persistent-события из инспектора Unity и очищаем динамические
                for (int i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
                {
                    btn.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                }
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onClickAction);
            }
        }

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
