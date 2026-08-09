# Expelliarmus / 除你武器

PEAK BepInEx 联机 Mod：将准心对准队友当前拿在手里的物品，按鼠标右键把物品抢到自己手中。

## 抢夺规则

- 只锁定队友当前从 1/2/3 栏拿在手里的物品。
- 抢夺成功后，队友对应物品栏与手持状态变为空。
- 目标物品进入你的物品栏，并立即装备到手中。
- 如果你已经拿着物品，原手持物会先按 PEAK 原生掉落逻辑落到地面。
- 槽位移除、地面掉落和拾取均交给主机处理，避免联机时两人同时持有同一物品。
- 非房主也可以主动抢夺；客户端会等待主机确认双方槽位状态后再请求拾取。

## 操作

| 输入 | 功能 |
| --- | --- |
| 鼠标右键 | 抢夺准心指向的队友手持物 |

## 安装

1. 确保 PEAK 已安装 BepInEx 5。
2. 将 `Expelliarmus.dll` 放入：

```text
PEAK\BepInEx\plugins\Expelliarmus\Expelliarmus.dll
```

3. 启动 PEAK。

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File "D:\trae projects\1\Expelliarmus\build.ps1"
```

构建脚本会编译 DLL，并尝试复制到 PEAK 的 BepInEx 插件目录。覆盖插件前需要完全退出 PEAK，否则 DLL 会被游戏进程锁定。

## 联机说明

Mod 通过 PEAK 自带的 Photon RPC 请求主机执行物品栏移除、掉落和拾取。建议房间内所有玩家安装相同版本。排查问题时查看：

```text
PEAK\BepInEx\LogOutput.log
```

Steam 更新可能会重建 PEAK 游戏目录并删除 `BepInEx`、`winhttp.dll` 和插件。若日志文件或整个 `BepInEx` 目录消失，需要先重新安装 Mod 加载环境；这与是否为房主无关。
