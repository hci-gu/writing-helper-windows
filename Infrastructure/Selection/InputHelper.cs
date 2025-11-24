using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GlobalTextHelper.Infrastructure.Selection;

internal static class InputHelper
{
    public static void SendCtrlC()
    {
        SendKeyCombo(NativeMethods.VK_CONTROL, NativeMethods.VK_C);
    }

    public static void SendCtrlV()
    {
        SendKeyCombo(NativeMethods.VK_CONTROL, NativeMethods.VK_V);
    }

    private static void SendKeyCombo(ushort modifier, ushort key)
    {
        var inputs = new List<NativeMethods.INPUT>
        {
            KeyDown(modifier),
            KeyDown(key),
            KeyUp(key),
            KeyUp(modifier)
        };

        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    private static NativeMethods.INPUT KeyDown(ushort vk)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private static NativeMethods.INPUT KeyUp(ushort vk)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}
