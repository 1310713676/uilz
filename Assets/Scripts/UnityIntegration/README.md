# UnityIntegration 接入说明

> 这个目录的代码**不参与本机编译**（依赖 `UnityEngine.UI` / `Physics` / `Input`）。
> 逻辑全部在已测试的 `Logic` 层，这里只做「接线」。
>
> 拷到 Unity：`Assets/Scripts/UnityIntegration/`

---

## 一、文件清单

| 文件 | 内容 | 挂在哪 |
|---|---|---|
| `UGuiViews.cs` | 4 个视图实现：`UGuiHudView` / `UGuiBuildMenuView` / `UGuiSkillBarView` / `UGuiPlacementView` | 分别挂 4 个 UI 物体 |
| `BattleUIHook.cs` | 组装 + 输入处理 | 挂 1 个空物体 |

---

## 二、场景结构（照着摆）

```
Battle.unity
├── Main Camera            正交，Size 40，朝向 Y-，y = 40
├── Directional Light
├── Ground                 ← 必须有 Collider！命名随便，Layer 设为 "Ground"
├── BattleController       ← 挂 BattleSceneController（拖 GameDatabase）
└── UIRoot (Canvas)
    ├── HudPanel           ← 挂 UGuiHudView
    │   ├── GoldText / EnergyText / TimeText
    │   ├── BaseHealthFill  (Image, Type=Filled)
    │   ├── BaseHealthText / SlotText / BuildingText
    │   ├── DamageFlash     (全屏红色 Image，默认 SetActive(false))
    │   └── OutcomePanel    (默认 SetActive(false)) └── OutcomeText
    ├── BuildMenuPanel     ← 挂 UGuiBuildMenuView
    │   ├── ItemContainer   (Horizontal Layout Group)
    │   ├── PlacingHint     (默认 SetActive(false)) └── PlacingLabel
    │   └── CancelButton
    ├── SkillBarPanel      ← 挂 UGuiSkillBarView
    │   └── ItemContainer
    ├── ErrorText          ← UGuiPlacementView 的 errorText
    ├── GhostPrefab        ← 一个 Quad 或 Plane（Material 随便，代码会替换）
    └── UIHook             ← 挂 BattleUIHook
```

---

## 三、三个 Prefab 的硬性要求

**1. BuildMenu 的 itemPrefab**

```
ItemPrefab (Button)
└── Label (Text)      ← 代码用 GetComponentInChildren<Text>() 找它
```

**2. SkillBar 的 itemPrefab**

```
SkillItemPrefab (Button)
├── Label (Text)
└── Cooldown (Image)  ← 名字必须是 "Cooldown"，代码用它显示冷却
                          Image Type 设为 Filled / Radial 360
```

**3. GhostPrefab**

一个扁平的 Quad 或 Cube。代码会改写它的 `position` / `localScale` / `sharedMaterial`，
所以**不要给它加脚本**，也不要指望它的原始尺寸。

---

## 四、Inspector 拖引用

`BattleUIHook` 上 5 个字段：

| 字段 | 拖什么 |
|---|---|
| `battle` | BattleController 物体 |
| `hudView` | HudPanel |
| `buildMenuView` | BuildMenuPanel |
| `skillBarView` | SkillBarPanel |
| `placementView` | UGuiPlacementView 所在物体（可以就挂在 UIPlaceholder 上） |

`worldCamera` 留空会自动用 `Camera.main`。

`groundMask` 建议手动设为 `Ground` 层，否则射线会打到建筑和兵身上。

---

## 五、跑起来的顺序

```
1. Play
2. Console 应出现：
   [Battle] 装配完成 seed=... 建筑=6 兵种=4 技能=2
   [UI] 已接管：6 建筑可建
3. 金条开始涨（10/秒）
4. 建造菜单出现 5 个按钮（钢铁防线的 5 个建筑）
5. 点「激光哨戒塔」→ 出现放置提示 → 点地面 → 塔开始建造（黄框）
6. Scene 视图能看到 AI 在自己基地旁造塔
```

---

## 六、已知简化（刻意的）

| 简化 | 原因 |
|---|---|
| 技能直接瞄准敌方基地 | 少一个「选点」状态机；AI 也是这么做的，人机一致 |
| 触摸与鼠标共用 `Input.mousePosition` | 手机的 `GetMouseButton` 映射到单指触摸，够用；多指缩放留给第二阶段 |
| 兵种出兵 UI 未做 | 出兵走 `GameCommand.Deploy`，需要一个「兵种选择 + 点地面」的流程，属于下一批 |
| 建筑升级/拆除 UI 未做 | 需要「点击已有建筑 → 弹出操作面板」，属于下一批 |

---

## 七、如果编译报错

本目录没做过类型检查，最可能的错误类型：

| 报错 | 原因 |
|---|---|
| `Text` / `Button` / `Image` 找不到 | 没装 `com.unity.ugui`（Packages/manifest.json 缺 `"com.unity.ugui"`） |
| `ConfigSeeder` 找不到 | `Assets/Scripts/Core` 没引入，或 asmdef 没引用 Core |
| `MatchBootstrap` 找不到 | 同上 |
| `BattleSceneController` 找不到 | `Assets/Scripts/Core` 没引入 |

**最可能的错因是 asmdef 少引用**，不是代码本身。检查顺序：
`UnityIntegration.asmdef` → References 里应该有 `DefenseCounter.Core`。
