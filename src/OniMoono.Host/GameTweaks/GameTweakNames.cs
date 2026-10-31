#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace OniMoono.Host.GameTweaks
{
    /// A row's measured gain, as a player would put it ("autosave freeze ~12 s → ~5 s on a large
    /// colony"), with where it comes from so it can be refreshed: the colony, the platform and
    /// when it was measured, and the document holding the measurement.
    internal sealed record TweakGain(string Effect, string Colony, string Platform, string Measured, string Source, string? ZhEffect = null);

    /// A row's text in Simplified Chinese, shown when the game's language is Chinese.
    internal sealed record TweakTranslation(string Title, string Effect, string Detail = "");

    /// A section of the Moono Tweaks screen. `Gain`: one shared by all of its rows (the save
    /// tweaks are measured together). `ZhTitle`: the section's name in Simplified Chinese.
    internal sealed record TweakGroup(string Title, TweakGain? Gain = null, string? ZhTitle = null);

    /// One row of the Moono Tweaks screen and of docs/TWEAKS.md: `Title` in sentence case,
    /// `Effect` one short line on what a player gets from it, `Detail` (optional) how it does it,
    /// for the row's tooltip and the docs. `IsTweak` is false for the player's own options (the
    /// splash, frame stats, log paths and the GPU), which are rows but not `--game-tweak` names.
    /// `Choices`: a row with more values than on and off (the GPU's), in the order its button
    /// steps through them; null for a toggle. `Zh`: the row's text in Simplified Chinese; docs
    /// and option names stay English.
    internal sealed record TweakInfo(string Id, TweakGroup Group, string Title, string Effect, string Detail = "", TweakGain? Gain = null, bool IsTweak = true,
        string[]? Choices = null, TweakTranslation? Zh = null)
    {
        /// The launch option that sets this row (shown in its tooltip).
        public string LaunchOption => Id switch
        {
            GameTweakNames.SplashRow => "--splash moo",
            GameTweakNames.FrameStatsRow => "--frame-stats",
            GameTweakNames.LogPathsRow => "--log-paths hidden",
            GameTweakNames.GpuRow => "--gpu " + string.Join("|", Choices!),
            _ => "--game-tweak " + Id,
        };
    }

    /// Every row of the Moono Tweaks screen, with its group, title, one-liner and gain: the one
    /// source for the screen (MoonoTweaksMenu), for option parsing (All, ChangingBehaviour) and
    /// for docs/TWEAKS.md's list (generated from this; GameTweakCatalogTests fails when they
    /// disagree, `ONI_UPDATE_DOCS=1` rewrites it). Plain .NET with no game types: options are read
    /// before the game's assemblies (Harmony among them) can be resolved, and the tests compile it in.
    internal static class GameTweakNames
    {
        public const string SplashRow = "splash", FrameStatsRow = "frame-stats", LogPathsRow = "log-paths", GpuRow = "gpu";
        public const string FastSaveCompression = "fast-save-compression", BackgroundSave = "background-save", NoSaveGc = "no-save-gc",
            FastSerialization = "fast-serialization", FastParticleCollision = "fast-particle-collision";

        // The gains shown in the screen's tooltips. Measured on Linux under WSL2, where the game's
        // code ran about half as fast as on Windows (docs/PERFORMANCE.md, "How it's measured"); a
        // Windows remeasure is pending: update Effect, Platform and Measured together (and
        // PERFORMANCE.md).
        static readonly TweakGain SaveGain = new("autosave freeze ~12 s → ~5 s on a large colony",
            "U57", "Linux under WSL2", "by 2026-10-20", "docs/PERFORMANCE.md#the-tweaks (save tweaks: 11.7 → 4.8 s)",
            "大型殖民地自动存档冻结约 12 秒 → 约 5 秒");
        static readonly TweakGain JobsGain = new("+11% frame rate on a large colony",
            "U57", "Linux under WSL2", "by 2026-10-20", "docs/PERFORMANCE.md#the-tweaks (fast-jobs)",
            "大型殖民地帧率提升 11%");
        static readonly TweakGain OverlaysGain = new("temperature overlay ~40% cheaper",
            "U57", "Linux under WSL2", "2026-10-24", "docs/research/overlays.md (Infrared.Update 10.44 → 6.0 ms; 2minnow 6.53 → 3.9)",
            "温度叠加层开销降低约 40%");
        static readonly TweakGain ConveyorGain = new("no hitch on rail edits (was 0.05–0.5 s)",
            "U57", "Linux under WSL2", "2026-10-25", "docs/PERFORMANCE.md#the-tweaks (a rebuild 50–510 → 2–14 ms)",
            "修改轨道不再卡顿（原本 0.05–0.5 秒）");

        public static readonly TweakGroup General = new("General", ZhTitle: "通用");
        public static readonly TweakGroup Saving = new("Saving", SaveGain, "存档");
        public static readonly TweakGroup Simulation = new("Simulation", ZhTitle: "模拟");
        public static readonly TweakGroup Graphics = new("Graphics & overlays", ZhTitle: "图形");
        /// The tweaks that change what the game does: their own group, last, in the warning colour.
        public static readonly TweakGroup ChangesGameplay = new("Changes gameplay", ZhTitle: "改变玩法");

        public static readonly TweakGroup[] Groups = [General, Saving, Simulation, Graphics, ChangesGameplay];

        /// The rows in the screen's order (by group, then as listed).
        public static readonly TweakInfo[] Rows =
        [
            new(SplashRow, General, "Gassy Moo loading splash", "Gassy Moos rain over the window while the game starts, with a progress bar.", IsTweak: false,
                Zh: new("释气海牛加载画面", "游戏启动时，窗口上会下雨般落下释气海牛，并显示进度条。")),
            new(FrameStatsRow, General, "Frame stats in the log", "Notes where the frame time goes in Player.log, for reporting slowness.",
                "Every 60 frames.", IsTweak: false,
                Zh: new("日志中的帧统计", "在 Player.log 中记录帧时间花在哪里，用于反馈卡顿问题。", "每 60 帧一次。")),
            new(LogPathsRow, General, "Hide personal paths in the log", "Your user folder reads %USERPROFILE% or ~ in Player.log, and your Steam ID is hidden.",
                "Mods still get the full text.", IsTweak: false,
                Zh: new("隐藏日志中的个人路径", "Player.log 中你的用户文件夹会显示为 %USERPROFILE% 或 ~，Steam ID 也会被隐藏。", "模组仍能读到完整文本。")),
            new(GpuRow, General, "Graphics card", "For laptops with two GPUs: which one the game draws with. Needs a restart.",
                "Default: as the original game (on Windows the faster, discrete GPU; on Linux the system's choice). Discrete: the faster GPU. "
                + "Integrated: the power-saving one. Windows' Graphics settings page wins over this. On Linux, on a desktop whose display is "
                + "on the discrete card, discrete may pick the integrated GPU.",
                IsTweak: false, Choices: [OniMoono.Host.GpuChoice.Default, OniMoono.Host.GpuChoice.Discrete, OniMoono.Host.GpuChoice.Integrated],
                Zh: new("显卡", "适用于双显卡笔记本——选择游戏用哪块 GPU 渲染。需要重启。",
                    "默认：与原版游戏相同（Windows 上选较快的独立显卡；Linux 上由系统决定）。独立：较快的 GPU。集成：省电的那块。"
                    + "Windows 的“图形设置”页面优先级高于此项。在 Linux 上，如果桌面显示输出接在独立显卡上，选“独立”可能会选中集成显卡。")),

            new(FastSerialization, Saving, "Faster save writing", "Autosaves take less time; the save file is the same.",
                "Writes the colony's numbers without boxing each one in an object.",
                Zh: new("更快的存档写入", "自动存档耗时更短；存档文件不变。", "写入殖民地数据时不再把每个数值装箱成对象。")),
            new("fast-save-type-checks", Saving, "Faster save checks", "Autosaves skip repeating the same checks for every object.",
                "Remembers what saving asks about each kind of component instead of asking again for every object.",
                Zh: new("更快的存档检查", "自动存档不再对每个对象重复相同的检查。", "记住每种组件在存档时会被问什么，而不是每个对象都再问一遍。")),
            new(FastSaveCompression, Saving, "Faster save compression", "Saving and loading take seconds less; files stay compatible, a little larger.",
                "Compresses with .NET's zlib instead of the game's own; the files are about 14% larger.",
                Zh: new("更快的存档压缩", "存档和读档快几秒；文件保持兼容，但略大一些。", "改用 .NET 的 zlib 压缩，而不是游戏自带的；文件大约大 14%。")),
            new(BackgroundSave, Saving, "Background saving", "The save is compressed and written to disk while the game plays on.",
                "Compression and the file write move to another thread; quitting waits for them.",
                Zh: new("后台存档", "游戏继续运行时，存档在后台压缩并写入磁盘。", "压缩和写文件移到另一个线程；退出游戏时会等待它们完成。")),
            new(NoSaveGc, Saving, "No cleanup pause when saving", "Skips the memory cleanup the game forces after each save, a pause of its own.",
                "Skips the full garbage collection the game asks for after saving.",
                Zh: new("存档时不再强制清理内存", "跳过游戏每次存档后强制进行的内存清理——那本身也是一次卡顿。", "跳过存档后游戏请求的完整垃圾回收。")),

            new("fast-jobs", Simulation, "Faster worker threads", "Less time lost waking idle worker threads every frame.",
                "Wakes only as many of the game's worker threads as a job has work for, instead of all of them for every job (about 14 a frame).", JobsGain,
                Zh: new("更快的工作线程", "减少每帧唤醒空闲工作线程的时间损失。", "只唤醒当前任务实际需要的游戏工作线程，而不是每个任务都唤醒全部（每帧约 14 个任务）。")),
            new("fast-sublimation", Simulation, "Faster sublimation", "Items that can't sublimate (polluted dirt in full gas) are skipped.",
                "Items already blocked by full gas cells are skipped in one batched update.",
                Zh: new("更快的升华", "无法升华的物品（如满气体环境中的污染土）会被跳过。", "已被满气体格挡住的物品会合并成一次批量更新跳过。")),
            new(FastParticleCollision, Simulation, "Faster radbolt collisions", "Radbolts look for each other only where one can be.",
                "A radbolt skips searching every stored item nearby when no other radbolt is close.",
                Zh: new("更快的辐射粒子碰撞", "辐射粒子只在可能有其他粒子的地方互相查找。", "附近没有其他辐射粒子时，粒子不再搜索附近所有存储物品。")),
            new("fast-offset-tables", Simulation, "Faster pickup checks", "Where an item can be picked up from is rechecked only when its surroundings change.",
                "Fetch updates recheck only the items whose work spots can have changed.",
                Zh: new("更快的拾取检查", "物品可被拾取的位置只在其周围环境变化时才重新检查。", "搬运更新只重新检查工作点可能发生变化的物品。")),
            new("fast-partitioner-cleanup", Simulation, "Faster spatial index cleanup", "The once-a-second tidy-up of what is where looks only at what was removed.",
                "The spatial index's cleanup visits only the entries that were removed.",
                Zh: new("更快的空间索引清理", "每秒一次“什么在哪里”的整理只查看被移除的内容。", "空间索引的清理只访问被移除的条目。")),
            new("fast-conveyor-paths", Simulation, "Faster conveyor rebuilds", "Building or removing conveyor rails no longer stalls the game.",
                "The rails' network rebuild finds its routes from an index instead of searching every kept route again.", ConveyorGain,
                Zh: new("更快的传送带重建", "建造或拆除传送带轨道不再让游戏卡顿。", "轨道网络重建从索引中查找路线，而不是把保留的每条路线都重新搜索一遍。")),

            new("fast-anim-events", Graphics, "Faster animation events", "Animations check their sound and effect cues with less work.",
                "Keeps a compact copy of what each playing animation's per-frame event check reads.",
                Zh: new("更快的动画事件", "动画检查音效和特效提示时工作量更少。", "为每个正在播放的动画保留一份紧凑副本，供其每帧事件检查读取。")),
            new("fast-anim-cleanup", Graphics, "Faster animation cleanup", "Removed animations are swept out only where one was removed.",
                "The sweep for destroyed animations skips grid chunks that can't hold any.",
                Zh: new("更快的动画清理", "被移除的动画只在发生移除的地方被清扫。", "销毁动画的清扫会跳过不可能含有动画的网格区块。")),
            new("fast-anim-batches", Graphics, "Parallel animation uploads", "Animation data for the graphics card is prepared on several threads.",
                "Animation batches write their instance and symbol data on worker threads, the rest in the game's order.",
                Zh: new("并行动画上传", "显卡所需的动画数据在多个线程上准备。", "动画批次在工作线程上写入实例和符号数据，其余按游戏原有顺序进行。")),
            new("fast-symbol-overrides", Graphics, "Fewer animation uploads", "Swapped animation parts are re-sent to the graphics card only when they change.",
                "Unchanged symbol-override blocks of Duplicants and buildings aren't uploaded again.",
                Zh: new("更少的动画上传", "被替换的动画部件只在变化时才重新发送到显卡。", "复制人和建筑未变的符号覆盖块不再重复上传。")),
            new("fast-overlays", Graphics, "Faster overlays", "Overlays skip rereading what can't have changed since the last frame.",
                "Temperature passes over buildings out of view without reading them, power asks each circuit's load once a frame, the rest keep what their targets' visits read.", OverlaysGain,
                Zh: new("更快的叠加层", "叠加层跳过重读上一帧以来不可能变化的内容。", "温度叠加层对视野外建筑直接略过不读取，电力每帧只查询每个电路负载一次，其余保留其目标访问时读到的值。")),
            new("fast-overlay-textures", Graphics, "Overlay texture cleanup", "Opening and closing overlays no longer piles up video memory.",
                "The screen-sized textures the game remakes on each coloured overlay toggle are freed, instead of kept until the next load (about 18 MB a toggle at 1024×768).",
                Zh: new("叠加层纹理清理", "反复开关叠加层不再累积显存。", "游戏每次切换彩色叠加层时重建的全屏纹理现在会被释放，而不是保留到下次加载（1024×768 下每次切换约 18 MB）。")),

            new("slow-reachability", ChangesGameplay, "Slower reachability checks", "Duplicants notice a newly opened or blocked path up to 3 s later.",
                "Which errands and items can be reached is rechecked every 4 s instead of every second; temperature and building checks are never delayed.",
                Zh: new("更慢的可达性检查", "复制人对新打开或被阻断的路径最多晚 3 秒才察觉。", "哪些任务和物品可达改为每 4 秒重新检查一次，而不是每秒；温度和建筑检查永不延迟。")),
            new("off-screen-conveyors", ChangesGameplay, "Lazy unseen conveyor items", "Items riding rails off screen are placed only when they stop or come into view.",
                "The rails carry exactly the same things, but items' positions lag while off screen.",
                Zh: new("屏幕外传送带物品延迟处理", "屏幕外轨道上的物品只在停下或进入视野时才放置。", "轨道运送的东西完全相同，但物品位置在屏幕外会滞后。")),
            new("off-world-fallers", ChangesGameplay, "Land items lost below a world", "Items that fall out the bottom of a world (a rocket's chute) stop falling forever.",
                "They hang just below the world, stepped as falling every tick; landing them where they hang stops that cost.",
                Zh: new("让掉出世界底部的物品落地", "掉出世界底部（火箭发射井）的物品不再无限下落。", "它们会悬在世界正下方，每个 tick 都按下落步进；让它们停在悬挂处即可消除这项开销。")),
            new("brain-rate", ChangesGameplay, "Critter and dupe brain updates capped at 30 updates a second", "Critters and dupes would otherwise update on every frame. This trims that bookkeeping above 30 fps.",
                "The game updates 1 Duplicant and 5 critter brains every rendered frame; this caps them at 30 and 150 a second, spread over frames. One that just finished an errand or is under threat still gets the next update. Saves time only above 30 fps.",
                Zh: new("小动物和复制人脑更新上限为每秒 30 次", "小动物和复制人原本每帧都会更新。此项在 30 fps 以上削减这部分簿记开销。", "游戏每渲染帧更新 1 个复制人和 5 个小动物的脑；此项将其限制为每秒 30 和 150 次，并分摊到各帧。刚完成任务或受到威胁的个体仍会立即获得下一次更新。仅在 30 fps 以上节省时间。")),
        ];

        /// The tweaks in src/OniMoono.GameTweaks (named here: options are read before that assembly is loaded).
        public static readonly string[] Typed = ["off-screen-conveyors", "fast-save-type-checks", "fast-anim-cleanup", "fast-sublimation", "fast-anim-events", "fast-symbol-overrides", "fast-jobs", "fast-offset-tables", "slow-reachability", "off-world-fallers", "fast-partitioner-cleanup", "fast-anim-batches", "fast-overlays", "fast-conveyor-paths", "fast-overlay-textures", "brain-rate"];

        /// Every `--game-tweak` name, in the screen's order.
        public static readonly string[] All = Rows.Where(r => r.IsTweak).Select(r => r.Id).ToArray();

        /// The tweaks that change what the game does (each documented in its class); `exact` turns
        /// on every tweak but these.
        public static readonly string[] ChangingBehaviour = Rows.Where(r => r.IsTweak && r.Group == ChangesGameplay).Select(r => r.Id).ToArray();

        /// A `--game-tweak` value: a tweak's name, `-NAME` (left out of `all` or `exact`, for
        /// measuring one against the rest), `all`, `exact` (all but ChangingBehaviour) or `none`
        /// (no tweak unless one is named, whatever the settings file says: test runs).
        public static bool IsKnown(string name) => name is "all" or "exact" or "none" || Array.IndexOf(All, name.StartsWith('-') ? name.Substring(1) : name) >= 0;

        /// The screen's text in Simplified Chinese, keyed by its English (the screen looks each
        /// string up when the game is in Chinese). The catalog's titles, one-liners, details and
        /// measured gains, and the GPU choices, all come from here; the screen's own labels do not.
        public static Dictionary<string, string> Chinese()
        {
            static string Capitalized(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;
            var text = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in Groups)
            {
                if (group.ZhTitle == null) continue;
                text[group.Title] = group.ZhTitle;
                if (group.Gain?.ZhEffect != null)
                    text[$"{group.Title} together: {group.Gain.Effect}"] = $"{group.ZhTitle}调整项合计：{group.Gain.ZhEffect}";
            }
            foreach (var row in Rows)
            {
                if (row.Zh == null) continue;
                text[row.Title] = row.Zh.Title;
                text[row.Effect] = row.Zh.Effect;
                if (row.Detail.Length > 0 && row.Zh.Detail.Length > 0) text[row.Detail] = row.Zh.Detail;
                if (row.Gain?.ZhEffect != null) text[Capitalized(row.Gain.Effect)] = row.Gain.ZhEffect;
            }
            text[OniMoono.Host.GpuChoice.Default] = "默认";
            text[OniMoono.Host.GpuChoice.Discrete] = "独立";
            text[OniMoono.Host.GpuChoice.Integrated] = "集成";
            return text;
        }

        /// docs/TWEAKS.md's list of rows, between its `<!-- tweaks:begin -->` and
        /// `<!-- tweaks:end -->` markers.
        public static string Markdown()
        {
            var md = new System.Text.StringBuilder();
            md.Append("<!-- tweaks:begin: generated from src/OniMoono.Host/GameTweaks/GameTweakNames.cs; ONI_UPDATE_DOCS=1 with the unit tests rewrites it -->\n");
            foreach (var group in Groups)
            {
                var rows = Rows.Where(r => r.Group == group && r.IsTweak).ToArray();
                if (rows.Length == 0) continue;
                string defaults = group == ChangesGameplay ? "off by default" : "on by default";
                md.Append($"\n### {group.Title} ({defaults})\n\n");
                if (group.Gain != null) md.Append($"All {rows.Length} together: {Gain(group.Gain)}.\n\n");
                foreach (var r in rows)
                {
                    md.Append($"- **{r.Title}** (`{r.Id}`): {r.Effect}");
                    if (r.Detail.Length > 0) md.Append(' ').Append(r.Detail);
                    if (r.Gain != null) md.Append($" *Measured: {Gain(r.Gain)}.*");
                    md.Append('\n');
                }
            }
            md.Append("<!-- tweaks:end -->");
            return md.ToString();
        }

        static string Gain(TweakGain g) => $"{g.Effect}; {g.Colony}, {g.Platform}, {g.Measured}";
    }
}
