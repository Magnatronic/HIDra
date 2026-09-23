using System;
using System.Threading.Tasks;
using System.Windows;
using HIDra.Models;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace HIDra.UI;

/// <summary>
/// Reads the selected text aloud, in whatever program it is in, with the voices built
/// into Windows - nothing extra to install. For checking her own writing, and for
/// reading what others have written.
///
/// The selection is fetched the only way that works in every program: copying it. The
/// clipboard is put back as it was straight afterwards, so pressing Read never loses
/// something she had copied to paste. Pressing Read again while it is speaking stops it.
/// </summary>
public sealed class ReadAloud : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _player = new();
    private bool _speaking;

    public ReadAloud()
    {
        _player.MediaEnded += (_, _) => _speaking = false;
    }

    /// <summary>
    /// Read the selection, or stop if already reading
    /// </summary>
    /// <param name="sendKeys">Sends a key combination to the program in front</param>
    public async Task ReadSelectionAsync(Action<VirtualKey[]> sendKeys)
    {
        if (_speaking)
        {
            Stop();
            return;
        }

        string text = await CopySelectionAsync(sendKeys);

        await SpeakAsync(string.IsNullOrWhiteSpace(text)
            ? "Nothing is selected. Select some words, then press Read."
            : text);
    }

    public void Stop()
    {
        _player.Pause();
        _player.Source = null;
        _speaking = false;
    }

    private async Task SpeakAsync(string text)
    {
        try
        {
            var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
            _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            _speaking = true;
            _player.Play();
        }
        catch
        {
            // No voice installed, or audio unavailable: nothing to be done from here
            _speaking = false;
        }
    }

    /// <summary>
    /// Copy the selection and return it, leaving the clipboard as it was found
    /// </summary>
    private static async Task<string> CopySelectionAsync(Action<VirtualKey[]> sendKeys)
    {
        IDataObject? saved = SaveClipboard();

        string text = "";
        try
        {
            // Emptied first, so an empty selection is not mistaken for whatever was
            // already on the clipboard
            Clipboard.Clear();
            sendKeys(new[] { VirtualKey.Control, (VirtualKey)'C' });

            // Programs take a moment to fill the clipboard; give them a few chances
            for (int attempt = 0; attempt < 6 && text.Length == 0; attempt++)
            {
                await Task.Delay(60);
                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                }
            }
        }
        catch
        {
            // The clipboard is shared and sometimes briefly locked by another program
        }
        finally
        {
            RestoreClipboard(saved);
        }

        return text;
    }

    /// <summary>
    /// A copy of everything on the clipboard, in every format that can be read. The
    /// clipboard's own object stops working once the clipboard changes, so it must be
    /// copied out now rather than kept.
    /// </summary>
    private static IDataObject? SaveClipboard()
    {
        try
        {
            var current = Clipboard.GetDataObject();
            if (current == null)
            {
                return null;
            }

            var copy = new DataObject();
            foreach (string format in current.GetFormats(autoConvert: false))
            {
                try
                {
                    if (current.GetData(format, autoConvert: false) is { } data)
                    {
                        copy.SetData(format, data);
                    }
                }
                catch
                {
                    // Some formats cannot be read back; the rest are still worth keeping
                }
            }

            return copy.GetFormats().Length > 0 ? copy : null;
        }
        catch
        {
            return null;
        }
    }

    private static void RestoreClipboard(IDataObject? saved)
    {
        try
        {
            if (saved != null)
            {
                Clipboard.SetDataObject(saved, copy: true);
            }
            else
            {
                Clipboard.Clear();
            }
        }
        catch
        {
            // Locked by another program; leaving the copied text there is harmless
        }
    }

    public void Dispose()
    {
        _player.Dispose();
        _synthesizer.Dispose();
    }
}
