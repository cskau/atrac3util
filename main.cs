// A .NET port of atrac3tool.
// Converts 44100 Hz, 2 channel WAV files into ATRAC3 LP2 files.

// To compile and run on Linux:
//   mcs -out:atrac3util.exe main.cs acm.cs 
//   wine atrac3util.exe my_music.wav my_music.at3

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ATRAC3Util {

  class ATRAC3Util {
    
    // How much to add to the conversion buffer allocation size.
    const uint ALLOC_SLACK = 0x400;
    
    // Magic value identifying the Sony ATRAC WAV sub-format.
    const ushort WAVE_FORMAT_SONY_SCX = 0x0270;
    
    const uint BITRATE_LP4 = 66;  // kb/s
    const uint BITRATE_LP2_105 = 105;  // kb/s
    const uint BITRATE_LP2 = 132;  // kb/s
    
    // Table of block align values per bit rate.
    static Dictionary<uint, ushort> BitRateBlockAlign = new Dictionary<uint, ushort> {
      {66, 192},
      {105, 304},
      {132, 384}
    };
    
    //

    static byte[] getBytes(object str) {
      int size = Marshal.SizeOf(str);
      byte[] arr = new byte[size];
      IntPtr ptr = Marshal.AllocHGlobal(size);

      Marshal.StructureToPtr(str, ptr, true);
      Marshal.Copy(ptr, arr, 0, size);
      Marshal.FreeHGlobal(ptr);

      return arr;
    }

    // Convinience function to check MmResults.
    static void handleMmResult(MmResult result, string message) {
      if (result != MmResult.NoError) {
        throw new Exception(message);
      }
    }
    
    //
    
    // Callback handler locating and returning the ATRAC3 driver.
    static bool atracDriverFilterCallback(
        int hAcmDriver, ref int dwInstance, int flags) {
      AcmDriverDetails details = new AcmDriverDetails();
      details.structureSize = System.Runtime.InteropServices.Marshal.SizeOf(
          details);
      ACM.acmDriverDetails(hAcmDriver, ref details, 0);
      if (details.shortName == "ATRAC3") {
        dwInstance = hAcmDriver;
        return false;
      }
      return true;
    }
    
    //
    
    // Somewhat naive WAV reader.
    static void readWav(
        string wavPath, out WaveFormatEx format, out byte[] data) {
      format = new WaveFormatEx();

      using (FileStream fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read))
      using (BinaryReader br = new BinaryReader(fs)) {
        var riffID = Encoding.UTF8.GetString(br.ReadBytes(4));
        var size = br.ReadUInt32();
        var wavID = Encoding.UTF8.GetString(br.ReadBytes(4));
        if (riffID != "RIFF" || wavID != "WAVE") {
          throw new Exception("Not a WAV!");
        }
        
        if (size < 36) {
          throw new Exception("WAV file is too short!");
        }
        
        var fmtID = Encoding.UTF8.GetString(br.ReadBytes(4));
        if (fmtID != "fmt ") {
          throw new Exception("fmt not found!");
        }
        var fmtSize = br.ReadUInt32();
        format.wFormatTag = br.ReadUInt16();
        format.nChannels = br.ReadUInt16();
        format.nSamplesPerSec = br.ReadUInt32();
        format.nAvgBytesPerSec = br.ReadUInt32();
        format.nBlockAlign = br.ReadUInt16();
        format.wBitsPerSample = br.ReadUInt16();
        // If the fmt sub-chunk includes a cbSize, read it and skip extra bytes.
        if (fmtSize >= 18) {
          format.cbSize = br.ReadUInt16();
          if (format.cbSize > 0) {
            br.ReadBytes((int)format.cbSize);
          }
        }
        
        // WAVE files might contain other optional sub-chunk types - we'll just
        // skip past those.
        var nextId = br.ReadBytes(4);
        while (Encoding.UTF8.GetString(nextId) != "data") {
          // Skip non-data sub-chunk.
          br.ReadBytes((int)br.ReadUInt32());
          nextId = br.ReadBytes(4);
        }
        // Found the data sub-chunk - read out data.
        var dataSize = br.ReadUInt32();
        data = br.ReadBytes((int)dataSize);
      }
    }

    // Minimal WAV file writer.
    static void writeWav(string path, byte[] fmt, byte[] data, uint dataSize) {
      using (FileStream fs = new FileStream(
          path, FileMode.Create, FileAccess.Write))
      using (BinaryWriter bw = new BinaryWriter(fs)) {
        uint size = 12 + (uint)fmt.Length + 8 + (uint)dataSize;
        bw.Write(Encoding.UTF8.GetBytes("RIFF"));
        bw.Write(BitConverter.GetBytes(size));
        bw.Write(Encoding.UTF8.GetBytes("WAVE"));
        bw.Write(Encoding.UTF8.GetBytes("fmt "));
        bw.Write(BitConverter.GetBytes(fmt.Length));
        bw.Write(fmt);
        bw.Write(Encoding.UTF8.GetBytes("data"));
        bw.Write(BitConverter.GetBytes(dataSize));
        bw.Write(data, 0, (int)dataSize);
      }
    }
    
    // Lookup the system ACM ATRAC3 driver.
    static IntPtr getAtrac3Driver() {
      int atracDriverId = 0;
      ACM.acmDriverEnum(
          new AcmDriverEnumCallback(atracDriverFilterCallback),
          ref atracDriverId, 0);
      
      if (atracDriverId == 0) {
        throw new Exception(
            "ATRAC3 codec not found!\nBe sure to install atrac3.acm!");
      }

      IntPtr driver;
      handleMmResult(
          ACM.acmDriverOpen(out driver, atracDriverId, 0),
          "Failed to open ATRAC3 driver!");
      
      return driver;
    }
    
    // Construct an ATRAC WAV format struct.
    static WaveFormatEx getAtrac3WaveFormat(uint bitRate) {
      WaveFormatEx dst = new WaveFormatEx();

      dst.wFormatTag = WAVE_FORMAT_SONY_SCX;
      dst.nChannels = 2;  // Stereo
      dst.nSamplesPerSec = 44100;  //  Hz
      dst.wBitsPerSample = 0;
      dst.nAvgBytesPerSec = (uint)(bitRate * 125);
      dst.nBlockAlign = BitRateBlockAlign[bitRate];
      dst.cbSize = 0x0E;  // 14
      // These values seem to primarily affect StreamSize recommendations.
      // TODO: experiment with these value to try pin the down.
      dst.extra = new byte[] {  // atrac3tool.cpp
          0x01, 0x00,
          0x00, 0x10,
          0x00, 0x00,
          0x00, 0x00,
          0x00, 0x00,
          0x01, 0x00,
          0x00, 0x00
          };
      // dst.extra = new byte[]{  // LP2.wav
      //     0x01, 0x00,
      //     0xc0, 0x00,
      //     0x00, 0x00,
      //     0x00, 0x00,
      //     0x00, 0x00,
      //     0x01, 0x00,
      //     0x00, 0x00};
      // At3_Format.wRevision = 1;  // word = 2 bytes?
      // At3_Format.nSamplesPerBlock = 0x800;  // int? 4bytes?
      // At3_Format.abReserved[2] = 1;
      // At3_Format.abReserved[4] = 1;
      // At3_Format.abReserved[6] = 1;
      // dst.extra = new byte[]{
      //     0x01, 0x00,
      //     0x00, 0x08, 0x00, 0x00,
      //     0x01, 0x00,
      //     0x01, 0x00,
      //     0x01, 0x00,
      //     0x00, 0x00};

      return dst;
    }
    
    static AcmStreamHeaderStruct getASH(
        byte[] wavData, byte[] atracData) {
      var ash = new AcmStreamHeaderStruct();
      ash.cbStruct = 128;
      ash.fdwStatus = 0;
      ash.sourceBufferPointer = GCHandle.Alloc(
          wavData, GCHandleType.Pinned).AddrOfPinnedObject();
      ash.sourceBufferLength = (uint)wavData.Length;
      ash.sourceBufferLengthUsed = 0;
      ash.destBufferPointer = GCHandle.Alloc(
          atracData, GCHandleType.Pinned).AddrOfPinnedObject();
      ash.destBufferLength = (uint)atracData.Length;
      ash.destBufferLengthUsed = 0;
      return ash;
    }
    
    // Converts WAV to ATRAC.
    static void convert(
        IntPtr driver,
        WaveFormatEx wavFmt, byte[] wavData,
        ref WaveFormatEx atracFmt, out byte[] atracData,
        out uint atracDataSize
        ) {
      // Set up conversion stream.
      IntPtr stream;
      handleMmResult(
          ACM.acmStreamOpen(
              out stream, driver, ref wavFmt, ref atracFmt,
              IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0),
          "acmStreamOpen failed");
      
      // Guesstimate data size for allocations.
      uint allocSize;
      handleMmResult(
          ACM.acmStreamSize(
              stream, (uint)wavData.Length, out allocSize,
              /*ACM_STREAMSIZEF_SOURCE=*/0),
          "acmStreamSize failed"
          );

      // Add a bit of slack - better over than under.
      allocSize += ALLOC_SLACK;
      
      atracData = new byte[allocSize];
      
      var ash = getASH(wavData, atracData);
      
      handleMmResult(
          ACM.acmStreamPrepareHeader(stream, ash, 0),
          "Failed to prepare stream headers!"
          );
      
      handleMmResult(
          ACM.acmStreamConvert(
              stream, ash, /*ACM_STREAMCONVERTF_BLOCKALIGN=*/4),
          "Conversion failed!");
      
      // Actual number of bytes of resulting ATRAC data (as opposed to alloc.).
      atracDataSize = ash.destBufferLengthUsed;
      
      // Anything that failes after this point doesn't really matter..
      
      handleMmResult(
          ACM.acmStreamUnprepareHeader(stream, ash, 0),
          "Failed to unprepare stream header!"
          );
      
      handleMmResult(
          ACM.acmStreamClose(stream, 0),
          "Failed to close the stream!"
          );
    }


    // Converts a WAV file into ATRAC3 at a given bitrate and writes it to disk.
    public static void convertWavToAtrac(
        string wavPath, string atracPath, uint bitRate,
        out uint wavDataSize, out uint atracDataSize) {
      IntPtr driver = getAtrac3Driver();

      WaveFormatEx wavFmt;
      byte[] wavData;
      readWav(wavPath, out wavFmt, out wavData);
      
      wavDataSize = (uint)wavData.Length;
      
      WaveFormatEx atracFmt = getAtrac3WaveFormat(bitRate);

      byte[] atracData;
      convert(
          driver,
          wavFmt, wavData,
          ref atracFmt, out atracData,
          out atracDataSize);
      
      writeWav(
          atracPath,
          getBytes(atracFmt),
          atracData,
          atracDataSize);
    }


    // Command-line interface.

    static void Main(string[] args) {
      if (args.Length < 2) {
        Console.WriteLine("ATRAC3Util - WAV to ATRAC3 converter.");
        Console.WriteLine("Usage: atra3util.exe my_music.wav my_music.at3");
        return;
      }
      
      var wavPath = args[0];
      var atracPath = args[1];

      uint wavDataSize;
      uint atracDataSize;
      convertWavToAtrac(
          wavPath, atracPath, BITRATE_LP2, out wavDataSize, out atracDataSize);
          
      Console.WriteLine(
          "Successfully converted {0:D} bytes of WAV into {1:D} bytes of ATRAC",
          wavDataSize, atracDataSize);
    }

  }

}
