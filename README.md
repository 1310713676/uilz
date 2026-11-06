# Defense Counter / 防线反击 — Unity 工程

Android 科幻塔防 + 出兵 + 反击。逻辑层 198 条断言全部通过，本仓库用于**云构建出 APK**。

---

## 这是什么

```
Assets/Scripts/Logic/              纯 C# 游戏逻辑（零 UnityEngine，198 条断言验证）
Assets/Scripts/Core/               ScriptableObject 配置 + 对局装配
Assets/Scripts/UnityIntegration/   UGUI 视图 + 输入接线
Assets/Editor/BuildScript.cs       云构建入口（自动生成资产 + 场景 + 打包）
```

**注意**：本仓库没有美术资源、没有手写场景、没有预制体。
首次构建时 `BuildScript` 会**用代码生成**配置资产和场景——所以仓库很干净，克隆下来就能构建。

---

## 这个 APK 里有什么

| 有 | 没有 |
|---|---|
| AI 在自己基地旁建造防御塔 | 任何 UI 按钮 |
| AI 出兵、沿 A\* 路径进攻 | 任何美术模型 |
| 塔开火、单位死亡、建筑被摧毁 | 音效 |
| 基地血量下降（Gizmos 柱表示） | 联机 |

**Scene 视图能看到全部战斗过程**，但手机屏幕上只有深色背景 —— 因为还没有 UI。
这一版的目标是**验证 Core 层能否在真机跑起来**，不是"能玩"。

---

## 云构建步骤

### 1. 推送代码

```bash
git init
git add .
git commit -m "init"
git branch -M main
git remote add origin https://github.com/<你的用户名>/<仓库名>.git
git push -u origin main
```

### 2. 配 3 个 Secret

`仓库 → Settings → Secrets and variables → Actions → New repository secret`

| Name | Value |
|---|---|
| `UNITY_LICENSE` | `.ulf` 文件的**全部内容** |
| `UNITY_EMAIL` | Unity 账号邮箱 |
| `UNITY_PASSWORD` | Unity 账号密码 |

`.ulf` 获取方式：见 `docs/云构建操作指引.md`。

### 3. 触发构建

推送到 `main` 会自动触发，也可以在 `Actions` 页面点 `Run workflow`。

### 4. 下载 APK

构建成功后，本次运行的 **Artifacts** 里有 `DefenseCounter-APK`。

---

## 构建预期

| 阶段 | 时长 |
|---|---|
| 首次（无 Library 缓存） | 15–25 分钟 |
| 后续（有缓存） | 5–10 分钟 |

产物大小：约 **25–40 MB**（IL2CPP + ARM64，无美术资源）。

---

## 如果构建失败

按这个顺序看日志：

1. **License 相关** → Secret 缺项或 `.ulf` 被截断（换行丢失最常见）
2. **`DefenseCounter.Logic` 找不到** → asmdef 没提交，或 `noEngineReferences` 被误改为 false
3. **`UnityEngine.UI` 找不到** → `Packages/manifest.json` 里缺 `com.unity.ugui`
4. **编译错误在 `UnityIntegration`** → 该目录未做过类型检查，属于预期风险
5. **IL2CPP 链接失败** → `Assets/Scripts/Core/link.xml` 是否正确保留

---

## 本地调试（有 PC 时）

```
Unity Hub 打开本目录（2021.3.33f1）
  → 菜单 DefenseCounter → Seed Data Assets   生成配置
  → 菜单 DefenseCounter → Build Android APK  或者直接 Play
  → Scene 视图看 Gizmos
```

Play 之后 Console 应出现：

```
[Battle] 装配完成 seed=... 建筑=6 兵种=4 技能=2
[UI] 已接管：6 建筑可建
[Battle] 对局结束: Lose  时长 340s
```

---

## 验证状态

| 项 | 状态 |
|---|---|
| 逻辑层（198 条断言） | ✅ 实际执行通过 |
| Core 层编译 | ✅ 0 warning 0 error |
| Core 层运行时 | ❌ 未执行 |
| UnityIntegration | ❌ 未编译验证 |

**这版构建的主要目的是把"❌ 未执行"变成有日志的实测。**