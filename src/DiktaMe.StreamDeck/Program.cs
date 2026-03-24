using BarRaider.SdTools;

namespace DiktaMe.StreamDeck;

internal static class Program
{
    static void Main(string[] args)
    {
        // SDWrapper discovers all [PluginActionId] classes via reflection
        // and handles the Stream Deck WebSocket lifecycle.
        SDWrapper.Run(args);
    }
}
