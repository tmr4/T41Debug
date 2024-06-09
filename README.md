# T41 Debug

T41 Debug is a Windows console app designed to receive debug messages from the T4 over a single USB serial connection.  Multiple debug windows can be open at the same time.  T41 Debug works with [T41 Server](https://github.com/tmr4/T41Server).

![T41 Server and Debug Windows w/ WSJT-X](https://github.com/tmr4/T41Debug/blob/main/images/t41Server_Debug.png)

The app currently does the following:

  * Displays debug messages associated with this window sent from the T41.

This is a work in progress.  Still to come is adding debug commands back to the T41, specifying the messages to view in a given window and in the server window.

## Build

Create a new Windows console app project in Visual Studio 2022 and replace the autogenerated C# file with the C# files in this repository.  Build as normal.  The executable file will be located in the bin folder.

## Use

Requires my [T41_SDR](https://github.com/tmr4/T41_SDR) software which must be compiled with one of the USB Types that includes both `Serial` and `Audio` (audio over USB isn't required if you connect the audio from the T41 to your PC in another way). Enter the T41 COM port on starting the server.

Two debug statements, DEBUG_SERIAL and DEBUG_SERIAL2, are enabled in the T41 code if DEBUG_ENABLED is defined.  Any DEBUG_SERIAL statements in your T41 code are sent to the first T41 Debug window opened.  DEBUG_SERIAL2 statements in your T41 code are sent to the second T41 Debug window opened.  Define more debug statements, DEBUG_SERIAL3, DEBUG_SERIAL4, DEBUG_SERIAL5, for instance, if you want more refined debug message separation and open additional debug windows to view the associated messages.  If a sufficient number of debug windows aren't opened, some messages are dumped to server window.

## Limitations

Debug messages associated with a debug window are discarded if you close the window.  Ideally, they should be displayed again in the server window.