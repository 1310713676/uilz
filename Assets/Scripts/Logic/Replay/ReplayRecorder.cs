using System.Collections.Generic;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.Replay
{
    /// <summary>一帧的回放数据：本帧输入 + 执行后的状态哈希。</summary>
    public struct ReplayFrame
    {
        public float Dt;
        public GameCommand[] Commands;
        public uint StateHash;
    }

    /// <summary>
    /// 一局的完整录像。命令序列 + 逐帧哈希足以精确复现任何一局。
    ///
    /// 不含 GameState 快照：还原靠「初始配置 + 命令序列 + 固定 dt」重跑一遍，
    /// 这样文件小，也能验证确定性本身是否成立。
    /// </summary>
    public sealed class Replay
    {
        public string LevelId;
        public uint Seed;
        public uint InitialHash;
        public uint FinalHash;
        public ReplayFrame[] Frames;

        public int FrameCount => Frames != null ? Frames.Length : 0;

        /// <summary>首帧到末帧是否哈希一致（回放自检用）。</summary>
        public bool HashChainValid
        {
            get
            {
                if (Frames == null || Frames.Length == 0)
                {
                    return true;
                }

                return Frames[Frames.Length - 1].StateHash == FinalHash;
            }
        }

        public override string ToString()
        {
            return "Replay[" + LevelId + "] frames=" + FrameCount
                   + " seed=" + Seed + " hash=" + FinalHash;
        }
    }

    /// <summary>
    /// 录制器。用法非常固定，顺序不能变：
    ///
    ///   recorder.BeforeTick();          // 清空本帧命令缓冲
    ///   ai.Tick(dt); loop.Tick(dt);    // 执行一帧（AI 与玩家的命令都走同一个 processor）
    ///   recorder.AfterTick(state, dt); // 收本帧命令 + 记哈希
    ///
    /// 玩家点击发生在两帧之间时，也归入下一帧 —— 与网络层的命令分组语义一致。
    /// </summary>
    public sealed class ReplayRecorder
    {
        private readonly RequestProcessor _processor;
        private readonly List<ReplayFrame> _frames = new List<ReplayFrame>();
        private readonly List<GameCommand> _buffer = new List<GameCommand>();

        private string _levelId = string.Empty;
        private uint _seed;
        private uint _initialHash;

        public ReplayRecorder(RequestProcessor processor)
        {
            _processor = processor;
        }

        public bool IsRecording { get; private set; }

        public int FrameCount => _frames.Count;

        public uint InitialHash => _initialHash;

        public void Start(GameState state, string levelId, uint seed)
        {
            _frames.Clear();
            _buffer.Clear();
            _levelId = levelId ?? string.Empty;
            _seed = seed;
            _initialHash = StateHash.Compute(state);

            _processor.RecordAcceptedCommands = true;
            _processor.AcceptedCommands.Clear();

            IsRecording = true;
        }

        public void BeforeTick()
        {
            if (!IsRecording)
            {
                return;
            }

            _buffer.Clear();
            _processor.AcceptedCommands.Clear();
        }

        public void AfterTick(GameState state, float dt)
        {
            if (!IsRecording)
            {
                return;
            }

            List<GameCommand> log = _processor.AcceptedCommands;
            for (int i = 0; i < log.Count; i++)
            {
                _buffer.Add(log[i]);
            }

            _frames.Add(new ReplayFrame
            {
                Dt = dt,
                Commands = _buffer.ToArray(),
                StateHash = StateHash.Compute(state)
            });
        }

        public Replay Build()
        {
            IsRecording = false;
            _processor.RecordAcceptedCommands = false;

            ReplayFrame[] frames = _frames.ToArray();

            return new Replay
            {
                LevelId = _levelId,
                Seed = _seed,
                InitialHash = _initialHash,
                FinalHash = frames.Length > 0 ? frames[frames.Length - 1].StateHash : _initialHash,
                Frames = frames
            };
        }
    }
}