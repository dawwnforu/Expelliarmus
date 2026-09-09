# Expelliarmus / 除你武器 1.0.5

PEAK BepInEx Mod：伸出右手，瞄准队友手中的物品，将它抢到自己手里。

## 操作

- 按住游戏的“次要使用/伸手”键，默认鼠标右键；支持游戏内改键。
- 可以先伸手，再对准物品。同一次按住只抢一次，松开后可以再次抢夺。
- 目标为队友从第 1/2/3 格拿出的可丢弃物品，最大距离 7.5 米。
- 自己已经拿着物品时，先把原物品放到地上，再抢夺。菜单、轮盘、昏迷和攀爬期间不触发。

## 联机与物品转移

使用游戏现有 RPC，设计上只需抢夺者安装，房主和队友无需安装本 Mod。

1. 按选中格子和物品唯一编号核对目标，不按物品种类猜测格子。
2. 请求房主通过原生掉落流程生成地面物品并更新库存。
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
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -PeakDir 'D:\steam\steamapps\common\PEAK' -Install
```

省略 `-Install` 只生成项目内的 DLL/PDB。

## 验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1 -PeakDir 'D:\steam\steamapps\common\PEAK'
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1
```

`test.ps1` 执行实际行为代码的回归检查（Unity/网络替身），并核对安装游戏的 RPC 签名。覆盖同类物品、空格残留数据、菜单/昏迷/攀爬、持续伸手、重复触发、掉落超时、旧对象未释放、拾取拒绝和房主格子变化。

`verify.ps1` 检查安装文件与版本；游戏运行过新版后可加 `-RequireRuntimeLoad` 检查加载日志。这些检查不能替代双人 PEAK 实测。旧的 `simulate-network.ps1` 只模拟 1.0.4 的等待策略，不用于验证 1.0.5 的物品转移。

双人实测重点：抢夺者分别作为房主/非房主、队友不装 Mod、两件同类物品、自己手持物品、队友中途换物、高延迟，以及物品数量和剩余使用次数是否一致。
