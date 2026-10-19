# 农场主

农场主是一个 Windows VS Code 扩展。它盯着编辑器里的红色错误：新增错误就抽鞭，并在出错位置显示粗俗注释，指出行号、列号和报错原因。

台词通过本地规则生成，错误信息来自 VS Code 的诊断服务。无需账号、网络或模型服务，不执行你的代码，也不改写源文件。

支持本地文件和未命名文件，提供六种台词语言，以及皮革、编织绳、黑色橡胶三种鞭子材质。

## 安装

1. 在 [Releases](https://github.com/wl8695573-blip/farmer-vscode/releases/latest) 下载 `code-whip-0.8.3.vsix`。
2. 在 VS Code 扩展面板右上角选择“…” →“从 VSIX 安装”。
3. 安装后执行“重新加载窗口”，点击状态栏的“农场主”修改设置。

仅支持 Windows 桌面版 VS Code。未命名文件需要先选择代码语言；错误提示由对应语言服务提供。

## 触发方式

- 跟随当前编辑器新增的红色 Error 诊断立即启动鞭子，不要求运行、保存、输入分号或等待冷却。
- 黄色警告不触发。同一条红标持续存在或仅移动行号时，不会因为重复发布诊断而反复抽。
- 修好后再次出现同样的错误，可以再次触发。一次诊断更新新增多个错误时播放一次动画。
- 注释只显示在编辑器里；红标消失，注释随之消失。

台词包含粗俗用语，例如：`你他妈怎么蠢得跟猪一样？第3行第9列：少了“}”，他妈把它补上！`

## 设置与说明

可调整语言、错误注释、鞭子材质、屏幕抖动、鞭响、音量和减少动态效果。

- [介绍](docs/介绍.md)
- [使用说明](docs/使用说明.md)

## 源码与构建

源码位于 `extension/`。扩展内部标识保持 `local-farm-tools.code-whip`，设置键保持 `codeWhip.*`，已有安装可以直接更新。

```powershell
cd extension
node tests/diagnostics.test.js
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-desktop.ps1
python scripts/package.py
```

桌面组件使用 Windows 自带的 .NET Framework 编译器。Python 仅用于开发打包，使用扩展不需要安装。

许可：[MIT](LICENSE)。
