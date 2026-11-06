// ⚠️ 本文件不参与本机编译。
// 原因：UnityEngine.UI（UGUI）属于 com.unity.ugui 包，
// 不在 NuGet 的 UnityEngine.Modules 参考程序集里，本机无法类型检查。
//
// 因此这里的代码只用「标准 UGUI 用法」，逻辑全部在已测试的 GameHudPresenter 里。
// 如果编译报错，99% 是字段名/方法名笔误，不是设计问题。
//
// 放置位置：Assets/Scripts/UnityIntegration/

using System;
using System.Collections.Generic;
using DefenseCounter.Core;
using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseCounter.UnityIntegration
{
    /// <summary>HUD 的 UGUI 实现。Inspector 里把 Text / Image 引用拖满即可。</summary>
    public sealed class UGuiHudView : MonoBehaviour, IHudView
    {
        [Header("资源与时间")]
        public Text goldText;
        public Text energyText;
        public Text timeText;

        [Header("基地与兵力")]
        public Image baseHealthFill;
        public Text baseHealthText;
        public Text slotText;
        public Text buildingText;

        [Header("反馈")]
        public GameObject damageFlash;
        public GameObject outcomePanel;
        public Text outcomeText;

        [Tooltip("受伤红光持续时间（秒）")]
        public float flashDuration = 0.25f;

        private float _flashRemaining;

        public void SetGold(int value)
        {
            if (goldText != null) { goldText.text = value.ToString(); }
        }

        public void SetEnergy(int value)
        {
            if (energyText != null) { energyText.text = value.ToString(); }
        }

        public void SetBaseHealth(float current, float max, float ratio)
        {
            if (baseHealthFill != null) { baseHealthFill.fillAmount = Mathf.Clamp01(ratio); }
            if (baseHealthText != null)
            {
                baseHealthText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
            }
        }

        public void SetUnitSlots(int used, int max)
        {
            if (slotText != null) { slotText.text = used + " / " + max; }
        }

        public void SetBuildingCount(int count)
        {
            if (buildingText != null) { buildingText.text = count.ToString(); }
        }

        public void SetMatchTime(float seconds)
        {
            if (timeText == null) { return; }

            int total = Mathf.FloorToInt(seconds);
            timeText.text = (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        public void FlashBaseDamaged()
        {
            _flashRemaining = flashDuration;
            if (damageFlash != null) { damageFlash.SetActive(true); }
        }

        public void ShowOutcome(MatchOutcome outcome)
        {
            if (outcomePanel != null) { outcomePanel.SetActive(true); }

            if (outcomeText != null)
            {
                switch (outcome)
                {
                    case MatchOutcome.Win: outcomeText.text = "胜利"; break;
                    case MatchOutcome.Lose: outcomeText.text = "失败"; break;
                    case MatchOutcome.Draw: outcomeText.text = "平局"; break;
                    default: outcomeText.text = string.Empty; break;
                }
            }
        }

        private void Update()
        {
            if (_flashRemaining <= 0f) { return; }

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining <= 0f && damageFlash != null)
            {
                damageFlash.SetActive(false);
            }
        }
    }

    /// <summary>建造菜单的 UGUI 实现。按钮由 itemPrefab 动态生成。</summary>
    public sealed class UGuiBuildMenuView : MonoBehaviour, IBuildMenuView
    {
        [Header("容器与模板")]
        public RectTransform itemContainer;
        public Button itemPrefab;

        [Header("放置提示")]
        public GameObject placingHint;
        public Text placingLabel;

        [Header("取消")]
        public Button cancelButton;

        /// <summary>外部订阅：点击某个建造项。</summary>
        public event Action<string> ItemClicked;

        /// <summary>外部订阅：点击取消。</summary>
        public event Action CancelClicked;

        private readonly List<Button> _spawned = new List<Button>();

        private void Awake()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (placingHint != null)
            {
                placingHint.SetActive(false);
            }
        }

        private void OnCancelClicked()
        {
            if (CancelClicked != null) { CancelClicked(); }
        }

        public void ClearItems()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i].gameObject);
                }
            }

            _spawned.Clear();
        }

        public void AddItem(string buildingId, int cost, bool enabled)
        {
            if (itemPrefab == null || itemContainer == null) { return; }

            Button button = Instantiate(itemPrefab, itemContainer);
            button.gameObject.SetActive(true);
            button.interactable = enabled;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = ConfigSeeder.DisplayNameOf(buildingId) + "\n" + cost;
            }

            string captured = buildingId;
            button.onClick.AddListener(delegate
            {
                if (ItemClicked != null) { ItemClicked(captured); }
            });

            _spawned.Add(button);
        }

        public void SetPlacing(string buildingId)
        {
            if (placingHint != null) { placingHint.SetActive(true); }
            if (placingLabel != null)
            {
                placingLabel.text = "放置：" + ConfigSeeder.DisplayNameOf(buildingId);
            }
        }

        public void ClearPlacing()
        {
            if (placingHint != null) { placingHint.SetActive(false); }
        }
    }

    /// <summary>
    /// 技能栏的 UGUI 实现。
    /// 冷却遮罩约定：按钮下挂一个名为 "Cooldown" 的 Image，用 fillAmount 表示进度。
    /// </summary>
    public sealed class UGuiSkillBarView : MonoBehaviour, ISkillBarView
    {
        [Header("容器与模板")]
        public RectTransform itemContainer;
        public Button itemPrefab;

        public event Action<string> ItemClicked;

        private readonly List<Button> _spawned = new List<Button>();

        public void ClearItems()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i].gameObject);
                }
            }

            _spawned.Clear();
        }

        public void AddItem(string skillId, int energyCost, bool ready, float cooldownFill)
        {
            if (itemPrefab == null || itemContainer == null) { return; }

            Button button = Instantiate(itemPrefab, itemContainer);
            button.gameObject.SetActive(true);
            button.interactable = ready;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = ConfigSeeder.DisplayNameOf(skillId) + "\n" + energyCost;
            }

            Transform cooldown = button.transform.Find("Cooldown");
            if (cooldown != null)
            {
                Image fill = cooldown.GetComponent<Image>();
                if (fill != null)
                {
                    fill.fillAmount = Mathf.Clamp01(cooldownFill);
                }
            }

            string captured = skillId;
            button.onClick.AddListener(delegate
            {
                if (ItemClicked != null) { ItemClicked(captured); }
            });

            _spawned.Add(button);
        }
    }

    /// <summary>放置预览的 UGUI（3D）实现：一个平铺的 ghost 方块 + 错误提示。</summary>
    public sealed class UGuiPlacementView : MonoBehaviour, IPlacementView
    {
        [Header("幽灵预览")]
        public GameObject ghostPrefab;
        public Material validMaterial;
        public Material invalidMaterial;

        [Tooltip("格子边长（米），必须与 MatchBootstrap.CellSize 一致")]
        public float cellSize = 2f;

        [Header("错误提示")]
        public Text errorText;
        public float errorDuration = 2f;

        private GameObject _ghost;
        private Renderer[] _ghostRenderers;
        private float _errorRemaining;

        public void ShowGhost(float centerX, float centerZ, int sizeX, int sizeZ, bool valid)
        {
            if (!EnsureGhost()) { return; }

            _ghost.SetActive(true);
            _ghost.transform.position = new Vector3(centerX, 0.05f, centerZ);
            _ghost.transform.localScale = new Vector3(
                Mathf.Max(1, sizeX) * cellSize * 0.95f,
                0.02f,
                Mathf.Max(1, sizeZ) * cellSize * 0.95f);

            Material target = valid ? validMaterial : invalidMaterial;
            if (target != null && _ghostRenderers != null)
            {
                for (int i = 0; i < _ghostRenderers.Length; i++)
                {
                    if (_ghostRenderers[i] != null)
                    {
                        _ghostRenderers[i].sharedMaterial = target;
                    }
                }
            }
        }

        public void HideGhost()
        {
            if (_ghost != null) { _ghost.SetActive(false); }
        }

        public void ShowError(string message)
        {
            if (errorText == null) { return; }

            errorText.text = message;
            errorText.gameObject.SetActive(true);
            _errorRemaining = errorDuration;
        }

        private bool EnsureGhost()
        {
            if (_ghost != null) { return true; }
            if (ghostPrefab == null) { return false; }

            _ghost = Instantiate(ghostPrefab);
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>();
            _ghost.SetActive(false);
            return true;
        }

        private void Update()
        {
            if (_errorRemaining <= 0f) { return; }

            _errorRemaining -= Time.deltaTime;
            if (_errorRemaining <= 0f && errorText != null)
            {
                errorText.gameObject.SetActive(false);
            }
        }
    }
}