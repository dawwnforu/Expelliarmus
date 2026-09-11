# Expelliarmus / 除你武器 1.0.8

你的零食？现在是我的了。给损友一点小小的反击！ / Your snack? Mine now. A little payback for your prankster pals.

[新手安装与更新 / Beginner installation and updates](#新手安装与更新--beginner-installation-and-updates)

PEAK BepInEx Mod：伸出右手，瞄准队友手中的物品，将它抢到自己手里。

## 操作

- 按住游戏的“次要使用/伸手”键，默认鼠标右键；支持游戏内改键。
- 可以先伸手，再对准物品。同一次按住只抢一次，松开后可以再次抢夺。
- 目标为队友从第 1/2/3 格拿出的可丢弃物品，最大距离 7.5 米。
- 自己已经拿着物品时，先把原物品放到地上，再抢夺。菜单、轮盘、昏迷和攀爬期间不触发。

## 联机与物品转移

使用游戏现有 RPC，设计上只需抢夺者安装，房主和队友无需安装本 Mod。

1. 按选中格子和物品唯一编号核对目标，不按物品种类猜测格子。
2. 请求房主通过原生流程在抢夺者手边生成交接物品并更新库存；确认编号后暂停交接物品的物理运动，等待旧手持对象释放。
3. 确认地面物品编号一致，再让原持有者通过原生卸下流程释放手持对象。
4. 等到旧手持对象已从网络中移除，再请求拾取新的房间物品。
5. 确认自己的库存收到该编号后才记录成功。

转移期间不重复发送请求。未收到确认时取消后续拾取；已经生成的地面物品保留，不凭客户端缓存复制或恢复物品。地面物品也可能被其他玩家先捡走。

**仅抢夺者安装的限制：** 原生掉落请求只有格子编号，不能在房主执行时检查“该格是否仍为指定唯一编号”。若队友恰好在网络传输期间替换同一格物品，房主可能把替换后的物品放到地上；Mod 会拒绝拾取编号不符的物品。转移过程中切换装备也可能被原生卸下请求打断，需要重新选取。完全排除这些竞态需要房主及持有者配合安装处理逻辑。

## 安装与构建

需要 PEAK 和 BepInEx 5。将 `Expelliarmus.dll` 放到：

```text
PEAK\BepInEx\plugins\Expelliarmus\Expelliarmus.dll
```

关闭 PEAK 后，在本项目目录构建并安装：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -PeakDir '<PEAK游戏目录>' -Install
```

省略 `-Install` 只生成项目内的 DLL/PDB。

## 验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1 -PeakDir '<PEAK游戏目录>'
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1
```

`test.ps1` 执行实际行为代码的回归检查（Unity/网络替身），并核对安装游戏的 RPC 签名。覆盖同类物品、空格残留数据、菜单/昏迷/攀爬、持续伸手、重复触发、掉落超时、旧对象未释放、拾取拒绝和房主格子变化。

`verify.ps1` 检查安装文件与版本；游戏运行过新版后可加 `-RequireRuntimeLoad` 检查加载日志。这些检查不能替代双人 PEAK 实测。旧的 `simulate-network.ps1` 只模拟 1.0.4 的等待策略，不用于验证 1.0.6 的物品转移。

双人实测重点：抢夺者分别作为房主/非房主、队友不装 Mod、两件同类物品、自己手持物品、队友中途换物、高延迟，以及物品数量和剩余使用次数是否一致。

## 1.0.6 实测状态

1.0.5 已由作者验证：加入朋友房间、仅抢夺者安装时可以成功抢夺。1.0.6 改为手边交接，超时恢复物理运动，减少物品先落地再到手的过程；新版视觉效果和高延迟表现仍待游戏内验证。房主场景未确认。


---

## 新手安装与更新 / Beginner installation and updates

**不需要会编程，不需要下载 GitHub 源码。** DLL 是 Mod 文件，BepInEx 是让游戏加载这些文件的基础工具。ZIP 只是压缩包，下载到“下载”文件夹不代表已经安装。

本教程只使用 `<PEAK游戏目录>` 等占位符，没有作者的私人目录。占位符表示你实际找到的文件夹，**不要创建一个叫 `<PEAK游戏目录>` 的文件夹**。

### 先选平台和安装方式

| 你的情况 | 使用哪条教程 |
| --- | --- |
| Windows，第一次装 Mod | 方法一：Thunderstore Mod Manager |
| Windows，想自己管理文件，或之前已经手动安装 | 方法二：手动安装 ZIP |
| Mac / macOS | 先看下方 Mac 说明；目前没有本 Mod 的原生 Mac 安装方案 |

同一套游戏环境选一种方法即可。管理器的“配置 / Profile”是一套单独保存的 Mod 组合，不等于 Steam 游戏目录；不要再把同一个 DLL 手动塞进管理器配置里。

### 方法一：Windows 使用 Thunderstore Mod Manager

1. 在 Steam 安装 PEAK，正常启动一次，确认游戏能运行，再退出。
2. 从 [Thunderstore 官方入口](https://get.thunderstore.io/)下载安装管理器。不要把管理器安装程序放进游戏的 plugins 文件夹。
3. 打开管理器，搜索并选择 **PEAK**。创建一个 **Profile / 配置**，例如 `MyMods`，然后进入它。
4. 在在线 Mod 列表搜索 **Expelliarmus**，确认作者是 **dawwnforu**。也可以在 [本 Mod 页面](https://thunderstore.io/c/peak/p/dawwnforu/Expelliarmus/)点击 **Install with App / 使用管理器安装**。
5. 点击 **Install / Download / 安装**，选择最新版本，并同意安装依赖 **BepInExPack_PEAK**。依赖是必需组件；管理器负责下载和放置文件，你不用选择 DLL 目录。
6. 在 **Installed / 已安装** 中确认本 Mod 和 BepInEx 已启用，然后点击 **Start modded / 启动模组游戏**。先用这个按钮启动，确保使用的是这套配置。
7. 进入游戏验证：按住鼠标右键伸手，对准队友手里的可丢弃物品；每次松开后才能再抢。

**以后更新：** 先退出游戏，打开同一个配置，在已安装列表查看更新提示，点击 **Update / 更新**（或全部更新）。管理器会处理该配置中的旧版文件，无需先卸载或清缓存。按钮名称可能随管理器版本变化。不要把“管理器自身更新”误认为“所有 Mod 都已更新”。

**新版没显示？** 刷新列表或稍后再打开；[官方说明](https://wiki.thunderstore.io/mods/mod-not-visible)指出，新版本可能因缓存延迟出现在管理器中。单纯从网页下载过 ZIP，不会让游戏内自动弹出更新提醒。

**已经手动装过，想改用管理器？** 关闭游戏，将原来手动安装的本 Mod DLL 移到游戏目录外备份，然后在管理器配置中重新安装。管理器通常不会替你清理 Steam 目录中手动放进去的文件；不要删除其他 Mod 或整个 BepInEx 文件夹。

### 方法二：Windows 手动下载安装包

**第一步：找到正确的游戏目录。** Steam → 库 → 右键 **PEAK** → **管理 / Manage** → **浏览本地文件 / Browse local files**。打开的文件夹里应该能看到 `PEAK.exe` 和 `PEAK_Data`。下文称它为 `<PEAK游戏目录>`，它不一定在 C 盘。

**第二步：首次安装 BepInEx。** 如果这套游戏已经能加载 BepInEx Mod，跳到第三步。

1. 完全退出 PEAK。
2. 打开 [BepInExPack_PEAK 官方包](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/)，点击 **Download / 手动下载**。
3. 在下载文件夹右键 ZIP → **全部解压 / Extract All**。先解压到临时文件夹。
4. 打开解压包里的 `BepInExPack` 文件夹，把它**里面的内容**复制到 `<PEAK游戏目录>`，而不是把外面的 `BepInExPack` 文件夹整层复制进去。
5. 检查 `winhttp.dll`、`doorstop_config.ini` 和 `BepInEx` 文件夹是否与 `PEAK.exe` 在同一层。
6. 从 Steam 启动 PEAK 一次，然后退出。查看 `<PEAK游戏目录>\BepInEx\LogOutput.log` 是否生成；没有日志时先检查上述位置，不要反复叠加安装。

**第三步：安装本 Mod。**

1. 在 [本 Mod 的 Thunderstore 页面](https://thunderstore.io/c/peak/p/dawwnforu/Expelliarmus/)点击 **Download / 手动下载**，选择最新版本。不要用 GitHub 的 **Code → Download ZIP**，那是源码，不是可直接安装的发布包。
2. 解压下载的 ZIP，在其中找到 `plugins/Expelliarmus/Expelliarmus.dll`。DLL 不需要双击打开。
3. 在 `<PEAK游戏目录>\BepInEx\plugins` 里创建 `Expelliarmus` 文件夹（已存在就使用原文件夹）。将 `Expelliarmus.dll` 复制进去。
4. 最终位置必须是：

   ```text
   <PEAK游戏目录>\BepInEx\plugins\Expelliarmus\Expelliarmus.dll
   ```

5. 不要多套一层 `BepInEx\BepInEx`。`README.md`、`manifest.json`、封面图和 ZIP 本身不用放进 plugins。
6. 重启 PEAK。用记事本打开 `BepInEx\LogOutput.log`，搜索 `Expelliarmus`，应能看到加载的版本和 `loaded successfully!`。这确认 Mod 已加载，具体功能还需要游戏内验证。

**手动更新旧版：** 退出游戏 → 下载并解压新版 → 用新版 `Expelliarmus.dll` 覆盖上述位置的同名旧文件 → 重启游戏。**仅下载不会自动替换。** 不需要清缓存、删存档或重装 BepInEx。只保留一份本 Mod DLL；旧版备份放到游戏目录外，不能改名为 `旧版.dll` 后继续留在 plugins 子目录。

### Mac / macOS 用户请先看这里

[PEAK 的 Steam 系统要求](https://store.steampowered.com/app/3527290/PEAK/)目前列的是 Windows；本包面向 Windows 版 `PEAK.exe`，**没有经过原生 macOS 或 Mac 兼容环境测试**。Thunderstore Mod Manager 的 Windows 安装程序不能直接作为 Mac App 打开。

如果你还不能在 Mac 上运行未装 Mod 的 PEAK，请先解决游戏本体的运行问题；只下载本 Mod 或 macOS 版 BepInEx 并不能让 Windows 游戏直接运行。

如果你**已经通过 CrossOver / Wine 等兼容环境正常运行 Windows 版 PEAK**，可按下面的实验性手动路线操作：

1. 退出游戏。在兼容工具里选中运行 PEAK 的同一个 **Bottle / 容器**（它是一套虚拟 Windows 环境）。
2. 打开该容器里的 Windows Steam，通过 **PEAK → 管理 → 浏览本地文件** 找到有 `PEAK.exe` 的目录；或用容器的“打开 C 盘”功能找到它。不要把文件放进 Mac 原生 Steam 的另一个目录，也不要照抄网上别人的用户名路径。
3. 使用上面“方法二”的 **Windows BepInExPack_PEAK** 和本 Mod DLL，复制到该容器的 PEAK 目录。运行的是 Windows 游戏，所以不要换成 macOS 原生 BepInEx 包。
4. 若 BepInEx 没有生成日志，在该容器的 Wine 配置（`winecfg`）中打开 **Libraries / 函数库**，添加 `winhttp` 覆盖并优先加载本地 DLL，然后应用。入口因兼容工具而异；参照 [BepInEx 官方 Wine 指南](https://docs.bepinex.dev/articles/advanced/proton_wine.html)。
5. 仍从同一个容器里的 Windows Steam 启动游戏，检查那份 `BepInEx\LogOutput.log`。没有加载成功就先排查兼容环境，不要当成已支持的 Mac 版本。
6. 后续更新也在该容器内覆盖同一个 DLL，再重启游戏。不需要清 Mac 缓存。

此处不提供“在 Mac 原生安装 Thunderstore 管理器”的步骤，因为它不是本项目已验证的路线；兼容层能运行游戏也不代表一定能运行管理器。

### 装好后没效果？先检查这四项

1. **版本和启动方式：** 管理器用户是否选对配置并点了 Start modded？手动用户是否重启游戏？
2. **文件位置：** DLL 是否已解压并放到 plugins，而不是还在下载目录或 ZIP 中？是否存在重复 DLL？
3. **操作条件：** 按住鼠标右键伸手，对准队友手里的可丢弃物品；每次松开后才能再抢。
4. **日志：** 搜索 Mod 名和错误信息。反馈时提供系统、版本、安装方式、谁是房主、哪些人装了 Mod；分享日志/截图前遮住真实姓名、用户名和个人目录。

### English: Windows installation and updating

No coding or source download is needed. `<PEAK folder>` means the folder containing your `PEAK.exe`; it is a placeholder, not a folder to create. A downloaded ZIP is not installed until its DLL is placed correctly.

**Method 1 — Thunderstore Mod Manager:**

1. Install and launch PEAK once through Steam, then close it.
2. Install [Thunderstore Mod Manager](https://get.thunderstore.io/), select **PEAK**, and create/select a **profile** (a separate collection of mods).
3. Search **Expelliarmus** by **dawwnforu**, or use **Install with App** on [this package](https://thunderstore.io/c/peak/p/dawwnforu/Expelliarmus/). Install the latest version and the required **BepInExPack_PEAK** dependency.
4. Check both are enabled under **Installed**, then use **Start modded**. The manager selects the file locations; do not copy a second DLL into the profile manually.
5. To update, close the game, return to the same profile and use **Update** or **Update all**. No manual removal or cache clearing is normally needed. New versions may take time to appear due to [caching](https://wiki.thunderstore.io/mods/mod-not-visible). Updating the manager application is different from updating installed mods.

**Method 2 — Manual ZIP installation:**

1. Close PEAK. In Steam, right-click PEAK → **Manage → Browse local files**. This opens `<PEAK folder>`; verify it contains `PEAK.exe`.
2. If BepInEx is not already installed, download [BepInExPack_PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/), extract it outside the game, then copy the **contents** of its `BepInExPack` folder beside `PEAK.exe`. `BepInEx`, `winhttp.dll` and `doorstop_config.ini` must be at that level. Launch and close the game; check `BepInEx/LogOutput.log`.
3. Download [this Mod's release ZIP](https://thunderstore.io/c/peak/p/dawwnforu/Expelliarmus/), not GitHub's **Code → Download ZIP**. Extract it and find `plugins/Expelliarmus/Expelliarmus.dll`.
4. Copy `Expelliarmus.dll` into `<PEAK folder>/BepInEx/plugins/Expelliarmus/` (create the Mod folder if needed). Do not double-click the DLL or leave it inside the ZIP. No extra `BepInEx/BepInEx` nesting is needed.
5. Restart PEAK and check the log for `Expelliarmus` and `loaded successfully!`. Then test the controls: Hold right-click and aim at a teammate's droppable held item; release before grabbing again.
6. To update, close the game and overwrite that same DLL with the new one, then restart. Downloading alone does not update the installation. Keep backups outside the game folder; do not leave renamed old DLLs anywhere under plugins. Do not delete saves, other mods or the entire BepInEx folder.

If switching from manual installation to a manager, move this Mod's manual DLL outside the game folder before installing it in a manager profile. Manager updates do not clean up unrelated manual copies.

### English: Mac / macOS

[PEAK currently lists Windows system requirements](https://store.steampowered.com/app/3527290/PEAK/). This package targets Windows `PEAK.exe`; native macOS and Mac compatibility layers have not been tested by this project. The Windows Thunderstore manager installer is not a native Mac application.

If unmodded Windows PEAK already works in your CrossOver/Wine bottle, the experimental manual route is: close PEAK; locate `PEAK.exe` through **Browse local files** in that bottle's Windows Steam; follow manual Method 2 using the **Windows** BepInExPack_PEAK and DLL; configure that bottle's `winhttp` library override if BepInEx does not load, following the [official Wine guide](https://docs.bepinex.dev/articles/advanced/proton_wine.html); restart through the same bottle and verify its log. Update by replacing the DLL in that same location. Do not use a native macOS BepInEx build or a different Mac Steam directory for this Windows game. If the base game does not run yet, installing this Mod will not make it run. We do not claim a tested Mac manager workflow.

For help, report OS, installation method, Mod/game versions, who hosted, and which players installed mods. Redact personal paths and usernames from logs/screenshots before sharing.
