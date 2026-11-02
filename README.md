# Radiant Defense Economy Trainer

该项目为AI生成的辐射塔防金钱修改器，此README也为AI辅助生成。

适用于Microsoft Store中的Radiant Defense 2.3.2-195，由Hexage开发的手机塔防游戏。

## 运行要求

Windows 10/11 x64 和 .NET Framework 4.8 或兼容的更高版本

## 编译

- Visual Studio 2026 的“.NET 桌面开发”和“使用 C++ 的桌面开发”工作负载，包含 MSVC x64/x86 构建工具和 Windows SDK
- .NET Framework 4.8 Developer Pack
- CMake 4.2 或更高版本

```powershell
cmake --preset vs2026
cmake --build --preset vs2026-release
```

如有环境变动

```powershell
cmake --fresh --preset vs2026
```

## 发布

完成 Release 构建后执行：

```powershell
cpack --preset vs2026-release
```

## 测试

运行已编译的 Release 版本整合自检：

```powershell
ctest --preset vs2026-release
```
