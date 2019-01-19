// An interface definition for msacm32.dll for use in C#
// WARNING: This is very minimal and is purposely not complete.
// If you're thinking about using this for anything else, consider using NAudio
// instead.

using System;
using System.Runtime.InteropServices;

namespace ATRAC3Util {
  // Windows multimedia error codes from mmsystem.h.
  public enum MmResult {
    NoError = 0,  // MMSYSERR_NOERROR
  }
  
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi, Pack=2)]
  public struct AcmDriverDetails {  // ACMDRIVERDETAILS
    public int structureSize;  // DWORD cbStruct
    public UInt32 fccType;  // FOURCC fccType
    public UInt32 fccComp;  // FOURCC fccComp
    public UInt16 manufacturerId;  // WORD wMid; 
    public UInt16 productId;  // WORD wPid
    public UInt32 acmVersion;  // DWORD vdwACM
    public UInt32 driverVersion;  // DWORD vdwDriver
    public int supportFlags;  // DWORD fdwSupport;
    public int formatTagsCount;  // DWORD cFormatTags
    public int filterTagsCount;  // DWORD cFilterTags
    public IntPtr hicon;  // HICON hicon

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=ShortNameChars)]
    public string shortName;  // TCHAR  szShortName[ACMDRIVERDETAILS_SHORTNAME_CHARS]; 

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=LongNameChars)]
    public string longName;  // TCHAR  szLongName[ACMDRIVERDETAILS_LONGNAME_CHARS];

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=CopyrightChars)]
    public string copyright;  // TCHAR  szCopyright[ACMDRIVERDETAILS_COPYRIGHT_CHARS]; 

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=LicensingChars)]
    public string licensing;  // TCHAR  szLicensing[ACMDRIVERDETAILS_LICENSING_CHARS]; 

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=FeaturesChars)]
    public string features;  // TCHAR  szFeatures[ACMDRIVERDETAILS_FEATURES_CHARS];

    //
    private const int ShortNameChars = 32;  // ACMDRIVERDETAILS_SHORTNAME_CHARS
    private const int LongNameChars = 128;  // ACMDRIVERDETAILS_LONGNAME_CHARS
    private const int CopyrightChars = 80;  // ACMDRIVERDETAILS_COPYRIGHT_CHARS
    private const int LicensingChars = 128;  // ACMDRIVERDETAILS_LICENSING_CHARS 
    private const int FeaturesChars = 512;  // ACMDRIVERDETAILS_FEATURES_CHARS
  }
  
  [StructLayout(LayoutKind.Sequential, Size=128)]
  public class AcmStreamHeaderStruct {
    public int cbStruct;
    public int fdwStatus = 0;  // AcmStreamHeaderStatusFlags
    public IntPtr userData;
    public IntPtr sourceBufferPointer;
    public uint sourceBufferLength;
    public uint sourceBufferLengthUsed;
    public IntPtr sourceUserData;
    public IntPtr destBufferPointer;
    public uint destBufferLength;
    public uint destBufferLengthUsed = 0;
    public IntPtr destUserData;
    // ...
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct WaveFormatEx {
    public UInt16 wFormatTag;
    public UInt16 nChannels;
    public UInt32 nSamplesPerSec;
    public UInt32 nAvgBytesPerSec;  // for buffer estimation
    public UInt16 nBlockAlign;  // block size of data
    public UInt16 wBitsPerSample;  // number of bits per sample of mono data
    public UInt16 cbSize;  // optional depending on WAVE sub-format

    /* extra information (after optional cbSize) */
    [MarshalAs(UnmanagedType.ByValArray, SizeConst=0x0E)]
    public byte[] extra;
  }
  
  //

  public delegate bool AcmDriverEnumCallback(
      int hAcmDriverId,
      ref int instance,
      int flags
      );

  //
    
  class ACM {

    [DllImport("msacm32.dll")]
    public static extern MmResult acmDriverEnum(
        AcmDriverEnumCallback fnCallback,  // ACMDRIVERENUMCB fnCallback,
        ref int dwInstance,  // DWORD_PTR dwInstance,
        int flags  // DWORD fdwEnum
        );

    [DllImport("msacm32.dll")]
    public static extern MmResult acmDriverDetails(
        int hAcmDriver,  // HACMDRIVERID hadid,
        ref AcmDriverDetails driverDetails,  // LPACMDRIVERDETAILS padd,
        int reserved  // DWORD fdwDetails
        );

    [DllImport("msacm32.dll")]
    public static extern MmResult acmDriverOpen(
        out IntPtr pAcmDriver,  // LPHACMDRIVER phad,
        int hAcmDriver,  // HACMDRIVERID hadid,
        int fdwOpen  // DWORD fdwOpen
        );
        
    [DllImport("msacm32.dll")]
    public static extern MmResult acmStreamClose(
        IntPtr hAcmStream,  // HACMSTREAM has
        int fdwClose  // DWORD fdwClose
        );

    [DllImport("msacm32.dll")]
    public static extern MmResult acmStreamConvert(
        IntPtr hAcmStream,  // HACMSTREAM has
        [In, Out] AcmStreamHeaderStruct streamHeader,  // LPACMSTREAMHEADER pash
        int fdwConvert  // DWORD fdwConvert
        );

    [DllImport("msacm32.dll")]
    public static extern MmResult acmStreamOpen(
        out IntPtr phas,  // LPHACMSTREAM phas
        IntPtr had,  // HACMDRIVER had
        ref WaveFormatEx pwfxSrc,  // LPWAVEFORMATEX pwfxSrc
        ref WaveFormatEx pwfxDest,  // LPWAVEFORMATEX pwfxDst
        IntPtr pwFltr,  // LPWAVEFILTER pwfltr
        IntPtr callBack,  // DWORD_PTR dwCallback
        IntPtr dwInstance,  // DWORD_PTR dwInstance
        [MarshalAs(UnmanagedType.U4)] uint dwOpen  // DWORD fdwOpen
        );
        
    [DllImport("msacm32.dll")]
    public static extern MmResult acmStreamPrepareHeader(
        IntPtr hAcmStream,  // HACMSTREAM has
        [In, Out] AcmStreamHeaderStruct streamHeader,  // LPACMSTREAMHEADER pash
        int fdwPrepare  // DWORD fdwPrepare
        );

    [DllImport("msacm32.dll")]
    public static extern MmResult acmStreamSize(
        IntPtr hAcmStream,  // HACMSTREAM has
        uint inputBytes,  // DWORD cbInput
        out uint outputBytes,  // LPDWORD pdwOutputBytes
        int fdwSize  // DWORD fdwSize
        );

    [DllImport("msacm32.dll")]
    public static extern MmResult acmStreamUnprepareHeader(
        IntPtr hAcmStream,  // HACMSTREAM has
        [In, Out] AcmStreamHeaderStruct streamHeader,  // LPACMSTREAMHEADER pash
        int fdwUnprepare  // DWORD fdwUnprepare
        );

  }

}
