# Radiant Defense Economy Trainer

该项目为AI生成的辐射塔防金钱修改器，此README也为AI生成。

适用于Microsoft Store中的Radiant Defense。

## 编译

- Visual Studio 2026 的“.NET 桌面开发”和“使用 C++ 的桌面开发”工作负载，包含 MSVC x64/x86 构建工具和 Windows SDK。
- .NET Framework 4.8 Developer Pack；仅安装运行时或其他版本的 .NET SDK 不能替代此引用包。
- CMake 4.2 或更高版本，并确保 `cmake`、`ctest` 可从命令行调用。

```powershell
cmake --preset vs2026
cmake --build --preset vs2026-release
```

如有环境变动

```powershell
cmake --fresh --preset vs2026
```

## 测试

编译并运行整合自检：

```powershell
cmake --build --preset vs2026-release
ctest --preset vs2026-release
```

`TrainerSelfTest` 会检查 YAML 与中英界面、窗口布局、自动余额定位、修改/增加/锁定写入，以及模拟游戏进程退出时的恢复提示。
