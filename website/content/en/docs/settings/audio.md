# Audio Settings

The **Audio** tab configures how dIKta.me interacts with your microphone and speakers.

## Microphone Selection
By default, dIKta.me listens to your primary, system-default Windows recording device (e.g., your headset, webcam, or built-in laptop mic).

If you have multiple microphones plugged into your computer and wish to dedicate one specifically to dIKta.me, you can explicitly set it here via the **Select Microphone** dropdown menu.

> [!TIP]
> **Exclusive Control Issues**: If you use software like Zoom or OBS Studio with "Exclusive Control" enabled on the same microphone you're trying to use with dIKta.me, you may experience recording failures. Try changing the input device or ensuring your other apps don't hoard exclusive access.

## Audio Options

*   **Max Recording Duration**: Sets a hard cutoff limit for a single recording session (in seconds). By default, this is 600s (10 minutes). If you accidentally leave your Dictate hotkey pressed, dIKta.me will automatically cut the recording and begin processing after this threshold to avoid creating abnormally large audio files or running up your API bills.
*   **Audio Ducking**: An advanced feature that automatically lowers the volume of other applications (like Spotify, YouTube, or video games) whenever you start dictating. When the recording finishes, your volume is instantly restored.
    *   **Attenuation Level**: Determines how dramatically the other sounds are reduced. 100% means other audio is completely muted, while 20% means the background music will only slightly dim.
