using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace BetterGenshinImpact.GameTask;

/// <summary>
/// 原神分辨率注册表读写辅助类
/// 模仿 March7thAssistant 机制：启动前强制设为 1920×1080 窗口，启动后恢复原始值
/// </summary>
public static class GenshinResolutionRegistryHelper
{
    // 注册表父路径（国服 / 国际服）
    public const string CnRegistryParentPath = @"Software\miHoYo\原神";
    public const string GlobalRegistryParentPath = @"Software\miHoYo\Genshin Impact";

    public static readonly string[] RegistryParentPaths = [CnRegistryParentPath, GlobalRegistryParentPath];

    // 缓存原始注册表值，用于恢复
    private static (string widthName, int width, string heightName, int height, string fullscreenName, int fullscreen)? _saved;
    private static string? _usedParentPath;

    /// <summary>
    /// 查找当前机器上存在的原神注册表键（国服优先）
    /// </summary>
    private static RegistryKey? OpenRegistryKey(bool writable = false)
    {
        foreach (var parentPath in RegistryParentPaths)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(parentPath, writable);
                if (key != null) return key;
            }
            catch
            {
                // 忽略
            }
        }
        return null;
    }

    /// <summary>
    /// 读取当前分辨率设置并缓存，然后写入 1920×1080 窗口模式
    /// </summary>
    public static bool SetTo1920x1080()
    {
        try
        {
            using var hk = OpenRegistryKey(writable: true);
            if (hk == null)
            {
                Debug.WriteLine("[GenshinResolution] 未找到原神注册表键");
                return false;
            }

            // 找到 Width / Height / Fullscreen 对应的键名（键名是动态 hash 名）
            string? widthName = null, heightName = null, fullscreenName = null;
            foreach (var name in hk.GetValueNames())
            {
                if (name.Contains("Width"))  widthName = name;
                if (name.Contains("Height")) heightName = name;
                if (name.Contains("Fullscreen")) fullscreenName = name;
            }

            if (widthName == null || heightName == null)
            {
                Debug.WriteLine("[GenshinResolution] 注册表中未找到分辨率键");
                return false;
            }

            // 缓存原始值
            var curWidth  = Convert.ToInt32(hk.GetValue(widthName));
            var curHeight = Convert.ToInt32(hk.GetValue(heightName));
            var curFullscreen = fullscreenName != null
                ? Convert.ToInt32(hk.GetValue(fullscreenName))
                : 0;

            _saved = (widthName, curWidth, heightName, curHeight, fullscreenName ?? "", curFullscreen);

            // 写 1920×1080 窗口模式 (isFullScreen = 0)
            hk.SetValue(widthName, 1920, RegistryValueKind.DWord);
            hk.SetValue(heightName, 1080, RegistryValueKind.DWord);
            if (fullscreenName != null)
                hk.SetValue(fullscreenName, 0, RegistryValueKind.DWord);

            Debug.WriteLine($"[GenshinResolution] 已将分辨率设为 1920×1080 窗口 (原始: {curWidth}x{curHeight})");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GenshinResolution] 设置分辨率失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 将注册表中的分辨率还原为缓存中的原始值
    /// </summary>
    public static bool RestoreOriginal()
    {
        if (_saved == null)
        {
            Debug.WriteLine("[GenshinResolution] 无缓存的分辨率值可恢复");
            return false;
        }

        try
        {
            using var hk = OpenRegistryKey(writable: true);
            if (hk == null) return false;

            var (widthName, width, heightName, height, fullscreenName, fullscreen) = _saved.Value;

            hk.SetValue(widthName, width, RegistryValueKind.DWord);
            hk.SetValue(heightName, height, RegistryValueKind.DWord);
            if (!string.IsNullOrEmpty(fullscreenName))
                hk.SetValue(fullscreenName, fullscreen, RegistryValueKind.DWord);

            Debug.WriteLine($"[GenshinResolution] 已恢复原始分辨率: {width}x{height}");
            _saved = null;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GenshinResolution] 恢复分辨率失败: {ex.Message}");
            return false;
        }
    }
}
