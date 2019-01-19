# atrac3util

Command-line tool for converting WAV files into LP2 ATRAC3 files.
Input WAV files are required to be 44100 Hz, 2 channel, 16 bit and output
ATRAC files are encoded as LP2 132 kb/s.

This is a C# .NET port of [Treeki's atrac3tool](https://github.com/Treeki/atrac3tool/)
though much has eventually been changed.
It being .NET means it's a lot easier to compile on non-Windows platforms and
the exe files run anywhere (though ACM still requires Windows/Wine).

## Prerequisites

atrac3util uses Sony's ATRAC3 ACM driver for encoding and thus requires the
Windows Audio Compression Manager (ACM) API as well as the driver itself.

The ACM API is built into Windows and by extension Wine (`msacm32.dll`).
Sony's driver (`atrac3.acm`) will however have to be installed.
The driver can be downloaded from elsewhere on the internet - e.g.
[atrac3.acm](https://samples.ffmpeg.org/A-codecs/ATRAC3/atrac3.acm).

On Windows you'll most like want to find and use the installer.
But if you're running this on Wine, you can copy `atrac3.acm` into
`~/.wine/drive_c/windows/system32/` and/or `~/.wine/drive_c/windows/syswow64/`.
And then add a line like `msacm.at3=atrac3.acm` or
`msacm.at3=c:\windows\system32\atrac3.acm` to
`~/.wine/drive_c/windows/system.ini`.

## compile

```
mcs -out:atrac3util.exe main.cs acm.cs
```

## Usage

To convert a regular WAV file into an ATRAC3 file:

```
atrac3util.exe my_music.wav my_music.at3
```

Tip: After conversion you can of course test play your ATRAC3 file in VLC, or
transfer them to your NetMD player via
[netmdcli](https://github.com/glaubitz/linux-minidisc):

```
netmdcli send my_music.at3
```
