# Radiant Defense Economy Trainer

该项目为AI生成的辐射塔防金钱修改器，此README也为AI生成。

适用于Microsoft Store中的Radiant Defense。

## 编译

需要 Windows x64、Visual Studio 2026 的 .NET 桌面开发组件、.NET Framework 4.8 Developer Pack，以及 CMake 4.2 或更高版本。

在项目目录运行：

```powershell
cmake --preset vs2026
cmake --build --preset vs2026-release
```

也可以在 Visual Studio 2026 中打开 `build\vs2026\RadiantDefenseEconomyTrainer.slnx`。Release 程序位于 `build\vs2026\Release\Radiant Defense Economy Trainer.exe`，运行时文本配置位于同一目录的 `trainer.yaml`。

## 测试

编译并运行整合自检：

```powershell
cmake --build --preset vs2026-release
ctest --preset vs2026-release
```

`TrainerSelfTest` 会检查 YAML 与中英界面、窗口布局、自动余额定位、修改/增加/锁定写入，以及模拟游戏进程退出时的恢复提示；测试使用独立的模拟进程，不会修改已安装的游戏。
