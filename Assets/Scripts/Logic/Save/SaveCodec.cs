using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.Replay;

// 命名空间 DefenseCounter.Logic.Replay 与类 Replay 同名，字段名叫 Replay 时会歧义。
// 用别名把类型显式钉住，对外 API（SaveData.Replay）保持不变。
using ReplayType = DefenseCounter.Logic.Replay.Replay;

namespace DefenseCounter.Logic.Save
{
    /// <summary>
    /// 存档数据。刻意**不存 GameState 快照**，只存「配置引用 + 种子 + 命令序列」。
    ///
    /// 理由：
    /// 1. 体积小一个数量级（一局只有几十条命令）
    /// 2. 自带校验：重放后 FinalHash 不一致就说明存档损坏或代码行为变了
    /// 3. 不做版本迁移地狱：字段变了只影响元数据，命令格式有独立版本号
    /// </summary>
    public sealed class SaveData
    {
        public int Version = SaveCodec.CurrentVersion;

        public string LevelId;
        public uint Seed;
        public string PlayerLoadoutId;
        public string AiLoadoutId;
        public string DifficultyId;

        public MatchOutcome Outcome;
        public float Duration;
        public uint InitialHash;
        public uint FinalHash;

        public ReplayType Replay;
    }

    /// <summary>
    /// 存档编解码。纯文本、行式格式、可人工阅读、无第三方依赖。
    ///
    /// 格式：
    ///   DC_SAVE 1
    ///   level   level_01
    ///   seed    4242
    ///   player  loadout_iron
    ///   ai      loadout_blitz
    ///   diff    ai_normal
    ///   outcome 2
    ///   duration 340.50
    ///   hash_init  3116968702
    ///   hash_final 1193927103
    ///   F 0.1 3116968702 1        ← 帧头：dt / 状态哈希 / 本帧命令数
    ///   C B 1 bld_rail_sniper 10 10 0
    ///   F 0.1 3116968702 0
    ///
    /// 为什么不用 JSON：JsonUtility 在 UnityEngine 里（Logic 不能引用），
    /// 而自带 JSON 解析器要几百行且容易在浮点格式上翻车。行式格式够用且可读。
    /// </summary>
    public static class SaveCodec
    {
        public const int CurrentVersion = 1;

        private const string Header = "DC_SAVE";
        private const char FieldSep = ' ';

        // ---------- 编码 ----------

        public static string Encode(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var sb = new StringBuilder(1024);

            sb.Append(Header).Append(' ').Append(CurrentVersion).Append('\n');
            AppendKey(sb, "level", data.LevelId);
            AppendKey(sb, "seed", data.Seed.ToString(CultureInfo.InvariantCulture));
            AppendKey(sb, "player", data.PlayerLoadoutId);
            AppendKey(sb, "ai", data.AiLoadoutId);
            AppendKey(sb, "diff", data.DifficultyId);

            sb.Append("outcome ").Append((int)data.Outcome).Append('\n');
            sb.Append("duration ").Append(FormatFloat(data.Duration)).Append('\n');
            sb.Append("hash_init ").Append(data.InitialHash).Append('\n');
            sb.Append("hash_final ").Append(data.FinalHash).Append('\n');

            ReplayType replay = data.Replay;
            if (replay != null && replay.Frames != null)
            {
                sb.Append("frames ").Append(replay.Frames.Length).Append('\n');

                for (int i = 0; i < replay.Frames.Length; i++)
                {
                    ReplayFrame frame = replay.Frames[i];
                    GameCommand[] commands = frame.Commands ?? new GameCommand[0];

                    sb.Append('F').Append(FieldSep)
                        .Append(FormatFloat(frame.Dt)).Append(FieldSep)
                        .Append(frame.StateHash).Append(FieldSep)
                        .Append(commands.Length).Append('\n');

                    for (int c = 0; c < commands.Length; c++)
                    {
                        AppendCommand(sb, commands[c]);
                    }
                }
            }
            else
            {
                sb.Append("frames 0\n");
            }

            return sb.ToString();
        }

        private static void AppendKey(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append(' ').Append(Sanitize(value)).Append('\n');
        }

        private static void AppendCommand(StringBuilder sb, GameCommand cmd)
        {
            sb.Append('C').Append(FieldSep)
                .Append(CommandLetter(cmd.Type)).Append(FieldSep)
                .Append(cmd.PlayerId).Append(FieldSep)
                .Append(Sanitize(cmd.ConfigId)).Append(FieldSep)
                .Append(cmd.Cell.X).Append(FieldSep)
                .Append(cmd.Cell.Z).Append(FieldSep)
                .Append(cmd.InstanceId).Append('\n');
        }

        private static char CommandLetter(CommandType type)
        {
            switch (type)
            {
                case CommandType.BuildBuilding: return 'B';
                case CommandType.UpgradeBuilding: return 'U';
                case CommandType.SellBuilding: return 'S';
                case CommandType.DeployUnit: return 'D';
                case CommandType.UseSkill: return 'K';
                default: return 'L';
            }
        }

        private static CommandType LetterToCommand(char c)
        {
            switch (c)
            {
                case 'B': return CommandType.BuildBuilding;
                case 'U': return CommandType.UpgradeBuilding;
                case 'S': return CommandType.SellBuilding;
                case 'D': return CommandType.DeployUnit;
                case 'K': return CommandType.UseSkill;
                default: return CommandType.SelectLoadout;
            }
        }

        // ---------- 解码 ----------

        public static bool TryDecode(string text, out SaveData data, out string error)
        {
            data = null;
            error = null;

            if (string.IsNullOrEmpty(text))
            {
                error = "存档为空";
                return false;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length == 0)
            {
                error = "没有行";
                return false;
            }

            string[] head = SplitLine(lines[0]);
            if (head.Length < 2 || head[0] != Header)
            {
                error = "文件头不是 " + Header;
                return false;
            }

            int version;
            if (!int.TryParse(head[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
            {
                error = "版本号无法解析";
                return false;
            }

            if (version > CurrentVersion)
            {
                error = "存档版本 " + version + " 高于当前支持的 " + CurrentVersion;
                return false;
            }

            var result = new SaveData { Version = version };
            var frames = new List<ReplayFrame>();
            ReplayFrame current = default;
            var pending = new List<GameCommand>();
            bool inFrame = false;
            int declaredFrames = -1;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                string[] parts = SplitLine(line);
                string key = parts[0];

                switch (key)
                {
                    case "level": result.LevelId = parts.Length > 1 ? parts[1] : null; break;
                    case "player": result.PlayerLoadoutId = parts.Length > 1 ? parts[1] : null; break;
                    case "ai": result.AiLoadoutId = parts.Length > 1 ? parts[1] : null; break;
                    case "diff": result.DifficultyId = parts.Length > 1 ? parts[1] : null; break;
                    case "seed":
                        if (!TryUInt(parts, 1, out uint seed)) { error = "seed 解析失败"; return false; }
                        result.Seed = seed;
                        break;
                    case "outcome":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int oc))
                        {
                            error = "outcome 解析失败";
                            return false;
                        }
                        result.Outcome = (MatchOutcome)oc;
                        break;
                    case "duration":
                        if (!TryFloat(parts, 1, out float dur)) { error = "duration 解析失败"; return false; }
                        result.Duration = dur;
                        break;
                    case "hash_init":
                        if (!TryUInt(parts, 1, out uint hi)) { error = "hash_init 解析失败"; return false; }
                        result.InitialHash = hi;
                        break;
                    case "hash_final":
                        if (!TryUInt(parts, 1, out uint hf)) { error = "hash_final 解析失败"; return false; }
                        result.FinalHash = hf;
                        break;
                    case "frames":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out declaredFrames))
                        {
                            error = "frames 解析失败";
                            return false;
                        }
                        break;
                    case "F":
                    {
                        if (inFrame)
                        {
                            current.Commands = pending.ToArray();
                            frames.Add(current);
                            pending.Clear();
                        }

                        if (parts.Length < 4
                            || !TryFloat(parts, 1, out float dt)
                            || !TryUInt(parts, 2, out uint hash)
                            || !int.TryParse(parts[3], out int count))
                        {
                            error = "帧行格式错误（第 " + (i + 1) + " 行）";
                            return false;
                        }

                        current = new ReplayFrame { Dt = dt, StateHash = hash, Commands = new GameCommand[count] };
                        inFrame = true;
                        break;
                    }
                    case "C":
                        if (!TryCommand(parts, out GameCommand cmd, out error))
                        {
                            error = "命令行解析失败： " + error + "（第 " + (i + 1) + " 行）";
                            return false;
                        }
                        pending.Add(cmd);
                        break;
                    default:
                        // 未知键直接忽略：为后续版本加字段留余地
                        break;
                }
            }

            if (inFrame)
            {
                current.Commands = pending.ToArray();
                frames.Add(current);
            }

            if (declaredFrames >= 0 && declaredFrames != frames.Count)
            {
                error = "帧数不符：声明 " + declaredFrames + "，实际 " + frames.Count;
                return false;
            }

            result.Replay = new ReplayType
            {
                LevelId = result.LevelId,
                Seed = result.Seed,
                InitialHash = result.InitialHash,
                FinalHash = result.FinalHash,
                Frames = frames.ToArray()
            };

            data = result;
            return true;
        }

        private static bool TryCommand(string[] parts, out GameCommand cmd, out string error)
        {
            cmd = default;
            error = null;

            if (parts.Length < 7)
            {
                error = "字段不足";
                return false;
            }

            if (parts[1].Length == 0 || !byte.TryParse(parts[2], out byte playerId))
            {
                error = "玩家 id 无效";
                return false;
            }

            if (!int.TryParse(parts[4], out int cx) || !int.TryParse(parts[5], out int cz)
                || !int.TryParse(parts[6], out int instanceId))
            {
                error = "坐标或实例 id 无效";
                return false;
            }

            cmd = new GameCommand
            {
                Type = LetterToCommand(parts[1][0]),
                PlayerId = playerId,
                ConfigId = parts[3] == "-" ? null : parts[3],
                Cell = new GridPos(cx, cz),
                InstanceId = instanceId
            };

            return true;
        }

        // ---------- 工具 ----------

        private static string[] SplitLine(string line)
        {
            return line.Split(new[] { FieldSep }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>id 里不能有空格；有的话换成下划线（这类 id 本身就不该出现空格）。</summary>
        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "-";
            }

            return value.IndexOf(' ') >= 0 ? value.Replace(' ', '_') : value;
        }

        private static string FormatFloat(float value)
        {
            // "R" + InvariantCulture：往返精度，且不会出现逗号小数点
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool TryFloat(string[] parts, int index, out float value)
        {
            value = 0f;
            return parts.Length > index
                   && float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryUInt(string[] parts, int index, out uint value)
        {
            value = 0u;
            return parts.Length > index
                   && uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}