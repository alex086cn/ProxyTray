# 更新日志

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [2.0.0] - 2026-11-23

### 新增
- 全新托盘图标：深蓝黑圆角底板 + 青绿/珊瑚红渐变圆环，开启态为电源符号，关闭态叠加斜杠。
- 图标按 16/20/24/32/44/48/64/96/128/256 十档尺寸逐帧绘制，小尺寸自动精简细节层，保证任务栏清晰度。
- 新增 `src/tools/MakeIcon.cs`，可重复生成多尺寸 ICO（`assets/proxytray.ico`）。
- 构建脚本 `build.ps1`，一条命令产出可直接分发的 exe。

### 修复
- 修复开机自启状态在菜单中不可见的问题：根因是 `ShowImageMargin=false` 使勾选标记无渲染位置，改为自绘图标 + 文字后缀「已开启 / 未开启」，并在每次菜单 `Opening` 时重读注册表真实值。
- 修复子菜单样式设置报 `CS1061`：`DropDown` 实际类型为 `ToolStripDropDownMenu`，需先 `as` 转换再设置 `ShowImageMargin`、`ShowCheckMargin`、`ImageScalingSize`、`Padding`、`BackColor`。

### 变更
- 右键菜单结构重排：状态行「已开启 / 已关闭 · 线路名」→ 开启/关闭代理 → 切换线路 → 开机自启 → 退出。
- 编译产物统一输出至 `dist\`，仓库不再保留中间 exe。

## [1.0.0] - 2026-11-23

### 新增
- 首个可用版本：托盘一键开关系统代理，`InternetSetOption` 即时生效。
- 多线路保存与切换（`routes.txt` / `current.txt`）。
- 开机自启（`HKCU\...\Run`）。
- 单实例互斥体 `ProxyTray_v2_single_instance`。
