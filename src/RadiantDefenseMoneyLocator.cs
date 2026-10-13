using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal sealed class GameModuleInfo
{
    public string Name;
    public long Base;
    public long Size;
}

/// <summary>Radiant Defense 2.3.2.195 runtime money-field locator and codec.</summary>
public static class RadiantDefenseMoneyLocator
{
    public const string GAME_MODULE = "RadiantDefense.exe";
    public const long EXPECTED_MODULE_SIZE = 0x1D8000;
    public const long GAME_VTABLE_RVA = 0x1AF3D0;
    public const long MONEY_FIELD_OFFSET = 0x9C;
    public const int MONEY_XOR_KEY = unchecked((int)0x98BADCFEu);

    public static int EncodeMoney(int value) { return value ^ MONEY_XOR_KEY; }
    public static int DecodeMoney(int value) { return value ^ MONEY_XOR_KEY; }

    public static bool TryLocate(IntPtr handle, int pid,
        out long gameObject, out long moneyAddress, out int money, out LocalizedMessage error)
    {
        List<GameModuleInfo> modules;
        if (!TryGetModules(pid, out modules, out error))
        {
            gameObject = 0;
            moneyAddress = 0;
            money = 0;
            return false;
        }
        return TryLocate(handle, modules, out gameObject, out moneyAddress, out money, out error);
    }

    internal static bool TryLocate(IntPtr handle, List<GameModuleInfo> modules,
        out long gameObject, out long moneyAddress, out int money, out LocalizedMessage error)
    {
        gameObject = 0;
        moneyAddress = 0;
        money = 0;
        error = null;

        GameModuleInfo gameModule = null;
        if (modules != null)
            foreach (GameModuleInfo module in modules)
                if (string.Equals(module.Name, GAME_MODULE, StringComparison.OrdinalIgnoreCase))
                { gameModule = module; break; }
        if (gameModule == null)
        {
            error = new LocalizedMessage("locator.module_missing", GAME_MODULE);
            return false;
        }
        if (gameModule.Size != EXPECTED_MODULE_SIZE)
        {
            error = new LocalizedMessage("locator.version_unsupported", gameModule.Size.ToString("X"));
            return false;
        }

        long vtable = gameModule.Base + GAME_VTABLE_RVA;
        List<long> objectAddresses = FindObjectAddresses(handle, vtable);
        List<long> validObjects = new List<long>();
        List<int> balances = new List<int>();
        HashSet<long> seen = new HashSet<long>();
        foreach (long address in objectAddresses)
        {
            if (!seen.Add(address)) continue;
            int current, readError;
            if (!TryReadMoney(handle, address + MONEY_FIELD_OFFSET, out current, out readError))
                continue;
            if (current < 0 || current > 10000000) continue;
            validObjects.Add(address);
            balances.Add(current);
        }

        if (validObjects.Count == 0)
        {
            error = new LocalizedMessage(objectAddresses.Count == 0
                ? "locator.instance_missing"
                : "locator.money_uninitialized");
            return false;
        }
        if (validObjects.Count > 1)
        {
            error = new LocalizedMessage("locator.multiple_instances");
            return false;
        }

        gameObject = validObjects[0];
        moneyAddress = gameObject + MONEY_FIELD_OFFSET;
        money = balances[0];
        return true;
    }

    public static bool TryReadMoney(IntPtr handle, long address, out int money, out int error)
    {
        money = 0;
        error = 0;
        byte[] bytes = new byte[4];
        if (!MemApi.Read(handle, address, bytes))
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }
        money = DecodeMoney(BitConverter.ToInt32(bytes, 0));
        return true;
    }

    public static bool TryWriteMoney(IntPtr handle, long address, int money,
        out int error, out int restoreError, out bool readBackMismatch)
    {
        error = 0;
        restoreError = 0;
        readBackMismatch = false;

        byte[] bytes = BitConverter.GetBytes(EncodeMoney(money));
        if (!MemApi.Write(handle, address, bytes, out error, out restoreError))
            return false;

        int actual, readError;
        if (!TryReadMoney(handle, address, out actual, out readError))
        {
            error = readError;
            return false;
        }
        if (actual != money)
        {
            readBackMismatch = true;
            return false;
        }
        return true;
    }

    static bool TryGetModules(int pid, out List<GameModuleInfo> modules, out LocalizedMessage error)
    {
        modules = new List<GameModuleInfo>();
        error = null;
        try
        {
            using (Process process = Process.GetProcessById(pid))
            {
                string enumerationError = null;
                try
                {
                    foreach (ProcessModule module in process.Modules)
                        modules.Add(new GameModuleInfo {
                            Name = module.ModuleName,
                            Base = module.BaseAddress.ToInt64(),
                            Size = module.ModuleMemorySize
                        });
                }
                catch (Exception ex) { enumerationError = ex.Message; }

                bool hasGameModule = false;
                foreach (GameModuleInfo module in modules)
                    if (string.Equals(module.Name, GAME_MODULE, StringComparison.OrdinalIgnoreCase))
                    { hasGameModule = true; break; }

                if (!hasGameModule)
                {
                    try
                    {
                        ProcessModule main = process.MainModule;
                        if (main != null)
                            modules.Add(new GameModuleInfo {
                                Name = main.ModuleName,
                                Base = main.BaseAddress.ToInt64(),
                                Size = main.ModuleMemorySize
                            });
                    }
                    catch (Exception ex)
                    {
                        error = new LocalizedMessage("locator.module_info_failed",
                            string.IsNullOrEmpty(enumerationError) ? ex.Message : enumerationError + " | " + ex.Message);
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            error = new LocalizedMessage("locator.process_info_failed", ex.Message);
            return false;
        }

        if (modules.Count == 0)
        {
            error = new LocalizedMessage("locator.module_list_empty");
            return false;
        }
        return true;
    }

    static List<long> FindObjectAddresses(IntPtr handle, long vtable)
    {
        List<long> found = new List<long>();
        byte[] buffer = new byte[256 * 1024];
        int infoSize = Marshal.SizeOf(typeof(MemApi.MEMORY_BASIC_INFORMATION));
        long address = 0;
        while (true)
        {
            MemApi.MEMORY_BASIC_INFORMATION info;
            if (!MemApi.VirtualQueryEx(handle, (IntPtr)address, out info, (IntPtr)infoSize))
                break;

            long regionBase = info.BaseAddress.ToInt64();
            long regionSize = info.RegionSize.ToInt64();
            bool writable = info.State == MemApi.MEM_COMMIT &&
                (info.Protect & MemApi.PAGE_GUARD) == 0 &&
                (info.Protect & (MemApi.PAGE_READWRITE | MemApi.PAGE_WRITECOPY |
                                 MemApi.PAGE_EXECUTE_READWRITE | MemApi.PAGE_EXECUTE_WRITECOPY)) != 0;
            if (writable && regionSize > 0)
            {
                for (long offset = 0; offset < regionSize; offset += buffer.Length)
                {
                    int length = (int)Math.Min((long)buffer.Length, regionSize - offset);
                    IntPtr bytesRead;
                    if (!MemApi.ReadProcessMemory(handle, (IntPtr)(regionBase + offset),
                        buffer, (IntPtr)length, out bytesRead) || bytesRead.ToInt64() != length)
                        break;

                    for (int i = 0; i <= length - 8; i += 4)
                        if (BitConverter.ToInt64(buffer, i) == vtable)
                            found.Add(regionBase + offset + i);
                }
            }

            long next = regionBase + regionSize;
            if (next <= address) break;
            address = next;
        }
        return found;
    }
}
