using System.Runtime.InteropServices;
using Snapkit.App.Input;

namespace Snapkit.App.Tests;

public class InputSenderTests
{
    [Fact]
    public void InputStruct_HasNativeSizeOnX64()
        // INPUT в Windows x64 занимает 40 байт; при другом размере SendInput ничего не отправит
        => Assert.Equal(40, Marshal.SizeOf<InputSender.INPUT>());

    [Fact]
    public void Marker_SpellsSnkt()
        => Assert.Equal((nuint)0x534E4B54, InputSender.Marker);
}
