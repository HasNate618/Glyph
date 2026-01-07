using System;
using System.Runtime.InteropServices;

namespace Glyph.Win32.Audio;

// Minimal Core Audio interop to toggle default capture (microphone) mute state.
public static class MicrophoneToggle
{
    public static void ToggleDefaultCaptureMute()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(EDataFlow.eCapture, DEVICE_STATE_ACTIVE, out var collection));

            // Get default capture device
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eMultimedia, out var devicePtr));
            var device = devicePtr;
            if (device is null) return;

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, CLSCTX.ALL, IntPtr.Zero, out var obj));
            var epv = (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(obj);

            epv.GetMute(out var isMuted);
            epv.SetMute(!isMuted, Guid.Empty);

            Marshal.ReleaseComObject(epv);
            Marshal.Release(obj);
            try { Marshal.ReleaseComObject(device); } catch { }
            try { Marshal.ReleaseComObject(enumerator); } catch { }
        }
        catch
        {
            // best-effort; do not throw
        }
    }

    private const uint DEVICE_STATE_ACTIVE = 0x00000001;

    private enum EDataFlow
    {
        eRender,
        eCapture,
        eAll,
    }

    private enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications,
    }

    [Flags]
    private enum CLSCTX : uint
    {
        INPROC_SERVER = 0x1,
        INPROC_HANDLER = 0x2,
        LOCAL_SERVER = 0x4,
        INPROC_SERVER16 = 0x8,
        REMOTE_SERVER = 0x10,
        INPROC_HANDLER16 = 0x20,
        RESERVED1 = 0x40,
        RESERVED2 = 0x80,
        RESERVED3 = 0x100,
        RESERVED4 = 0x200,
        NO_CODE_DOWNLOAD = 0x400,
        RESERVED5 = 0x800,
        NO_CUSTOM_MARSHAL = 0x1000,
        ENABLE_CODE_DOWNLOAD = 0x2000,
        NO_FAILURE_LOG = 0x4000,
        DISABLE_AAA = 0x8000,
        ENABLE_AAA = 0x10000,
        FROM_DEFAULT_CONTEXT = 0x20000,
        ACTIVATE_32_BIT_SERVER = 0x40000,
        ACTIVATE_64_BIT_SERVER = 0x80000,
        ALL = INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER
    }

    private const uint DEVICE_STATE = DEVICE_STATE_ACTIVE;

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
        int GetDevice(string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        int GetId(out IntPtr ppstrId);
        int GetState(out uint pdwState);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(Guid pguidEventContext);
        int VolumeStepDown(Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }
}
